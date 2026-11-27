# TurboTad

**Fly It Like You Stole It!** A six-degree-of-freedom flight-control mod for the **Subnautica 2 Tadpole** on Windows. Fly inverted, roll, pitch, yaw, strafe, boost, and customize how the Tadpole handles.

> **Release status (November 26, 2026):** TurboTad 0.4.1 is the current verified runtime package. GitHub Releases is the planned primary download location; the 0.4.1 asset is pending upload. The earlier v0.4.0 Nexus submission is still awaiting manual security review.
>
> **Installing TurboTad?** Download the named `TurboTad_0.4.1.zip` asset from [GitHub Releases](https://github.com/Timah81/TurboTad/releases) once it is available. Do **not** use GitHub's green **Code → Download ZIP** button or the automatically generated source archives; those are not the installable Windows mod.
>
> **WARNING: This mod can break your gameplay session. Breaching the water surface at high speed can launch the Tadpole out of the map. Back up your saves and slow down before surfacing.**

## Start here

| I want to… | Go here |
| --- | --- |
| **Download the mod** | [GitHub Releases](https://github.com/Timah81/TurboTad/releases) (0.4.1 asset pending upload); [Nexus Mods](https://www.nexusmods.com/subnautica2/mods/540) (earlier submission pending review) |
| **Install TurboTad** | [Installation steps](#installation) below |
| **Understand who made it** | [How TurboTad was made](HOW-IT-WAS-MADE.md) |
| **Inspect or build the source** | [Source code map](#source-code-map) and [Build verification](#build-and-verification) |
| **Report a problem** | [GitHub Issues](https://github.com/Timah81/TurboTad/issues) |

**For players:** GitHub Releases will host the installable runtime ZIP; this repository hosts source code, documentation, and build evidence. **For developers:** the integrated source on `main` is v0.4.0; the 0.4.1 source synchronization is pending. Do not assume the current source checkout reproduces the 0.4.1 binary. Other feature, audit, and experiment branches are development history, not separate player downloads.

## Features

- True 6DoF Tadpole flight: pitch, yaw, roll, forward/reverse, left/right strafe, and ship-relative vertical motion.
- Inverted flight, adjustable rotation/movement response and top speed, and scaled acceleration.
- Directional boost, optional in-game HUD and user-supplied WAV boost sound.
- Windows controls GUI supporting compatible Xbox/XInput and flight-stick/Windows joystick inputs.
- Custom mappings and saved profiles; optionally save flight tuning with controller bindings.

## Requirements

- Subnautica 2 **stable branch** on Windows.
- [UE4SS for Subnautica 2](https://www.nexusmods.com/subnautica2/mods/36), installed separately. Tested with **UE4SS v3.0.1 Beta** (build `d7e7826d`).
- Microsoft **.NET Framework 4.x** runtime for the Windows controls application.

Other game builds, Steam Deck/Proton, multiplayer, and combinations of competing input/movement mods have **not** been certified.

## Installation

1. **Back up your Subnautica 2 saves.** When updating, also back up any existing `ue4ss/Mods/TurboTad/` folder, including your mappings, profiles, and WAV files.
2. Install a compatible **UE4SS** separately.
3. Extract the **complete installable TurboTad release ZIP** into `Subnautica2/Binaries/Win64/`. The installed mod should end up at `Subnautica2/Binaries/Win64/ue4ss/Mods/TurboTad/`—not inside an extra nested TurboTad folder.
4. Run `TurboTad.cmd` from `Binaries/Win64/`, or run `ue4ss/Mods/TurboTad/TurboTad.Controls.exe` directly.
5. Configure your inputs in the Windows GUI, click **START CONTROLS**, then enter the Tadpole in-game.

**Tip:** The 0.4.1 companion supports desktop shortcut creation. You can also pin the running application to your taskbar to avoid navigating to the game folder each time.

The ZIP contains no user profiles or WAV files; they are created or added locally as needed. To remove TurboTad, close the game and helper, back up personal settings, delete only `ue4ss/Mods/TurboTad/` and the optional `TurboTad.cmd` launcher. Leave UE4SS and game files alone.

## Safety

**WARNING: This mod can break your gameplay session. High-speed surface breaches can launch the Tadpole out of the map. Back up saves and slow down before surfacing.** The displayed mph value is an estimate, not calibrated telemetry.

## Source code map

| Location | Purpose |
| --- | --- |
| [`Scripts/`](Scripts/) | UE4SS Lua flight control, boost, input and HUD |
| [`Source/`](Source/) | Windows C# controls application and automated tests |
| [`Assets/`](Assets/) | Application icon and logo |
| [`BuildAndVerify.ps1`](BuildAndVerify.ps1) | Windows build and isolated tests; not a game installer |
| [`.github/workflows/`](.github/workflows/) | Automated Windows build/test/package workflow |
| [`Diagnostics/`](Diagnostics/) | Historical development probes; **not** part of the player ZIP |
| [`HOW-IT-WAS-MADE.md`](HOW-IT-WAS-MADE.md) | Human direction, AI contributions, audits and testing limitations |
| [`LICENSE`](LICENSE) | MIT license for original project code/documentation |

## Build and verification

### Current runtime: 0.4.1

- Game-side keyboard mapping, desktop shortcut support, and adjustable rotational and translational gains are included in the human-approved TEST 4.1 baseline. The final 0.4.1 rebuild changes public branding and version metadata only.
- **Runtime ZIP SHA-256:** `e7a8bd675fc765101435baf09e2bd5935449507e31d70fa06c9579f740c238de`
- **Companion EXE SHA-256:** `f13cf1ef3495f26ef0e77159f90b7b9390e69b275ca67b1a058a9b3dc15a7609`
- The 14-file runtime archive includes the compiled Windows helper, complete TurboTad Lua mod, launcher, assets, license and enabled marker. UE4SS is a separate prerequisite.
- Human gameplay testing passed on TEST 4.1. Automated regression and packaging verification passed for the branding-only 0.4.1 build, including isolated GUI fixtures under Mono. The final rebuilt executable has not independently been launched in native Windows gameplay as part of release verification. These checks are not antivirus certification.

### Historical: 0.4.0 Nexus upload

The **v0.4.0 Nexus upload** was assembled from the tested GitHub Actions build for source commit [`ee0efc405e6c…`](https://github.com/Timah81/TurboTad/commit/ee0efc405e6ce68dc0c45ecfc2b28bd66b47c5f5), including the final Lua exception-logging fix.

- [Windows CI run — successful](https://github.com/Timah81/TurboTad/actions/runs/45071566524)
- **Nexus ZIP SHA-256:** `04287a30142ef6d422d7841f1d33824a782e84f37227970ed9f87413e6a63b22`
- The runtime ZIP contains **13 files**: the Windows helper and UE4SS mod files, launcher, required assets, and license. It does **not** include UE4SS itself, personal mappings, diagnostic probes, or unrelated mods.

Source reviews, CI, and the creator's in-game testing are evidence, **not a guarantee** of security or compatibility on every machine. Rebuilding from source may not produce byte-identical Windows binaries due to compiler metadata.

## Credits and rights

**Developed with AI under human direction.** The creator designed and directed the project, performed hands-on testing, and made acceptance/release decisions. ChatGPT and DeepSeek contributed substantially to coding and technical research; Claude, ChatGPT, and other tools contributed reviews. Read [How TurboTad was made](HOW-IT-WAS-MADE.md) for details.

Original project code/documentation is MIT-licensed; see [LICENSE](LICENSE). Subnautica 2, Unreal Engine, UE4SS, Windows, .NET, and third-party materials retain their own rights. TurboTad is an unofficial fan project, not endorsed by the game's creators.
