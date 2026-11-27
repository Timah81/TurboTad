# TurboTad

**Fly It Like You Stole It!** TurboTad gives the **Subnautica 2 Tadpole** six degrees of freedom on Windows, with inverted flight, boost, and adjustable handling.

## Download TurboTad 0.4.1

**[Download the installable ZIP](https://github.com/Timah81/TurboTad/raw/refs/heads/main/downloads/TurboTad_0.4.1.zip)** · [Installation](#installation) · [Report a bug](https://github.com/Timah81/TurboTad/issues)

The installable file is named `TurboTad_0.4.1.zip` and includes the compiled Windows companion. GitHub's **Code → Download ZIP** downloads the source tree.

> **Warning:** Breaching the water surface at high speed can launch the Tadpole out of the map. Back up your saves and slow down before surfacing.

## Installation

Requires **Subnautica 2 stable branch on Windows**, [UE4SS for Subnautica 2](https://www.nexusmods.com/subnautica2/mods/36), and **.NET Framework 4.8**. Testing used UE4SS v3.0.1 Beta (build `d7e7826d`).

1. Close the game and TurboTad. Back up your saves and, if updating, your existing `ue4ss/Mods/TurboTad/` folder.
2. Install UE4SS separately. Extract the TurboTad ZIP into the game's `Subnautica2/Binaries/Win64/` directory. The extracted `TurboTad.cmd` should be next to `Subnautica2-Win64-Shipping.exe`.
3. Run `TurboTad.cmd`, configure your inputs, and click **START CONTROLS**. Return to Subnautica 2 and pilot a powered Tadpole.

TurboTad offers to add a desktop shortcut on first launch. You can also create one from **About → Create Desktop Shortcut**. To uninstall, close the game and companion, back up any personal settings, then remove the TurboTad mod folder and its launcher.

## Features

- Independent pitch, yaw, roll, and three-axis movement. Fly inverted and tune angular rates, rotational and translation gains, and top speed.
- Keyboard bindings, Xbox/XInput support, and compatible Windows flight sticks.
- Directional boost, optional HUD, custom WAV boost sound, and reusable profiles with optional flight tuning.

## Source and verification

- [Lua flight mod](Scripts/) · [Windows companion and tests](Source/) · [Build and test script](BuildAndVerify.ps1)
- **ZIP SHA-256** `e7a8bd675fc765101435baf09e2bd5935449507e31d70fa06c9579f740c238de`
- The archive has 14 runtime files. The 0.4.1 build passed automated regression and packaging checks. Human gameplay testing covered the approved baseline before the final branding and version metadata update. The final companion was cross-built for Windows x64 and GUI-tested under Mono.
- [How TurboTad was made](HOW-IT-WAS-MADE.md) credits the creator's design, direction, and hands-on testing, and documents the AI contributions.

Original project code and documentation are [MIT-licensed](LICENSE). Subnautica 2, Unreal Engine, UE4SS, Windows, .NET, and third-party materials retain their respective rights. TurboTad is an unofficial fan project.
