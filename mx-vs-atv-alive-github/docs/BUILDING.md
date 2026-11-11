# Building and running

This document describes a from-scratch Windows build of the native
recompilation. The project was developed on Windows 11 x64 with the toolchain
below; other versions are likely to work but have not been tested.

## Tested toolchain

| Component | Version used | Notes |
|---|---|---|
| OS | Windows 11 x64 (build 26200) | Windows 10 should work |
| Compiler | Clang 22.1.8 | rexglue requires Clang 18+ |
| MSVC headers/libs | VS 2022 Build Tools, MSVC 14.44 | Used by Clang for the C++ standard library and Windows SDK |
| Windows SDK | 10.0.26100 | |
| CMake | 3.31.8 | 3.25+ required |
| Ninja | 1.13.2 | |
| Node.js | 24.21.0 | Dev tools only |
| SDK | rexglue-sdk v0.10.0 | Pinned submodule |
| GPU | AMD Radeon RX 7800 XT | D3D12 backend |

Python and the Vulkan SDK are not required.

## 1. Clone

```bat
git clone --recursive https://github.com/gw1337x/mx-vs-atv-alive-recomp.git
cd mx-vs-atv-alive-recomp
```

If the submodule was not fetched:

```bat
git submodule update --init --recursive
```

The rexglue submodule is large (about 545 MB to download and more with its
build output). Leave room for the game (about 2.7 GB) and the build tree.

## 2. Build and install rexglue

```bat
tools\build-rexglue.bat
```

This script:

1. Initializes the MSVC x64 environment through `tools\dev-env.bat`.
2. Materializes the libmspack symlinks that Git for Windows checks out as text
   files (`tools\fix-rexglue-symlinks.ps1`). The SDK build fails without this.
3. Applies `patches/rexglue-local.patch` to the submodule if it is not already
   applied. The patch adds the async readback worker, FPS telemetry, crash
   diagnostics, level-load logging, and the 1000 Hz refresh clamp.
4. Configures and builds the `win-amd64` preset and installs it to
   `third_party\rexglue-sdk\out\install\win-amd64`.

The installed package is found automatically by `CMakePresets.json` through
`CMAKE_PREFIX_PATH`. The first SDK build is the longest step.

If the patch fails to apply, the submodule has diverged from v0.10.0 or already
contains other changes. The script warns and builds the tree as-is; inspect
`git -C third_party\rexglue-sdk status` before trusting the result.

## 3. Add the game files

The recompiler needs the user's own copy of the Xbox 360 release. Place the
complete disc contents under:

```
Game\MX vs. ATV Alive (USA Europe)\
    default.xex
    ...the rest of the game tree...
```

`default.xex` is required at build time. The rest of the files are required at
runtime (`--game_data_root` points at this directory).

The manifest refers to `game/mx vs. atv alive (usa europe)/default.xex`.
Windows paths are case-insensitive, so the exact directory casing is not
important, but the filename must remain `default.xex`.

Do not commit anything under `Game/`; it is gitignored.

## 4. Build the game

```bat
tools\build-game.bat
```

The script:

1. Verifies that `default.xex` and the installed `rexglue.exe` exist.
2. On the first build, runs `rexglue codegen mx_alive_manifest.toml` to generate
   `generated/default/`. This is required before the first CMake configure,
   because the project cannot create the recompiled-object target until
   `generated/default/sources.cmake` exists.
3. Configures the `win-amd64-release` preset and builds it.

Outputs:

- `out\build\win-amd64-release\mx_alive.exe`
- `rexruntime.dll` and `rexgpu-xenos.dll`, staged beside the executable
- `out\mx_alive.map` (linker map used by the crash-symbolization tools)
- `out\build\win-amd64-release\logs\mx_alive_NNN.log`

Later builds regenerate the recompiled code through the CMake codegen
dependency. The recompiled code is not committed.

## 5. Run

```bat
tools\run-game.bat 60
```

The launcher snapshots the save first (`tools\backup-save.ps1`) and starts:

```
mx_alive.exe
  --game_data_root "<repo>\Game\MX vs. ATV Alive (USA Europe)"
  --mnk_mode
  --gpu_allow_invalid_fetch_constants
  --d3d12_readback_resolve
  --query_occlusion_fake_sample_count=-1
  --video_mode_refresh_rate <fps * 2>
```

### Frame-rate presets

The game performs **two guest vblank waits per frame**, so:

```
guest FPS = guest video-mode refresh rate / 2
```

The launchers therefore pass `--video_mode_refresh_rate = target FPS * 2`.

| Launcher | Target FPS | Guest refresh | Status |
|---|---|---|---|
| `tools\run-game-30.bat` | 30 (Console) | 60 Hz | Verified 30.0 fps |
| `tools\run-game-60.bat` | 60 | 120 Hz | Verified ~59–60 fps |
| `tools\run-game-120.bat` | 120 | 240 Hz | Best-effort; ~85–95 fps in races |
| `tools\run-game-144.bat` | 144 | 288 Hz | Best-effort; target not reached |
| `tools\run-game-240.bat` | 240 | 480 Hz | Best-effort; target not reached |
| `tools\run-game.bat 500` | uncapped experiment | 1000 Hz | Used to separate pacing from CPU limits |
| `tools\run-game-1080p.bat [fps]` | 1080p guest mode | fps × 2 | Verified; no draw scaling |
| `tools\run-game-2k.bat [fps]` | 2× draw scale | fps × 2 | **Unsafe**: artifacts and a mid-save crash |
| `tools\run-game-120-sync.bat` | 120, sync readback | 240 Hz | A/B against the async worker |
| `tools\run-game-debug.bat [fps]` | any | fps × 2 | Debug log level |

### Runtime flags

| Flag | Why |
|---|---|
| `--game_data_root <dir>` | Points the VFS at the game files |
| `--d3d12_readback_resolve` | **Required for this title.** Without it the 3D world renders black and `k_4_4_4_4 signed` errors flood the log. Xenia's compatibility notes independently require readback resolve. |
| `--gpu_allow_invalid_fetch_constants` | Suppresses invalid fetch-constant failures seen during boot |
| `--query_occlusion_fake_sample_count=-1` | Xenia workaround for the blinding sun; confirmed at 30 and 60 fps |
| `--mnk_mode` | Keyboard/mouse input |
| `--video_mode_refresh_rate <n>` | Guest vblank rate; set to twice the desired frame rate |
| `--resolution_scale=2` | 2× draw resolution (2560×1440 from a 1280×720 guest mode); unsafe |
| `--no-d3d12_readback_resolve_async` | Disables the async copy worker for A/B testing |
| `--log_level debug` | Verbose logs, including the loading-screen hook diagnostics |

The async readback worker is a local SDK change and defaults to on. Disabling it
restores the original synchronous memcpy path and costs roughly 9% in races.

## Saves and backups

Save data lives outside the repository:

```
%USERPROFILE%\Documents\mx_alive\
    5451087B\00000001\SaveGame\SAVEGAME
    Headers\00000001\SaveGame.header
    cache\                       (shader cache)
```

`tools\backup-save.ps1` copies everything except `cache` to
`%USERPROFILE%\Documents\mx_alive.bak-<timestamp>` and keeps the newest 20
automatic backups. `tools\run-game.bat` calls it before every launch. A backup
can be restored by copying its contents back into `Documents\mx_alive`.

The game writes the save when returning to the career/menu flow. Exiting
directly from a results screen did not persist an event's progress in testing.

## Logs and measuring FPS

Logs are written to:

```
out\build\win-amd64-release\logs\mx_alive_NNN.log
```

Useful lines:

```
guest VdSwap: N frames / M ms = X fps     <- actual guest/game frame rate
present: N frames / M ms = X fps          <- host presentation rate, NOT game FPS
mx_alive: level loaded: ...               <- level coverage (local SDK patch)
```

The host presenter is decoupled from the guest: it commonly runs at 270–330 fps
while the game runs at 60. Third-party overlays and the present counter report
that host rate, not game speed. The F3 debug overlay exists but has no stats
provider wired in this project; use the `VdSwap` log line.

## Regenerating code and editing the manifest

`mx_alive_manifest.toml` controls codegen. It contains:

- the entrypoint XEX and output directory;
- explicit `[entrypoint.functions."0x..."]` boundaries for indirect targets the
  analyzer could not discover, including a 27-entry virtual/adjustor thunk
  table at `0x82327BF8`–`0x82327D90`;
- two `[[entrypoint.midasm_hook]]` entries that skip null loading-screen
  dereferences during the save transition.

After editing the manifest, run `tools\build-game.bat`; CMake's codegen
dependency regenerates the changed sources. To force a full regeneration,
delete `generated\default\codegen.stamp` or run:

```bat
third_party\rexglue-sdk\out\install\win-amd64\bin\rexglue.exe codegen --ignore-stamp mx_alive_manifest.toml
```

To find newly missed indirect targets, `tools\boot-iterate.js` automates the
launch → read fatal address → extend manifest → rebuild loop. It expects the
default repository layout and the Release build to exist.

## Local SDK patch contents

`patches/rexglue-local.patch` is relative to rexglue-sdk v0.10.0. It changes:

| File | Change |
|---|---|
| `include/rex/graphics/d3d12/command_processor.h`, `src/graphics/d3d12/command_processor.cpp` | `ReadbackCopyWorker`: offloads readback-resolve memcpy to a worker thread; cvar `d3d12_readback_resolve_async` (default true); waits at frame close, buffer reuse, and eviction |
| `src/kernel/xboxkrnl/xboxkrnl_video.cpp` | Guest `VdSwap` FPS telemetry every 2 s; refresh clamp raised from 240 to 1000 Hz |
| `src/ui/window.cpp` | `video_mode_refresh_rate` range raised to 1000 Hz |
| `src/ui/d3d12/d3d12_presenter.cpp` | Host present-rate telemetry every 2 s |
| `src/system/mmio_handler.cpp` | Logs host RIP, module base, and RVA on an unhandled guest access violation |
| `src/graphics/d3d12/texture_cache.cpp` | Maps signed `k_4_4_4_4` through the unsigned load path (visibility-first fallback; may be redundant now that readback resolve is on) |
| `src/graphics/CMakeLists.txt`, `src/system/CMakeLists.txt` | Emit `rexgpu-xenos.map` and `rexruntime.map` for RIP symbolization |
| `src/kernel/xboxkrnl/xboxkrnl_io.cpp` | Logs successful `.level` opens for level-coverage tracking |

The libmspack symlink fix is a script, not part of the patch, because it
materializes files inside a nested submodule.

If the SDK is moved to a newer release, re-apply or forward-port the patch.
Generic improvements (async readback worker, telemetry, crash diagnostics) are
good candidates to send upstream.

## Troubleshooting

### CMake cannot find rexglue

```
ReXGlue SDK not found
```

Run `tools\build-rexglue.bat` first. The Windows presets set
`CMAKE_PREFIX_PATH` to
`third_party/rexglue-sdk/out/install/win-amd64`. If the SDK was installed
elsewhere, pass `-DCMAKE_PREFIX_PATH=<prefix>` or set `REXSDK_DIR` to the SDK
source tree.

### The SDK build fails in libmspack

Git for Windows checked out symlinks as text pointers. Run
`powershell -NoProfile -ExecutionPolicy Bypass -File tools\fix-rexglue-symlinks.ps1 -Sdk third_party\rexglue-sdk`,
or simply rerun `tools\build-rexglue.bat`.

### Visual Studio 2026 is installed but the build cannot find C++ headers

The development machine had VS 2026 Community without the MSVC C++ standard
library installed. `tools\dev-env.bat` selects the newest Visual Studio
installation that has the C++ workload; it fell back to VS 2022 Build Tools
14.44. Install the **Desktop development with C++** workload in any VS 2022/2026
installation, or adjust the fallback path in `tools\dev-env.bat`.

### Codegen reports an unresolved branch target

The analyzer found a function boundary it cannot prove. Add an explicit entry:

```toml
[entrypoint.functions."0xADDRESS"]
end = 0xENDADDRESS
```

`tools\boot-iterate.js` does this automatically for indirect-call failures.

### The game exits with `0xC0000409` after a fatal log line

An indirect call reached a guest address that was not recompiled. Register the
function in the manifest, regenerate, and rebuild. The log line names the
address. This was the main bring-up loop for M1/M2; `tools\boot-iterate.js`
automates it.

### The 3D world is black but the HUD renders

`--d3d12_readback_resolve` is missing. The launchers pass it by default.

### `Unsupported texture formats ... k_4_4_4_4 signed`

This disappears when readback resolve is enabled. The local texture-cache patch
is a fallback and may no longer be necessary.

### Blinding sun

`--query_occlusion_fake_sample_count=-1` is the Xenia compatibility workaround.
It is passed by the launchers and is not frame-rate-dependent.

### Save corrupted or truncated

Restore the newest `%USERPROFILE%\Documents\mx_alive.bak-*` directory into
`%USERPROFILE%\Documents\mx_alive`. The 2K preset is the only known trigger so
far; automatic backups are made before every launch.

### Illegal instruction or a binary that only runs on the build machine

The game targets are built with `-march=native` and LTO. Rebuild on the target
machine, or remove the `-march=native` options from `CMakeLists.txt`.

### Missing camera BXML warnings

```
game:\Cameras\{PROTruck,Buggy,UTV,PRO2Truck}_Camera.bxml
```

These files are not present in the tested dump. The warnings are harmless.

### `dxcompiler.dll` is absent

Only DXIL debug disassembly is affected; normal rendering works.

## Updating the pinned SDK

1. Check out the new rexglue release in `third_party/rexglue-sdk`.
2. Forward-port `patches/rexglue-local.patch` and commit the updated patch.
3. Run `tools\build-rexglue.bat` and `tools\build-game.bat`.
4. Re-run the status tests in [STATUS.md](STATUS.md), especially readback and
   the high-FPS presets; both are sensitive to renderer changes.
