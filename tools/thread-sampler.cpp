// Poor man's sampling profiler for a running process.
//
// Waits for a process by name, then continuously ranks its threads by recent
// CPU time and records the instruction pointer (RIP) of the busiest ones.
// Output is a CSV intended to be symbolized against out/mx_alive.map.
//
// Build: clang++ -O2 -std=c++20 tools/thread-sampler.cpp -o out/thread-sampler.exe
// Usage: thread-sampler.exe mx_alive.exe 180 6 out/rip-samples.csv

#include <windows.h>
#include <tlhelp32.h>

#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <map>
#include <string>
#include <vector>

namespace {

struct ThreadInfo {
  HANDLE handle = nullptr;
  uint64_t last_kernel = 0;
  uint64_t last_user = 0;
  double score = 0.0;
};

uint64_t FileTimeToU64(const FILETIME& ft) {
  return (uint64_t(ft.dwHighDateTime) << 32) | ft.dwLowDateTime;
}

DWORD FindProcessId(const char* exe_name) {
  HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
  if (snapshot == INVALID_HANDLE_VALUE) {
    return 0;
  }
  PROCESSENTRY32 entry = {};
  entry.dwSize = sizeof(entry);
  DWORD pid = 0;
  if (Process32First(snapshot, &entry)) {
    do {
      if (_stricmp(entry.szExeFile, exe_name) == 0) {
        pid = entry.th32ProcessID;
        break;
      }
    } while (Process32Next(snapshot, &entry));
  }
  CloseHandle(snapshot);
  return pid;
}

struct ModuleInfo {
  uint64_t base;
  uint64_t size;
  std::string name;
};

std::vector<ModuleInfo> ListModules(DWORD pid) {
  std::vector<ModuleInfo> modules;
  HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, pid);
  if (snapshot == INVALID_HANDLE_VALUE) {
    return modules;
  }
  MODULEENTRY32 entry = {};
  entry.dwSize = sizeof(entry);
  if (Module32First(snapshot, &entry)) {
    do {
      modules.push_back({reinterpret_cast<uint64_t>(entry.modBaseAddr), entry.modBaseSize,
                         entry.szModule});
    } while (Module32Next(snapshot, &entry));
  }
  CloseHandle(snapshot);
  return modules;
}

std::vector<DWORD> ListThreads(DWORD pid) {
  std::vector<DWORD> threads;
  HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
  if (snapshot == INVALID_HANDLE_VALUE) {
    return threads;
  }
  THREADENTRY32 entry = {};
  entry.dwSize = sizeof(entry);
  if (Thread32First(snapshot, &entry)) {
    do {
      if (entry.th32OwnerProcessID == pid) {
        threads.push_back(entry.th32ThreadID);
      }
    } while (Thread32Next(snapshot, &entry));
  }
  CloseHandle(snapshot);
  return threads;
}

// Refreshes scores and opens handles for newly seen threads. Returns the
// threads sorted by recent CPU usage, busiest first.
std::vector<DWORD> RankThreads(DWORD pid, std::map<DWORD, ThreadInfo>& threads,
                               double window_seconds) {
  for (DWORD tid : ListThreads(pid)) {
    auto& info = threads[tid];
    if (!info.handle) {
      info.handle = OpenThread(THREAD_QUERY_INFORMATION | THREAD_SUSPEND_RESUME |
                                   THREAD_GET_CONTEXT,
                               FALSE, tid);
    }
    if (!info.handle) {
      continue;
    }
    FILETIME creation = {}, exit = {}, kernel = {}, user = {};
    if (!GetThreadTimes(info.handle, &creation, &exit, &kernel, &user)) {
      continue;
    }
    const uint64_t kernel_now = FileTimeToU64(kernel);
    const uint64_t user_now = FileTimeToU64(user);
    const uint64_t delta = (kernel_now - info.last_kernel) + (user_now - info.last_user);
    info.last_kernel = kernel_now;
    info.last_user = user_now;
    // 100 ns ticks -> share of one core over the window.
    info.score = (double(delta) * 1e-7) / window_seconds;
  }

  std::vector<std::pair<double, DWORD>> ranked;
  for (const auto& [tid, info] : threads) {
    if (info.handle) {
      ranked.push_back({info.score, tid});
    }
  }
  std::sort(ranked.begin(), ranked.end(), std::greater<>());
  std::vector<DWORD> result;
  for (const auto& [score, tid] : ranked) {
    if (score <= 0.01) {
      break;
    }
    result.push_back(tid);
  }
  return result;
}

void WriteHeader(FILE* file, const std::vector<ModuleInfo>& modules, const SYSTEMTIME& start) {
  std::fprintf(file, "# start_local=%04u-%02u-%02uT%02u:%02u:%02u\n", start.wYear, start.wMonth,
               start.wDay, start.wHour, start.wMinute, start.wSecond);
  for (const auto& module : modules) {
    std::fprintf(file, "# module=%s,0x%llX,0x%llX\n", module.name.c_str(),
                 static_cast<unsigned long long>(module.base),
                 static_cast<unsigned long long>(module.size));
  }
  std::fprintf(file, "time_ms,tid,rip,ra\n");
}

}  // namespace

int main(int argc, char** argv) {
  const char* process_name = argc > 1 ? argv[1] : "mx_alive.exe";
  const int seconds = argc > 2 ? std::atoi(argv[2]) : 180;
  const int top_n = argc > 3 ? std::atoi(argv[3]) : 6;
  const char* out_path = argc > 4 ? argv[4] : "out/rip-samples.csv";

  std::printf("Waiting for %s...\n", process_name);
  DWORD pid = 0;
  const DWORD wait_start = GetTickCount();
  while (!(pid = FindProcessId(process_name))) {
    if (GetTickCount() - wait_start > 300000) {
      std::fprintf(stderr, "Process not found within 5 minutes\n");
      return 1;
    }
    Sleep(500);
  }
  std::printf("Sampling PID %lu\n", pid);

  HANDLE process = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, pid);
  if (!process) {
    std::fprintf(stderr, "OpenProcess failed: %lu\n", GetLastError());
    return 1;
  }

  const std::vector<ModuleInfo> modules = ListModules(pid);
  FILE* file = std::fopen(out_path, "w");
  if (!file) {
    std::fprintf(stderr, "Cannot open %s\n", out_path);
    return 1;
  }
  SYSTEMTIME start = {};
  GetLocalTime(&start);
  WriteHeader(file, modules, start);

  std::map<DWORD, ThreadInfo> threads;
  std::vector<DWORD> selected;
  LARGE_INTEGER freq, begin;
  QueryPerformanceFrequency(&freq);
  QueryPerformanceCounter(&begin);

  DWORD last_rank = GetTickCount();
  DWORD last_second = GetTickCount();
  uint64_t total_samples = 0;
  while (GetTickCount() - last_second < DWORD(seconds) * 1000) {
    // Handle exit.
    DWORD code = 0;
    if (!GetExitCodeProcess(process, &code) || code != STILL_ACTIVE) {
      break;
    }

    const DWORD now = GetTickCount();
    if (now - last_rank >= 1000) {
      selected = RankThreads(pid, threads, double(now - last_rank) / 1000.0);
      if (selected.size() > size_t(top_n)) {
        selected.resize(top_n);
      }
      last_rank = now;
    }

    LARGE_INTEGER counter;
    QueryPerformanceCounter(&counter);
    const double time_ms = double(counter.QuadPart - begin.QuadPart) * 1000.0 / double(freq.QuadPart);

    for (DWORD tid : selected) {
      auto it = threads.find(tid);
      if (it == threads.end() || !it->second.handle) {
        continue;
      }
      if (SuspendThread(it->second.handle) == DWORD(-1)) {
        continue;
      }
      CONTEXT context = {};
      context.ContextFlags = CONTEXT_CONTROL;
      if (GetThreadContext(it->second.handle, &context)) {
        // The return address at [RSP] identifies the caller of leaf functions
        // such as memcpy (whose implementations here do not push registers).
        uint64_t return_address = 0;
        SIZE_T read = 0;
        ReadProcessMemory(process, reinterpret_cast<LPCVOID>(context.Rsp), &return_address,
                          sizeof(return_address), &read);
        std::fprintf(file, "%.1f,%lu,0x%llX,0x%llX\n", time_ms, tid,
                     static_cast<unsigned long long>(context.Rip),
                     static_cast<unsigned long long>(return_address));
        ++total_samples;
      }
      ResumeThread(it->second.handle);
    }
    Sleep(10);
  }

  std::printf("Wrote %llu samples from %zu threads to %s\n",
              static_cast<unsigned long long>(total_samples), threads.size(), out_path);
  for (auto& [tid, info] : threads) {
    if (info.handle) {
      CloseHandle(info.handle);
    }
  }
  std::fclose(file);
  CloseHandle(process);
  return 0;
}
