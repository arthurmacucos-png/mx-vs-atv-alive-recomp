# MX vs. ATV Alive Static Recompilation

The project turns the Xbox 360 release of MX vs. ATV Alive into a native Windows
executable by recompiling its PowerPC code, then adds PC capabilities such as
higher frame rates. The vocabulary below separates the original console game
from the native build and names the game's own content and data concepts.

## Recompilation

**Guest**:
The original Xbox 360 PowerPC game — its executable, code, and memory space.
_Avoid_: original game, source binary

**Host**:
The native Windows x64 build that executes recompiled guest code.
_Avoid_: port, emulator

**Recompiled function**:
A C++ function generated from one guest function that performs the same work on
the host.
_Avoid_: translated function, decompiled function

**Glue**:
The runtime layer that connects recompiled code to host graphics, audio, input,
and file services.
_Avoid_: harness, shim, wrapper

**Coverage**:
The share of guest functions that execute as recompiled code rather than
falling back to interpretation.
_Avoid_: progress

## Game identity

**Ravage**:
Rainbow Studios' internal codename for the Alive engine and executable.
_Avoid_: engine name, Vicious Engine

## Game content

**National**:
One of 12 full-length outdoor motocross venues in the base game.
_Avoid_: outdoor track, circuit

**Short Track**:
One of 4 compact race courses with crossover sections.
_Avoid_: sprint track

**Free Ride**:
One of 2 open areas built around challenges rather than races.
_Avoid_: open world

**Rider XP**:
The player's overall progression level, 1–50, which unlocks events and
playlists.
_Avoid_: player level, rank

**Vehicle XP**:
Progression tied to a specific bike or ATV that unlocks and upgrades its parts.
_Avoid_: bike level

**MotoClub Depot**:
The game's in-game store for DLC vehicles, tracks, parts, and gear.
_Avoid_: shop, marketplace

## Performance

**Guest frame**:
One simulation frame of the original game, ending in a `VdSwap` call.
_Avoid_: render frame, present

**Guest refresh rate**:
The vblank rate reported to the guest video mode; Alive waits two vblanks per
frame, so guest FPS = refresh / 2.
_Avoid_: monitor refresh, display rate

**Present rate**:
The host's presentation frequency, decoupled from guest frames and not a
measure of game speed.
_Avoid_: FPS, game frame rate

**Console 30**:
The preset that reproduces the original two-vblank pacing at 60 Hz guest
refresh.
_Avoid_: original speed

## Game data

**XEX**:
The Xbox 360 executable container; `default.xex` holds the guest program.
_Avoid_: executable, binary

**Xenos**:
The Xbox 360 GPU, whose command stream and shaders the host must reproduce.
_Avoid_: 360 GPU

**BXML**:
Rainbow Studios' binary XML format used for configuration files, level
manifests, and asset indices.
_Avoid_: binary XML

**Database**:
A `.xenon.database` asset index describing the contents of its companion
package.
_Avoid_: index file

**Package**:
A `.xenon.package` asset heap holding compressed game resources, indexed by a
database.
_Avoid_: archive, pak

**Level manifest**:
A `.xenon.level` file naming the databases, packages, and subscenes that make
up one level.
_Avoid_: level file

**Scene**:
A `.xenon.scene` container holding a level's compiled scene data.
_Avoid_: map file
