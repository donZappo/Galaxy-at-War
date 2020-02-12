using System;
using System.IO;
using System.Linq;
using BattleTech;
using BattleTech.Save.Test;
using Harmony;
using Newtonsoft.Json;
using UnityEngine;
using static Logger;
using BattleTech.Save;
using System.Diagnostics;
using HBS;
using BattleTech.UI;
using System.Collections.Generic;
using BattleTech.Save.Core;
using BattleTech.Framework;
using Stopwatch = HBS.Stopwatch;
using System.Diagnostics;
using System.Text.RegularExpressions;

public static class SaveHandling
{
    [HarmonyPatch(typeof(SimGameState), "Rehydrate")]
    public static class SimGameState_Rehydrate_Patch
    {
        static void Prefix()
        {
        }
        static void Postfix(SimGameState __instance, GameInstanceSave gameInstanceSave)
        {
            LogDebug("Rehydrate");
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete"))
                return;

            try
            {
                bool NewGaW = true;
                Core.BorkedSave = false;
                foreach (string tag in __instance.CompanyTags)
                {
                    if (tag.StartsWith("GalaxyAtWarSave{"))
                        NewGaW = false;
                }
                if (!NewGaW)
                {
                    DeserializeWar();
                    RebuildState();
                }
            }
            catch (Exception ex)
            {
                LogDebug(ex);
                LogDebug("The solution is not spaghetti");
                sim.CompanyTags.Where(tag => tag.StartsWith("GalaxyAtWar")).Do(x => sim.CompanyTags.Remove(x));
                Core.BorkedSave = true;
            }
        }
    }

    internal static void DeserializeWar()
    {
        LogDebug("DeserializeWar");
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        Core.WarStatus = JsonConvert.DeserializeObject<WarStatus>(sim.CompanyTags.First(x => x.StartsWith("GalaxyAtWarSave{")).Substring(15));
        LogDebug($">>> Deserialization complete (Size after load: {JsonConvert.SerializeObject(Core.WarStatus).Length / 1024}kb)");
    }

    [HarmonyPatch(typeof(SimGameState), "Dehydrate")]
    public static class SimGameState_Dehydrate_Patch
    {
        public static void Prefix(SimGameState __instance)
        {
            LogDebug("Dehydrate");
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete"))
                return;

            if (Core.WarStatus == null)
            {
                Core.WarStatus = new WarStatus();
                Core.SystemDifficulty();
                Core.WarTick(true, true);
                SerializeWar();
            }
            else
            {
                ConvertToSave();
                SerializeWar();
            }
        }
    }

    internal static void SerializeWar()
    {
        LogDebug("SerializeWar");
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        sim.CompanyTags.Where(tag => tag.StartsWith("GalaxyAtWar")).Do(x => sim.CompanyTags.Remove(x));
        sim.CompanyTags.Add("GalaxyAtWarSave" + JsonConvert.SerializeObject(Core.WarStatus));
        LogDebug($">>> Serialization complete (object size: {JsonConvert.SerializeObject(Core.WarStatus).Length / 1024}kb)");
    }

    public static void RebuildState()
    {
        LogDebug("RebuildState");
        Core.FactionValues = FactionEnumeration.FactionList;
        Galaxy_at_War.HotSpots.ExternalPriorityTargets.Clear();
        Galaxy_at_War.HotSpots.FullHomeContendedSystems.Clear();
        Galaxy_at_War.HotSpots.HomeContendedSystems.Clear();
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        //var ssDict = sim.StarSystemDictionary;
        Dictionary<string, StarSystem> starSystemDictionary = new Dictionary<string, StarSystem>();
        foreach (var system in sim.StarSystems)
        {
            LogDebug($"{system.Name} {system.ID}");
            //var simpleName = Regex.Match(system.ID, "_(.+)$").Groups[1].Value;
            starSystemDictionary.Add(system.ID, system);
        }
        Core.SystemDifficulty();

        try
        {
            if (Core.Settings.ResetMap)
            {
                sim.CompanyTags.Where(tag => tag.StartsWith("GalaxyAtWar"))?.Do(x => sim.CompanyTags.Remove(x));
                return;
            }
            
            foreach (var system in Core.WarStatus.systems)
            {
                LogDebug(0);
                StarSystemDef systemDef;
                if (starSystemDictionary.ContainsKey(system.CoreSystemID))
                {
                    systemDef = starSystemDictionary[system.CoreSystemID].Def; 
                }
                else
                {
                    LogDebug($"{system.name} not in StarSystemDictionary");
                    continue;
                }
                LogDebug(0.1);
                
                string systemOwner = systemDef.OwnerValue.Name;
                Traverse.Create(systemDef).Property("OwnerValue").SetValue(Core.FactionValues.Find(x => x.Name == system.owner));
                Traverse.Create(systemDef).Property("OwnerID").SetValue(system.owner);
                Core.RefreshContracts(system.starSystem);
                LogDebug("Done refreshing system contracts");
                if (system.influenceTracker.Keys.Contains("AuriganPirates") && !system.influenceTracker.Keys.Contains("NoFaction"))
                {
                    system.influenceTracker.Add("NoFaction", system.influenceTracker["AuriganPirates"]);
                    system.influenceTracker.Remove("AuriganPirates");
                    LogDebug(0.2);
                }
                //if (!system.influenceTracker.Keys.Contains("NoFaction"))
                //    system.influenceTracker.Add("NoFaction", 0);

                if (systemDef.OwnerValue.Name != systemOwner && systemOwner != "NoFaction")
                {
                    LogDebug("Changed");
                    LogDebug(1.1);
                    if (systemDef.SystemShopItems.Count != 0)
                    {
                        LogDebug(1.2);
                        List<string> TempList = systemDef.SystemShopItems;
                        LogDebug(2);
                        TempList.Add(Core.Settings.FactionShops[system.owner]);
                        LogDebug(3);
                        Traverse.Create(systemDef).Property("SystemShopItems").SetValue(TempList);
                    }

                    LogDebug(4);
                    if (systemDef.FactionShopItems != null)
                    {
                        LogDebug(5);
                        Traverse.Create(systemDef).Property("FactionShopOwnerValue").SetValue(Core.FactionValues.Find(x => x.Name == system.owner));
                        Traverse.Create(systemDef).Property("FactionShopOwnerID").SetValue(system.owner);
                        List<string> factionShopItems = systemDef.FactionShopItems;
                        if (factionShopItems.Contains(Core.Settings.FactionShopItems[systemOwner]))
                            factionShopItems.Remove(Core.Settings.FactionShopItems[systemOwner]);
                        factionShopItems.Add(Core.Settings.FactionShopItems[system.owner]);
                        Traverse.Create(systemDef).Property("FactionShopItems").SetValue(factionShopItems);
                        LogDebug(6);
                    }
                }
                else
                {
                    LogDebug("Unchanged");
                }
            }

            LogDebug(10);
            LogDebug(Core.WarStatus.ExternalPriorityTargets.Keys.Count);
            foreach (var faction in Core.WarStatus.ExternalPriorityTargets.Keys)
            {
                Galaxy_at_War.HotSpots.ExternalPriorityTargets.Add(faction, new List<StarSystem>());
                foreach (var system in Core.WarStatus.ExternalPriorityTargets[faction])
                    Galaxy_at_War.HotSpots.ExternalPriorityTargets[faction].Add(starSystemDictionary[system]);
            }

            LogDebug(Core.WarStatus.FullHomeContendedSystems.Count);
            foreach (var system in Core.WarStatus.FullHomeContendedSystems)
            {
                Galaxy_at_War.HotSpots.FullHomeContendedSystems.Add(new KeyValuePair<StarSystem, float>(starSystemDictionary[system.Key], system.Value));
            }
            LogDebug(Core.WarStatus.HomeContendedSystems.Count);
            foreach (var system in Core.WarStatus.HomeContendedSystems)
            {
                Galaxy_at_War.HotSpots.HomeContendedSystems.Add(starSystemDictionary[system]);
            }
            LogDebug(Core.WarStatus.FullPirateSystems.Count);
            foreach (var starSystem in Core.WarStatus.FullPirateSystems)
            {
                Galaxy_at_War.PiratesAndLocals.FullPirateListSystems.Add(Core.WarStatus.systems.Find(x => x.name == starSystem));
            }
            LogDebug(Core.WarStatus.deathListTracker.Count);
            foreach (var deathListTracker in Core.WarStatus.deathListTracker)
            {
                Core.AdjustDeathList(deathListTracker, sim, true);
            }
            LogDebug(Core.Settings.DefensiveFactions.Count);
            foreach (var defensivefaction in Core.Settings.DefensiveFactions)
            {
                if (Core.WarStatus.warFactionTracker.Find(x => x.faction == defensivefaction) == null)
                    continue;

                var targetfaction = Core.WarStatus.warFactionTracker.Find(x => x.faction == defensivefaction);

                if (targetfaction.AttackResources != 0)
                {
                    targetfaction.DefensiveResources += targetfaction.AttackResources;
                    targetfaction.AttackResources = 0;
                }
            }
            LogDebug(sim.CurSystem.activeSystemBreadcrumbs);
            foreach (var contract in sim.CurSystem.activeSystemBreadcrumbs)
            {
                if (Core.WarStatus.DeploymentContracts.Contains(contract.Override.contractName))
                    Traverse.Create(contract.Override).Field("contractDisplayStyle").SetValue(ContractDisplayStyle.BaseCampaignStory);
            }
        }
        catch (Exception ex)
        {
            LogDebug(ex);
            LogDebug("This is why you don't put code in catch blocks");
            sim.CompanyTags.Where(tag => tag.StartsWith("GalaxyAtWar"))?.Do(x => sim.CompanyTags.Remove(x));
            Core.BorkedSave = true;
        }
    }

    public static void ConvertToSave()
    {
        LogDebug("ConvertToSave");
        Core.WarStatus.ExternalPriorityTargets.Clear();
        Core.WarStatus.FullHomeContendedSystems.Clear();
        Core.WarStatus.HomeContendedSystems.Clear();
        Core.WarStatus.FullPirateSystems.Clear();

        var sim = UnityGameInstance.BattleTechGame.Simulation;
        foreach (var faction in Galaxy_at_War.HotSpots.ExternalPriorityTargets.Keys)
        {
            Core.WarStatus.ExternalPriorityTargets.Add(faction, new List<string>());
            foreach (var system in Galaxy_at_War.HotSpots.ExternalPriorityTargets[faction])
                Core.WarStatus.ExternalPriorityTargets[faction].Add(system.Def.CoreSystemID);
        }
        foreach (var system in Galaxy_at_War.HotSpots.FullHomeContendedSystems)
            Core.WarStatus.FullHomeContendedSystems.Add(system.Key.Def.CoreSystemID, system.Value);
        foreach (var system in Galaxy_at_War.HotSpots.HomeContendedSystems)
            Core.WarStatus.HomeContendedSystems.Add(system.Def.CoreSystemID);
    }
}