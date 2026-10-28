# Changelog

## 0.5.0

First release.

- Endless chasers stay until their per-chaser despawn chance triggers, and new chasers join each room by a global chance, picked by per-chaser weightings.
- Per-chaser `SpawnChance`, `DespawnChance`, `MaxRoomsBeforeDespawn` and `MaxCopies`.
- Global `SpawnChancePerRoom`, `MinRoomsBetweenSpawns`, `MinRoomsBeforeDespawn`, `AllowDuplicateChasers`, `OnlySpawnMetSpecimens`, `PersistChasers` and `ExtraChaserSlots`.
- Escalation settings that raise the spawn chance and the cap every 100 rooms.
- Spooper and Lisa don't count rooms or despawn until they become a threat.
- Lisa's chase and White Face's second pursuit still end within their normal length, because they stop the room count.
- `SkipIntroRooms` removes specimen intro rooms from Endless floors.
- Endless saves stay valid and you can quit with chasers active (`AllowSaveWithChasers`).
- Scores aren't uploaded to the leaderboards unless `UploadToLeaderboards` is on. Mod spawns don't award the duplicate-chaser achievement.
- F1 window (Configuration Manager): presets, "Apply to all" buttons, and the camera locks while it's open.
- F2 overlay showing active chasers, the cap and the spawn chance.
- Optional debug hotkeys to spawn a chosen chaser (F3) or clear all chasers (F4).
- Chasers never spawn in the Endless starting room.
- `ModEnabled` master switch to turn the whole mod off without uninstalling it.
