# Pipe Flow

A portrait puzzle game. Rotate pipe segments to guide water from the inlet to the outlet, complete stages, collect stars and unlock new areas.

## Project layout

| Directory | Contents |
| --- | --- |
| `Assets/Scripts/` | Gameplay, puzzle generation, save data and localization |
| `Assets/Scenes/` | Main Unity scene |
| `Assets/Resources/Art/` | UI artwork, puzzle animation and world maps |
| `Assets/Resources/Images/` | Shared interface and puzzle sprites |
| `Assets/Resources/Audio/` | Music and sound effects |
| `Assets/Resources/RawArt/` | Offline texture fallbacks |
| `Assets/Editor/` | Asset checks and build helpers |
| `Docs/` | Setup and testing guides |

## Open in Unity

1. Open this folder in Unity Hub using the editor specified in `ProjectSettings/ProjectVersion.txt` (or a compatible Unity 6 release).
2. Open `Assets/Scenes/Main.unity`.
3. Start Play Mode.

## Features

- 120 generated puzzle stages across 10 map areas
- Star-based progress and a reward chest every 15 stars
- Inventory with level-based shop unlocks, purchase affordability states, tutorial prompts and locally saved progress
- English, Turkish, German, French and Spanish language options
- Animated pipe rotation and world-map road transitions

Shop, purchase and chest UI testing instructions are in [`Docs/SHOP_TESTING_TR.md`](Docs/SHOP_TESTING_TR.md).

## Project status

This project includes the full Unity source and bundled artwork. Asset paths and archive integrity can be checked without the editor; Unity compilation, mobile device performance and store readiness **must be verified in Unity** before release. Visual changes may require small screen-specific adjustments.

## License and attribution

Artwork, music and source code are supplied for use by the project owner. No open-source license is included. Add a license only after confirming the rights to all included assets.
