# Relentless Chasers

A [BepInEx](https://github.com/BepInEx/BepInEx) 5 mod for **Spooky's Jump Scare Mansion: HD Renovation** that makes Endless mode chasers stay longer and join more often, with every chaser tunable in game.

By default the mod only affects Endless mode. The main game is untouched.

## Features

- **Per-chaser spawn and despawn settings** for all 25 chasers.
  - `SpawnChance` (0-1) is a weighting used when a new chaser joins. 0 means that chaser never spawns.
  - `DespawnChance` (0-1, default 0.1) is rolled at the end of each room once the chaser has stayed long enough. 0 means never, 1 means always.
- **Global settings**
  - `SpawnChancePerRoom` (default 0.5): chance each room that a new chaser joins (while under the cap). When it triggers, the chaser is picked using the per-chaser weightings.
  - `MinRoomsBeforeDespawn` (default 0): a chaser always stays at least this many rooms. The first despawn roll comes at the end of room N+1.
  - `AllowDuplicateChasers` (default on): the same chaser can be active more than once.
  - `PersistChasers` (default off): override that stops all chasers despawning.
  - `ExtraChaserSlots` (default 2): added to the game's chaser cap. The game's own Endless cap is 1 chaser per 100 rooms, up to 5.
  - `EndlessOnly` (default on): only apply the mod in Endless mode.
  - `AllowSaveWithChasers` (default on): lets you quit to the menu with a chaser active, and stops the game invalidating the Endless save. Active chasers aren't saved, so they're gone when you resume.
- **Bulk edit** (F1 window, "Bulk edit" section): pick a value with the slider and press **Apply** to overwrite every chaser's `SpawnChance` or `DespawnChance` at once. You can then adjust individual chasers afterwards. The buttons need Configuration Manager; the two slider values also appear in the `.cfg` file but do nothing there.
- Chasers are still cleared when you enter their spawn section, as in the base game.
- Settings are labelled `ID (nickname)`, e.g. `EMKHMonster5 (Ghost Cow)`.

## Installation

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (Mono, x64) into the game folder.
2. Install [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager) (`ConfigurationManager.dll` in `BepInEx\plugins\`). It gives you the F1 settings window. The mod also works without it, but then you can only edit the `.cfg` file.
3. In `BepInEx\config\BepInEx.cfg`, set `HideManagerGameObject = true` under `[Chainloader]`. The game destroys BepInEx's manager object, which otherwise stops the F1 window from working.
4. Put `RelentlessChasers.dll` in `BepInEx\plugins\`.
5. Launch the game. Press **F1** to open the settings. The camera is locked while the window is open.

Settings are saved to `BepInEx\config\spooky.relentlesschasers.cfg`.

## Building

Requires the .NET SDK and a game install with BepInEx and Configuration Manager already set up (the project references their DLLs from the game folder; none are included in this repo).

```
dotnet build
```

If your game isn't in the default Steam location, pass its path, keeping the trailing backslash:

```
dotnet build -p:GameDir="D:\Games\Spooky's Jump Scare Mansion HD Renovation\"
```

A successful build copies `RelentlessChasers.dll` into the game's `BepInEx\plugins\` folder. The DLL is also in `bin\`.

## Notes

- Spawning a chaser makes the game unlock its encyclopedia (CAT-DOS) entry, which counts towards the "Specimen Researcher" achievement.
- Endless checkpoints every 100 rooms, so quitting mid-floor resumes at the last checkpoint. This is the game's own behaviour.

## License

[MIT](LICENSE)
