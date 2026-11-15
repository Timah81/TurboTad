# TurboTad — Device Discovery isolated hardware test

Status: SOURCE IMPLEMENTED; Windows build and in-game hardware test pending.
Branch: feature/device-discovery
Base: experiment/vertical-ascend-suppression (baa76a04).

## What it does
The Windows Controls helper has a DISCOVER CONTROLS button on Input Assignments. Release controls, click START SCAN, move each axis, squeeze triggers, and press buttons. Detected inputs appear in a list. Click USE RESULTS to promote them to the top of all six analog choices, Boost, and the negative and positive direction-pair lists.

Scans are saved in a separate device-discovery.xml file, not mapping.xml. The full supported inputs remain available even if the user forgets to exercise one during discovery. No automatic bindings are made.

## Test
1. Close TurboTad Controls and Subnautica 2; back up the working configuration.
2. Download the entire feature/device-discovery branch and run InstallUniversalMapper.cmd. It compiles and runs tests on Windows before replacing the helper.
3. Launch Controls. Select DISCOVER CONTROLS, then START SCAN while all sticks, triggers and buttons are released.
4. Exercise each joystick axis, Xbox stick, LT and RT, controller button, and keyboard key. Verify each is recorded.
5. Click USE RESULTS. Check the analog dropdown on every flight row, both direction-pair selectors, and Boost. Discovered sources should appear first.
6. Close and reopen Controls and confirm the input inventory is retained without altering existing bindings.
7. Validate gameplay: VKB and Xbox axes, paired triggers, ascend and descend suppression, Boost, Stop and on-foot controls.

## Limits
Only inputs exposed through existing XInput/WinMM readers and keyboard events can be learned. The scanner cannot reveal hidden Elite paddles or additional HID features not reported by those APIs. An Xbox pad may be visible through both XInput and WinMM; the scanner records both observed identities, with XInput appearing first for simultaneous reports. New WinMM USB devices may require RESCAN DEVICES first. Interface selection/deduplication is intentionally out of scope.

## Rollback
Close game and helper and run the existing RollbackUniversalMapper.ps1 script, which restores the backed-up helper and mapping. Older helpers ignore the optional device-discovery.xml sidecar.

Compilation and live hardware acceptance are unverified in this environment.
