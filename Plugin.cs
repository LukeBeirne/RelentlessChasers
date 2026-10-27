using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

[BepInPlugin(Guid, "Relentless Specimens", Version)]
[BepInDependency(ConfigManagerGuid, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    internal const string Guid = "spooky.relentlessspecimens";
    internal const string Version = "0.5.0";
    internal const string ConfigManagerGuid = "com.bepis.bepinex.configurationmanager";

    // Every chaser the game marks rechasable (checked against the game at runtime, see LogChasers).
    internal static readonly string[] KnownChasers =
    {
        "EMClown", "EMHookedDoll", "EMHuskSwarm", "EMKHMonster2", "EMKHMonster3", "EMKHMonster4", "EMKHMonster5",
        "EMKHMonster6", "EMSpecimenFour", "EMUnknownSpecimen1", "EMUnknownSpecimen2", "EMUnknownSpecimen3",
        "EMUnknownSpecimen4", "EMUnknownSpecimen5", "EMWoormyCharles", "SpecimenEight", "SpecimenEleven",
        "SpecimenFive", "SpecimenFour", "SpecimenSix", "SpecimenTen", "SpecimenThirteen", "SpecimenThree",
        "SpecimenTwelve", "SpecimenTwo"
    };

    // Names the community uses (Fandom wiki, Steam Endless guide).
    private static readonly Dictionary<string, string> Nicknames = new Dictionary<string, string>
    {
        { "EMClown", "The Clown" },
        { "EMHookedDoll", "Hooked Doll" },
        { "EMHuskSwarm", "Husk" },
        { "EMKHMonster2", "Body Bag" },
        { "EMKHMonster3", "Baby Face" },
        { "EMKHMonster4", "Hanged Man" },
        { "EMKHMonster5", "Ghost Cow" },
        { "EMKHMonster6", "Bekka" },
        { "EMUnknownSpecimen1", "White Face" },
        { "EMUnknownSpecimen2", "Otto the Otter" },
        { "EMUnknownSpecimen3", "Spooper" },
        { "EMUnknownSpecimen4", "Tirsiak" },
        { "EMUnknownSpecimen5", "Lisa" },
        { "EMWoormyCharles", "Woormy Charles" },
        { "EMSpecimenFour", "Ringu (Endless)" },
        { "SpecimenEight", "Deer Lord" },
        { "SpecimenEleven", "Food Demon" },
        { "SpecimenFive", "Bab" },
        { "SpecimenFour", "Ringu" },
        { "SpecimenSix", "The Merchant" },
        { "SpecimenTen", "Parasite" },
        { "SpecimenThirteen", "Mermaid" },
        { "SpecimenThree", "Centipede" },
        { "SpecimenTwelve", "The Old Man" },
        { "SpecimenTwo", "Gel" }
    };

    internal sealed class ChaserSettings
    {
        public ConfigEntry<float> SpawnChance;
        public ConfigEntry<float> DespawnChance;
        public ConfigEntry<int> MaxRoomsBeforeDespawn;
        public ConfigEntry<int> MaxCopies;
    }

    private sealed class Preset
    {
        public string Name;
        public float SpawnChancePerRoom;
        public int MinRoomsBetweenSpawns;
        public int MinRoomsBeforeDespawn;
        public int ExtraChaserSlots;
        public float EscalationSpawnChance;
        public int EscalationSlots;
        public float DespawnChance;
        public int MaxRoomsBeforeDespawn;
    }

    // Presets leave which chasers can spawn (SpawnChance, MaxCopies, duplicates, met-only) and the other sections alone.
    private static readonly Preset[] Presets =
    {
        new Preset { Name = "Vanilla-ish", SpawnChancePerRoom = 0.125f, MinRoomsBetweenSpawns = 20, MinRoomsBeforeDespawn = 10, ExtraChaserSlots = 0,
            EscalationSpawnChance = 0f, EscalationSlots = 0, DespawnChance = 0.2f, MaxRoomsBeforeDespawn = 19 },
        new Preset { Name = "Relentless", SpawnChancePerRoom = 0.5f, MinRoomsBetweenSpawns = 0, MinRoomsBeforeDespawn = 0, ExtraChaserSlots = 2,
            EscalationSpawnChance = 0f, EscalationSlots = 0, DespawnChance = 0.1f, MaxRoomsBeforeDespawn = 0 },
        new Preset { Name = "Nightmare", SpawnChancePerRoom = 0.75f, MinRoomsBetweenSpawns = 0, MinRoomsBeforeDespawn = 10, ExtraChaserSlots = 5,
            EscalationSpawnChance = 0.05f, EscalationSlots = 1, DespawnChance = 0.05f, MaxRoomsBeforeDespawn = 0 },
    };

    internal static Plugin Instance;
    internal static ManualLogSource Log;
    internal static ConfigEntry<bool> EndlessOnly;
    internal static ConfigEntry<bool> PersistChasers;
    internal static ConfigEntry<int> ExtraChaserSlots;
    internal static ConfigEntry<float> SpawnChancePerRoom;
    internal static ConfigEntry<int> MinRoomsBeforeDespawn;
    internal static ConfigEntry<int> MinRoomsBetweenSpawns;
    internal static ConfigEntry<bool> AllowDuplicateChasers;
    internal static ConfigEntry<bool> OnlySpawnMetSpecimens;
    internal static ConfigEntry<float> EscalationSpawnChance;
    internal static ConfigEntry<int> EscalationSlots;
    internal static ConfigEntry<bool> AllowSaveWithChasers;
    internal static ConfigEntry<bool> SkipIntroRooms;
    internal static ConfigEntry<bool> UploadToLeaderboards;
    internal static ConfigEntry<KeyboardShortcut> ShowChasersKey;
    internal static ConfigEntry<bool> EnableDebugHotkeys;
    internal static ConfigEntry<string> DebugChaserToSpawn;
    internal static ConfigEntry<KeyboardShortcut> DebugSpawnKey;
    internal static ConfigEntry<KeyboardShortcut> DebugClearKey;

    private static readonly Dictionary<string, ChaserSettings> Chasers = new Dictionary<string, ChaserSettings>();
    private static bool loggedChasers;

    // Rooms loaded since the mod last spawned a chaser. Starts high so the first spawn of a run isn't held back.
    internal static int RoomsSinceSpawn = NoRecentSpawn;
    internal const int NoRecentSpawn = int.MaxValue / 2;

    // Set while the mod itself starts a chaser, so the duplicate-chaser achievement isn't handed out for it.
    internal static bool SpawningFromMod;

    private bool showOverlay;
    private string toast;
    private float toastUntil;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        EndlessOnly = Config.Bind("General", "EndlessOnly", true,
            "Only apply the mod's changes in Endless mode. The main game is left untouched.");

        PersistChasers = Config.Bind("Chasers", "PersistChasers", false,
            "Override: chasers never despawn, ignoring every chaser's despawn chance and the minimum and maximum rooms settings. Exception: Lisa's chase and White Face's second pursuit stop the room count, so they still end after their normal length.");
        SpawnChancePerRoom = Config.Bind("Chasers", "SpawnChancePerRoom", 0.5f,
            new ConfigDescription("Chance each room that a new chaser joins, while you are under the chaser cap. When it triggers, which chaser joins is picked using each chaser's own SpawnChance as a weighting.",
                new AcceptableValueRange<float>(0f, 1f)));
        MinRoomsBetweenSpawns = Config.Bind("Chasers", "MinRoomsBetweenSpawns", 0,
            new ConfigDescription("After the mod spawns a chaser, it waits at least this many rooms before it can spawn another. 0 = no wait.",
                new AcceptableValueRange<int>(0, 100),
                new ConfigurationManagerAttributes { ShowRangeAsPercent = false }));
        MinRoomsBeforeDespawn = Config.Bind("Chasers", "MinRoomsBeforeDespawn", 0,
            new ConfigDescription("Applies to every chaser. A chaser always stays for at least this many rooms after it spawns. After that, each room it rolls its own despawn chance. Spooper and Lisa don't count rooms or despawn until they can actually hurt you.",
                new AcceptableValueRange<int>(0, 100),
                new ConfigurationManagerAttributes { ShowRangeAsPercent = false }));
        AllowDuplicateChasers = Config.Bind("Chasers", "AllowDuplicateChasers", true,
            "Allow the same chaser to be active more than once at the same time. When on, each chaser's MaxCopies sets the limit.");
        OnlySpawnMetSpecimens = Config.Bind("Chasers", "OnlySpawnMetSpecimens", true,
            "Only spawn chasers already in your encyclopedia (CAT-DOS), like the game's own re-chases. Turning this off lets the mod spawn specimens you haven't met, which unlocks their entries and counts towards the Specimen Researcher achievement. With SkipIntroRooms also on, you won't meet new specimens at all.");
        ExtraChaserSlots = Config.Bind("Chasers", "ExtraChaserSlots", 2,
            new ConfigDescription("Added to the game's chaser cap so new chasers can keep joining after earlier ones stay. The game's default cap in Endless is 1 chaser per 100 rooms (rooms 1-100: 0, 101-200: 1, 201-300: 2, and so on up to a maximum of 5 from room 501). If you skip ahead at the start of a run, the base cap is instead the number of chasers you picked on the skip screen. The maximum is 100. Nobody should set it to 100. The specimens' union has been notified.",
                new AcceptableValueRange<int>(0, 100),
                new ConfigurationManagerAttributes { ShowRangeAsPercent = false }));

        EscalationSpawnChance = Config.Bind("Escalation", "SpawnChancePer100Rooms", 0f,
            new ConfigDescription("Added to SpawnChancePerRoom for every 100 rooms you've reached (room 250 counts as 2). The total is capped at 100%. 0 = off.",
                new AcceptableValueRange<float>(0f, 1f)));
        EscalationSlots = Config.Bind("Escalation", "ExtraSlotsPer100Rooms", 0,
            new ConfigDescription("Added to ExtraChaserSlots for every 100 rooms you've reached (room 250 counts as 2). 0 = off.",
                new AcceptableValueRange<int>(0, 10)));

        AllowSaveWithChasers = Config.Bind("Endless", "AllowSaveWithChasers", true,
            "Lets you exit to the menu while a chaser is active and stops the game from invalidating the Endless save. Active chasers are not saved, so they will be gone when you resume.");
        SkipIntroRooms = Config.Bind("Endless", "SkipIntroRooms", false,
            "Removes the specimen intro rooms from Endless floors. Those rooms clear your active chasers and start a guaranteed chase with a specimen you haven't met yet. Takes effect from the next floor (every 100 rooms) or when you load a save.");
        UploadToLeaderboards = Config.Bind("Endless", "UploadToLeaderboards", false,
            "Send Endless scores to the Steam leaderboards while this mod is installed. Off by default, because modded runs aren't comparable with unmodded ones.");

        ShowChasersKey = Config.Bind("Overlay", "ShowChasersKey", new KeyboardShortcut(KeyCode.F2),
            "Shows or hides a small overlay listing the active chasers, how long each has stayed, the chaser cap and the current spawn chance.");

        EnableDebugHotkeys = Config.Bind("Debug", "EnableDebugHotkeys", false,
            new ConfigDescription("Turns on the debug hotkeys in this section (SpawnChaserKey and ClearChasersKey), for testing settings.",
                null,
                new ConfigurationManagerAttributes { Order = 100 }));
        DebugChaserToSpawn = Config.Bind("Debug", "ChaserToSpawn", "SpecimenTwo",
            new ConfigDescription("Which chaser the spawn hotkey spawns. It respects OnlySpawnMetSpecimens.",
                new AcceptableValueList<string>(KnownChasers)));
        DebugSpawnKey = Config.Bind("Debug", "SpawnChaserKey", new KeyboardShortcut(KeyCode.F3),
            "Spawns ChaserToSpawn in the current room. It ignores the cap.");
        DebugClearKey = Config.Bind("Debug", "ClearChasersKey", new KeyboardShortcut(KeyCode.F4),
            "Removes every active chaser.");

        BindPresets();
        BindBulkEdit("SetAllSpawnChance", "Set all chasers' spawn chance", "SpawnChance", 0.1f, 0f, 1f, false, s => s.SpawnChance);
        BindBulkEdit("SetAllDespawnChance", "Set all chasers' despawn chance", "DespawnChance", 0.1f, 0f, 1f, false, s => s.DespawnChance);
        BindBulkEdit("SetAllMaxRoomsBeforeDespawn", "Set all chasers' max rooms", "MaxRoomsBeforeDespawn", 0, 0, 100, true, s => s.MaxRoomsBeforeDespawn);
        BindBulkEdit("SetAllMaxCopies", "Set all chasers' max copies", "MaxCopies", 0, 0, 20, true, s => s.MaxCopies);

        foreach (string id in KnownChasers)
        {
            SettingsFor(id);
        }

        new Harmony(Guid).PatchAll();

        if (Chainloader.PluginInfos.ContainsKey(ConfigManagerGuid))
        {
            ConfigWindowLock.Hook(Chainloader.PluginInfos[ConfigManagerGuid].Instance);
        }
        Logger.LogInfo("Relentless Specimens " + Version + " loaded!");
    }

    private void Update()
    {
        try
        {
            if (ShowChasersKey.Value.IsDown())
            {
                showOverlay = !showOverlay;
            }
            if (EnableDebugHotkeys.Value)
            {
                if (DebugSpawnKey.Value.IsDown())
                {
                    DebugSpawn();
                }
                if (DebugClearKey.Value.IsDown())
                {
                    DebugClear();
                }
            }
        }
        catch (Exception e)
        {
            Log.LogError("Hotkey failed: " + e);
        }
    }

    private void OnGUI()
    {
        if (toast != null && Time.unscaledTime < toastUntil)
        {
            Rect box = new Rect(Screen.width / 2f - 220f, Screen.height - 70f, 440f, 30f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 10f, box.y + 5f, box.width - 20f, 22f), toast);
        }
        if (!showOverlay)
        {
            return;
        }
        List<string> lines;
        try
        {
            lines = OverlayLines();
        }
        catch (Exception e)
        {
            lines = new List<string> { "Relentless Specimens", "Overlay error: " + e.Message };
        }
        const float lineHeight = 20f;
        Rect rect = new Rect(10f, 10f, 440f, 10f + lineHeight * lines.Count);
        GUI.Box(rect, GUIContent.none);
        GUI.Box(rect, GUIContent.none);
        for (int i = 0; i < lines.Count; i++)
        {
            GUI.Label(new Rect(rect.x + 8f, rect.y + 5f + lineHeight * i, rect.width - 16f, lineHeight), lines[i]);
        }
    }

    private List<string> OverlayLines()
    {
        List<string> lines = new List<string> { "Relentless Specimens" + (Active() ? "" : " (inactive here)") };
        SpecimenManager mgr = ServiceLocator.GetService<SpecimenManager>();
        HSMGameManager game = ServiceLocator.GetService<HSMGameManager>();
        HSMFloorState floor = game != null ? game.ClosestState<HSMFloorState>() : null;
        HSMMansionState mansion = game != null ? game.ClosestState<HSMMansionState>() : null;
        if (mgr == null || floor == null || mansion == null)
        {
            lines.Add("Not in a run.");
            return lines;
        }

        lines.Add("Room " + mansion.currentData.GetKey("room") + "   Chasers " + mgr.activeSpecimens.Count + "/" + floor.MaxSpecs());
        string spawnLine = "Spawn chance " + Mathf.RoundToInt(CurrentSpawnChance() * 100f) + "% per room";
        int wait = MinRoomsBetweenSpawns.Value - RoomsSinceSpawn;
        if (wait >= 0)
        {
            spawnLine += ", none for " + (wait + 1) + " more room" + (wait == 0 ? "" : "s");
        }
        lines.Add(spawnLine);

        foreach (GameObject go in mgr.activeSpecimens)
        {
            if (go == null)
            {
                continue;
            }
            SpecimenInterface spec = go.GetComponent<SpecimenInterface>();
            string id = spec.spawnedFromSpecId;
            if (!IsChaser(mgr, id))
            {
                lines.Add("- " + DisplayName(id) + ": scripted (the game's rules apply)");
                continue;
            }
            string status;
            if (IsFreezingRoomCount(spec))
            {
                status = "room count stopped, leaves within " + Math.Max(spec.hp, 0) + " rooms";
            }
            else if (!IsThreat(spec))
            {
                status = "warming up (not counting yet)";
            }
            else
            {
                status = RoomCounter.Peek(go) + " rooms";
                int max = SettingsFor(id).MaxRoomsBeforeDespawn.Value;
                if (PersistChasers.Value)
                {
                    status += " (persistent)";
                }
                else if (max > 0)
                {
                    status += " (max " + max + ")";
                }
            }
            lines.Add("- " + DisplayName(id) + ": " + status);
        }
        return lines;
    }

    private void Toast(string message)
    {
        toast = message;
        toastUntil = Time.unscaledTime + 3f;
        Log.LogInfo(message);
    }

    private void DebugSpawn()
    {
        string id = DebugChaserToSpawn.Value;
        SpecimenManager mgr = ServiceLocator.GetService<SpecimenManager>();
        HSMGameManager game = ServiceLocator.GetService<HSMGameManager>();
        HSMSystemsLoadState loadState = game != null ? game.ClosestState<HSMSystemsLoadState>() : null;
        if (mgr == null || loadState == null || loadState.roomRuntimeData == null)
        {
            Toast("Debug spawn: you need to be in a room.");
            return;
        }
        if (!Active())
        {
            Toast("Debug spawn: the mod is inactive here (EndlessOnly is on).");
            return;
        }
        if (ServiceLocator.GetService<MansionManager>().isPaused)
        {
            Toast("Debug spawn: unpause first.");
            return;
        }
        if (OnlySpawnMetSpecimens.Value && !IsMet(id))
        {
            Toast("Debug spawn: you haven't met " + DisplayName(id) + " yet (OnlySpawnMetSpecimens is on).");
            return;
        }
        GameObject go = StartChaser(mgr, id);
        if (go == null)
        {
            Toast("Debug spawn: the game refused to spawn " + DisplayName(id) + " (is the Specimens option off?).");
            return;
        }
        // Same as the game's own mid-room spawns: start it from the door you came in through.
        if (loadState.roomRuntimeData.playerSpawn != null)
        {
            go.GetComponent<SpecimenInterface>().StartRoom(loadState.roomRuntimeData.playerSpawn);
        }
        mgr.UpdateCurrentAllowedRechasable();
        Toast("Debug spawn: " + DisplayName(id) + ".");
    }

    private void DebugClear()
    {
        SpecimenManager mgr = ServiceLocator.GetService<SpecimenManager>();
        if (mgr == null || mgr.activeSpecimens == null)
        {
            return;
        }
        List<GameObject> chasers = new List<GameObject>();
        foreach (GameObject go in mgr.activeSpecimens)
        {
            if (go != null && IsChaser(mgr, go.GetComponent<SpecimenInterface>().spawnedFromSpecId))
            {
                chasers.Add(go);
            }
        }
        foreach (GameObject go in chasers)
        {
            mgr.EndSpecimen(go);
        }
        Toast("Debug clear: removed " + chasers.Count + " chaser" + (chasers.Count == 1 ? "" : "s") + ".");
    }

    // One row of buttons in the Configuration Manager window that sets many settings at once.
    private static void BindPresets()
    {
        string status = "";
        Action<ConfigEntryBase> drawer = _ =>
        {
            GUILayout.BeginHorizontal();
            foreach (Preset preset in Presets)
            {
                if (GUILayout.Button(preset.Name, GUILayout.ExpandWidth(false)))
                {
                    ApplyPreset(preset);
                    status = "Done";
                }
            }
            if (status.Length > 0)
            {
                GUILayout.Label(status, GUILayout.ExpandWidth(false));
            }
            GUILayout.EndHorizontal();
        };

        Instance.Config.Bind("Bulk edit", "Presets", "",
            new ConfigDescription("Sets the spawn, despawn, cap and escalation settings in one go. Vanilla-ish roughly matches the base game's pacing, Relentless is the mod's defaults and Nightmare is much harder. Presets don't change which chasers can spawn (SpawnChance, MaxCopies, AllowDuplicateChasers, OnlySpawnMetSpecimens) or any other section. Only works from the F1 window (Configuration Manager).",
                null,
                new ConfigurationManagerAttributes { CustomDrawer = drawer, HideDefaultButton = true, Order = 100 }));
    }

    private static void ApplyPreset(Preset preset)
    {
        Instance.Config.SaveOnConfigSet = false;
        try
        {
            PersistChasers.Value = false;
            SpawnChancePerRoom.Value = preset.SpawnChancePerRoom;
            MinRoomsBetweenSpawns.Value = preset.MinRoomsBetweenSpawns;
            MinRoomsBeforeDespawn.Value = preset.MinRoomsBeforeDespawn;
            ExtraChaserSlots.Value = preset.ExtraChaserSlots;
            EscalationSpawnChance.Value = preset.EscalationSpawnChance;
            EscalationSlots.Value = preset.EscalationSlots;
            foreach (ChaserSettings settings in Chasers.Values)
            {
                settings.DespawnChance.Value = preset.DespawnChance;
                settings.MaxRoomsBeforeDespawn.Value = preset.MaxRoomsBeforeDespawn;
            }
        }
        finally
        {
            Instance.Config.SaveOnConfigSet = true;
            Instance.Config.Save();
        }
        Log.LogInfo("Applied the " + preset.Name + " preset.");
    }

    // A slider plus an Apply button in the Configuration Manager window that overwrites one setting on every chaser.
    private static void BindBulkEdit<T>(string key, string displayName, string settingName, T defaultValue, T min, T max, bool whole,
        Func<ChaserSettings, ConfigEntry<T>> target) where T : IComparable
    {
        float minF = Convert.ToSingle(min);
        float maxF = Convert.ToSingle(max);
        string status = "";
        Action<ConfigEntryBase> drawer = entryBase =>
        {
            ConfigEntry<T> entry = (ConfigEntry<T>)entryBase;
            float current = Convert.ToSingle(entry.Value);
            GUILayout.BeginHorizontal();
            float slid = GUILayout.HorizontalSlider(current, minF, maxF, GUILayout.ExpandWidth(true));
            float rounded = whole ? Mathf.Round(slid) : Mathf.Round(slid * 100f) / 100f;
            if (rounded != current)
            {
                entry.Value = (T)Convert.ChangeType(rounded, typeof(T));
                status = "";
            }
            GUILayout.Label(rounded.ToString(whole ? "0" : "0.00"), GUILayout.Width(40f));
            if (GUILayout.Button("Apply to all", GUILayout.ExpandWidth(false)))
            {
                ApplyToAll(target, entry.Value, settingName);
                status = "Done";
            }
            if (status.Length > 0)
            {
                GUILayout.Label(status, GUILayout.ExpandWidth(false));
            }
            GUILayout.EndHorizontal();
        };

        Instance.Config.Bind("Bulk edit", key, defaultValue,
            new ConfigDescription("Pick a value, then press Apply to all to overwrite every chaser's " + settingName + " with it. Only works from the F1 window (Configuration Manager).",
                new AcceptableValueRange<T>(min, max),
                new ConfigurationManagerAttributes { CustomDrawer = drawer, HideDefaultButton = true, DispName = displayName }));
    }

    private static void ApplyToAll<T>(Func<ChaserSettings, ConfigEntry<T>> target, T value, string settingName)
    {
        Instance.Config.SaveOnConfigSet = false;
        try
        {
            foreach (ChaserSettings settings in Chasers.Values)
            {
                target(settings).Value = value;
            }
        }
        finally
        {
            Instance.Config.SaveOnConfigSet = true;
            Instance.Config.Save();
        }
        Log.LogInfo("Set " + settingName + " on all " + Chasers.Count + " chasers to " + value + ".");
    }

    internal static string DisplayName(string id)
    {
        string nickname;
        return id != null && Nicknames.TryGetValue(id, out nickname) ? nickname : id;
    }

    // Per-chaser settings. Chasers the game adds later that aren't in KnownChasers get settings on first use.
    internal static ChaserSettings SettingsFor(string id)
    {
        ChaserSettings settings;
        if (Chasers.TryGetValue(id, out settings))
        {
            return settings;
        }
        string nickname;
        string section = Nicknames.TryGetValue(id, out nickname) ? id + " (" + nickname + ")" : id;
        settings = new ChaserSettings
        {
            SpawnChance = Instance.Config.Bind(section, "SpawnChance", 0.1f,
                new ConfigDescription("Weighting for how likely this chaser is picked when a new chaser joins, compared with the other chasers' values. 0 = never spawns.",
                    new AcceptableValueRange<float>(0f, 1f))),
            DespawnChance = Instance.Config.Bind(section, "DespawnChance", 0.1f,
                new ConfigDescription("Chance this chaser despawns at the end of each room once it has stayed the minimum number of rooms. 0 = never despawns, 1 = always despawns.",
                    new AcceptableValueRange<float>(0f, 1f))),
            MaxRoomsBeforeDespawn = Instance.Config.Bind(section, "MaxRoomsBeforeDespawn", 0,
                new ConfigDescription("This chaser always leaves at the end of this many rooms, counted the same way as MinRoomsBeforeDespawn. 0 = no limit. If it's lower than MinRoomsBeforeDespawn, this wins. Ignored when PersistChasers is on.",
                    new AcceptableValueRange<int>(0, 100),
                    new ConfigurationManagerAttributes { ShowRangeAsPercent = false })),
            MaxCopies = Instance.Config.Bind(section, "MaxCopies", 0,
                new ConfigDescription("The most copies of this chaser that can be active at once. 0 = no limit. Turning AllowDuplicateChasers off limits every chaser to 1.",
                    new AcceptableValueRange<int>(0, 20)))
        };
        Chasers[id] = settings;
        return settings;
    }

    internal static bool Roll(float chance)
    {
        return chance >= 1f || (chance > 0f && UnityEngine.Random.value < chance);
    }

    internal static HSMMansionState MansionState()
    {
        try
        {
            HSMGameManager game = ServiceLocator.GetService<HSMGameManager>();
            return game != null ? game.ClosestState<HSMMansionState>() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static bool IsEndless()
    {
        HSMMansionState state = MansionState();
        return state != null && state.mansion != null && state.mansion.isEndless;
    }

    internal static bool Active()
    {
        return !EndlessOnly.Value || IsEndless();
    }

    internal static int HundredsOfRooms()
    {
        HSMMansionState state = MansionState();
        if (state == null || state.currentData == null)
        {
            return 0;
        }
        return Math.Max(0, state.currentData.GetKey("room") / 100);
    }

    internal static float CurrentSpawnChance()
    {
        return Mathf.Clamp01(SpawnChancePerRoom.Value + EscalationSpawnChance.Value * HundredsOfRooms());
    }

    internal static int CurrentExtraSlots()
    {
        return ExtraChaserSlots.Value + EscalationSlots.Value * HundredsOfRooms();
    }

    internal static bool IsChaser(SpecimenManager mgr, string id)
    {
        return mgr.allRechasableSpecimen != null && id != null && mgr.allRechasableSpecimen.Contains(id);
    }

    // "Met" means in the global encyclopedia (CAT-DOS), the same list the Specimen Researcher achievement checks.
    internal static bool IsMet(string id)
    {
        PersistenceManager persistence = PersistenceManager.GetInstance();
        List<string> met = persistence != null && persistence.globalData != null ? persistence.globalData.catdosSpecimens : null;
        return met != null && met.Contains(id);
    }

    // The Endless starting room (axe pickup) has no spawn doors and doesn't set noSpecimenSpawnAllowed.
    internal static bool IsStartRoom(RoomConfig room)
    {
        if (room.sceneName == "EM_Start")
        {
            return true;
        }
        HSMMansionState state = MansionState();
        if (state == null || state.mansion == null || !state.mansion.isEndless)
        {
            return false;
        }
        string start = state.mansion.endlessData.startRoom;
        if (string.IsNullOrEmpty(start))
        {
            return false;
        }
        int colon = start.IndexOf(':');
        if (colon >= 0)
        {
            start = start.Substring(0, colon);
        }
        return room.uniqueRoomId == start || room.sceneName == start;
    }

    internal static GameObject StartChaser(SpecimenManager mgr, string id)
    {
        SpawningFromMod = true;
        try
        {
            return mgr.StartSpecimen(id, ServiceLocator.GetInstance().transform, rechase: true);
        }
        finally
        {
            SpawningFromMod = false;
        }
    }

    // Some chasers announce themselves for a while before they can hurt you. They don't despawn or count rooms until they can.
    internal static bool IsThreat(SpecimenInterface spec)
    {
        UnknownSpecimen3Controller spooper = spec as UnknownSpecimen3Controller;
        if (spooper != null)
        {
            return spooper.emergenceCounter <= 0;
        }
        UnknownSpecimen5Controller lisa = spec as UnknownSpecimen5Controller;
        if (lisa != null)
        {
            return lisa.state == UnknownSpecimen5Controller.State.Chasing;
        }
        return true;
    }

    // Lisa's chase and White Face's second pursuit stop the room count (and floor progress) until they leave.
    internal static bool IsFreezingRoomCount(SpecimenInterface spec)
    {
        UnknownSpecimen5Controller lisa = spec as UnknownSpecimen5Controller;
        if (lisa != null)
        {
            return Traverse.Create(lisa).Field("didStopRooms").GetValue<bool>();
        }
        UnknownSpecimen1Controller whiteFace = spec as UnknownSpecimen1Controller;
        if (whiteFace != null)
        {
            return Traverse.Create(whiteFace).Field("overrodeRoomCount").GetValue<bool>();
        }
        return false;
    }

    // Also checks the hardcoded chaser list against the game, so a game update that renames a specimen shows up in the log.
    internal static void LogChasers(SpecimenManager mgr)
    {
        if (loggedChasers || mgr.allRechasableSpecimen == null)
        {
            return;
        }
        loggedChasers = true;
        List<string> notRechasable = new List<string>();
        foreach (SpecimenConfig config in AssetDataManager.GetInstance().specConfigs.Values)
        {
            if (!mgr.allRechasableSpecimen.Contains(config.uniqueId))
            {
                notRechasable.Add(config.uniqueId);
            }
        }
        Log.LogInfo("Chasers with settings: " + string.Join(", ", mgr.allRechasableSpecimen.ToArray()));
        Log.LogInfo("Not chasers (the game's own despawn rules still apply): " + string.Join(", ", notRechasable.ToArray()));
        foreach (string id in KnownChasers)
        {
            if (!mgr.allRechasableSpecimen.Contains(id))
            {
                Log.LogWarning(id + " is in the mod's chaser list but the game no longer has it as a chaser. Its settings do nothing. The game may have been updated.");
            }
        }
        foreach (string id in mgr.allRechasableSpecimen)
        {
            if (Array.IndexOf(KnownChasers, id) < 0)
            {
                Log.LogWarning(id + " is a chaser the mod doesn't know about. It gets settings when it first spawns, but no nickname. The game may have been updated.");
            }
        }
    }
}

// Configuration Manager finds this by its name and field names, so the mod needs no reference to it to use a custom drawer.
internal sealed class ConfigurationManagerAttributes
{
    public Action<ConfigEntryBase> CustomDrawer;
    public bool? HideDefaultButton;
    public string DispName;
    public bool? ShowRangeAsPercent;
    public int? Order;
}

// Keeps the camera (and attacks) still while the Configuration Manager window is open, using the game's own look lock.
internal static class ConfigWindowLock
{
    private static SpookyCharacterController locked;

    // Kept in its own class so the mod still loads if Configuration Manager isn't installed.
    internal static void Hook(BaseUnityPlugin configManager)
    {
        ((ConfigurationManager.ConfigurationManager)configManager).DisplayingWindowChanged += OnWindowChanged;
    }

    private static void OnWindowChanged(object sender, ConfigurationManager.ValueChangedEventArgs<bool> e)
    {
        try
        {
            if (e.NewValue)
            {
                SpookyCharacterController controller = SpookyCharacterController.GetInstance();
                if (controller != null && locked == null)
                {
                    controller.AddLookLock();
                    locked = controller;
                }
            }
            else if (locked != null)
            {
                // The game resets its lock counters on some transitions, so only release a lock that is still there.
                if (locked && Traverse.Create(locked).Field("lookLocked").GetValue<int>() > 0)
                {
                    locked.DecLookLock();
                }
                locked = null;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError("ConfigWindowLock failed: " + ex);
            locked = null;
        }
    }
}

internal static class RoomCounter
{
    private sealed class Count
    {
        public int Rooms;
    }

    private static readonly ConditionalWeakTable<GameObject, Count> Counts = new ConditionalWeakTable<GameObject, Count>();

    // Number of rooms this chaser has now been through.
    internal static int Increment(GameObject chaser)
    {
        Count count = Counts.GetOrCreateValue(chaser);
        count.Rooms++;
        return count.Rooms;
    }

    internal static int Peek(GameObject chaser)
    {
        Count count;
        return Counts.TryGetValue(chaser, out count) ? count.Rooms : 0;
    }
}

[HarmonyPatch(typeof(SpecimenManager), nameof(SpecimenManager.EndRoom))]
internal static class DespawnPatch
{
    // Same as the original EndRoom, except chasers despawn by their own chance instead of the game's hp countdown.
    private static bool Prefix(SpecimenManager __instance)
    {
        if (!Plugin.Active())
        {
            return true;
        }
        try
        {
            __instance.specimenAccessories.Clear();
            List<GameObject> toRemove = new List<GameObject>();
            foreach (GameObject go in __instance.activeSpecimens)
            {
                SpecimenInterface spec = go.GetComponent<SpecimenInterface>();
                spec.EndRoom();
                string id = spec.spawnedFromSpecId;
                if (!Plugin.IsChaser(__instance, id))
                {
                    if (spec.shouldRemove)
                    {
                        toRemove.Add(go);
                    }
                    continue;
                }
                if (Plugin.IsFreezingRoomCount(spec))
                {
                    // Keep the game's own countdown for this phase so the room count and floor can't stall forever.
                    if (spec.shouldRemove)
                    {
                        Plugin.Log.LogInfo("Despawning " + id + ": its room-freezing phase ran its normal length.");
                        toRemove.Add(go);
                        continue;
                    }
                }
                else
                {
                    spec.hp = Math.Max(spec.hp, 1);
                }
                if (!Plugin.IsThreat(spec))
                {
                    continue;
                }
                int rooms = RoomCounter.Increment(go);
                if (Plugin.PersistChasers.Value)
                {
                    continue;
                }
                Plugin.ChaserSettings settings = Plugin.SettingsFor(id);
                int max = settings.MaxRoomsBeforeDespawn.Value;
                if (max > 0 && rooms >= max)
                {
                    Plugin.Log.LogInfo("Despawning " + id + " after " + rooms + " rooms (its maximum).");
                    toRemove.Add(go);
                    continue;
                }
                if (rooms <= Plugin.MinRoomsBeforeDespawn.Value)
                {
                    continue;
                }
                if (Plugin.Roll(settings.DespawnChance.Value))
                {
                    Plugin.Log.LogInfo("Despawning " + id + " after " + rooms + " rooms.");
                    toRemove.Add(go);
                }
            }
            foreach (GameObject go in toRemove)
            {
                __instance.EndSpecimen(go);
            }
            float health = SpookyCharacterController.GetInstance().currentHealth.currentValue;
            if (toRemove.Count > 0 && health > 0f && health <= 15f && __instance.activeSpecimens.Count == 0)
            {
                AchievementManager.GetInstance().GrantAchievement(AchievementManager.AchievementId.NEAR_DEATH_EXPERIENCE);
            }
            return false;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("DespawnPatch failed, falling back to the game's EndRoom: " + e);
            return true;
        }
    }
}

[HarmonyPatch(typeof(HSMFloorState), nameof(HSMFloorState.MaxSpecs))]
internal static class ChaserCapPatch
{
    private static void Postfix(ref int __result)
    {
        if (Plugin.Active())
        {
            __result += Plugin.CurrentExtraSlots();
        }
    }
}

// The game's own random chaser pick (after 20+ rooms, then 1 in 8 per room) is replaced by SpawnPatch below.
[HarmonyPatch(typeof(SpecimenManager), nameof(SpecimenManager.StartSpecimenRechase))]
internal static class DisableGameRechasePatch
{
    private static bool Prefix()
    {
        return !Plugin.Active();
    }
}

[HarmonyPatch(typeof(SpecimenManager), nameof(SpecimenManager.StartWithMansion))]
internal static class ResetPerRunStatePatch
{
    private static void Postfix()
    {
        Plugin.RoomsSinceSpawn = Plugin.NoRecentSpawn;
    }
}

[HarmonyPatch(typeof(SpecimenManager), "RoomLoadedTask")]
internal static class SpawnPatch
{
    // Runs before the rest of the game's room-load logic, so a chaser added here gets RoomLoaded() and StartRoom() like any other.
    private static void Prefix(SpecimenManager __instance, HSMSystemsLoadState loadState)
    {
        try
        {
            Plugin.LogChasers(__instance);
            if (!Plugin.Active() || __instance.allRechasableSpecimen == null)
            {
                return;
            }
            Plugin.RoomsSinceSpawn++;
            RoomConfig room = loadState.roomRuntimeData.roomConfig;
            if (room.noSpecimenSpawnAllowed || Plugin.IsStartRoom(room))
            {
                return;
            }
            // Chasers picked on the Endless selection screen are spawned by the game right after this; let them go first.
            if (__instance.enqueuedSpecimen != null && __instance.enqueuedSpecimen.Count > 0)
            {
                return;
            }
            if (Plugin.RoomsSinceSpawn <= Plugin.MinRoomsBetweenSpawns.Value)
            {
                return;
            }
            int cap = loadState.FindInParent<HSMFloorState>().MaxSpecs();
            if (__instance.activeSpecimens.Count >= cap)
            {
                return;
            }
            if (!Plugin.Roll(Plugin.CurrentSpawnChance()))
            {
                return;
            }

            List<string> candidates = new List<string>();
            List<float> weights = new List<float>();
            float total = 0f;
            foreach (string id in __instance.GetCurrentSpawnables(__instance.allRechasableSpecimen))
            {
                Plugin.ChaserSettings settings = Plugin.SettingsFor(id);
                int active = __instance.ActiveSpecimenCount(id);
                if (active > 0 && !Plugin.AllowDuplicateChasers.Value)
                {
                    continue;
                }
                if (settings.MaxCopies.Value > 0 && active >= settings.MaxCopies.Value)
                {
                    continue;
                }
                if (Plugin.OnlySpawnMetSpecimens.Value && !Plugin.IsMet(id))
                {
                    continue;
                }
                float weight = settings.SpawnChance.Value;
                if (weight <= 0f)
                {
                    continue;
                }
                candidates.Add(id);
                weights.Add(weight);
                total += weight;
            }
            if (candidates.Count == 0)
            {
                return;
            }

            float pick = UnityEngine.Random.value * total;
            int chosen = candidates.Count - 1;
            for (int i = 0; i < candidates.Count; i++)
            {
                pick -= weights[i];
                if (pick <= 0f)
                {
                    chosen = i;
                    break;
                }
            }

            string chaser = candidates[chosen];
            if (Plugin.StartChaser(__instance, chaser) != null)
            {
                Plugin.RoomsSinceSpawn = 0;
                __instance.UpdateCurrentAllowedRechasable();
                Plugin.Log.LogInfo("Spawned " + chaser + " (" + __instance.activeSpecimens.Count + "/" + cap + " chasers).");
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError("SpawnPatch failed: " + e);
        }
    }
}

[HarmonyPatch(typeof(EndlessFloorGenerator), nameof(EndlessFloorGenerator.MakeFloorFor))]
internal static class SkipIntroRoomsPatch
{
    // Each generated floor gets at most one intro series. Dropping it leaves the start room and everything else alone.
    private static void Postfix(FloorConfig __result)
    {
        // Only Endless generates floors this way, so no Active() check is needed (it could also misread the game state mid-generation).
        if (!Plugin.SkipIntroRooms.Value || __result == null)
        {
            return;
        }
        if (__result.tempRoomSeries != null && __result.tempRoomSeries.Length > 0)
        {
            Plugin.Log.LogInfo("Removed the intro room from this floor.");
            __result.tempRoomSeries = new FloorConfig.TempRoomSeries[0];
        }
    }
}

// The mod changes how hard Endless is, so scores aren't uploaded unless the player opts in.
[HarmonyPatch(typeof(MansionManager), nameof(MansionManager.MaybeWriteLeaderboard))]
internal static class LeaderboardPatch
{
    private static bool Prefix(MansionConfig mansion, bool forceEndless)
    {
        if (Plugin.UploadToLeaderboards.Value)
        {
            return true;
        }
        if ((mansion != null && mansion.isEndless) || forceEndless)
        {
            Plugin.Log.LogInfo("Skipped the leaderboard upload (UploadToLeaderboards is off).");
        }
        return false;
    }
}

// Duplicates are far more common with the mod, so its own spawns don't award the duplicate-chaser achievement.
[HarmonyPatch(typeof(AchievementManager), nameof(AchievementManager.GrantAchievement))]
internal static class CloneWarsPatch
{
    private static bool Prefix(AchievementManager.AchievementId id)
    {
        return !(Plugin.SpawningFromMod && id == AchievementManager.AchievementId.CLONE_WARS);
    }
}

[HarmonyPatch(typeof(PersistenceManager), nameof(PersistenceManager.SetInvalidFlagForId))]
internal static class KeepEndlessSavePatch
{
    // The game flags the Endless save as invalid whenever a chaser starts, which makes it delete the save on next load.
    private static bool Prefix(bool newValue)
    {
        if (newValue && Plugin.AllowSaveWithChasers.Value && Plugin.IsEndless())
        {
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(PauseMenuComponent), "Start")]
internal static class AllowExitWithChasersPatch
{
    private static void Prefix(PauseMenuComponent __instance, out LocalizedText __state)
    {
        __state = __instance.exitToMenuText.localizedText;
    }

    private static void Postfix(PauseMenuComponent __instance, ref bool ___noExitActive, LocalizedText __state)
    {
        if (___noExitActive && Plugin.AllowSaveWithChasers.Value && Plugin.IsEndless())
        {
            ___noExitActive = false;
            __instance.exitToMenuText.localizedText = __state;
            __instance.exitToMenuText.UpdateLocalizedText();
        }
    }
}
