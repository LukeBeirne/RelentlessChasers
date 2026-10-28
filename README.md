# Relentless Specimens

A [BepInEx](https://github.com/BepInEx/BepInEx) 5 mod for **Spooky's Jump Scare Mansion: HD Renovation** that makes Endless mode chasers stay longer and join more often, with every chaser tunable in game.

By default the mod only affects Endless mode. The main game is untouched.

## Features

- **Per-chaser settings** for all 25 chasers (the specimens that can re-chase you in Endless).
  - `SpawnChance` (0-1) is a weighting used when a new chaser joins. 0 means that chaser never spawns.
  - `DespawnChance` (0-1, default 0.1) is rolled at the end of each room once the chaser has stayed long enough. 0 means never, 1 means always.
  - `MaxRoomsBeforeDespawn` (default 0 = no limit): the chaser always leaves after this many rooms.
  - `MaxCopies` (default 0 = no limit): the most copies of this chaser that can be active at once.
- **Chaser settings**
  - `SpawnChancePerRoom` (default 0.5): chance each room that a new chaser joins (while under the cap). When it triggers, the chaser is picked using the per-chaser weightings.
  - `MinRoomsBetweenSpawns` (default 0): after a spawn, wait at least this many rooms before the next one.
  - `MinRoomsBeforeDespawn` (default 0): a chaser always stays at least this many rooms. The first despawn roll comes at the end of room N+1. Spooper and Lisa, who give a warning before they can hurt you, don't count rooms or despawn until they become a threat.
  - `AllowDuplicateChasers` (default on): the same chaser can be active more than once, up to its `MaxCopies`.
  - `OnlySpawnMetSpecimens` (default on): only spawn specimens already in your encyclopedia (CAT-DOS), like the game's own re-chases. See [Encyclopedia and achievements](#encyclopedia-and-achievements).
  - `PersistChasers` (default off): override that stops all chasers despawning.
  - `ExtraChaserSlots` (default 2, max 100): added to the game's chaser cap. The game's own Endless cap is 1 chaser per 100 rooms, up to 5. High values work, and are exactly as ridiculous as they sound.
  - Exception to all of the above: Lisa's chase and White Face's second pursuit stop the room count (and floor progress) while they last, so they still end no later than in the base game (19 and 15 rooms).
- **Escalation**: `SpawnChancePer100Rooms` and `ExtraSlotsPer100Rooms` (both default 0 = off) raise the spawn chance and the cap for every 100 rooms you reach, so the deeper you go the worse it gets.
- **General settings**
  - `ModEnabled` (default on): master switch. Turn it off and the mod does nothing, so you can play the unmodded game without uninstalling it. It takes effect immediately, except `SkipIntroRooms`, which changes from the next floor.
  - `EndlessOnly` (default on): only apply the mod in Endless mode.
- **Endless settings**
  - `AllowSaveWithChasers` (default on): lets you quit to the menu with a chaser active, and stops the game invalidating the Endless save. Active chasers aren't saved, so they're gone when you resume. This is also how the base game treats them.
  - `SkipIntroRooms` (default off): removes the specimen intro rooms from Endless floors. Those rooms clear your active chasers and start a guaranteed chase with a specimen you haven't met yet. Takes effect from the next floor (every 100 rooms) or when you load a save.
  - `UploadToLeaderboards` (default off): whether Endless scores are sent to the Steam leaderboards while the mod is installed.
- **Bulk edit** (F1 window, "Bulk edit" section)
  - **Presets**: one-click *Vanilla-ish*, *Relentless* (the defaults) and *Nightmare*. They set the spawn, despawn, cap and escalation settings, but leave which chasers can spawn alone.
  - **Apply to all**: pick a value and overwrite every chaser's `SpawnChance`, `DespawnChance`, `MaxRoomsBeforeDespawn` or `MaxCopies` at once. You can then adjust individual chasers.
  - These buttons need Configuration Manager. Their slider values also appear in the `.cfg` file but do nothing there.
- **Overlay**: press **F2** in game to show the active chasers, how many rooms each has stayed, the cap and the current spawn chance.
- **Debug hotkeys** (off by default, `EnableDebugHotkeys`): **F3** spawns the chaser chosen in `ChaserToSpawn`, **F4** removes all chasers.
- Chasers are still cleared when you enter a specimen's intro rooms, as in the base game, unless `SkipIntroRooms` is on.
- Settings are labelled `ID (nickname)`, e.g. `EMKHMonster5 (Ghost Cow)`. All hotkeys can be changed.

## Installation

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (Mono, x64) into the game folder.
2. Install [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager) (`ConfigurationManager.dll` in `BepInEx\plugins\`). It gives you the F1 settings window. The mod also works without it, but then you can only edit the `.cfg` file.
3. In `BepInEx\config\BepInEx.cfg`, set `HideManagerGameObject = true` under `[Chainloader]`. The game destroys BepInEx's manager object, which otherwise stops the F1 window, the overlay and the hotkeys from working.
4. Put `RelentlessSpecimens.dll` in `BepInEx\plugins\`. If you're upgrading from the old "Relentless Chasers" build, delete `RelentlessChasers.dll`.
5. Launch the game. Press **F1** to open the settings. The camera is locked while the window is open.

Settings are saved to `BepInEx\config\spooky.relentlessspecimens.cfg`.

## Encyclopedia and achievements

- With `OnlySpawnMetSpecimens` on (the default), the mod never spawns a specimen you haven't met, so it can't unlock encyclopedia entries or progress the "Specimen Researcher" achievement for you. You still meet new specimens through their intro rooms, unless `SkipIntroRooms` is on.
- With it off, any chaser can spawn, and spawning one unlocks its encyclopedia entry.
- Duplicates spawned by the mod don't award the duplicate-chaser achievement ("Clone Wars"). It can still be earned the normal way.
- Endless scores aren't uploaded to the leaderboards unless you turn on `UploadToLeaderboards`.

## Building

Requires the .NET SDK and a game install with BepInEx and Configuration Manager already set up (the project references their DLLs from the game folder; none are included in this repo).

```
dotnet build
```

If your game isn't in the default Steam location, pass its path, keeping the trailing backslash:

```
dotnet build -p:GameDir="D:\Games\Spooky's Jump Scare Mansion HD Renovation\"
```

A successful build copies `RelentlessSpecimens.dll` into the game's `BepInEx\plugins\` folder. The DLL is also in `bin\`.

### Packaging a release

```
powershell -ExecutionPolicy Bypass -File package.ps1
```

This builds the mod and creates two zips in `dist\`:

- `RelentlessSpecimens-<version>.zip`: for manual installs, laid out as `BepInEx\plugins\RelentlessSpecimens.dll`.
- `RelentlessSpecimens-<version>-thunderstore.zip`: a Thunderstore/r2modman package (`manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md` and the DLL).

The script checks that the version in `Plugin.cs` matches `manifest.json`.

## Notes

- Endless checkpoints every 100 rooms, so quitting mid-floor resumes at the last checkpoint. This is the game's own behaviour.
- Very high caps stack every specimen's effects (fog, lights, blocked doors). Nothing breaks, but it gets silly fast.

## License

[MIT](LICENSE)
