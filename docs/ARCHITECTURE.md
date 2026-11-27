# Architecture

## Technology

Unity 6000.3.23f1, C#, Universal 2D template, Unity Input System, 2D physics, uGUI and local persistence. Android and iOS are target platforms. Verify current platform requirements before release.

## Planned systems

- Game state controller manages ready, play, pause, completion and game over.
- Input and player controllers handle touch, movement and arena bounds.
- Level loader reads editable level data and loads world presentation.
- Spawn director selects patterns and validates a reachable safe route. An obstacle pool reuses instances.
- Objective tracker responds to gameplay events and calculates stars.
- Progression and save services retain unlocked levels, best stars, cosmetics, settings and per-difficulty endless high scores.
- UI, audio and haptics respond to game events and saved settings.

Level data should define objectives, obstacle patterns, speed, world assets and special mechanics. Save data should be versioned, handle missing or invalid data, and preserve the best result. Pause on focus loss and require the player to resume.

These systems are planned and have not yet been implemented.
