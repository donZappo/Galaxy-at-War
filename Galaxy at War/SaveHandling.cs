using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.Framework;
using Harmony;
using Newtonsoft.Json;
using static GalaxyatWar.Logger;
using static GalaxyatWar.Globals;
using static GalaxyatWar.Helpers;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace GalaxyatWar
{
    public static class SaveHandling
    {

        [HarmonyPatch(typeof(SimGameState), "Rehydrate")]
        public static class SimGameStateRehydratePatch
        {
            private const int EmptySaveLength = 20_000;
            private static void Postfix(SimGameState __instance)
            {
                LogDebug("Rehydrate");
                Sim = __instance;
                SimGameInterruptManager = Sim.InterruptQueue;
                if (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete"))
                {
                    LogDebug("Aborting GaW loading");
                    return;
                }

                var gawTag = __instance.CompanyTags.FirstOrDefault(x => x.StartsWith("GalaxyAtWarSave"));
                if (!string.IsNullOrEmpty(gawTag) &&
                     gawTag.Length > EmptySaveLength)
                {
                    DeserializeWar();
                    RebuildState();
                }
                else
                {
                    LogDebug("Spawning new instance");
                    WarStatusTracker = new WarStatus();
                    SystemDifficulty();
                    WarTick.Tick(true, true);
                }
            }
        }

        private static void DeserializeWar()
        {
            LogDebug("DeserializeWar");
            var tag = Sim.CompanyTags.First(x => x.StartsWith("GalaxyAtWarSave{")).Substring(15);
            WarStatusTracker = JsonConvert.DeserializeObject<WarStatus>(tag);
            LogDebug($">>> Deserialization complete (Size after load: {tag.Length / 1024}kb)");
        }

        [HarmonyPatch(typeof(SimGameState), "Dehydrate")]
        public static class SimGameStateDehydratePatch
        {
            public static void Prefix(SimGameState __instance)
            {
                LogDebug("Dehydrate");
                Sim = __instance;
                if (Sim.IsCampaign && !Sim.CompanyTags.Contains("story_complete"))
                    return;

                if (WarStatusTracker == null)
                {
                    WarStatusTracker = new WarStatus();
                    SystemDifficulty();
                    WarTick.Tick(true, true);
                    SerializeWar();
                }
                else
                {
                    ConvertToSave();
                    SerializeWar();
                }
            }

            public static void Postfix()
            {
                if (Settings.CleanUpCompanyTag)
                {
                    Sim.CompanyTags.Where(tag => tag.StartsWith("GalaxyAtWar")).Do(x => Sim.CompanyTags.Remove(x));
                }
            }
        }

        internal static void SerializeWar()
        {
            LogDebug("SerializeWar");
            var gawTag = Sim.CompanyTags.FirstOrDefault(x => x.StartsWith("GalaxyAtWar"));
            if (!string.IsNullOrEmpty(gawTag))
            {
                Sim.CompanyTags.Remove(Sim.CompanyTags.FirstOrDefault(x => x.StartsWith("GalaxyAtWar")));
            }

            var tag = "GalaxyAtWarSave" + JsonConvert.SerializeObject(WarStatusTracker);
            Sim.CompanyTags.Add(tag);
            LogDebug($">>> Serialization complete (object size: {tag.Length / 1024}kb)");
        }

        public static void RebuildState()
        {
            LogDebug("RebuildState");
            HotSpots.ExternalPriorityTargets.Clear();
            HotSpots.FullHomeContendedSystems.Clear();
            HotSpots.HomeContendedSystems.Clear();
            var ssDict = Sim.StarSystemDictionary;
            SystemDifficulty();

            try
            {
                if (Settings.ResetMap)
                {
                    Sim.CompanyTags.Where(tag => tag.StartsWith("GalaxyAtWar")).Do(x => Sim.CompanyTags.Remove(x));
                    return;
                }

                for (var i = 0; i < WarStatusTracker.SystemStatuses.Count; i++)
                {
                    StarSystemDef systemDef;
                    var system = WarStatusTracker.SystemStatuses[i];
                    if (ssDict.ContainsKey(system.CoreSystemID))
                    {
                        systemDef = ssDict[system.CoreSystemID].Def;
                    }
                    else
                    {
                        LogDebug($"BOMB {system.name} not in StarSystemDictionary, removing it from WarStatus.systems");
                        WarStatusTracker.SystemStatuses.Remove(system);
                        continue;
                    }

                    var systemOwner = systemDef.OwnerValue.Name;
                    // needs to be refreshed since original declaration in Mod
                    var ownerValue = FactionValues.Find(x => x.Name == system.owner);
                    Traverse.Create(systemDef).Property("OwnerValue").SetValue(ownerValue);
                    Traverse.Create(systemDef).Property("OwnerID").SetValue(system.owner);
                    RefreshContracts(system.starSystem);
                    if (system.influenceTracker.Keys.Contains("AuriganPirates") && !system.influenceTracker.Keys.Contains("NoFaction"))
                    {
                        system.influenceTracker.Add("NoFaction", system.influenceTracker["AuriganPirates"]);
                        system.influenceTracker.Remove("AuriganPirates");
                    }
                    //if (!system.influenceTracker.Keys.Contains("NoFaction"))
                    //    system.influenceTracker.Add("NoFaction", 0);

                    if (systemDef.OwnerValue.Name != systemOwner && systemOwner != "NoFaction")
                    {
                        if (systemDef.SystemShopItems.Count != 0)
                        {
                            var TempList = systemDef.SystemShopItems;
                            TempList.Add(Settings.FactionShops[system.owner]);
                            Traverse.Create(systemDef).Property("SystemShopItems").SetValue(systemDef.SystemShopItems);
                        }

                        if (systemDef.FactionShopItems != null)
                        {
                            Traverse.Create(systemDef).Property("FactionShopOwnerValue").SetValue(FactionValues.Find(x => x.Name == system.owner));
                            Traverse.Create(systemDef).Property("FactionShopOwnerID").SetValue(system.owner);
                            var factionShopItems = systemDef.FactionShopItems;
                            if (factionShopItems.Contains(Settings.FactionShopItems[systemOwner]))
                                factionShopItems.Remove(Settings.FactionShopItems[systemOwner]);
                            factionShopItems.Add(Settings.FactionShopItems[system.owner]);
                            Traverse.Create(systemDef).Property("FactionShopItems").SetValue(factionShopItems);
                        }
                    }
                }

                foreach (var faction in WarStatusTracker.ExternalPriorityTargets.Keys)
                {
                    HotSpots.ExternalPriorityTargets.Add(faction, new List<StarSystem>());
                    foreach (var system in WarStatusTracker.ExternalPriorityTargets[faction])
                        HotSpots.ExternalPriorityTargets[faction].Add(ssDict[system]);
                }

                foreach (var system in WarStatusTracker.FullHomeContendedSystems)
                {
                    HotSpots.FullHomeContendedSystems.Add(new KeyValuePair<StarSystem, float>(ssDict[system.Key], system.Value));
                }

                foreach (var system in WarStatusTracker.HomeContendedSystems)
                {
                    HotSpots.HomeContendedSystems.Add(ssDict[system]);
                }

                foreach (var starSystem in WarStatusTracker.FullPirateSystems)
                {
                    PiratesAndLocals.FullPirateListSystems.Add(WarStatusTracker.SystemStatuses.Find(x => x.name == starSystem));
                }

                foreach (var deathListTracker in WarStatusTracker.DeathListTrackers)
                {
                    AdjustDeathList(deathListTracker, true);
                }

                foreach (var defensiveFaction in Settings.DefensiveFactions)
                {
                    if (WarStatusTracker.warFactionTracker.Find(x => x.faction == defensiveFaction) == null)
                        continue;

                    var targetFaction = WarStatusTracker.warFactionTracker.Find(x => x.faction == defensiveFaction);

                    if (targetFaction.AttackResources != 0)
                    {
                        targetFaction.DefensiveResources += targetFaction.AttackResources;
                        targetFaction.AttackResources = 0;
                    }
                }

                foreach (var contract in Sim.CurSystem.activeSystemBreadcrumbs)
                {
                    if (WarStatusTracker.DeploymentContracts.Contains(contract.Override.contractName))
                        Traverse.Create(contract.Override).Field("contractDisplayStyle").SetValue(ContractDisplayStyle.BaseCampaignStory);
                }
            }
            catch (Exception ex)
            {
                LogDebug(ex);
            }
        }

        public static void ConvertToSave()
        {
            LogDebug("ConvertToSave");
            WarStatusTracker.ExternalPriorityTargets.Clear();
            WarStatusTracker.FullHomeContendedSystems.Clear();
            WarStatusTracker.HomeContendedSystems.Clear();
            WarStatusTracker.FullPirateSystems.Clear();

            foreach (var faction in HotSpots.ExternalPriorityTargets.Keys)
            {
                WarStatusTracker.ExternalPriorityTargets.Add(faction, new List<string>());
                foreach (var system in HotSpots.ExternalPriorityTargets[faction])
                    WarStatusTracker.ExternalPriorityTargets[faction].Add(system.Def.CoreSystemID);
            }

            foreach (var system in HotSpots.FullHomeContendedSystems)
                WarStatusTracker.FullHomeContendedSystems.Add(system.Key.Def.CoreSystemID, system.Value);
            foreach (var system in HotSpots.HomeContendedSystems)
                WarStatusTracker.HomeContendedSystems.Add(system.Def.CoreSystemID);
        }
    }
}
