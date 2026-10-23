# TrueTrack: current algorithm

This document describes the algorithm used for new calibration sessions. Thresholds are engineering choices unless explicitly identified as statistical tests. The software measures performance and tests bounded proposals; it does not guarantee better aim or identify a hardware defect.

## Optional arena presentation

Before starting a new baseline, users may select the 3D training room. It is a stationary viewpoint with mouse-driven camera rotation, a central crosshair and an original, low-contrast room. The classic view remains available. Both presentations use the same target projection, angular distances, scoring areas, input transformation and timing rules. The room is visual context, not a separate sensitivity correction or a claim of measured transfer to a game.

The arena choice is stored in the session setup and preserved in subsequent and confirmation rounds. A/B uses the same environment for both profiles, without identifying the active A/B profile in the arena HUD. Comparison fingerprints and history matching distinguish arena from classic measurements. Switching environments requires a new baseline; old sessions retain their original view. Room geometry is clipped at the camera's near plane before drawing. Results remain based on recorded input and click events, not room geometry.

## Calibration flow

A phase consists of flick mapping followed by 16 precision targets. Flick mapping can extend from 48 to 64 to 80 attempts when fitting needs more evidence. Phase 1 ends with a baseline and a proposal. The user can test that proposal with A/B, explicitly apply it for the next phase, or keep the current profile. Later phases compare their next proposal with the profile used for their measurements.

English is the first-launch default; an explicitly saved language choice is preserved. Before each flick/precision part and the circle test, instructions ask the user to center the mouse on the pad with room in every direction and a comfortable hand position. This is physical preparation, not cursor warping or a change in sensitivity. Each task transition has centered instructions and a click to start; that click is not a trial. Target onset varies by 150–900 ms. Flick trials advance on the first click, including misses. Precision trials advance on a hit or the 3000 ms deadline. Misses do not reset the precision clock. Flick bullseye radius is 0.8 degrees and precision radius is 0.35 degrees, with a 5-pixel minimum. Precision uses eight directions at distances of 8 and 14 degrees.

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

There are three prespecified routes to ending the flick comparison early. Flick improvement alone does not approve a new profile. Each uses a one-sided exact binomial tail at p=0.5, with alpha=0.05/(3 routes * 3 looks). Support must cover at least six directions.

1. Distance: count pairs where B error is less than 0.95*A error minus 0.001 target distance; additionally require at least 5% lower median distance. Ties/non-wins count against approval.
2. Hits: use the discordant hit pairs (A miss/B hit versus A hit/B miss). Require positive net hit improvement and median distance no more than 0.002 worse.
3. Correction: count pairs where A correction travel exceeds 0.03 and B is less than 0.9*A minus 0.005. Require at least 10% lower median corrective travel, median distance no more than 0.002 worse, and median time no more than 2% worse.

All routes additionally require a computed, repeatable proposal; no fewer observed hits; median time no more than 5% worse and 90th percentile time no more than 10% worse; 90th percentile error no more than 0.01 worse; directional spread no more than 0.005 worse; median correction travel and overshoot no more than 0.02 worse; path excess no more than 0.03 worse; and median re-entries no more than 0.5 worse. Valid trials must have analyzable trajectories. These guards are practical tolerances, not formal noninferiority tests.

Movement duration is also checked separately from reaction time: its median must not be more than 5% worse. Median time after the initial stroke must not rise by more than max(20 ms, 10% of A). This prevents faster reaction from masking slower movement or substantially longer adjustment.

The exact binomial and multiple-look thresholds have their nominal interpretation only under their statistical assumptions, including suitable independence/symmetry of paired outcomes. Trial order counterbalancing reduces simple drift but does not prove independence or remove adaptation and learning. Repeated manual runs are separate experiments; the nominal family error budget is per run, not lifetime. No claim of a 95% probability that a profile helps the user is made.

## Precision acceptance

After the sequential flick comparison, 32 precision trials form 16 matched A/B pairs with balanced order per direction. Every click is recorded. Timeouts contribute 3000 ms to time summaries.

A complete comparison requires matching target geometry, one A and one B per pair, correct directional coverage and internally consistent clicks. The primary objective is lower total time to an actual precision hit. Time runs from target appearance through every miss and correction until the successful click. A failed attempt costs 3000 ms; failed attempts are never omitted.

New sessions explicitly store PrecisionFirst=true, ControlPriority=true and SmallGainProtocol=true. The speed route requires at least eight hits per profile, no fewer B hits, at least 0.5% lower total precision time, and a one-sided sign-tail <=0.025 over all 16 pairs where a win means B takes less than 99.5% of A's time. Non-wins, including ties, count against approval. Wins must cover at least six directions. The alternative control route requires at least 15% lower mean corrective travel, baseline mean corrective travel above 3% of target distance, at least 13 of 16 matched pairs with baseline travel above 3% and reduction exceeding 15%, wins in at least six directions, and sign-tail <=0.025. Hit count must not fall; total time may rise at most 2% and the 90th percentile time at most 10%. Both routes retain all flick safeguards. These two routes split the nominal alpha budget at one fixed precision look; it is not a guarantee of a benefit or proof against practice effects.

The completed flick comparison must pass all regression guards above, including median error at most 0.002 worse, but need not independently improve. Extra precision miss clicks are reported rather than vetoing a faster successful hit. A switching recommendation requires this complete test to pass and then a frozen confirmation with the same profile/protocol identity to pass again. The first pass is promising, not an approval to switch. Manual experimental activation remains explicitly available. Historical sessions without the protocol flag retain their original flick-first evaluation and original target presentation.

## Confirmation without further fitting

A confirmation run clones the session and freezes the same A/B proposal. It preserves a compact record of the previous comparison and clears only the current comparison data. It does not advance the calibration phase, refit, or activate a new profile. Changing profile type requires leaving the frozen comparison.

A SHA-256 identity includes the new precision-first/ring protocol when present and covers setup, profile mode, input correction, candidate correction and active curve. Frozen proposals are checked on load. Repeat summaries compare records only when this identity matches. Different configurations are not pooled.

The primary result distinguishes incomplete testing, a promising first pass, confirmed improvement, a previous profile that performed better in this test, and no clear winner. Failure to approve is not proof that the previous profile is superior. The previous-profile label is descriptive: fewer precision hits without a time gain, or over 2% longer precision time without more flick hits. Detailed thresholds remain in an expandable explanation.

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

## Optional game setup and blind distance estimate

Game setup changes the draft for the next new baseline; it does not mutate an existing session, apply a driver profile or change game settings. Existing sessions retain their recorded DPI, cm/360 and horizontal FOV. All helpers can be skipped. Defaults are 1600 DPI, 40 cm/360 and 103 degrees horizontal FOV, not an inferred optimum.

For ordinary perspective, horizontal FOV = 2 atan(tan(vertical FOV / 2) × width / height). The user must supply the aspect ratio actually used by the game. Resolution alone does not determine FOV, and stretched or reference-aspect game settings require separate interpretation.

With unchanged DPI and linear game sensitivity, suggested game sensitivity = current sensitivity × current measured cm/360 / desired cm/360. This is a manual suggestion, not an automatic calibration result.

The blind helper records five horizontal net displacements from the same relative-input mouse between pairs of clicks, without a visual trace. Absolute counts × 2.54 / nominal DPI gives an estimated distance in centimetres. The median is the suggested preferred cm/360; the range is displayed. Attempts outside 3–200 cm are retried. Focus loss discards the incomplete attempt and requires Space to resume. Escape cancels. Five completed attempts require explicit Enter acceptance.

Blind estimates require known driver status, neutral Raw Accel and no running TrueTrack Interception filter. Driver state is checked again before accepting the result. Other input transforms and inaccurate nominal DPI can still bias the estimate. The helper does not detect an in-game turn, measure sensor DPI or establish an optimal sensitivity. A ruler and an actual turn in-game are needed to verify physical cm/360.

## Flick rings and comparison gauges

New-protocol flick targets use a 0.8-degree bullseye (minimum 5 pixels), with guide rings at 2, 3, 4 and 5 times its radius. The angular diameter is approximately 23% larger than the preceding protocol. Score is max(1, 100 - floor(99 * r / 5)) for projected distance r in bullseye radii from 0 to 5, and zero outside. The center scores 100 and the outer edge 1. The bullseye remains the actual hit boundary for hit-rate safeguards; a scored outer-ring click is not a bullseye hit. Scores are recorded per attempt, with zero for timeout. Historical attempts have no score and are not retrospectively scored or pooled with the changed visual task.

Both results and statistics show four gauges: mean precision time per target (including failed attempts), mean precision corrective travel as a percentage of target distance, mean flick time and mean flick points. A/B compares the current candidate B to its measured input A. Without A/B, linked round baselines are shown only as a descriptive trend. Missing measurements remain unavailable. Red/green represent the direction of an observed difference, not statistical confidence. Faster times are not colored as improvement when hit rate decreases. Arc saturation represents 25% relative change; exact signed differences and before/after values remain visible.

## Balanced target distances

New sessions explicitly store BalancedTargets=true. Flick mapping uses fixed directions, alternating 8 and 18 degree radial target offsets, with randomized order. Every 16-target block has one near and one far target per direction. Mapping totals are 624, 832 and 1040 radial degrees for 48, 64 and 80 targets respectively: always mean 13 degrees per target. A/B uses exact paired geometry and counterbalanced order. Each A/B checkpoint has identical total distance for A and B. Precision retains its fixed 8/14-degree target plan. Onset timing remains randomized. The camera starts each target from its neutral origin; distances are intended camera-angle offsets, not the user's actual traveled path. Equal distance does not eliminate reaction-time, learning or fatigue effects. Raw total time from different attempt counts is not compared as improvement.

## Fixed baseline and result explanation

The first phase's measured trials, setup, input profile, circle data and protocol flags are deep-copied into InitialBaseline and carried into later phases. This reference does not advance with each new proposal. Older sessions resolve the first phase through their parent-file chain when available; missing ancestors remain unavailable, never silently replaced by a later phase. Saved references survive loss of the original parent file. A new independent calibration starts a new baseline.

The results and statistics pages explain mean precision time including misses, actual hits, corrective travel and flick safeguards, followed by the latest A/B gauges and an expandable first-baseline-versus-current-mapping trend. Different target protocols are not presented as directly comparable. Between-round trends remain descriptive. A failed recommendation does not deny a measured improvement in one metric. Historical sessions and frozen confirmations retain the objective and target plan used when measured; new protocols are included in comparison identity.

## Optional measured circle verification

After activating the selected Raw Accel proposal, the user may run 30 natural clockwise and 30 counterclockwise attempts again. The active profile must match the proposal before recording and the driver fingerprint must remain unchanged afterwards. The initial baseline must contain circle data. Incomplete checks are not substituted for completed comparisons. This verification saves separate circle data and a copy of the measured profile; it does not refit or activate settings. The display overlays the centered, size-normalized first-baseline median and newly measured median, with axis ratios. Both curves are measurements, not predicted transforms. Rounder shapes alone are not evidence of better aim. This check currently supports Raw Accel; Interception verification is not enabled.

Circle attempts are counted independently of fit quality: all 30 attempts per direction, including timeouts, count toward progress. Finite moving paths enter the median regardless of closure, roundness or fit residual. No-motion/invalid paths cannot supply a shape. The separate requirements for proposing an axis adjustment do not invalidate recorded attempts. Instructions explicitly discourage trying to draw cosmetically perfect circles.

## Result display and directional map

The time-to-hit panel compares Baseline (profile A measured in this comparison) with New profile (B), using the median of all precision attempt times. Misses remain inside elapsed time; timeouts count as 3000 ms. Both bars share one scale. Separate median components are not stacked, because their sum need not equal the median total. Existing mean-based safeguards and comparison decisions remain unchanged. The first-ever baseline remains a separate historical reference.

Hit rates show completed precision targets and first-click flick bullseyes separately. Differences are percentage points, not relative percent. Unknown historical hit outcomes and empty samples display as unavailable. Colors describe measured differences, not statistical confirmation.

The eight-direction map uses only the current A/B comparison, showing sample counts, mean precision time, mean corrective travel, completed hits, and first-flick reach where the primary stroke was detected for every sample. Missing trajectory measurements are not treated as zero. Screen directions match the target coordinate system. Per-direction groups are small and descriptive; 100% initial reach is not automatically optimal.

## Profile history, recovery and application updates

History lists saved measurements with input/proposal settings and their assessment. Opening a result never activates it. Restoring a measured profile or first-baseline profile requires an explicit choice, backs up current Raw Accel settings and verifies the driver readback. This restores Raw Accel settings, not an Interception curve. Neutral reset and last-driver-backup recovery remain distinct controls.

Production measurements, profile exports, backups and language preferences live in %LOCALAPPDATA%/TrueTrack/profiles, independently of the executable folder. Existing adjacent profiles are copied on startup, never moved or overwritten. Conflicting files are preserved in import-conflicts; originals remain intact. Development tests retain isolated storage.

Update checks query this project's GitHub Releases. Startup checks are opt-in; manual checks are available. Experimental releases are included and labeled. Installation requires user acceptance, checks the published SHA-256 checksum and accepts assets only from this project's release URLs. The updater stages an allowlisted package, waits for the app to exit, backs up replaced program files and attempts rollback on copy failure. It never installs drivers, activates a profile or overwrites the data directory. No measurement data is sent to GitHub. Checksums detect corruption; they are not independent publisher signatures.

## Guided interface

The interface separates setup, baseline collection, proposal review, comparison and confirmation. The next action is placed above the results. Proposed settings are never displayed as measured after-results before an A/B measurement exists. An incomplete comparison offers another A/B test; completed comparisons offer confirmation, or global-profile selection when validated. Global activation remains explicit. Diagnostic graphs, alternative next-round actions and legacy circle reanalysis remain available in expandable sections. These layout changes do not alter fitting or validation thresholds.
