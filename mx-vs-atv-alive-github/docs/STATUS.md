# Project status and test record

Snapshot date: **2026-10-19**. This document records what was tested, what was
measured, what is still in progress, and where the next maintainer should
start. It is a curated public summary of the development log, not a promise
that the same results reproduce on every machine.

## Milestones

| Milestone | State | Evidence |
|---|---|---|
| M0 — Toolchain | ✅ Passed 2026-10-19 | Retail XEX decrypted and ingested; 395 generated files / 196 translation units; 41,682 recompiled functions registered; executable ran guest code |
| M1 — Boot | ✅ Passed 2026-10-19 | Main menu rendered and navigated; Bink attract videos, XMA/XAudio2 audio, XInput input and a PC save all worked |
| M2 — Vertical slice | ✅ Passed 2026-10-19 | One Short Track race completed start-to-finish and Free Ride entered, with full HUD/audio and no crashes observed |
| M2.5 — 60 fps | ✅ Passed 2026-10-19 | Guest `VdSwap` measured ~58.5–60 fps with a 120 Hz guest refresh |
| M3 — Full single-player | 🟡 In progress | One National event completed (2,517 Rider XP / 1,050 bike XP; achievement `0x17` unlocked); the other levels and the full progression graph are not validated |
| M4 — Splitscreen | ⬜ Not started | |
| M5 — 120/144/240 | 🟡 Best-effort, closed | 60 is the supported rate; higher presets are CPU-bound below target. Async readback worker added |
| M6 — Polish | ⬜ Not started | Enhancements, DLC, online/LIVE revisit |

## Test environment

| Item | Value |
|---|---|
| CPU | AMD Ryzen 7 9800X3D (16 logical cores) |
| GPU | AMD Radeon RX 7800 XT |
| OS | Windows 11 x64 build 26200 |
| Compiler | Clang 22.1.8 with MSVC 14.44 headers/libs |
| Renderer | rexglue D3D12 Xenos plugin |
| SDK | rexglue-sdk v0.10.0 with `patches/rexglue-local.patch` |

Game image: retail `default.xex`, Title ID `5451087B`, Media ID `5306E1D5`,
version `0.0.0.3`, filetime 2011-04-02. Game tree: about 2.7 GB / 385 files,
including 20 level files (12 `NAT_*`, 4 `ST_*`, 2 `FR_*`, plus `UI_World` and
`Model_Viewer_Tool`), 131 `.xenon.database`/`.xenon.package` pairs, 42 Bink
videos, and 29 BXML configs.

## Feature test matrix

| Area | Test | Result | Notes |
|---|---|---|---|
| Boot | Load retail XEX | ✅ Pass | Decrypted directly; no custom protection |
| Boot | Recompile and link | ✅ Pass | 41,682 functions; `mx_alive.exe` ~67 MB |
| Boot | Reach main menu | ✅ Pass | Requires the two loading-screen hooks in the manifest |
| Video | THQ splash and attract videos | ✅ Pass | Bink guest middleware executes and renders |
| Menus | Navigate front end | ✅ Pass | Controller input |
| Audio | XMA decode / XAudio2 output | ✅ Pass | 48 kHz endpoint opened |
| Input | XInput controller | ✅ Pass | |
| Input | Keyboard and mouse | ✅ Pass | `--mnk_mode` |
| Save | Create profile and save | ✅ Pass | Writes `SAVEGAME` (8,959 bytes) and its header |
| Save | Reload after restart | ✅ Pass | |
| Save | Exit-to-career write path | ⚠️ Caveat | Returning to the career/menu flow saves; exiting from the results screen did not |
| Race | Short Track start-to-finish | ✅ Pass | Full HUD, audio, no crash observed at 30 and 60 fps |
| Race | Free Ride | ✅ Pass | Playable; water corruption when driving into water |
| Race | National event | ✅ Pass once | 2,517 Rider XP / 1,050 bike XP; one achievement unlock |
| Frame rate | Console 30 (refresh 60) | ✅ Pass | 30.0 guest fps measured |
| Frame rate | 60 (refresh 120) | ✅ Pass | ~58.5–60 guest fps measured |
| Frame rate | 120 (refresh 240) | ⚠️ Best-effort | Races measured ~85–95 fps |
| Frame rate | 144 / 240 | ⚠️ Target not reached | CPU-bound before the requested rate |
| Display | 1080p guest mode | ✅ Pass | 1920×1080 guest video mode, no draw scaling |
| Display | 2× draw scale (2K) | ❌ Unsafe | Artifacts and a mid-save crash; can truncate the save |
| Splitscreen | 2 players | ⬜ Untested | |
| DLC | MotoClub Depot content | ⬜ Untested | Deferred until base-game parity; DLC files not present |
| Online / LIVE | Multiplayer, party, mic | ⬜ Out of scope | Failure-returning stubs are the agreed approach |
| Achievements | Unlock notification | ⚠️ Observed once | Event-completion achievement `0x17`; leaderboards untested |

## Performance measurements

### Preset ceiling

Measured from `guest VdSwap` telemetry in 2-second windows. Race windows were
bounded by the frame-rate discontinuity at race start and the achievement
unlock at race end; menu samples are excluded.

| Run | Configuration | Race mean | Median | p90 | Menus |
|---|---|---:|---:|---:|---:|
| Log 051 | 120 preset, pre-optimization | 79.2 | 79.3 | 86.2 | 120 cap |
| Log 052 | 120 preset, `-march=native` + LTO | 86.5 | 85.2 | 100.0 | 120 cap |
| Log 053 | Uncapped (1000 Hz guest refresh) | 74.4 | 71.4 | 89.6 | 192–302 |

LTO and `-march=native` bought roughly 7–9%. The uncapped run stayed below
90 fps in race even with no pacing target, so pacing is not the limiter. Menus
are cheap; the bottleneck is race simulation and per-frame draw submission.
One confound: the uncapped race used a different track/bike than the preset
runs.

### Where the CPU time went

Per-thread sampling (log 055) joined to the race window:

| Thread | Average CPU | Max CPU |
|---|---:|---:|
| GPU command processor | 81% | 95% |
| Guest thread calling `VdSwap` | 58% | 74% |
| Unidentified guest/host thread | 51% | 75% |
| Process total | ~280% | |

The command processor was the binding constraint at about 11.6 ms/frame.
RIP sampling (logs 057/058) showed:

- ~33% of command-processor samples in VCRUNTIME `memcpy`, and **76% of those
  from `D3D12CommandProcessor::IssueCopy_ReadbackResolvePath`**.
- ~24% in ntdll, mostly waits and syscall stubs (`ZwWaitForSingleObject` 11%,
  `ZwProtectVirtualMemory` 2%, condition-variable/SRW waits ~2%).
- ~30% spread across command translation (`WriteRegister`, `GetRegisterInfo`,
  `UpdateBindings`, `ExecutePacket`, `IssueDraw`, render-target cache work).

Readback instrumentation (logs 059/060):

- ~7,500 resolves per 2 s (~3,800/s), ~12 GB copied per 2 s (~6 GB/s).
- Only 27 unique resolve targets; each is resolved about once per frame.
- Average ~1.6 MB per resolve, ~50–85 MB copied per frame.
- `readback_resolve=some` skips the copies but corrupts textures, so eager
  copies are required for correctness.

### Async readback worker

`ReadbackCopyWorker` (local SDK patch) moves the resolve memcpy to a dedicated
thread. Same-track A/B within one race (log 063):

| Mode | Mean | Median |
|---|---:|---:|
| Async worker on | 92.6 fps | **93.5 fps** |
| Async worker off (synchronous) | 86.8 fps | 85.7 fps |

That is roughly +9% in race; Free Ride reached 110–120 fps. After the change,
the command processor's VCRUNTIME share fell from ~33% to ~5%. At the high
rates the remaining budget is tight: command processor ~8.2 ms/frame and two
guest threads ~7.6/6.7 ms against an 8.33 ms frame at 120 fps.

## Verified mechanics

### Two vblanks per frame

The game waits two guest vblanks per frame. Measured guest frame rate is
therefore exactly half the guest video-mode refresh rate:

| Guest refresh | Measured guest FPS |
|---|---|
| 60 Hz | 30.0 |
| 120 Hz | ~59–60 |
| 240 Hz | ~85–95 in race |
| 288 Hz | ~73–95 in race |
| 1000 Hz | ~54–129, mean ~74 in race |

The launchers map target FPS to `video_mode_refresh_rate = target × 2`. The SDK
refresh clamp was raised from 240 to 1000 Hz to allow the high presets.

### Host present rate is not game frame rate

The D3D12 host presenter runs decoupled from the guest and measured ~270–330
fps while the game ran at 60. Third-party overlays and the `present:` log line
report that host rate. Use the `guest VdSwap` line to measure game speed.

### Readback resolve is mandatory

Without `--d3d12_readback_resolve`, the 3D world rendered black (HUD and audio
still worked) and `Unsupported texture formats ... k_4_4_4_4 signed` appeared
thousands of times per session. Enabling it fixed the world and eliminated the
format errors. Xenia Canary's compatibility issue for this title independently
requires `readback_resolve`.

### Occlusion-query workaround

The Xenia workaround `query_occlusion_fake_sample_count=-1` removes the
blinding sun. It was reproduced at both 30 and 144 fps, so the bug is not
frame-rate-dependent.

### Level scope

On-disk level files confirm the scope: 12 `NAT_*` National venues, 4 `ST_*`
Short Tracks, and 2 `FR_*` Free Ride areas, plus `UI_World` and
`Model_Viewer_Tool`. One Short Track and Free Ride were played; one National
event was completed.

### Save behaviour

The game writes:

```
%USERPROFILE%\Documents\mx_alive\5451087B\00000001\SaveGame\SAVEGAME
%USERPROFILE%\Documents\mx_alive\Headers\00000001\SaveGame.header
```

The console save path is redirected to a PC directory. A completed National
event awarded 2,517 Rider XP and 1,050 bike XP. The save is written when the
game returns to the career/menu flow; quitting directly from the results screen
left the event unsaved in one test.

### Loading-screen Bink lookup

During the save transition, a lookup for a `"bink"` resource returns null and
the loading-screen init writes through it at guest path `sub_8234CE20 + 0x10B`
(store to object+148). A later loading-screen transition returned a valid
handle (`0x42005BA0`, `0x41B0F2F0` in other runs), so the resource system works
for some resources; only this lookup fails. Two mid-asm hooks skip the null
dereference:

- `mx_alive_loading_screen_has_bink` at `0x8234CE20` skips loading-screen init
  when object+148 is null.
- `mx_alive_loading_screen_object_valid` at `0x823A66B8` skips the null
  `object+412` dereference and leaves `manager+3600` unset so the per-frame
  update skips itself.

These are workarounds; the resource-system root cause is open.

### Missed indirect targets

rexglue's analyzer missed several indirect targets. The manifest now carries
explicit function boundaries for them, including a 27-entry virtual/adjustor
thunk table at `0x82327BF8`–`0x82327D90`. The thunk region is dumped at startup
and can be parsed with `tools\parse-thunks.js`. **The dump is still enabled**
and writes ~320 log lines per launch; it should be removed when no longer
needed.

## In progress: M3 full single-player

What has been done:

- One National event completed at 60 fps with a clean exit.
- Rider XP and bike XP accrual observed and persisted.
- Achievement `0x17` unlocked at event completion.
- The `.level` load log was added to the SDK patch so a playthrough can record
  which levels were exercised.
- The 18-level scope was confirmed from the game data.
- Save persistence was confirmed, including the exit-path caveat above.

What remains:

- Play all 12 National, 4 Short Track, and 2 Free Ride levels.
- Exercise event unlocks and the progression graph.
- Validate Rider XP 1–50 and per-vehicle XP across sessions.
- Look for per-level crashes, hangs, and rendering issues.
- Decide whether any remaining missing indirect targets are per-level or
  per-event.

Suggested protocol:

1. Start at the career menu with a fresh backup.
2. Complete an event and return to the career menu so the save is written.
3. Tail the log for `mx_alive: level loaded:` and for `Unregistered`.
4. Symbolize any crash RVA with `tools\symbolize.js`.
5. Restore a backup if a save is damaged.

## Known issues

### 1. Water textures corrupt in Free Ride

**Observed:** water renders correctly until the player drives into or through
it, then the water texture becomes visibly corrupted.

**Repro:** launch the 120 preset, enter Free Ride, drive into the water.

**Ruled out:** the async readback worker (reproduced with
`--no-d3d12_readback_resolve_async`) and the cvar experiment states.

**Hypotheses:** the eager readback resolve path provides previous-frame data
and water refraction/reflection reads the resolve result in the same frame; or
a water-specific texture format/resolve target is mishandled; or the
render-target cache clear/ownership path is involved.

**Next:** capture the corrupted frame, test `readback_resolve=full`, and log
which resolve targets change volume when entering water.

### 2. 2K draw scale crashed mid-save and truncated `SAVEGAME`

**Observed:** on 2026-10-19 at 20:22:37, a `resolution_scale=2` session hit an
unhandled guest access violation (read of guest `0x495EFB40`, crash site
`sub_82AC7908+0xD28`) while writing `SAVEGAME`. The file was left as a 36-byte
BXML header. An intact backup from 00:55 was restored; the 19:44 save was lost.

**Mitigation:** `tools\run-game.bat` snapshots the save before every launch;
`tools\run-game-2k.bat` prints a warning and waits 5 seconds. The 1080p guest
mode is unaffected and is the recommended sharpness option.

**Next:** reproduce with backups enabled and determine whether the trigger is
`resolution_scale=2`, the async worker, or a pre-existing save race. Inspect
`sub_82AC7908+0xD28` and the guest save path. Consider hard-disabling the 2K
preset.

### 3. High-FPS ceiling is CPU-bound

60 fps is the supported setting. Races measured ~85–95 fps after the async
readback offload; 120 was not reached. The command processor and two guest
threads are all close to the 8.33 ms frame budget. Further work needs broad
per-frame cost reductions, coalescing repeated resolves, or a shared-memory
resolve path that avoids CPU copies. Codegen/PGO and fixed-step simulation were
not expected to fix this.

### 4. Bink save-transition lookup root cause

See “Loading-screen Bink lookup” above. The two hooks are workarounds; the
resource lookup that returns null is still unexplained.

### 5. Signed 4:4:4:4 fallback may be redundant

`src/graphics/d3d12/texture_cache.cpp` maps signed `k_4_4_4_4` through the
unsigned load path. It did not fix the black world by itself, and the format
errors disappeared once readback resolve was enabled. Remove the patch and
retest before accepting it as necessary.

### 6. Temporary thunk dump still runs at startup

`mx_alive_dump_thunk_region()` writes ~320 log lines per launch. It was useful
for mapping the thunk table and can now be removed or gated behind a flag.

### 7. Save write depends on the exit path

Return to the career/menu flow after an event before quitting. This is likely
game behaviour, not a recomp bug, but it surprised testing and should be
documented for anyone running progression tests.

### 8. Incomplete behavioural/visual parity validation

There has been no pixel comparison against the console or Xenia. Xenia Canary
issue #596 runs the same title and needs `readback_resolve`,
`resolution_scale = 1`, and `query_occlusion_fake_sample_count = -1`, but it
shows its own sun/water artifacts and is only a rough behaviour oracle. Audio
parity, effects parity, and controller deadzones have not been measured.

### 9. Minor warnings

- Missing `Cameras\{PROTruck,Buggy,UTV,PRO2Truck}_Camera.bxml`: these files are
  not in the tested dump; harmless.
- Missing `dxcompiler.dll`: only DXIL debug disassembly is affected.
- `gpu_allow_invalid_fetch_constants` is passed by the launchers to suppress
  invalid fetch-constant failures during boot.

### 10. Untested or out of scope

Splitscreen (M4), DLC, online/LIVE, leaderboards, and the M6 polish features
(enhancements, settings UI) are not implemented or not validated. The original
scope excluded online and force-feedback wheels.

## Testing and profiling method

- **FPS:** parse `guest VdSwap: N frames / M ms = X fps` from the newest log.
  The value is a 2-second window. Exclude menu samples and bound race windows
  by the race-start discontinuity and the end-of-race achievement.
- **Per-thread CPU:** `tools\profile-race.ps1` samples WMI
  `Win32_PerfFormattedData_PerfProc_Thread` once per second and writes a CSV.
- **RIP sampling:** `tools\thread-sampler.cpp` records the busiest threads'
  instruction pointers; `tools\symbolize-rip.js` symbolizes them against
  `out\mx_alive.map`, `rexgpu-xenos.map`, and `rexruntime.map`.
- **Crash symbolization:** the local mmio-handler patch logs the host RIP,
  module base, and RVA for an unhandled guest access violation.
  `tools\symbolize.js <rva>` maps the RVA to the nearest generated symbol.
- **Missing functions:** `tools\boot-iterate.js` automates the
  launch/fatal/manifest/rebuild loop; `tools\parse-thunks.js` turns a guest code
  dump into manifest entries.
- **Oracle:** Xenia Canary was used only for compatibility hints and rough
  behaviour comparison, not pixel parity.

## Where to pick up

1. Finish the M3 level sweep and record a pass/fail table per level.
2. Triage water corruption with `readback_resolve=full`.
3. Reproduce the 2K save crash safely and either fix or hard-disable the preset.
4. Investigate the Bink resource lookup and replace the two hooks.
5. Profile an async race to find the new limiter; consider coalescing resolves.
6. Remove the startup thunk dump and retest the signed 4:4:4:4 fallback.
7. Start M4 splitscreen once M3 is stable.
8. Send generic SDK improvements upstream.
