using System.Linq;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using static Logger;
using BattleTech.Save;
using System.Collections.Generic;
using BattleTech.Framework;


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
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (sim.IsCampaign && !sim.CompanyTags.Contains("story_complete"))
                return;

            try
            {
                Core.BorkedSave = false;
                if (__instance.CompanyTags.Any(x => x.StartsWith("GalaxyAtWarSave")))
                {
                    LogDebug("Have a save.. it starts with " + __instance.CompanyTags.First(x => x.StartsWith("GalaxyAtWarSave")).Substring(0, 30)); 
                    DeserializeWar();
                    RebuildState(); 
                }
            }
            catch
            {
                sim.CompanyTags.Where(tag => tag.StartsWith("GalaxyAtWar")).Do(x => sim.CompanyTags.Remove(x));
                Core.BorkedSave = true;
            }
        }
    }

    internal static void DeserializeWar()
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        Core.WarStatus = JsonConvert.DeserializeObject<WarStatus>(sim.CompanyTags.First(x => x.StartsWith("GalaxyAtWarSave{")).Substring(15));
        LogDebug($">>> Deserialization complete (Size after load: {JsonConvert.SerializeObject(Core.WarStatus).Length / 1024}kb)");
    }

    [HarmonyPatch(typeof(SimGameState), "Dehydrate")]
    public static class SimGameState_Dehydrate_Patch
    {
        public static void Prefix(SimGameState __instance)
        {
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
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        sim.CompanyTags.Where(tag => tag.StartsWith("GalaxyAtWar")).Do(x => sim.CompanyTags.Remove(x));
        sim.CompanyTags.Add("GalaxyAtWarSave" + JsonConvert.SerializeObject(Core.WarStatus));
        LogDebug($">>> Serialization complete (object size: {JsonConvert.SerializeObject(Core.WarStatus).Length / 1024}kb)");
    }

    public static void RebuildState()
    {
        Core.FactionValues = FactionEnumeration.FactionList;
        Galaxy_at_War.HotSpots.ExternalPriorityTargets.Clear();
        Galaxy_at_War.HotSpots.FullHomeContendedSystems.Clear();
        Galaxy_at_War.HotSpots.HomeContendedSystems.Clear();
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        var ssDict = sim.StarSystemDictionary;
        Core.SystemDifficulty();

        try
        {
            foreach (var system in Core.WarStatus.systems)
            {
                var systemDef = ssDict[system.CoreSystemID].Def;
                string systemOwner = systemDef.OwnerValue.Name;
                Traverse.Create(systemDef).Property("OwnerValue").SetValue(Core.FactionValues.Find(x => x.Name == system.owner));
                Traverse.Create(systemDef).Property("OwnerID").SetValue(system.owner);
                Core.RefreshContracts(system.starSystem);

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
                        List<string> TempList = systemDef.SystemShopItems;
                        TempList.Add(Core.Settings.FactionShops[system.owner]);
                        Traverse.Create(systemDef).Property("SystemShopItems").SetValue(TempList);
                    }

                    if (systemDef.FactionShopItems != null)
                    {
                        Traverse.Create(systemDef).Property("FactionShopOwnerValue").SetValue(Core.FactionValues.Find(x => x.Name == system.owner));
                        Traverse.Create(systemDef).Property("FactionShopOwnerID").SetValue(system.owner);
                        List<string> FactionShops = systemDef.FactionShopItems;
                        if (FactionShops.Contains(Core.Settings.FactionShopItems[systemOwner]))
                            FactionShops.Remove(Core.Settings.FactionShopItems[systemOwner]);
                        FactionShops.Add(Core.Settings.FactionShopItems[system.owner]);
                        Traverse.Create(systemDef).Property("FactionShopItems").SetValue(FactionShops);
                    }
                }
            }

            foreach (var faction in Core.WarStatus.ExternalPriorityTargets.Keys)
            {
                Galaxy_at_War.HotSpots.ExternalPriorityTargets.Add(faction, new List<StarSystem>());
                foreach (var system in Core.WarStatus.ExternalPriorityTargets[faction])
                    Galaxy_at_War.HotSpots.ExternalPriorityTargets[faction].Add(ssDict[system]);
            }
            foreach (var system in Core.WarStatus.FullHomeContendedSystems)
            {
                Galaxy_at_War.HotSpots.FullHomeContendedSystems.Add(new KeyValuePair<StarSystem, float>(ssDict[system.Key], system.Value));
            }
            foreach (var system in Core.WarStatus.HomeContendedSystems)
            {
                Galaxy_at_War.HotSpots.HomeContendedSystems.Add(ssDict[system]);
            }
            foreach (var starSystem in Core.WarStatus.FullPirateSystems)
            {
                Galaxy_at_War.PiratesAndLocals.FullPirateListSystems.Add(Core.WarStatus.systems.Find(x => x.name == starSystem));
            }
            foreach (var deathListTracker in Core.WarStatus.deathListTracker)
            {
                Core.AdjustDeathList(deathListTracker, sim, true);
            }
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
            foreach (var contract in sim.CurSystem.activeSystemBreadcrumbs)
            {
                if (Core.WarStatus.DeploymentContracts.Contains(contract.Override.contractName))
                    Traverse.Create(contract.Override).Field("contractDisplayStyle").SetValue(ContractDisplayStyle.BaseCampaignStory);
            }
        }
        catch
        {
            sim.CompanyTags.Where(tag => tag.StartsWith("GalaxyAtWar")).Do(x => sim.CompanyTags.Remove(x));
            Core.BorkedSave = true;
        }
    }

    public static void ConvertToSave()
    {
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