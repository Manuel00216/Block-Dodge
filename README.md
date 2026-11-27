# Block Dodge

A portrait mobile arcade game where players dodge falling obstacles and progress through themed worlds. Android and iOS are planned.

## Status

Core gameplay prototype available in `Assets/Scenes/Prototype.unity`. Level progression and mobile build verification are planned. See [progress](docs/PROGRESS.md) and [roadmap](docs/ROADMAP.md).

## Getting started

1. Install Unity Hub and Unity 6000.3.23f1 with Android Build Support, SDK, NDK, and OpenJDK.
2. Clone this repository, including Git LFS assets if any are added.
3. Add the project folder to Unity Hub and open it with the specified Editor version.
4. For iOS builds, use a Mac with Xcode and the appropriate Unity iOS build support.

`Assets/` contains game assets, `Packages/` contains package configuration, `ProjectSettings/` contains shared Unity settings, and `docs/` contains the design and implementation plan.

[Game design](docs/GAME-DESIGN.md) · [Architecture](docs/ARCHITECTURE.md) · [Roadmap](docs/ROADMAP.md) · [Level design](docs/LEVEL-DESIGN.md) · [Testing](docs/TESTING.md) · [Decisions](docs/DECISIONS.md)

## Playing the prototype

Open `Assets/Scenes/Prototype.unity` in Unity and press Play. Select Play in the game, then hold the left or right side of the screen. In the Editor, use A/D, the arrow keys, or hold the mouse on either side. Escape or the pause button opens the pause screen.
