# Development progress

## Current phase

Phase 4 — Jungle map and local best-star progression implemented; broader device testing remains.

## Completed

- Created the portrait Unity Universal 2D project and public GitHub repository.
- Added a prototype scene with left/right touch movement, keyboard and mouse Editor controls, falling blocks, collisions, a dodge counter, pause, interruption pause and retry.
- Configured the prototype as the build scene and imported TextMesh Pro essentials for the interface.

## Next steps

- Verify screen layouts, safe areas and performance on more Android devices and on an iPhone.
- Test the five levels and star goals on devices. Add warning cues and validate safe routes before expanding the level set.
- Verify the regular Android build retains the restored map progress after restart, then add warning cues and safe obstacle patterns.

## Known gaps

- Visuals remain placeholders. Warning cues and fairness validation are not implemented. Levels 6–10 are marked as coming soon. Level definitions need tuning.
- Obstacles use simple random positions; guaranteed reachable patterns are planned for a later phase.
- Android offline builds succeeded using the project build script and an existing Gradle cache. Unity logs licensing handshake warnings despite reporting a successful build. iOS builds need macOS and Xcode and remain untested.
- During initial project setup, one console error and four warnings appeared; their details were not captured. Later sessions reported none.

## Last verified

2026-11-27 — In Unity 6000.3.23f1, the prototype entered Play Mode. Checked keyboard movement and arena boundary, obstacle scoring, collision/game over, restart, pause/resume and simulated app pause. The Editor console reported zero errors.

2026-11-27 — On a realme RMX3263 running Android 11, the user confirmed left/right touch movement, pause/resume, game over/restart with score reset, and automatic pause after returning from the Home screen. The app launched and Android logcat showed no Unity or AndroidRuntime errors at the time checked. Other devices, safe areas and long-run performance remain untested.

2026-11-27 — Added five sequential prototype levels, required dodge targets, optional stars, paired obstacles from level 3, a safe-area HUD and larger text. Accelerated Editor checks verified the five target transitions. The Android offline Unity build succeeded. On the realme phone, the user confirmed the larger instructions and counter are readable, that level 1 shows stars and a Next Level button, and that level 2 starts at 0 / 25. Full level progression and broader device testing remain to be verified.

2026-11-27 — Added a Jungle map for levels 1–10, local best-star saves and unlocks for the first five levels. Editor checks verified saved progress after a Play Mode restart and that a lower replay score preserves the best stars. At the user’s request, one-time restoration on their phone recorded one star for levels 1–3 and unlocked level 4 because older star totals were unavailable. Close-dodge detection now measures proximity while the obstacle passes the player and displays a confirmation.

2026-11-27 — Installed the regular map build on the realme phone after the one-time progress restoration. The Android build succeeded through the offline build script. Close-dodge proximity and the in-game confirmation were verified in the Editor. Full device playtesting of star criteria and other screen sizes remains.
