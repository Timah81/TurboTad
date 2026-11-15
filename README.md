# TurboTad

A six-degree-of-freedom (6DOF) Tadpole flight-control mod for **Subnautica 2** (stable branch), developed for Windows. The project pairs a C# Windows controls/configuration helper with UE4SS Lua gameplay scripts.

> **Status: private pre-release staging.** The source baseline and first-time installation procedure are being prepared. The repository is not yet a certified public release or a complete ready-to-install mod.

## What it does

- Independent pitch, yaw, roll, forward/reverse, lateral and vertical movement, including inverted flight
- Adjustable response and rotational rates, plus a normal top-speed setting
- Directional boost with cooldown and HUD feedback
- A Windows controls GUI for configuring the currently supported joystick setup
- Optional local, user-selected WAV playback for boost sound

This feature branch contains **Universal Input Mapper stage 1 — a test candidate**. Each flight axis can use a WinMM analog axis or two keyboard/WinMM button directions, with mixed devices, separate boost binding and input-only profiles. Existing joystick mappings and the accepted v7 numeric editors are retained. XInput-specific trigger pairs and relative mouse input remain later stages; this candidate has not passed Windows hardware or in-game acceptance.

To test against an existing working installation, close the game/helper and run `InstallUniversalMapper.cmd`. It compiles and runs isolated tests before replacing the helper and backs up the existing executable and mapping. `RollbackUniversalMapper.cmd` restores that pre-install pair and preserves newer bindings in a separate checkpoint.

## Architecture

- `Source/`: C# Windows helper, GUI code and self-tests
- `Scripts/`: UE4SS Lua modules
- `Assets/`: icons and GUI images used by the helper
- `BuildAndVerify.cmd` / `BuildAndVerify.ps1`: Windows source build and isolated self-tests

UE4SS is a **separate dependency** and is not included in this source repository. Compatibility depends on the game and UE4SS versions; recorded development used the game's stable branch and UE4SS 3.0.1 Beta. These are observations, not a universal compatibility guarantee.

## Safety warning

**High speed can launch the Tadpole out of the game world when breaching the water surface.** Excessive speed or boost can also cause collisions or an unrecoverable gameplay session. Back up game saves and test cautiously in open water. No automatic high-speed surface limiter is promised.

## Building and verification

Once the source files are imported, Windows users can run `BuildAndVerify.cmd` to compile the C# helper in a temporary directory and run isolated audio/GUI checks. These checks do **not** replace in-game regression testing or establish source-to-binary equivalence.

## Distribution and rights

The original project code/documentation is MIT-licensed; see `LICENSE`. Subnautica 2, UE4SS, Unreal Engine and other dependencies remain under their respective terms. Do not redistribute captured game audio, personal controller mappings, save data, or another project's binaries as part of TurboTad.

This repository tracks source history. Runtime release archives should be created and validated separately.

## TEST 1A acceptance — 2026-11-14

The user reports that TEST 1A fixed the reported Start/conflict problem. Extract the complete ZIP before running InstallUniversalMapper.cmd; launching it inside the archive can leave its companion PowerShell script unavailable. This acceptance covers the reported fix; broader hardware/gameplay regression and later adapters remain pending.
