using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.Framework;
using Harmony;
using Newtonsoft.Json;
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
        [HarmonyPatch(typeof(Starmap), "PopulateMap", typeof(SimGameState))]
        public class StarmapPopulateMapPatch
        {
            private static void Postfix(Starmap __instance)
            {
                LogDebug("PopulateMap");
                Globals.Sim = __instance.sim;
                Globals.SimGameInterruptManager = Globals.Sim.InterruptQueue;
                // thanks to mpstark for this
                var fonts = Resources.FindObjectsOfTypeAll(typeof(TMP_FontAsset));
                foreach (var o in fonts)
                {
                    var font = (TMP_FontAsset) o;
                    if (font.name == "UnitedSansSemiExt-Light")
                    {
                        Globals.font = font;
                    }
                }
                
                if (Globals.Sim.IsCampaign && !Globals.Sim.CompanyTags.Contains("story_complete"))
                {
                    LogDebug("Aborting GaW loading.");
                    return;
                }

                if (Globals.Settings.ResetMap)
                {
                    LogDebug("Resetting map due to settings.");
                    Spawn();
                    return;
                }

                // is there a tag?  is it not just an empty broken shell?
                var gawTag = Globals.Sim.CompanyTags.FirstOrDefault(x => x.StartsWith("GalaxyAtWarSave"));
                if (!string.IsNullOrEmpty(gawTag))
                {
                    DeserializeWar();
                    if (Globals.WarStatusTracker.systems.Count == 0)
                    {
                        LogDebug("Found tag but it's broken and being respawned:");
                        LogDebug($"{gawTag}");
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
            }

            private static void Spawn()
            {
                LogDebug("Spawning new instance");
                Globals.WarStatusTracker = new WarStatus();
                Globals.WarStatusTracker.systemsByResources =
                    Globals.WarStatusTracker.systems.OrderBy(x => x.TotalResources).ToList();
                if (!Globals.WarStatusTracker.StartGameInitialized)
                {
                    var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                    Globals.Sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
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
            var tag = Globals.Sim.CompanyTags.First(x => x.StartsWith("GalaxyAtWarSave{")).Substring(15);
            Globals.WarStatusTracker = JsonConvert.DeserializeObject<WarStatus>(tag);
            LogDebug($">>> Deserialization complete (Size after load: {tag.Length / 1024}kb)");
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
            gawTag = "GalaxyAtWarSave" + JsonConvert.SerializeObject(Globals.WarStatusTracker);
            Globals.Sim.CompanyTags.Add(gawTag);
            LogDebug($">>> Serialization complete (object size: {gawTag.Length / 1024}kb)");
        }

        public static void RebuildState()
        {
            LogDebug("RebuildState");
            HotSpots.ExternalPriorityTargets.Clear();
            HotSpots.FullHomeContendedSystems.Clear();
            HotSpots.HomeContendedSystems.Clear();
            var ssDict = Globals.Sim.StarSystemDictionary;
            Globals.WarStatusTracker.systemsByResources =
                Globals.WarStatusTracker.systems.OrderBy(x => x.TotalResources).ToList();
            SystemDifficulty();

            try
            {
                if (Globals.Settings.ResetMap)
                {
                    Globals.Sim.CompanyTags.Where(tag =>
                        tag.StartsWith("GalaxyAtWar")).Do(x => Globals.Sim.CompanyTags.Remove(x));
                    return;
                }

                for (var i = 0; i < Globals.WarStatusTracker.systems.Count; i++)
                {
                    StarSystemDef systemDef;
                    var system = Globals.WarStatusTracker.systems[i];
                    if (ssDict.ContainsKey(system.CoreSystemID))
                    {
                        systemDef = ssDict[system.CoreSystemID].Def;
                    }
                    else
                    {
                        LogDebug($"BOMB {system.name} not in StarSystemDictionary, removing it from WarStatus.systems");
                        Globals.WarStatusTracker.systems.Remove(system);
                        continue;
                    }

                    var systemOwner = systemDef.OwnerValue.Name;
                    // needs to be refreshed since original declaration in Mod
                    var ownerValue = Globals.FactionValues.Find(x => x.Name == system.owner);
                    systemDef.OwnerValue = ownerValue;
                    systemDef.factionShopOwnerID = system.owner;
                    RefreshContracts(system);
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
                        HotSpots.ExternalPriorityTargets[faction].Add(ssDict[system]);
                }

                foreach (var system in Globals.WarStatusTracker.FullHomeContendedSystems)
                {
                    HotSpots.FullHomeContendedSystems.Add(new KeyValuePair<StarSystem, float>(ssDict[system.Key], system.Value));
                }

                foreach (var system in Globals.WarStatusTracker.HomeContendedSystems)
                {
                    HotSpots.HomeContendedSystems.Add(ssDict[system]);
                }

                foreach (var starSystem in Globals.WarStatusTracker.FullPirateSystems)
                {
                    PiratesAndLocals.FullPirateListSystems.Add(Globals.WarStatusTracker.systems.Find(x => x.name == starSystem));
                }

                foreach (var deathListTracker in Globals.WarStatusTracker.deathListTracker)
                {
                    AdjustDeathList(deathListTracker, true);
                }

                foreach (var defensiveFaction in Globals.Settings.DefensiveFactions)
                {
                    if (Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == defensiveFaction) == null)
                        continue;

                    var targetFaction = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == defensiveFaction);

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
            Globals.WarStatusTracker.FullHomeContendedSystems.Clear();
            Globals.WarStatusTracker.HomeContendedSystems.Clear();
            Globals.WarStatusTracker.FullPirateSystems.Clear();

            foreach (var faction in HotSpots.ExternalPriorityTargets.Keys)
            {
                Globals.WarStatusTracker.ExternalPriorityTargets.Add(faction, new List<string>());
                foreach (var system in HotSpots.ExternalPriorityTargets[faction])
                    Globals.WarStatusTracker.ExternalPriorityTargets[faction].Add(system.Def.CoreSystemID);
            }

            foreach (var system in HotSpots.FullHomeContendedSystems)
                Globals.WarStatusTracker.FullHomeContendedSystems.Add(system.Key.Def.CoreSystemID, system.Value);
            foreach (var system in HotSpots.HomeContendedSystems)
                Globals.WarStatusTracker.HomeContendedSystems.Add(system.Def.CoreSystemID);
        }
    }
}
