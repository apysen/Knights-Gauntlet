# Knight's Gauntlet

Knight's Gauntlet is a chess-inspired arcade game built with Unity. Move your piece with knight-style L-jumps, then adapt as the board fills with hazards and enemies.

## Screenshots

| Main menu | Trap mode | Battle mode |
| --- | --- | --- |
| <img src="screenshots/phone-screenshot-menu.png" alt="Knight's Gauntlet main menu" width="200"> | <img src="screenshots/phone-screenshot-trap.png" alt="Trap mode gameplay" width="200"> | <img src="screenshots/phone-screenshot-battle.png" alt="Battle mode gameplay" width="200"> |

| Adventure mode | Transformation | Shop |
| --- | --- | --- |
| <img src="screenshots/phone-screenshot-adventure.png" alt="Adventure mode gameplay" width="200"> | <img src="screenshots/phone-screenshot-transform.png" alt="Player transformation gameplay" width="200"> | <img src="screenshots/phone-screenshot-shop.png" alt="In-game shop" width="200"> |

## Game modes

- **Trap** — navigate through gaps in falling trap rows and survive as the pace increases.
- **Battle** — capture enemy chess pieces, build a score multiplier, and transform into captured forms.
- **Adventure** — play handcrafted, solver-validated levels across worlds that introduce traps, moving enemies, collapsing tiles, and transformations. Includes a multi-phase queen boss.

The game also includes four difficulty settings, a tutorial, daily challenges and rewards, unlockable cosmetics, power-ups, achievements, and leaderboards.

## Open and run

1. Install **Unity 6.5** (`6000.5.0f1`) with Unity Hub. Add Android and/or iOS build support if you plan to build for those platforms.
2. In Unity Hub, open this repository as a project. Unity will resolve the packages listed in `Packages/manifest.json`.
3. Open `Assets/Scenes/SampleScene.unity` and press **Play**.

The scene is configured as the project's build scene. The game builds its board and interface at runtime. On a touch device, tap a highlighted square to move; the tutorial in the game explains the rules. Grandmaster difficulty hides move highlights.

## Project layout

```text
Assets/
  Scenes/        Main playable scene
  Scripts/Core/  Rules, simulation, AI, generators, and adventure solvers
  Scripts/View/  Unity presentation, UI, persistence, and service integrations
  Resources/     Game assets loaded by Resources.Load at runtime
  Settings/      Render pipeline and build-profile assets
  Editor/        Project-specific Unity editor scripts
  ...            Imported plugins and art packs remain in their own folders
ArtSource/       Original artwork and models kept outside Unity's import tree
Tools/Art/       Scripts that generate or import game-ready art
Packages/        Unity package manifest and lock file
ProjectSettings/ Unity project and build settings
supabase/        Backend-related project files, if configured
```

The game logic is separated from the Unity view layer: `Assets/Scripts/Core` contains the board rules and simulation, while `Assets/Scripts/View` connects that logic to scenes, UI, saves, and platform services.

Keep editable source art in `ArtSource/` and imported, game-ready assets in `Assets/`. This keeps large source models out of Unity's import process while leaving runtime-loaded content under `Assets/Resources/`. The chess-piece Blender sources and original archive are in `ArtSource/Models/ChessPieces/` and `ArtSource/Models/` respectively.

## Optional services

The game can run without an online leaderboard. To enable Supabase, create a `SupabaseConfig` asset using **Create → Knight's Gauntlet → Supabase Config**, place it under an `Assets/**/Resources/` folder (the conventional path is `Assets/Resources/SupabaseConfig.asset`), and fill in the project URL and anon/publishable key. This asset is gitignored; provide it locally or through your secure build setup. Without it, online leaderboard features are disabled while local play and personal scores remain available.

Ads and in-app purchases use Unity LevelPlay and Unity IAP. Store products, ad units, platform credentials, and dashboard configuration must be set up for the target platform before expecting live ads or purchases. For local development, the service abstractions include no-op or stub implementations that can be selected in `Assets/Scripts/View/GameManager.cs`.

## Builds

Use **File → Build Profiles** in Unity to select and configure the target platform. Android and iOS store builds may require platform-specific SDKs, signing credentials, store product configuration, and service setup. Keep private credentials out of source control.

## Development notes

- Unity version: `6000.5.0f1`.
- Main scene: `Assets/Scenes/SampleScene.unity`.
- Runtime entry point: `GameManager` in `Assets/Scripts/View/GameManager.cs`.
- Unity-generated folders such as `Library/`, `Logs/`, and build outputs are excluded from version control.
