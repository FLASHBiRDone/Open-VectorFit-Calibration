# TrueTrack

<img src="src/assets/truetrack.png" alt="TrueTrack logo" width="96">

**Free, open-source mouse calibration and aim testing for Windows.**

[![Download for Windows](https://img.shields.io/badge/Download_for_Windows-v0.16.0-168A66?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/FLASHBiRDone/TrueTrack/releases/download/v0.16.0/TrueTrack-Windows.zip)

**[Download the Windows ZIP](https://github.com/FLASHBiRDone/TrueTrack/releases/download/v0.16.0/TrueTrack-Windows.zip)** · [Release notes and checksums](https://github.com/FLASHBiRDone/TrueTrack/releases/tag/v0.16.0) · [Roadmap](ROADMAP.md)

Extract the entire ZIP, open the **TrueTrack** folder and run **TrueTrack.exe**. No build required. Windows x64 and .NET Framework 4.7.2 or later are required. The application is unsigned; Windows may show a reputation warning.

Measure your flicks and precision, compare sensitivity profiles, and check whether changes help without simply taking longer. Designed for standard mice across brands.

**Available now:** fixed first-test baseline, explained recommendations, balanced target distances, optional measured circle verification, expanded flick scoring rings, precision-first assessment, four before/after gauges, calibration, A/B testing, repeat confirmation, optional game setup with FOV and preferred cm/360, blind distance estimation, and optional global profiles.

Our long-term goal is a free, open-source aim-training platform shaped by its community. The current release is experimental—not yet a full Aimlabs replacement. Testers and contributors are welcome.

The app supports English (first-launch default) and Norwegian; saved language choices are retained. Calibration and local comparisons do not require installing an input driver. Global profiles use optional drivers with separate setup and compatibility considerations.

## Help shape the aim trainer

See the [roadmap](ROADMAP.md) for the proposed path from calibration to broader aim training: reliable onboarding, tracking and target-switching exercises, practice routines, and local progress history.

[Report a bug or suggest an exercise](https://github.com/FLASHBiRDone/TrueTrack/issues/new). For bug reports, include the app version, Windows version, mouse model, steps to reproduce and expected versus actual behavior. Review logs and screenshots before sharing; you do not need to upload personal measurement files.

## Mouse compatibility

Calibration is designed to work with any standard mouse that Windows exposes as mouse input, including wired and wireless mice. No Corsair, Logitech or other manufacturer-specific hardware is required. Every mouse model has not been tested.

In-app comparisons work locally. Applying a profile across Windows and games requires a compatible input driver, installed through the optional built-in setup. Device support, game input paths and anti-cheat restrictions can affect global operation. The tool does not write calibration to mouse firmware.

## How to use it

1. Select English. Start with the defaults or optionally enter your normal DPI, desired cm/360 and horizontal FOV. The defaults (1600 DPI, 40 cm/360, 103 degrees) are starting points, not personalized recommendations. Keep setup unchanged during comparisons.
   Game setup offers an optional vertical-to-horizontal FOV converter, an in-game sensitivity calculator, and five blind horizontal movements summarized by their median. This estimates preferred physical travel from raw counts and nominal DPI; verify actual cm/360 with a ruler in your game.
2. Record the baseline. In flick trials, aim quickly and click once; misses count. In precision trials, keep correcting until you hit; every click and the elapsed time count.
3. Inspect the proposed sensitivity and axis changes. Read accuracy, hit rate and time together.
4. Run A/B to compare the measured profile with the proposal.
5. Choose **Confirm the same proposal** to repeat that comparison without changing the proposal.
6. Only apply a global profile when you choose to. After changing a profile, measure a new calibration phase.

Optional circle calibration records 30 natural rotations in each direction. Move naturally rather than trying to draw perfect circles. It estimates consistent shape tendencies; a rounder simulated circle alone does not establish better aim.

## What the results mean

- **Promising**: descriptive improvements appeared, but the acceptance checks have not established an improvement.
- **Closer but slower**: lower aiming error came with increased time.
- **Mixed or worse**: the proposal has no clear advantage under the checks.
- **Supported in repeated checks**: the same settings passed consecutive checks. This is not a guarantee of better performance in every game.

Repeated tests retain their earlier results. Different profiles are not pooled as if they were one repeated experiment. Practice and fatigue can affect comparisons across phases.

## Build

Requires Windows x64, .NET Framework 4.7.2 or later and Windows PowerShell. Obtain Raw Accel 1.7.1 from https://github.com/RawAccelOfficial/rawaccel/releases and extract it locally. Supply a dependency directory containing its matching wrapper.dll and Newtonsoft.Json.dll, plus a neutral rawaccel-default.json configuration. Keep upstream license notices in that directory.

    .\build.ps1 -DependencyDirectory C:\path\to\dependencies

Outputs are in artifacts/TrueTrack. Run TrueTrack.exe. Tests can be run without installing or activating a driver:

    $report = Join-Path $PWD 'artifacts\tests.txt'
    Start-Process .\artifacts\VectorFit\VectorFit.Tests.exe -ArgumentList '--selftest', $report -Wait
    Get-Content $report

Optional tests of historical local measurements are skipped when those private files are absent. No user measurements are distributed.

## Global input

Raw Accel integration requires its separately installed driver. Open **Use in Windows and games â†’ Set up Raw Accel** to install the bundled official driver. If the package is missing, setup downloads the fixed official release. Its SHA-256 is checked before extraction. Installation requires your choice and Windows administrator approval; restart and check status afterwards. No profile is activated by installation. Applying a global profile is an explicit action with backup and readback. **Reset to neutral** disables adjustments; **Restore profile** retrieves the last backup. This does not program mouse firmware.

Interception is an optional experimental backend with separate licensing. **Optional: Interception setup** explains the license and anti-cheat risks, then lets you choose an official download and installation or uninstallation. Its binaries are not included in the repository. Check upstream terms for your intended use, particularly commercial use. Stopping the filter does not uninstall the driver. Check the rules and compatibility of each game's anti-cheat before using any input driver; compatibility is not guaranteed.

## Data and privacy

Measurements and profiles are stored locally. They may contain personal movement and device information. Do not commit them. This repository excludes private profiles, measurements and reports. The unmodified official Raw Accel installer archive is included with its license; Interception binaries are not included.

## License and project status

Original project code is offered under the MIT license; see LICENSE. Third-party dependencies retain their own licenses; see THIRD_PARTY_NOTICES.md. This project is independent and not affiliated with Corsair, Aimlabs, Logitech, Raw Accel or Interception.

The working name and implementation have not received trademark or patent clearance. MIT licensing does not grant rights owned by unrelated third parties.


