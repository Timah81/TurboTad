# TurboTad

**6-degree-of-freedom Tadpole flight controls for Subnautica 2 (Windows, stable game branch).** Fly with proportional pitch, yaw, roll, forward/reverse, left/right, and up/down control. The project uses a Windows C# controls application and UE4SS Lua scripts.

> **PROJECT STATUS — NOT PUBLICLY RELEASED YET.** The repository is private and contains development **source code**, not a finished installer. A separate release candidate has been tested, but final public-release checks are still open.
>
> **Don't click the green `Code → Download ZIP` button expecting to install TurboTad.** That ZIP is a *source-code snapshot*, and normally does not contain the compiled `TurboTad.Controls.exe` or UE4SS.

## Start here — what are you trying to do?

| I want to... | Go here | What you'll find |
| --- | --- | --- |
| **Download and play TurboTad** | **[Releases](https://github.com/Timah81/TurboTad/releases)** | This is where a ready-to-install ZIP **will** appear after release validation. **No GitHub Release has been published yet.** |
| **Find the baseline source** | **[main branch](https://github.com/Timah81/TurboTad/tree/main)** | The saved source baseline. **Not** a compiled installer or necessarily the newest development work. |
| **See newer controller work** | **[Universal Input Mapper branch](https://github.com/Timah81/TurboTad/tree/feature/universal-input-mapper)** | Ongoing input-mapping development; don't assume it is release-tested. |
| **Review proposed fixes** | **[Pull requests](https://github.com/Timah81/TurboTad/pulls)** | Proposed code changes that may or may not be merged. |
| **See all development branches** | **[Branches](https://github.com/Timah81/TurboTad/branches)** | Separate copies of the project used to develop or test changes without altering `main`. |

**Rule of thumb:** **Releases = downloads for players. Branches = code being worked on.** A branch with more commits is not automatically better or safer.

## What the current branches mean

| Branch | Purpose | Is this a public release? |
| --- | --- | --- |
| [`main`](https://github.com/Timah81/TurboTad/tree/main) | Preserved source baseline, including an accepted neutral-boost fix | **No** |
| [`feature/universal-input-mapper`](https://github.com/Timah81/TurboTad/tree/feature/universal-input-mapper) | Development of unified analog/digital/controller mapping | **No** |
| [`feature/device-discovery`](https://github.com/Timah81/TurboTad/tree/feature/device-discovery) | Input-device discovery changes | **No** |
| [`audit/discovery-xml-hardening`](https://github.com/Timah81/TurboTad/tree/audit/discovery-xml-hardening) | Review/testing of discovery XML parsing safeguards | **No** |
| [`audit/release-lua-logging-cleanup`](https://github.com/Timah81/TurboTad/tree/audit/release-lua-logging-cleanup) | Review/testing of diagnostic Lua logging cleanup | **No** |
| [`experiment/vertical-ascend-suppression`](https://github.com/Timah81/TurboTad/tree/experiment/vertical-ascend-suppression) | Isolated experiment, not approved release behavior | **No** |

### GitHub words in plain English

- **Branch:** a separate work area. Switching branches changes which *source files* you are looking at; it does not install anything on your PC.
- **Commit:** a saved code checkpoint. Clicking one shows what changed.
- **Ahead / Behind:** how many commits differ from `main`; **not** a quality, safety, or release-readiness score.
- **Pull request (PR):** a proposal to combine one branch with another. It is not automatically accepted or merged.
- **Release:** a named, published version. For TurboTad, the goal is a clearly labeled ready-to-install ZIP in its **Assets** section.

## If you are giving TurboTad to another AI agent for an audit

1. Decide **which branch** or exact commit you want audited. Don't just ask for “the newest code” — the branches have different purposes.
2. Open that branch, then use **Code → Download ZIP** to supply its **source** to the agent, or give an agent with GitHub access the branch/commit link.
3. For a *runtime, packaging or install* audit, also provide the separately verified runtime release-candidate ZIP. Source ZIPs by themselves do not establish what was compiled or tested in the game.
4. Ask the agent to report the branch and commit hash it actually inspected, and to distinguish static review from compilation and in-game testing.

**Important:** This repository is currently private. Someone without access cannot open these links, even if you send them the URL.

## What TurboTad does

- Proportional inverted/6DOF Tadpole flight
- Adjustable movement response, rotational control and top speed
- Directional boost, configurable cooldown and in-game HUD
- Windows control/mapping GUI
- Optional local, user-provided WAV sound for boost

The newer Universal Input Mapper is under active development on its own branch; don't treat every adapter or mixed-device scenario as validated. The Windows helper, UE4SS version, and game build all matter for compatibility.

## Source folders (for developers)

| Path | What it is |
| --- | --- |
| `Scripts/` | In-game UE4SS Lua flight, boost, controller and HUD code |
| `Source/` | Windows C# helper, settings GUI and source tests |
| `Assets/` | GUI icons and images |
| `BuildAndVerify.cmd` / `BuildAndVerify.ps1` | Compile/test helper source in an isolated temporary location; **does not install or certify a release** |
| `TurboTad.cmd` | Launcher for an **installed** mod layout; not a complete installer by itself |
| `LICENSE` | Project's license text |

**UE4SS is a separate dependency**, not included in this repository. Development used the Subnautica 2 stable branch and UE4SS 3.0.1 Beta; compatibility with other builds is not established.

## Safety and distribution

**WARNING: High-speed surface breaches can launch the Tadpole out of the game world and break your gameplay session. Back up your saves and slow down before surfacing.** Boost and extreme speed settings can also cause collisions. No automatic surface limiter is promised.

Do not publish private controller mappings, saved games, logs, user-recorded game sounds, or bundled UE4SS binaries as if they were TurboTad assets. Users may supply their own WAV files. See [LICENSE](LICENSE); third-party software and game assets retain their own rights. AI tools have assisted development; any public mod page should disclose their actual use accurately.

---

**For players:** wait for a validated ZIP under **[Releases](https://github.com/Timah81/TurboTad/releases)**.

**For developers and auditors:** use the exact **branch + commit hash** for the code you intend to inspect.
