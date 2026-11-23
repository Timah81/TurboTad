# TurboTad — v0.4.0

A six-degree-of-freedom (6DOF) Tadpole flight-control mod for **Subnautica 2 stable branch**, using a Windows controls/configuration helper and UE4SS Lua gameplay scripts.

**Release source:** The v0.4.0 runtime ZIP uploaded to Nexus was built from source commit `ee0efc405e6ce68dc0c45ecfc2b28bd66b47c5f5` and verified against its GitHub Actions artifact. The ZIP SHA-256 is `04287a30142ef6d422d7841f1d33824a782e84f37227970ed9f87413e6a63b22`. The tested build includes the Lua exception-logging fix. Availability on Nexus is subject to its security review.

## How it was made

**Developed with AI under human direction.** ChatGPT and DeepSeek were major contributors to code and technical research; Claude, ChatGPT, and other tools assisted with audits and reviews. The creator directed the project and personally tested and accepted its flight controls and GUI. See [HOW-IT-WAS-MADE.md](HOW-IT-WAS-MADE.md) for an open account of who did what, what was tested, and the limits of that testing.

## Features

- Independent pitch, yaw, roll, forward/reverse, strafe, and vertical movement, including inverted flight.
- Adjustable rotational and translational response, live top-speed control, and dynamically scaled swim acceleration.
- Directional boost, HUD feedback and optional local WAV sound playback.
- Windows GUI with VKB/WinMM and Xbox/XInput mapping, mixed analog/button control inputs, and saved controller profiles.
- Optional **Include flight tuning** setting when saving profiles; unchecked profiles save input bindings only.

## Prerequisites

1. A legitimate installation of **Subnautica 2 (stable branch)** on Windows.
2. **UE4SS**, installed separately and compatible with that game build. Development used UE4SS v3.0.1 Beta (build d7e7826d); other versions have not been verified.
3. Windows with the **.NET Framework 4.x runtime** for `TurboTad.Controls.exe`. The Windows build tooling uses the .NET Framework 4.x x64 C# compiler.

TurboTad does **not** bundle Subnautica 2, UE4SS, game DLLs, save data or Windows/.NET installers. No Steam Deck/Proton, multiplayer or future game-version compatibility guarantee is made.

## Manual installation

1. Exit Subnautica 2 and the TurboTad companion. **Back up your saves and your existing** `Subnautica2/Binaries/Win64/ue4ss/Mods/TurboTad/` directory, including personal `mapping.xml`, profiles and WAV files.
2. Install a compatible UE4SS separately, following its own instructions.
3. Extract the complete TurboTad runtime ZIP **into** `Subnautica2/Binaries/Win64/`, so that the extracted mod is at `Subnautica2/Binaries/Win64/ue4ss/Mods/TurboTad/`. Do not add an extra `TurboTad` folder layer.
4. Run `TurboTad.cmd` from `Binaries/Win64/`, or directly run `ue4ss/Mods/TurboTad/TurboTad.Controls.exe`. Configure your axes and button directions using the input mapper; click **START CONTROLS** and enter the Tadpole.
5. Configure an optional WAV with the GUI's audio picker. **No WAV ships with this package**, so the helper must also function silently without one.

On a new install, `mapping.xml`, saved profiles, runtime `input.txt` and `boost-status.txt` are created as needed; they are not included in the ZIP. Xbox inputs can appear once as XInput (`Xbox 1`) and again via a generic Windows joystick driver; select the Xbox-labeled source where practical. This duplicate display is not a confirmed flight-control failure.

## Smoke-test checklist

- Clean launch with no WAV; all six axes, inverted control and ordinary analog fine control.
- VKB and/or Xbox remapping, STOP/START, Alt-Tab, Tadpole exit/re-entry, game-focus recovery.
- Two saved input profiles; check **Include flight tuning** and verify restored rates and top speed. Unchecked profiles should leave current flight tuning unchanged.
- Directional boost, cooldown, HUD, custom WAV selection/test, and no recurring one-second stutter.
- Stop/exit restores ordinary controls; restore your original setup if any check fails.

This is a recommended user verification checklist. The 2026-11-22 human acceptance covered core GUI, profiles, boost, WAV behavior, and flight, but not every combination of device, game build, or other mod.

## Rollback

Exit the game and helper. Restore the backup of `ue4ss/Mods/TurboTad/`. For a clean removal, delete only that mod folder and the optional `TurboTad.cmd` launcher from `Binaries/Win64/`; leave UE4SS and game files alone. Keep your personal controller profiles and WAVs outside the mod folder before removing it.

## Safety

**WARNING: This mod can break your game. Breaching the water surface at high speed can launch the Tadpole out of the map. Back up your saves and slow down before surfacing.**

Speed is not automatically limited near the surface. The displayed mph figure is an **estimate**, not measured speed telemetry.

## Build, provenance and rights

The exact source commit, compiler output hash, package manifest and full Windows self-test result should accompany each release-candidate build. `BuildAndVerify.ps1` runs isolated Windows build/self-tests but does **not** certify in-game behavior.

Original TurboTad code is licensed under MIT; see `LICENSE` (also included in the runtime ZIP). Subnautica 2, Unreal Engine, UE4SS, Windows and .NET remain subject to their own terms. TurboTad's icon/logo redistribution rights should be confirmed before a public release. Do not bundle personal game audio, mappings, logs, probes or another project's executables.
