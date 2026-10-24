using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("spooky.relentlesschasers", "Relentless Chasers", "2.0.0")]
[BepInDependency(ConfigManagerGuid, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    internal const string ConfigManagerGuid = "com.bepis.bepinex.configurationmanager";

    // Every chaser the game marks rechasable, and the display name the game gives it (if any).
    private static readonly string[] KnownChasers =
    {
        "EMClown", "EMHookedDoll", "EMHuskSwarm", "EMKHMonster2", "EMKHMonster3", "EMKHMonster4", "EMKHMonster5",
        "EMKHMonster6", "EMSpecimenFour", "EMUnknownSpecimen1", "EMUnknownSpecimen2", "EMUnknownSpecimen3",
        "EMUnknownSpecimen4", "EMUnknownSpecimen5", "EMWoormyCharles", "SpecimenEight", "SpecimenEleven",
        "SpecimenFive", "SpecimenFour", "SpecimenSix", "SpecimenTen", "SpecimenThirteen", "SpecimenThree",
        "SpecimenTwelve", "SpecimenTwo"
    };

    // Display names from the game's text where it has them ("Specimen N"), otherwise the names the community uses (Fandom wiki, Steam Endless guide).
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
        { "EMSpecimenFour", "Specimen 4" },
        { "SpecimenEight", "Specimen 8" },
        { "SpecimenEleven", "Specimen 11" },
        { "SpecimenFive", "Specimen 5" },
        { "SpecimenFour", "Specimen 4" },
        { "SpecimenSix", "Specimen 6" },
        { "SpecimenTen", "Specimen 10" },
        { "SpecimenThirteen", "Specimen 13" },
        { "SpecimenThree", "Specimen 3" },
        { "SpecimenTwelve", "Specimen 12" },
        { "SpecimenTwo", "Specimen 2" }
    };

    internal sealed class ChaserSettings
    {
        public ConfigEntry<float> SpawnChance;
        public ConfigEntry<float> DespawnChance;
    }

    internal static Plugin Instance;
    internal static ManualLogSource Log;
    internal static ConfigEntry<bool> EndlessOnly;
    internal static ConfigEntry<bool> PersistChasers;
    internal static ConfigEntry<int> ExtraChaserSlots;
    internal static ConfigEntry<float> SpawnChancePerRoom;
    internal static ConfigEntry<int> MinRoomsBeforeDespawn;
    internal static ConfigEntry<bool> AllowDuplicateChasers;
    internal static ConfigEntry<bool> AllowSaveWithChasers;

    private static readonly Dictionary<string, ChaserSettings> Chasers = new Dictionary<string, ChaserSettings>();
    private static bool loggedChasers;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        EndlessOnly = Config.Bind("General", "EndlessOnly", true,
            "Only apply the mod's changes in Endless mode. The main game is left untouched.");
        PersistChasers = Config.Bind("Chasers", "PersistChasers", false,
            "Override: chasers never despawn, ignoring every chaser's despawn chance and the minimum rooms setting.");
        SpawnChancePerRoom = Config.Bind("Chasers", "SpawnChancePerRoom", 0.5f,
            new ConfigDescription("Chance each room that a new chaser joins, while you are under the chaser cap. When it triggers, which chaser joins is picked using each chaser's own SpawnChance as a weighting.",
                new AcceptableValueRange<float>(0f, 1f)));
        MinRoomsBeforeDespawn = Config.Bind("Chasers", "MinRoomsBeforeDespawn", 0,
            new ConfigDescription("Applies to every chaser. A chaser always stays for at least this many rooms after it spawns. After that, each room it rolls its own despawn chance.",
                new AcceptableValueRange<int>(0, 100)));
        AllowDuplicateChasers = Config.Bind("Chasers", "AllowDuplicateChasers", true,
            "Allow the same chaser to be active more than once at the same time.");
        ExtraChaserSlots = Config.Bind("Chasers", "ExtraChaserSlots", 2,
            new ConfigDescription("Added to the game's chaser cap so new chasers can keep joining after earlier ones stay. In Endless this is on top of the chasers you pick. The game's default cap in Endless is 1 chaser per 100 rooms (rooms 1-100: 0, 101-200: 1, 201-300: 2, and so on up to a maximum of 5 from room 501), or the number of chasers you pick when starting from a skipped floor.",
                new AcceptableValueRange<int>(0, 20)));
        AllowSaveWithChasers = Config.Bind("Endless", "AllowSaveWithChasers", true,
            "Lets you exit to the menu while a chaser is active and stops the game from invalidating the Endless save. Active chasers are not saved, so they will be gone when you resume.");

        foreach (string id in KnownChasers)
        {
            SettingsFor(id);
        }

        new Harmony("spooky.relentlesschasers").PatchAll();

        if (Chainloader.PluginInfos.ContainsKey(ConfigManagerGuid))
        {
            ConfigWindowLock.Hook(Chainloader.PluginInfos[ConfigManagerGuid].Instance);
        }
        Logger.LogInfo("Relentless Chasers loaded!");
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
            DespawnChance = Instance.Config.Bind(section, "DespawnChance", 0f,
                new ConfigDescription("Chance this chaser despawns at the end of each room once it has stayed the minimum number of rooms. 0 = never despawns, 1 = always despawns.",
                    new AcceptableValueRange<float>(0f, 1f)))
        };
        Chasers[id] = settings;
        return settings;
    }

    internal static bool Roll(float chance)
    {
        return chance >= 1f || (chance > 0f && UnityEngine.Random.value < chance);
    }

    internal static bool IsEndless()
    {
        try
        {
            HSMMansionState state = ServiceLocator.GetService<HSMGameManager>().ClosestState<HSMMansionState>();
            return state != null && state.mansion != null && state.mansion.isEndless;
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static bool Active()
    {
        return !EndlessOnly.Value || IsEndless();
    }

    internal static bool IsChaser(SpecimenManager mgr, string id)
    {
        return mgr.allRechasableSpecimen != null && id != null && mgr.allRechasableSpecimen.Contains(id);
    }

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
    }
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
                spec.hp = Math.Max(spec.hp, 1);
                int rooms = RoomCounter.Increment(go);
                if (Plugin.PersistChasers.Value || rooms <= Plugin.MinRoomsBeforeDespawn.Value)
                {
                    continue;
                }
                if (Plugin.Roll(Plugin.SettingsFor(id).DespawnChance.Value))
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
            __result += Plugin.ExtraChaserSlots.Value;
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
            if (loadState.roomRuntimeData.roomConfig.noSpecimenSpawnAllowed)
            {
                return;
            }
            // Chasers picked on the Endless selection screen are spawned by the game right after this; let them go first.
            if (__instance.enqueuedSpecimen != null && __instance.enqueuedSpecimen.Count > 0)
            {
                return;
            }
            int cap = loadState.FindInParent<HSMFloorState>().MaxSpecs();
            if (__instance.activeSpecimens.Count >= cap)
            {
                return;
            }
            if (!Plugin.Roll(Plugin.SpawnChancePerRoom.Value))
            {
                return;
            }

            List<string> candidates = new List<string>();
            List<float> weights = new List<float>();
            float total = 0f;
            foreach (string id in __instance.GetCurrentSpawnables(__instance.allRechasableSpecimen))
            {
                if (!Plugin.AllowDuplicateChasers.Value && __instance.IsSpecimenActive(id))
                {
                    continue;
                }
                float weight = Plugin.SettingsFor(id).SpawnChance.Value;
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
            if (__instance.StartSpecimen(chaser, ServiceLocator.GetInstance().transform, rechase: true) != null)
            {
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
