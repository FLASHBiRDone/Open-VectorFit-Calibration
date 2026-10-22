# Open VectorFit Calibration

An open-source Windows tool for measuring your natural mouse movement and testing whether a personalized sensitivity profile improves your aim. Designed for standard mice across brands, without requiring a specific gaming mouse. The application currently displays VectorFit and supports English and Norwegian.

Measure a baseline, compare a bounded sensitivity/directional proposal with the measured profile, and repeat the exact same comparison before considering further changes. Flick trials record misses as well as hits. Precision trials count all clicks and time until a hit or deadline. Natural circle measurements estimate typical axis tendencies.

Results distinguish accuracy, hits, speed and uncertainty. A more circular trace or promising result is not proof of improved gameplay. Confirmation keeps the same A/B settings and previous results; it does not calculate or activate another correction.

## Mouse compatibility

Calibration is designed to work with any standard mouse that Windows exposes as mouse input, including wired and wireless mice. No Corsair, Logitech or other manufacturer-specific hardware is required. Every mouse model has not been tested.

In-app comparisons work locally. Applying a profile across Windows and games requires a compatible, separately installed input driver. Device support, game input paths and anti-cheat restrictions can affect global operation. The tool does not write calibration to mouse firmware.

## How to use it

1. Select English and enter your normal DPI and game sensitivity. Keep these settings unchanged during comparisons.
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

Outputs are in artifacts/VectorFit. Run VectorFit.exe. Tests can be run without installing or activating a driver:

    $report = Join-Path $PWD 'artifacts\tests.txt'
    Start-Process .\artifacts\VectorFit\VectorFit.Tests.exe -ArgumentList '--selftest', $report -Wait
    Get-Content $report

Optional tests of historical local measurements are skipped when those private files are absent. No user measurements are distributed.

## Global input

Raw Accel integration requires its separately installed driver. The application does not install drivers automatically. Applying a global profile is an explicit action with backup and readback. This does not program mouse firmware.

Interception is an optional experimental backend, separately licensed and installed. Its binaries are not included. Check the rules and compatibility of each game's anti-cheat before using any input driver; compatibility is not guaranteed.

## Data and privacy

Measurements and profiles are stored locally. They may contain personal movement and device information. Do not commit them. This source-only repository excludes profiles, measurements, reports, executables and third-party binaries.

## License and project status

Original project code is offered under the MIT license; see LICENSE. Third-party dependencies retain their own licenses; see THIRD_PARTY_NOTICES.md. This project is independent and not affiliated with Corsair, Aimlabs, Logitech, Raw Accel or Interception.

The working name and implementation have not received trademark or patent clearance. MIT licensing does not grant rights owned by unrelated third parties.

