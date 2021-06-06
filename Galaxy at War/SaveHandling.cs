using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BattleTech;
using BattleTech.Framework;
using BattleTech.Save.SaveGameStructure;
using BestHTTP.Extensions;
using Harmony;
using Newtonsoft.Json;
using TB.ComponentModel;
using TMPro;
using UnityEngine;
using static GalaxyatWar.Logger;
using static GalaxyatWar.Helpers;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace GalaxyatWar
{
    public static class SaveHandling
    {
        [HarmonyPatch(typeof(SaveGameStructure), "Load", typeof(string))]
        public class SimGameStateRehydratePatch
        {
            private static void Postfix()
            {
                Globals.ModInitialized = false;
            }
        }

        [HarmonyPatch(typeof(Starmap), "PopulateMap", typeof(SimGameState))]
        public class StarmapPopulateMapPatch
        {
            private static void Postfix(Starmap __instance)
            {
                LogDebug("PopulateMap");
                if (Globals.ModInitialized)
                {
                    return;
                }

                Globals.Sim = __instance.sim;
                Globals.SimGameInterruptManager = Globals.Sim.InterruptQueue;
                Globals.GaWSystems = Globals.Sim.StarSystems.Where(x =>
                    !Globals.Settings.ImmuneToWar.Contains(x.OwnerValue.Name)).ToList();
                if (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                {
                    LogDebug("Aborting GaW loading.");
                    return;
                }

                // thanks to mpstark for this
                var fonts = Resources.FindObjectsOfTypeAll(typeof(TMP_FontAsset));
                foreach (var o in fonts)
                {
                    var font = (TMP_FontAsset) o;
                    if (font.name == "UnitedSansSemiExt-Light")
                    {
                        Globals.Font = font;
                    }
                }

                if (Globals.Settings.ResetMap)
                {
                    LogDebug("Resetting map due to settings.");
                    Spawn();
                    return;
                }

                // is there a tag?  does it deserialize properly?
                var gawTag = Globals.Sim.CompanyTags.FirstOrDefault(x => x.StartsWith("GalaxyAtWarSave"));
                if (!string.IsNullOrEmpty(gawTag))
                {
                    DeserializeWar();
                    // cleaning up old tag data
                    ValidateState();
                    // copied from WarStatus - establish any systems that are new
                    AddNewStarSystems();

                    // try to recover from negative DR
                    // temporary code
                    foreach (var systemStatus in Globals.WarStatusTracker.Systems)
                    {
                        if (systemStatus.DefenseResources < 0 || systemStatus.AttackResources < 0)
                        {
                            systemStatus.AttackResources = GetTotalAttackResources(systemStatus.StarSystem);
                            systemStatus.DefenseResources = GetTotalDefensiveResources(systemStatus.StarSystem);
                            systemStatus.TotalResources = systemStatus.AttackResources + systemStatus.DefenseResources;
                            systemStatus.PirateActivity = 0;
                        }
                    }

                    if (Globals.WarStatusTracker.Systems.Count == 0)
                    {
                        LogDebug("Found tag but it's broken and being respawned:");
                        LogDebug($"{gawTag.Substring(0, 500)}");
                        Spawn();
                    }
                    else
                    {
                        RebuildState();
                        Globals.WarStatusTracker.FirstTickInitialization = true;
                    }
                }
                else
                {
                    Spawn();
                }

                Globals.ModInitialized = true;
            }

            private static void ValidateState()
            {
                if (Globals.GaWSystems.Count < Globals.WarStatusTracker.Systems.Count)
                {
                    for (var index = 0; index < Globals.WarStatusTracker.Systems.Count; index++)
                    {
                        var systemStatus = Globals.WarStatusTracker.Systems[index];
                        if (Globals.Settings.ImmuneToWar.Contains(systemStatus.OriginalOwner))
                        {
                            LogDebug($"Removed: {systemStatus.StarSystem.Name,-15} -> Immune to war, owned by {systemStatus.StarSystem.OwnerValue.Name}.");
                            Globals.WarStatusTracker.Systems.Remove(systemStatus);
                        }
                    }
                }

                foreach (var deathListTracker in Globals.WarStatusTracker.DeathListTracker)
                {
                    if (deathListTracker.Enemies.Any(x => Globals.Settings.ImmuneToWar.Contains(x)))
                    {
                        LogDebug($"Pruning immune factions from deathListTracker of {deathListTracker.Faction}.");
                    }

                    for (var i = 0; i < deathListTracker.Enemies.Count; i++)
                    {
                        if (Globals.Settings.ImmuneToWar.Contains(deathListTracker.Enemies[i]))
                        {
                            LogDebug($"Removing enemy {deathListTracker.Enemies[i]} from {deathListTracker.Faction}.");
                            deathListTracker.Enemies.Remove(deathListTracker.Enemies[i]);
                        }
                    }
                }

                var _ = new Dictionary<string, float>();
                foreach (var kvp in Globals.WarStatusTracker.FullHomeContestedSystems)
                {
                    if (!Globals.Settings.ImmuneToWar.Contains(kvp.Key))
                    {
                        _.Add(kvp.Key, kvp.Value);
                    }
                    else
                    {
                        LogDebug($"Removing {kvp.Key} from FullHomeContestedSystems, as they are immune to war.");
                    }
                }

                Globals.WarStatusTracker.FullHomeContestedSystems = _;
            }

            private static void AddNewStarSystems()
            {
                for (var index = 0; index < Globals.Sim.StarSystems.Count; index++)
                {
                    var system = Globals.Sim.StarSystems[index];
                    if (Globals.Settings.ImmuneToWar.Contains(Globals.Sim.StarSystems[index].OwnerValue.Name) ||
                        Globals.WarStatusTracker.Systems.Any(x => x.StarSystem == system))
                    {
                        continue;
                    }

                    LogDebug($"Trying to add {system.Name}, owner {system.OwnerValue.Name}.");
                    var systemStatus = new SystemStatus(system, system.OwnerValue.Name);
                    Globals.WarStatusTracker.Systems.Add(systemStatus);
                    if (system.Tags.Contains("planet_other_pirate") && !system.Tags.Contains("planet_region_hyadesrim"))
                    {
                        Globals.WarStatusTracker.FullPirateSystems.Add(system.Name);
                        PiratesAndLocals.FullPirateListSystems.Add(systemStatus);
                    }

                    if (system.Tags.Contains("planet_region_hyadesrim") &&
                        !Globals.WarStatusTracker.FlashpointSystems.Contains(system.Name) &&
                        (system.OwnerValue.Name == "NoFaction" || system.OwnerValue.Name == "Locals"))
                        Globals.WarStatusTracker.HyadesRimGeneralPirateSystems.Add(system.Name);
                }
            }


            internal static void Spawn()
            {
                LogDebug("Spawning new instance.");
                Globals.WarStatusTracker = new WarStatus();
                LogDebug("New global state created.");
                Globals.WarStatusTracker.SystemsByResources =
                    Globals.WarStatusTracker.Systems.OrderBy(x => x.TotalResources).ToList();
                if (!Globals.WarStatusTracker.StartGameInitialized)
                {
                    // bug loading a bad/older tag logs this but leaves 5 basic contracts
                    // or maybe it's loading any existing game?  that would suck
                    LogDebug($"Refreshing contracts at spawn ({Globals.Sim.CurSystem.Name}).");
                    var cmdCenter = Globals.Sim.RoomManager.CmdCenterRoom;
                    Globals.Sim.CurSystem.GenerateInitialContracts(() => cmdCenter.OnContractsFetched());
                    Globals.WarStatusTracker.StartGameInitialized = true;
                }

                SystemDifficulty();
                Globals.WarStatusTracker.FirstTickInitialization = true;
                WarTick.Tick(true, true);
            }
        }

        private static void DeserializeWar()
        {
            LogDebug("DeserializeWar");
            var tag = Globals.Sim.CompanyTags.First(x => x.StartsWith("GalaxyAtWarSave")).Substring(15);
            try
            {
                Globals.WarStatusTracker = JsonConvert.DeserializeObject<WarStatus>(Unzip(Convert.FromBase64String(tag)));
                LogDebug($">>> Deserialization complete (Size after load: {tag.Length / 1024}kb)");
            }
            catch (Exception ex)
            {
                Error(ex);
                LogDebug("Error deserializing tag, generating new state.");
                StarmapPopulateMapPatch.Spawn();
            }
        }

        [HarmonyPatch(typeof(SimGameState), "Dehydrate")]
        public static class SimGameStateDehydratePatch
        {
            public static void Prefix(SimGameState __instance)
            {
                LogDebug("Dehydrate");
                Globals.Sim = __instance;
                if (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                    return;

                if (Globals.WarStatusTracker == null)
                {
                    Globals.WarStatusTracker = new WarStatus();
                    SystemDifficulty();
                    WarTick.Tick(true, true);
                    SerializeWar();
                }
                else
                {
                    ConvertToSave();
                    SerializeWar();
                }

                if (Globals.FirstDehydrate)
                {
                    Globals.FirstDehydrate = false;
                    Globals.WarStatusTracker.StartGameInitialized = false;
                }
            }

            public static void Postfix()
            {
                if (Globals.Settings.CleanUpCompanyTag)
                {
                    Globals.Sim.CompanyTags.Where(tag =>
                        tag.StartsWith("GalaxyAtWar")).Do(x => Globals.Sim.CompanyTags.Remove(x));
                }
            }
        }

        internal static void SerializeWar()
        {
            LogDebug("SerializeWar");
            var gawTag = Globals.Sim.CompanyTags.FirstOrDefault(x => x.StartsWith("GalaxyAtWar"));
            Globals.Sim.CompanyTags.Remove(gawTag);
            gawTag = $"GalaxyAtWarSave{Convert.ToBase64String(Zip(JsonConvert.SerializeObject(Globals.WarStatusTracker)))}";
            Globals.Sim.CompanyTags.Add(gawTag);
            LogDebug($">>> Serialization complete (object size: {gawTag.Length / 1024}kb)");
        }

        public static void RebuildState()
        {
            LogDebug("RebuildState");
            HotSpots.ExternalPriorityTargets.Clear();
            HotSpots.FullHomeContestedSystems.Clear();
            HotSpots.HomeContestedSystems.Clear();
            var starSystemDictionary = Globals.Sim.StarSystemDictionary;
            Globals.WarStatusTracker.SystemsByResources =
                Globals.WarStatusTracker.Systems.OrderBy(x => x.TotalResources).ToList();
            SystemDifficulty();

            try
            {
                if (Globals.Settings.ResetMap)
                {
                    Globals.Sim.CompanyTags.Where(tag =>
                        tag.StartsWith("GalaxyAtWar")).Do(x => Globals.Sim.CompanyTags.Remove(x));
                    return;
                }

                for (var i = 0; i < Globals.WarStatusTracker.Systems.Count; i++)
                {
                    StarSystemDef systemDef;
                    var system = Globals.WarStatusTracker.Systems[i];
                    if (starSystemDictionary.ContainsKey(system.CoreSystemID))
                    {
                        systemDef = starSystemDictionary[system.CoreSystemID].Def;
                    }
                    else
                    {
                        LogDebug($"BOMB {system.name} not in StarSystemDictionary, removing it from WarStatus.systems");
                        Globals.WarStatusTracker.Systems.Remove(system);
                        continue;
                    }

                    var systemOwner = systemDef.OwnerValue.Name;
                    // needs to be refreshed since original declaration in Mod
                    var ownerValue = Globals.FactionValues.Find(x => x.Name == system.owner);
                    systemDef.OwnerValue = ownerValue;
                    systemDef.factionShopOwnerID = system.owner;
                    RefreshContractsEmployersAndTargets(system);
                    if (system.InfluenceTracker.Keys.Contains("AuriganPirates") && !system.InfluenceTracker.Keys.Contains("NoFaction"))
                    {
                        system.InfluenceTracker.Add("NoFaction", system.InfluenceTracker["AuriganPirates"]);
                        system.InfluenceTracker.Remove("AuriganPirates");
                    }
                    //if (!system.influenceTracker.Keys.Contains("NoFaction"))
                    //    system.influenceTracker.Add("NoFaction", 0);

                    if (systemDef.OwnerValue.Name != systemOwner && systemOwner != "NoFaction")
                    {
                        if (systemDef.SystemShopItems.Count != 0)
                        {
                            var tempList = systemDef.SystemShopItems;
                            tempList.Add(Globals.Settings.FactionShops[system.owner]);

                            Traverse.Create(systemDef).Property("SystemShopItems").SetValue(systemDef.SystemShopItems);
                        }

                        if (systemDef.FactionShopItems != null)
                        {
                            systemDef.FactionShopOwnerValue = Globals.FactionValues.Find(x => x.Name == system.owner);
                            systemDef.factionShopOwnerID = system.owner;
                            var factionShopItems = systemDef.FactionShopItems;
                            if (factionShopItems.Contains(Globals.Settings.FactionShopItems[systemOwner]))
                                factionShopItems.Remove(Globals.Settings.FactionShopItems[systemOwner]);
                            factionShopItems.Add(Globals.Settings.FactionShopItems[system.owner]);
                            systemDef.FactionShopItems = factionShopItems;
                        }
                    }
                }

                foreach (var faction in Globals.WarStatusTracker.ExternalPriorityTargets.Keys)
                {
                    HotSpots.ExternalPriorityTargets.Add(faction, new List<StarSystem>());
                    foreach (var system in Globals.WarStatusTracker.ExternalPriorityTargets[faction])
                        HotSpots.ExternalPriorityTargets[faction].Add(starSystemDictionary[system]);
                }

                foreach (var system in Globals.WarStatusTracker.FullHomeContestedSystems)
                {
                    HotSpots.FullHomeContestedSystems.Add(new KeyValuePair<StarSystem, float>(starSystemDictionary[system.Key], system.Value));
                }

                foreach (var system in Globals.WarStatusTracker.HomeContestedSystems)
                {
                    HotSpots.HomeContestedSystems.Add(starSystemDictionary[system]);
                }

                foreach (var starSystem in Globals.WarStatusTracker.FullPirateSystems)
                {
                    PiratesAndLocals.FullPirateListSystems.Add(Globals.WarStatusTracker.Systems.Find(x => x.name == starSystem));
                }

                foreach (var deathListTracker in Globals.WarStatusTracker.DeathListTracker)
                {
                    if (deathListTracker.WarFaction is null)
                    {
                        LogDebug($"{deathListTracker.Faction} has a null WarFaction, resetting state.");
                        StarmapPopulateMapPatch.Spawn();
                        break;
                    }
                    AdjustDeathList(deathListTracker, true);
                }

                foreach (var defensiveFaction in Globals.Settings.DefensiveFactions)
                {
                    if (Globals.WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == defensiveFaction) == null)
                        continue;

                    var targetFaction = Globals.WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == defensiveFaction);

                    if (targetFaction.AttackResources != 0)
                    {
                        targetFaction.DefensiveResources += targetFaction.AttackResources;
                        targetFaction.AttackResources = 0;
                    }
                }

                foreach (var contract in Globals.Sim.CurSystem.activeSystemBreadcrumbs)
                {
                    if (Globals.WarStatusTracker.DeploymentContracts.Contains(contract.Override.contractName))
                        contract.Override.contractDisplayStyle = ContractDisplayStyle.BaseCampaignStory;
                }
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        public static void ConvertToSave()
        {
            LogDebug("ConvertToSave");
            Globals.WarStatusTracker.ExternalPriorityTargets.Clear();
            Globals.WarStatusTracker.FullHomeContestedSystems.Clear();
            Globals.WarStatusTracker.HomeContestedSystems.Clear();
            Globals.WarStatusTracker.FullPirateSystems.Clear();

            foreach (var faction in HotSpots.ExternalPriorityTargets.Keys)
            {
                Globals.WarStatusTracker.ExternalPriorityTargets.Add(faction, new List<string>());
                foreach (var system in HotSpots.ExternalPriorityTargets[faction])
                    Globals.WarStatusTracker.ExternalPriorityTargets[faction].Add(system.Def.CoreSystemID);
            }

            foreach (var system in HotSpots.FullHomeContestedSystems)
                Globals.WarStatusTracker.FullHomeContestedSystems.Add(system.Key.Def.CoreSystemID, system.Value);
            foreach (var system in HotSpots.HomeContestedSystems)
                Globals.WarStatusTracker.HomeContestedSystems.Add(system.Def.CoreSystemID);
        }
    }
}
