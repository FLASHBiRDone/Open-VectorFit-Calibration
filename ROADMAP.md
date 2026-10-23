# Roadmap: a free, open-source aim trainer

Our goal is to grow TrueTrack into a free, open-source aim-training platform for Windows, designed for standard mice across brands.

This is a direction for development, not a delivery schedule. Planned features are not available until implemented and tested. Priorities may change with community feedback. The current release is an experimental calibration and aim-testing tool, not a full replacement for Aimlabs.

## Available today

- [x] Flick and precision measurement with separate accuracy and timing metrics.
- [x] Bounded sensitivity and directional calibration proposals.
- [x] Natural-circle tendency analysis.
- [x] Paired A/B comparisons, uncertainty intervals and optional three-check confirmation of the same proposal.
- [x] Local measurement history and English/Norwegian interface.
- [x] A Windows download that does not require building from source.
- [x] Optional Raw Accel setup, neutral reset and profile restoration.
- [x] Optional Interception setup with license and anti-cheat information.

These features are implemented; their presence does not establish effectiveness for every player, mouse or game.

## 1. Make the first session dependable

- [ ] Test clean installation and driver setup across supported Windows configurations.
- [ ] Publish a tested mouse/driver compatibility matrix.
- [ ] Improve first-run guidance, recovery instructions and error explanations.
- [ ] Make builds and release verification easier to reproduce.
- [ ] Validate whether calibration benefits persist across sessions, accounting for practice, fatigue and timing.

**Milestone:** a new user can download, complete a baseline, interpret the result and undo profile changes without developer help.

## 2. Add a core aim-training library

- [ ] Dedicated flick exercises with configurable target size, distance and cadence.
- [x] Smooth tracking exercise.
- [ ] Reactive tracking exercises.
- [x] Target-switching exercise with four visible targets.
- [x] Free precision practice.
- [ ] Dedicated micro-adjustment scenarios.
- [ ] Consistent sensitivity and field-of-view configuration, with clearly documented assumptions.

Training exercises should work without a global input driver. Driver-based calibration remains optional.

**Milestone:** users can practice flicking, tracking and switching with understandable, repeatable scoring.

## 3. Build useful practice and progression

- [x] Save and run a local practice routine.
- [x] Replay recorded movement and clicks.
- [x] Session summaries and comparable practice history charts.
- [x] Report throughput, accuracy and successful-hit time separately.
- [ ] Additional consistency metrics.
- [x] Compare equivalent tasks, settings, durations and input profiles across sessions.
- [ ] Offer transparent difficulty adjustments and optional practice suggestions.

**Milestone:** users can identify what changed and choose what to practice next without being promised a universal “perfect” sensitivity.

## 4. Support community-created exercises

- [ ] Define an inspectable exercise format.
- [ ] Add a simple exercise editor.
- [ ] Support importing and exporting routines.
- [ ] Publish contribution guidance and small, well-scoped starter tasks.
- [ ] Expand accessibility and community translations.

**Milestone:** contributors can create and share exercises without modifying the core calibration code.

## Research proposal: unwanted movement curves

**Planned, experimental and not implemented.** Investigate correction of repeatable curved deviations in a user's movement, while preserving deliberate curves and ordinary variation.

- [ ] Establish whether a consistent tendency repeats across directions, speeds and sessions.
- [ ] Test whether it can be distinguished from deliberate curved motion without knowing an in-game target.
- [ ] Prototype optional correction in the local test environment, with a clear neutral reset.
- [ ] Compare unchanged and corrected input in repeated blind tests: corrective travel, time to hit and accuracy must be considered together.
- [ ] Assess global input integration separately; do not assume an existing driver can express the correction.

Existing path analysis and sensitivity/axis calibration are not this proposed real-time feature. No effectiveness or delivery date is established. The current algorithm remains documented separately in [ALGORITHM.md](ALGORITHM.md).

## Principles

- Keep the core training and calibration tools free and open source.
- Support ordinary mice across brands without requiring proprietary mouse firmware.
- Keep local practice usable without an account.
- Preserve user control: no automatic driver installation or profile activation.
- Keep third-party licenses and anti-cheat limitations explicit.
- Treat promising results as hypotheses to test, not guaranteed performance gains.
- Keep [ALGORITHM.md](ALGORITHM.md) focused on the current implementation; planned work belongs here.

## Contribute

[Open an issue](https://github.com/FLASHBiRDone/TrueTrack/issues/new) with a reproducible bug, a proposed exercise, a usability problem or a focused implementation idea. Explain the problem and how we could tell whether the change helps.

If you share measurements, remove identifiers and only share data you are comfortable making public. Public GitHub issues are not a private support channel.
