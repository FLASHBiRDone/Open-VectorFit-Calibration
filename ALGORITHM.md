# VectorFit 0.12: dual tasks and tendency-based calibration

## Measurement and fitting

The original timestamped movement path and click outcome are preserved. Hit detection and target-entry detection use the same perspective projection and the recorded angular target radius. An invalid mapping attempt is stored but not used to fit direction. An invalid A/B attempt is retained as a miss with normalized distance loss at least 1; timeouts are not retried selectively.

Trajectory analysis interpolates positions at 4 ms intervals, with a three-sample position filter for stroke segmentation. Once axial progress exceeds 35% and movement has lasted at least 32 ms, a reversal of at least max(2.5% target distance, half the target radius) lasting at least 12 ms, or a 32 ms low-speed interval below 12% of the prior peak speed, can identify the end of the initial stroke. If no such boundary is found, fitting uses the click endpoint. These are engineering heuristics, not a validated motor-control model.

The boundary is identified on the observed path. The active profile is inverted per raw delta before fitting the corresponding prefix, preventing double correction. Direction, overall sensitivity and the optional curve model use that prefix. The full path still contributes to observed performance statistics and A/B assessment. Circle fitting remains a weak, bounded prior. Existing total and per-round correction bounds are unchanged.

Metrics include click distance, actual hits, initial reach, maximum axial overshoot, reverse axial travel, path length after the first stroke, excess path length, first target entry, target re-entries and time after the first stroke. Target exits use 8% radial hysteresis to limit boundary jitter. Missing trajectory or target geometry is not presented as zero error.

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

## Compatibility and verification

Version 0.8 and older retain the original fitting method and assessments when reopened. Newly requested A/B on an old proposal uses the adaptive protocol without refitting it. The comparison protocol and achieved sample count are stored separately for Raw Accel and Interception. New rounds use the 0.9 fitting method, and existing source files remain intact through new versioned filenames.

Tests cover corrected hits, direct hits, undershoot, polling-rate variation, exact stopping thresholds, balanced scheduling, actual live early-stop and extension transitions, timeouts, missing/mismatched pairs, hit/time/path regressions, legacy profile equivalence and UI rendering. Synthetic and offline historical tests verify mechanics, not real-world efficacy. No driver installation, activation or mouse-input injection is performed by these tests.

## Historical fixed-size circle protocol and shape estimation (0.10)

`CircleData.Protocol=1` is explicitly recorded by the new circle form. Legacy files omit it and retain protocol 0, including their original 15-lap contribution and previously tested proposals. New protocol: radius 12 degrees, 30 completed clockwise laps then 30 counterclockwise. A timeout is retained but retried without advancing the completed-lap index. Direction change has a 2.5-second unmeasured countdown. Pause and closure retain incomplete attempts. Completion opens a median preview; it can be saved and resumed at target mapping without repeating the circles.

The existing robust conic fit supplies each lap's center and axes. Each valid lap is centered and divided by sqrt(major*minor), removing overall size while retaining orientation. Radius is summarized in 72 equal angular bins. At least 60 bins must be occupied; missing bins are interpolated cyclically only when the surrounding span is at most 9 bins. The median radius across laps at each bin forms a pointwise median path. A conic fit to that path measures its ellipse. Original paths remain unmodified. CW, CCW and combined median paths are all shown; simulation is labeled as prediction rather than a measured improvement.

Eligibility requires 30 complete laps each direction, >=24 eligible shapes each direction, the configured common radius, and a valid median ellipse. Shape disagreement uses the vector log(major/minor)*(cos(2*angle),sin(2*angle)), so opposite tilted ellipses do not falsely agree just because their width/height matches. Median distance from the aggregate ellipse vector must be <0.10. CW versus CCW median vector distance must be <0.08, as must early-half versus late-half distance separately within each direction. Fixed ordering may still confound direction with fatigue; these are engineering safeguards, not independent-sample confidence tests or proof of a sensor defect.

The circle prior predicts the median under the existing flick-derived proposal after undoing the measured input correction per delta. It tries a horizontal multiplier clamp(1/sqrt(width/height),0.98,1.02) and reciprocal vertical multiplier, subject to the existing absolute [0.92,1.08] axis bounds. It contributes only if simulated major/minor ratio falls by at least 0.005 and the starting ratio is >=1.02. It does not change rotation or scalar sensitivity. A tilted ellipse may therefore remain partly or entirely uncorrected. It is not silently whitened with a large rotation. Real A/B remains mandatory for a positive recommendation.

## Conservative sensitivity evidence (0.10)

New rounds set `ConservativeSensitivity=true`. In addition to existing six-of-eight direction support and split agreement, sensitivity changes require repeatable directional fitting, absolute median reach error >=max(0.02,0.75*MAD), and the same over/undershoot direction beyond 2% in the chronological first and last halves. This responds to the user's v0.9 recording: +3.18% sensitivity was proposed from initial undershoot despite unstable direction fitting; the candidate increased overshoot and failed A/B. The stronger rule leaves sensitivity unchanged for that recording in offline tests. It has not yet been demonstrated to improve real-user performance. Old files retain false and their exact tested proposals. New filenames use `-v10`.

## Active-profile recovery

The mismatch dialog offers an explicit Neutralize profile button with Cancel as the default key action. It backs up, applies neutral, re-reads and verifies the driver through the established path. A neutral-input measurement can resume after a successful readback. A nonneutral-input round returns to setup for a fresh measurement instead of combining old samples with a new input basis. Driver absence or uncertain readback never implies successful neutralization. Tests do not activate or neutralize a real driver.

## Historical deferred idea (through 0.11)

The user proposed separate flick and precision stages to explore the speed/accuracy optimum, with click-to-advance versus hit-to-advance behavior. They explicitly asked not to build this yet. Version 0.11 retains the existing target-stage click behavior.

## Sources

- https://www.yorku.ca/mack/CHI01.htm — trajectory measures complement endpoint accuracy.
- https://www.itl.nist.gov/div898/software/dataplot/refman1/auxillar/signtest.htm — paired sign test and binomial interpretation.
- https://itl.nist.gov/div898/handbook/prc/section4/prc473.htm — prespecified multiple-comparison error allocation.

## Guided calibration and natural circles (0.11)

New sessions explicitly set GuidedCalibration=true. Phase 1 ends after mapping and proposal fitting, without automatic A/B. Trying a proposal uses the existing explicit activation confirmation and driver readback before starting the next phase. Keeping the current profile starts the next phase on its verified input basis. Later phases retain the existing adaptive comparison. Legacy files retain their tested proposals. Result rows show scalar sensitivity, four directional multipliers and rotation relative to InputCorrection. History deltas are descriptive, not causal estimates.

Circle protocol 2 records Counted separately from Complete. A timeout consumes one of the 30 attempts per direction; pauses do not. Natural tracking removes the radius-based completion gate. No target dots or tracer ask the user to repair their natural form.

All counted finite moving paths contribute to the median regardless of roundness, closure or fit residual. Observed paths are centered by their bounding box and resampled to 72 equally spaced path-length positions, without adding a closing segment. CCW order is reversed; RMS radius normalizes size. Cyclic starting-point alignment does not rotate geometry. Pointwise medians preserve irregular shapes. Zero-motion attempts remain recorded but cannot supply shape data.

Ellipse fitting and reliability are separate from inclusion in the displayed median. At least 24 measurable ellipse estimates per direction and 30 recorded attempts per direction are required for the bounded prior. Spread, direction agreement and early/late agreement retain the 0.10, 0.08 and 0.08 thresholds. These are engineering heuristics. The existing bounded axis-only proposal and simulated improvement requirement remain; no performance gain is inferred from geometric rounding alone.

## Version 0.12: dual tasks and tendency-based circles

New phases set DualTask. Mapping uses the existing flick trials followed by 16 precision targets (8 directions, radii 8 and 14 degrees). Precision target radius is 0.35 degrees, subject to the existing 5-pixel minimum; flick remains 0.65 degrees. Each task transition and the post-warmup transition has centered instructions and requires a click that does not consume a trial. Targets retain the 150–900 ms onset jitter. Phase 1 ends after both mapping tasks. Later phases run the existing sequential flick comparison, then 32 precision trials in matched A/B pairs, balanced in order within each direction.

Precision is hit-to-advance with a fixed 3000 ms deadline from target onset, including reaction time. Miss clicks retain their timestamps, positions and hit flags without resetting the target or clock. All attempts, including timeouts and interrupted attempts, are kept. Precision uses separate session lists, never the flick endpoint-fitting dataset. A timeout contributes 3000 ms to precision time summaries. A successful flick comparison is gated by complete matched precision data, at least 8 of 16 hits in each profile, no fewer B hits, no additional B miss clicks and no more than 5% increase in both median and total target time. These are engineering regression guards, not a significance claim or formal noninferiority proof.

Circle protocol 3 retains every natural moving path from protocol 2. Proposal eligibility now tolerates ordinary individual variation: anisotropy-vector median spread <0.55, CW/CCW gap <0.25, early/late gap <0.35, at least 24 measurable shapes per direction and exactly 30 counted attempts each. Opposing median log width/height biases beyond ±0.02 block a common proposal. Maximum per-axis adjustment is 0.02 * clamp(1-max(0,spread-0.10)/0.60,0.25,1), within the existing absolute bounds. The simulated median must still become rounder by at least 0.005. These thresholds are explicit engineering choices, not empirically established optimum values. The largest deviations are controlled by robust medians and the disagreement gates; no shape is removed from the displayed median for being ugly.

Old protocol 2 proposals remain unchanged on load. Explicit ReanalyzeCircleTrend clones the data, records its source filename, upgrades only the circle protocol, clears both profiles' validation records and creates a new timestamp. It does not increment the measurement phase or activate the driver. Its candidate needs new validation.

InputCorrection is copied from the selected previous profile. MainForm verifies driver matching before measurement and fingerprint continuity afterward. Ordinary input passes through once. UndoInputCorrection inverts each movement delta before fitting; A/B inverses the old profile then applies the complete candidate. Tests cover sign-changing movement, unequal axes, rotation, nonunit sensitivity, repeated rounds and agreement with the official Raw Accel runtime simulation. Tests do not activate real driver settings.
