# Open VectorFit Calibration: current algorithm

This document describes the algorithm used for new calibration sessions. Thresholds are engineering choices unless explicitly identified as statistical tests. The software measures performance and tests bounded proposals; it does not guarantee better aim or identify a hardware defect.

## Calibration flow

A phase consists of flick mapping followed by 16 precision targets. Flick mapping can extend from 48 to 64 to 80 attempts when fitting needs more evidence. Phase 1 ends with a baseline and a proposal. The user can test that proposal with A/B, explicitly apply it for the next phase, or keep the current profile. Later phases compare their next proposal with the profile used for their measurements.

Each task transition has centered instructions and a click to start; that click is not a trial. Target onset varies by 150–900 ms. Flick trials advance on the first click, including misses. Precision trials advance on a hit or the 3000 ms deadline. Misses do not reset the precision clock. Flick target radius is 0.65 degrees and precision radius is 0.35 degrees, with a 5-pixel minimum. Precision uses eight directions at distances of 8 and 14 degrees.

Measurements, click outcomes, timestamps, profile identity and phase ancestry are saved. Precision data are kept separately and do not enter flick endpoint fitting. History differences are descriptive: practice, fatigue and changing task samples can affect them.

## Input coordinates and avoiding double correction

The selected current profile is copied into InputCorrection. Driver matching is checked before measurement and its configuration fingerprint is checked afterwards. Unknown driver status or changed settings block use of the measurement.

Ordinary measurement observes the existing input once. Fitting inverts the measured profile for each movement delta to recover the input basis. A/B then applies either the full measured profile A or the full candidate B to that recovered input. If B is already active globally, the comparison instead undoes B first. The candidate replaces the previous settings; it is not applied twice.

## Measurement and fitting

The original timestamped movement path and click outcome are preserved. Hit detection and target-entry detection use the same perspective projection and the recorded angular target radius. An invalid mapping attempt is stored but not used to fit direction. An invalid A/B attempt is retained as a miss with normalized distance loss at least 1; timeouts are not retried selectively.

Trajectory analysis interpolates positions at 4 ms intervals, with a three-sample position filter for stroke segmentation. Once axial progress exceeds 35% and movement has lasted at least 32 ms, a reversal of at least max(2.5% target distance, half the target radius) lasting at least 12 ms, or a 32 ms low-speed interval below 12% of the prior peak speed, can identify the end of the initial stroke. If no such boundary is found, fitting uses the click endpoint. These are engineering heuristics, not a validated motor-control model.

The boundary is identified on the observed path. The active profile is inverted per raw delta before fitting the corresponding prefix, preventing double correction. Direction, overall sensitivity and the optional curve model use that prefix. The full path still contributes to observed performance statistics and A/B assessment. Circle fitting remains a weak, bounded prior. Correction bounds are specified below.

Metrics include click distance, actual hits, initial reach, maximum axial overshoot, reverse axial travel, path length after the first stroke, excess path length, first target entry, target re-entries and time after the first stroke. Target exits use 8% radial hysteresis to limit boundary jitter. Missing trajectory or target geometry is not presented as zero error.

## Direction and scalar sensitivity

The directional model has separate right, left, up and down gains, rotation and one scalar sensitivity. The fitted directional gains are normalized independently of scalar reach. The proposal blends the previous and fitted gains using their geometric mean and averages rotation. Absolute directional gains are limited to 0.92–1.08; rotation is limited to ±3 degrees.

Split-sample directional fits must agree within 0.08 per gain and 2 degrees rotation for repeatability. Sensitivity uses stable over- or undershoot in at least six of eight directions, split reach agreement within 0.08, and repeatable direction fitting. Absolute median reach error must exceed max(0.02, 0.75 × median absolute deviation), with both chronological halves showing the same bias beyond 2%.

When supported, scalar sensitivity is multiplied by 1/sqrt(median reach), bounded to a 0.95–1.05 change per phase and 0.75–1.25 overall. Otherwise scalar sensitivity is retained. The initial stroke can reveal an overshoot even when the user corrects back and ultimately hits; the full movement and final hit are still assessed in A/B.

## Natural circle tendency

The circle task records 30 attempts clockwise followed by 30 counterclockwise, with a 2.5-second pause on direction change. Timeouts consume an attempt; pauses do not. No target dots or drawn trail encourage cosmetic correction of the user's natural movement. Zero-motion attempts count but cannot supply shape estimates.

All counted finite moving paths contribute to the displayed median, regardless of roundness, closure or fit residual. Paths are centered by their bounding box and resampled at 72 equal path-length positions without adding a closing segment. Counterclockwise order is reversed. RMS radius normalizes size. Cyclic starting-point alignment does not rotate the geometry. Pointwise medians preserve irregular shapes.

Ellipse fitting is separate from median inclusion. A prior requires 30 counted attempts and at least 24 measurable shapes in each direction. Anisotropy is represented by log(major/minor) × (cos(2 angle), sin(2 angle)). Median vector spread must be below 0.55, direction disagreement below 0.25 and early/late disagreement below 0.35. Opposing median log width/height biases beyond ±0.02 block a common correction.

The prior evaluates the median after undoing the input profile and applying the flick-derived proposal. It tries reciprocal horizontal/vertical adjustments toward equal width and height. The per-axis limit is 0.02 × clamp(1 − max(0, spread − 0.10)/0.60, 0.25, 1), within the absolute gain limits. Simulated major/minor ratio must improve by at least 0.005 from a starting ratio of at least 1.02. Circles alone change neither scalar sensitivity nor rotation. A tilted ellipse may remain uncorrected. Simulated roundness is not evidence of improved aim.

## Optional movement-curve correction

Interception profiles can additionally use a direction-dependent curve model. Training resamples movement spatially at 0.5-degree intervals into eight heading sectors and up to 50 distance bins. It estimates repeated local heading deviations rather than targeting objects on screen. Unsupported bins remain neutral and corrections are bounded to ±8 degrees. Runtime processing uses the same curve and directional transform as the local trainer, retaining fractional output counts.

This component requires its own validated Interception profile. Raw Accel must be neutral or absent during global Interception use. The app must stay running. Closing or stopping the filter ends correction; it does not uninstall the driver.

## Prespecified comparison plan

- Checkpoints: 32, 48 and 64 attempts only. Each has equal A/B counts and full coverage of eight directions.
- Each PairId identifies exactly one A and one B attempt with identical angle, distance and target radius.
- The first 32 attempts use randomized ABBA/BAAB quartets per direction. The next 16 use four AB and four BA pairs across randomized directions. The final 16 reverse that per-direction order.
- No decisions are made mid-checkpoint. Unclear results extend by 16. A clear improvement or worsening stops the run. At 64, insufficient evidence is reported as inconclusive, never as equivalence.

There are three prespecified routes to approval. Each uses a one-sided exact binomial tail at p=0.5, with alpha=0.05/(3 routes * 3 looks). Support must cover at least six directions.

1. Distance: count pairs where B error is less than 0.95*A error minus 0.001 target distance; additionally require at least 5% lower median distance. Ties/non-wins count against approval.
2. Hits: use the discordant hit pairs (A miss/B hit versus A hit/B miss). Require positive net hit improvement and median distance no more than 0.002 worse.
3. Correction: count pairs where A correction travel exceeds 0.03 and B is less than 0.9*A minus 0.005. Require at least 10% lower median corrective travel, median distance no more than 0.002 worse, and median time no more than 2% worse.

All routes additionally require a computed, repeatable proposal; no fewer observed hits; median time no more than 5% worse and 90th percentile time no more than 10% worse; 90th percentile error no more than 0.01 worse; directional spread no more than 0.005 worse; median correction travel and overshoot no more than 0.02 worse; path excess no more than 0.03 worse; and median re-entries no more than 0.5 worse. Valid trials must have analyzable trajectories. These guards are practical tolerances, not formal noninferiority tests.

Movement duration is also checked separately from reaction time: its median must not be more than 5% worse. Median time after the initial stroke must not rise by more than max(20 ms, 10% of A). This prevents faster reaction from masking slower movement or substantially longer adjustment.

The exact binomial and multiple-look thresholds have their nominal interpretation only under their statistical assumptions, including suitable independence/symmetry of paired outcomes. Trial order counterbalancing reduces simple drift but does not prove independence or remove adaptation and learning. Repeated manual runs are separate experiments; the nominal family error budget is per run, not lifetime. No claim of a 95% probability that a profile helps the user is made.

## Precision acceptance

After the sequential flick comparison, 32 precision trials form 16 matched A/B pairs with balanced order per direction. Every click is recorded. Timeouts contribute 3000 ms to time summaries.

A complete comparison requires matching target geometry, one A and one B per pair, correct directional coverage and internally consistent clicks. Precision acceptance requires at least eight hits in each profile, no fewer B hits, no extra B miss clicks, and at most 5% increases in both median and total target time. Flick improvement must also pass this precision guard for overall approval. This is a regression safeguard, not a separate significance test.

## Confirmation without further fitting

A confirmation run clones the session and freezes the same A/B proposal. It preserves a compact record of the previous comparison and clears only the current comparison data. It does not advance the calibration phase, refit, or activate a new profile. Changing profile type requires leaving the frozen comparison.

A SHA-256 identity covers setup, profile mode, input correction, candidate correction and active curve. Frozen proposals are checked on load. Repeat summaries compare records only when this identity matches. Different configurations are not pooled.

The result headline distinguishes incomplete data, supported improvement, promising change, slower-but-closer results and mixed or weaker results. A descriptive promising result requires at least 5% less median flick error, no fewer flick hits, at most 5% longer median flick time, and no deterioration beyond the specified precision limits. It never substitutes for the statistical approval rules.

Two consecutive completed checks that each pass can be described as supported in two controls. Repeated promising results remain unconfirmed. This does not establish a lifetime statistical guarantee or remove learning effects.

## Global application and recovery

Raw Accel applies the complete scalar/directional/rotation configuration. The previous driver configuration is backed up before changes. The bridge reads back settings; failures trigger a best-effort restoration. Reset to neutral removes adjustments while preserving a backup; restore retrieves the last backup.

Driver installation is separate from profile application. Optional setup downloads a fixed official release and verifies its SHA-256 before extracting it. Administrator approval is requested through Windows. Installer exit is not treated as proof that the driver is usable; the user restarts and checks driver access. No calibration profile is activated by setup.

## Verification and limits

Tests cover fitting, corrected hits, misses, polling-rate variation, paired scheduling, sequential checkpoints, timeouts, precision regressions, repeat identity, driver-transform equivalence, package checksums and setup construction. Tests do not install drivers or inject global input. Offline and synthetic tests verify implementation mechanics rather than real-world efficacy.

## Method references

- https://www.yorku.ca/mack/CHI01.htm — trajectory measures complement endpoint accuracy.
- https://www.itl.nist.gov/div898/software/dataplot/refman1/auxillar/signtest.htm — sign tests and binomial interpretation.
- https://itl.nist.gov/div898/handbook/prc/section4/prc473.htm — multiple-comparison error allocation.
