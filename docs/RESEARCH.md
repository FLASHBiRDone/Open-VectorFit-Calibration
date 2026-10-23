# Aim practice, sensitivity and field of view

Research review: 23 October 2026. This document separates evidence from design proposals. It does not validate TrueTrack's effectiveness.

## What the studies support

- **Sensitivity involves a speed/precision trade-off.** Boudaoud, Spjut and Kim's first-person targeting study describes a range of useful sensitivities rather than one universally optimal setting. It measures sensitivity as physical mouse displacement relative to view rotation. Task difficulty matters. This supports testing time and accuracy together, not selecting a profile solely because its paths look straighter. [Mouse Sensitivity in First-person Targeting Tasks, 2023](https://research.nvidia.com/publication/2023-07_mouse-sensitivity-first-person-targeting-tasks).
- **Practice effects must be separated from profile effects.** Listman and colleagues analysed 7,174 participants and 682,564 repetitions of a 60-second aiming task. Hits per second improved substantially, while accuracy improved more modestly; gains were only partly retained the next day. Additional practice had diminishing returns. This observational study cannot establish a universal ideal session length or prove transfer to competitive matches. Some authors were affiliated with Statespace. [Long-Term Motor Learning in the Wild, 2021](https://www.cns.nyu.edu/heegerlab/content/publications/Listman-Frontiers2021.pdf).
- **Repeatability is not proof of training benefit.** A small KovaaK's pilot study examined the reliability of shooting metrics across repeated sessions. This is useful evidence for repeated measurement, but it does not establish that any particular calibration improves real-game performance. [KovaaK's aim trainer as a reliable metrics platform, 2024](https://www.frontiersin.org/journals/sports-and-active-living/articles/10.3389/fspor.2024.1309991/full).

These sources do not establish that matching FOV and sensitivity is necessary for all learning transfer. They also do not establish that TrueTrack's proposed profile improves a player's aim. We need to measure that separately.

## Matching a game: two different quantities

**cm/360 describes physical turning sensitivity:** how far the mouse moves for one full camera rotation. Equal in-game sensitivity numbers do not necessarily mean equal cm/360 across games.

**FOV describes projection:** how much of the scene fits on screen. Even at identical cm/360, a different FOV changes the apparent motion and size of targets. Matching both is a sensible engineering choice when trying to reproduce a particular game; it is not evidence that other configurations cannot teach useful skills.

For the standard perspective projection used here:

`horizontal FOV = 2 × atan(tan(vertical FOV / 2) × aspect ratio)`

Near screen centre, pixels per radian are proportional to `width / (2 × tan(horizontal FOV / 2))`. Thus, at equal screen width and cm/360, changing horizontal FOV from 90° to 110° reduces local screen motion to about 70% of its former value. This is a geometric calculation, not a measured training effect. A single sensitivity multiplier cannot make different perspective projections match at every screen angle.

A game's FOV may be vertical, actual horizontal, or horizontal relative to a reference aspect ratio. Stretched rendering and scopes add further differences. Resolution alone cannot determine an optimal FOV.

## Practical configuration procedure

1. Choose the context to reproduce: ordinary view or a particular scope/zoom. Keep these as separate comparisons.
2. Match the mouse's actual DPI setting. Use the game's actual horizontal FOV and rendering aspect ratio; convert only when the game's convention is known.
3. Establish the base physical sensitivity with neutral driver scaling and acceleration disabled. A ruler measurement of a 180° turn can be doubled to estimate cm/360; repeat it. TrueTrack's nominal conversion assumes the specified DPI.
4. If a global profile is active, record that separately. Do not enter a gain-adjusted measured cm/360 as the base conversion while also applying that gain to input: that would count the change twice. With acceleration, physical turning distance can depend on movement speed; a single cm/360 value is incomplete.
5. Keep setup, task difficulty and input configuration stable during comparisons. After a proposed change, check the result in the actual game as well as the trainer.

The app's blind movement estimate asks what travel feels desirable. It does not measure the game's camera rotation, infer its sensitivity scale, or establish an optimal setting.

## Implications for TrueTrack

Prefer fewer corrective movements and faster successful hits, while retaining misses and timeouts in the assessment. Report accuracy alongside speed so that waiting longer or giving up on difficult targets does not masquerade as improvement. Keep precision, flicking and tracking visible as separate skills.

Use balanced A/B order, repeat the same proposal, and test again on another day. Uncertainty intervals and repeated checks help interpret variation; neither establishes causality by itself. A 0.5% threshold is a product decision for detecting small candidates, not a scientifically established minimum benefit.

For training, use repeatable task-specific practice and compare equivalent sessions. Validate transfer in the intended game. Avoid promising a perfect sensitivity, a prescribed universal practice duration, or automatic correction of every movement.
