# Credits and third-party notices

This project is a thin layer of game-specific work on top of an enormous amount
of other people's effort. The core recompilation stack, the compatibility
research, and the reverse-engineering tools below made it possible.

## Core recompilation stack

- **[rexglue-sdk](https://github.com/rexglue/rexglue-sdk) v0.10.0** — Tom Clay
  (`tomcl7`) and contributors. BSD-3-Clause. The pinned submodule in
  `third_party/rexglue-sdk` is the entire recompiler, runtime, D3D12/Xenos GPU
  layer, XMA/XAudio2 audio path, input, VFS, and build integration. Portions are
  derived from Xenia.
- **[Xenia](https://github.com/xenia-project/xenia)** — Ben Vanik and the Xenia
  project contributors. BSD-3-Clause. Xenia's Xbox 360 emulation research is the
  foundation of rexglue's runtime and its Xenos GPU implementation. See the
  license header in the rexglue submodule.
- **[XenonRecomp](https://github.com/hedge-dev/XenonRecomp)** — hedge-dev. MIT.
  Pioneered the modern ahead-of-time PowerPC recompilation approach; rexglue's
  codegen analysis and instruction translation build on it.
- **[rexdex/recompiler](https://github.com/redex/recompiler)** — rexdex. MIT.
  The original Xbox 360 static recompiler and an important influence on the
  lineage.
- **[XenosRecomp](https://github.com/hedge-dev/XenosRecomp)** — hedge-dev. MIT.
  The native-renderer alternative to the Xenos plugin; not used here, but it is
  the escape hatch if the reference renderer ever blocks progress.

## Compatibility and performance research

- **[Xenia Canary game-compatibility issue #596](https://github.com/xenia-canary/game-compatibility/issues/596)**
  — the compatibility notes for this title. They identified the
  `readback_resolve` requirement and the
  `query_occlusion_fake_sample_count = -1` sun workaround used by the launchers.
- **[Digital Foundry, Xbox 360 vs PS3 Round 31](https://www.digitalfoundry.net/articles/digitalfoundry-xbox360-vs-ps3-round-31-face-off?page=5)**
  — the source for the original 1280×720 / 30 fps target and the v-sync
  behaviour.
- The broader Xbox 360 homebrew and modding communities whose documentation and
  tooling made the data formats and platform behaviour tractable.

## Game-data format research

These were used as references for understanding Rainbow Studios' data formats.
No source code from them is included in this repository.

- **[XeNTaX BXML reverse-engineering notes](https://github.com/XeNTaXBackup/XeNTaXBackup.github.io/blob/master/markdown/MX%20vs%20ATV%20Reflex%20BXML%20files_5479.md)**
  — BXML format documentation.
- **[bxml-tool](https://github.com/x1nixmzeng/bxml-tool)** — x1nixmzeng.
- **[reflex-bxml-editor](https://github.com/daniilkorochansky/reflex-bxml-editor)**
  and **[reflex-package-tool](https://github.com/daniilkorochansky/reflex-package-tool)**
  — daniilkorochansky. GPL-3.0; research reference only, not linked or copied.
- **[reflex-font-package-viewer](https://github.com/daniilkorochansky/reflex-font-package-viewer)**
  — daniilkorochansky. GPL-3.0; research reference only.
- **QuickBMS `mx_atv.bms`** — Luigi Auriemma (`aluigi`).
  [aluigi.altervista.org/bms/mx_atv.bms](https://aluigi.altervista.org/bms/mx_atv.bms)
  — archive extraction script for the MX vs. ATV series.

## Platform and build tools

- **Microsoft Windows, Direct3D 12, XAudio2, XInput, the Windows SDK, and the
  MSVC C++ standard library** — host platform.
- **[LLVM/Clang](https://llvm.org/)** — compiler used for the SDK, the
  recompiled code, and the host executable.
- **[CMake](https://cmake.org/)**, **[Ninja](https://ninja-build.org/)**,
  **[Git for Windows](https://gitforwindows.org/)**,
  **[Node.js](https://nodejs.org/)**, and **PowerShell** — build and tooling.
- **SDL3, fmt, spdlog, CLI11, inja, Tracy, xxHash, libmspack, and the other
  bundled dependencies** in rexglue-sdk — their licenses are in
  `third_party/rexglue-sdk/thirdparty/*/LICENSE` (or the equivalent file in
  each dependency).

## Game and middleware

- **MX vs. ATV Alive** was developed by **Rainbow Studios** and published by
  **THQ** in 2011. The game, its engine ("Ravage"), assets, and trademarks
  belong to their respective owners. This project is not affiliated with or
  endorsed by them.
- **Bink Video** (RAD Game Tools, now part of Epic Games) — the game's video
  middleware. Bink code is recompiled as guest code; no Bink SDK is
  distributed here.
- **FMOD** (Firelight Technologies) — the game's audio middleware, likewise
  recompiled as guest code where it is statically linked.

## Project

- **gw1337x** — initial recompilation work, bring-up, testing, performance
  investigation, tooling, and documentation.
- **Claude Code (Anthropic)** — development, debugging, profiling, and
  documentation assistance. The native Windows bring-up, SDK patches, test
  runs, and these docs were produced with its help.

## License notes

The project's own code and documentation are BSD-3-Clause; see
[../LICENSE](../LICENSE). The pinned rexglue-sdk submodule retains its own
BSD-3-Clause license and third-party notices. No game code, game assets,
encryption keys, or generated recompiled code is distributed in this
repository.
