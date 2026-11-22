# How TurboTad was made

TurboTad is an AI-developed mod created under human direction. I did not personally hand-write its source code, and I don't want players to mistake an AI-assisted development process for conventional solo programming.

I decided what TurboTad should do, how its controls should feel, which behaviors mattered, and what counted as an acceptable result. I directed the research, chose which changes to keep, tested the mod myself, and made the final calls on scope and safety.

## Who did what

**My role — creator, designer, project director, and test pilot**

- Conceived the Subnautica 2 Tadpole six-degree-of-freedom flight-control project.
- Defined requirements for pitch, yaw, roll, strafe, vertical movement, boost, controller mapping, profiles, and GUI behavior.
- Directed investigations when the game's native input or movement behavior interfered with the desired controls.
- Tested flight handling with my own controls, described problems I encountered, and accepted or rejected fixes based on actual gameplay.
- Ran repeated GUI, profile, input, audio, and clean-install tests and decided which results were good enough for release.
- Set the release boundaries, including separation from the Racing Gates project and warnings about extreme-speed surface breaches.

**The AI tools — programming, analysis, research, and review**

- ChatGPT and DeepSeek were major contributors to implementation, debugging, and technical investigation, including reverse-engineering-assisted work.
- Claude and ChatGPT were also used as code auditors. Additional free AI services were consulted at different stages for alternative reviews and second opinions.
- Google Search was used during informal research and checking. A search result or an informal AI opinion is not the same thing as an independent source-code audit.
- AI tools assisted with automated tests, documentation, code review, release-candidate packaging, and troubleshooting.

These are the principal roles I can describe accurately; this is not an exhaustive per-file attribution of every model or session.

## How it was tested

TurboTad's release candidate went through automated tests, source-backed AI-assisted audits, package checks, and hands-on testing in Subnautica 2. The latest Lua-error-logging candidate passed its automated CI checks and a subsequent clean TurboTad reinstall and manual acceptance run covering the GUI, profile switching/saving, boost bindings, WAV playback/testing, and flight.

**AI review is not a guarantee of correctness.** The audits help catch problems, but they do not replace running the software in a real game. Conversely, my successful flights do not prove the mod will behave identically on every computer, controller, or game version.

The tested environment includes Windows, Subnautica 2's stable branch, and UE4SS 3.0.1 Beta (build `d7e7826d`). Multiplayer, other operating systems, and broad combinations of other mods have not been certified. Future game updates may require changes.

## Safety and player expectations

TurboTad changes the Tadpole's behavior while the game runs. At high speed, breaking the water's surface can launch the Tadpole out of the map. Back up your saves and slow down before surfacing. See the README for installation, rollback, and compatibility guidance.

The release candidate was tested and audited with substantial care, but the software is provided **as is**, without warranty. Problems should be reported to the mod author, not the Subnautica 2 developers.

## Open source and third-party rights

TurboTad's original code is distributed under the **MIT License** (see `LICENSE`). The license permits use, modification, sharing, and commercial reuse of that original code subject to preserving the license and copyright notice.

Subnautica 2, Unreal Engine, UE4SS, Windows, .NET, and third-party materials remain subject to their own licenses and rights. The MIT license does not grant rights to artwork, audio, or other assets that the project does not own.

TurboTad is an unofficial fan project and is not made, endorsed, or supported by the game's creators.

---

This disclosure is modeled on the clear approach taken by [Absolute Flight's development explanation](https://github.com/SultanDesync/AbsoluteFlight/blob/main/HOW-IT-WAS-MADE.md), while describing TurboTad's own development and testing rather than copying its claims.
