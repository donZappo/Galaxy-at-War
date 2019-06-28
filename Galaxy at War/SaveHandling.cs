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


public static class SaveHandling
{
    [HarmonyPatch(typeof(SimGameState), "Rehydrate")]
    public static class SimGameState_Rehydrate_Patch
    {
        static void Postfix(SimGameState __instance, GameInstanceSave gameInstanceSave)
        {
            bool NewGaW = true;
            foreach (string tag in __instance.CompanyTags)
            {
                if (tag.StartsWith("GalaxyAtWarSave{"))
                    NewGaW = false;
            }
            if (NewGaW)
            {
                Core.WarStatus = new WarStatus();
                Core.SystemDifficulty();
                Core.WarTick(true, true);
                SerializeWar();
            }
            else
            {
                DeserializeWar();
                RebuildState();
            }
            var sim = UnityGameInstance.BattleTechGame.Simulation;
           // StarmapMod.SetupRelationPanel();
        }
    }
    internal static void DeserializeWar()
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        Core.WarStatus = JsonConvert.DeserializeObject<WarStatus>(sim.CompanyTags.First(x => x.StartsWith("GalaxyAtWarSave{")).Substring(15));
        LogDebug(">>> Deserialization complete");
        LogDebug($"Size after load: {JsonConvert.SerializeObject(Core.WarStatus).Length / 1024}kb");
    }
    //[HarmonyPatch(typeof(GameInstance), "Load")]
    //public static class GameInstance_Load_Patch
    //{
    //    public static void Postfix()
    //    {
    //        Log("Second");
    //        var sim = UnityGameInstance.BattleTechGame.Simulation;
    //        foreach (var system in sim.StarSystems)
    //        {
    //            Core.CalculateAttackAndDefenseTargets(system);
    //            Core.RefreshContracts(system);
    //        }
    //        StarmapMod.SetupRelationPanel();
    //        //Core.GaW_Notification();
    //    }
    //}


    [HarmonyPatch(typeof(SimGameState), "Dehydrate")]
    public static class SimGameState_Dehydrate_Patch
    {
        public static void Prefix(SimGameState __instance)
        {

            if (Core.WarStatus == null)
            {
                Core.WarStatus = new WarStatus();
                Core.SystemDifficulty();
                Core.WarTick(true, true);
                SerializeWar();
               // StarmapMod.SetupRelationPanel();
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
        LogDebug($"Serializing object size: {JsonConvert.SerializeObject(Core.WarStatus).Length / 1024}kb");
        LogDebug(">>> Serialization complete");
    }

    public static void RebuildState()
    {
        Galaxy_at_War.HotSpots.ExternalPriorityTargets.Clear();
        Galaxy_at_War.HotSpots.FullHomeContendedSystems.Clear();
        Galaxy_at_War.HotSpots.HomeContendedSystems.Clear();
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        var ssDict = sim.StarSystemDictionary;
        Core.SystemDifficulty();
        foreach (var system in Core.WarStatus.systems)
        {
            Core.RefreshContracts(system.starSystem);
        }
        foreach (var faction in Core.WarStatus.ExternalPriorityTargets.Keys)
        {
            Galaxy_at_War.HotSpots.ExternalPriorityTargets.Add(faction, new List<StarSystem>());
            foreach (var system in Core.WarStatus.ExternalPriorityTargets[faction])
                Galaxy_at_War.HotSpots.ExternalPriorityTargets[faction].Add(ssDict[system]);
        }
        foreach (var system in Core.WarStatus.FullHomeContendedSystems)
            Galaxy_at_War.HotSpots.FullHomeContendedSystems.Add(ssDict[system]);
        foreach (var system in Core.WarStatus.HomeContendedSystems)
            Galaxy_at_War.HotSpots.HomeContendedSystems.Add(ssDict[system]);
    }

    public static void ConvertToSave()
    {
        Core.WarStatus.ExternalPriorityTargets.Clear();
        Core.WarStatus.FullHomeContendedSystems.Clear();
        Core.WarStatus.HomeContendedSystems.Clear();
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        foreach (var faction in Galaxy_at_War.HotSpots.ExternalPriorityTargets.Keys)
        {
            Core.WarStatus.ExternalPriorityTargets.Add(faction, new List<string>());
            foreach (var system in Galaxy_at_War.HotSpots.ExternalPriorityTargets[faction])
                Core.WarStatus.ExternalPriorityTargets[faction].Add(system.Def.CoreSystemID);
        }
        foreach (var system in Galaxy_at_War.HotSpots.FullHomeContendedSystems)
            Core.WarStatus.FullHomeContendedSystems.Add(system.Def.CoreSystemID);
        foreach (var system in Galaxy_at_War.HotSpots.HomeContendedSystems)
            Core.WarStatus.HomeContendedSystems.Add(system.Def.CoreSystemID);;
    }

    //****************************************************************************************************
    //Hotkeys*********************************************************************************************
    [HarmonyPatch(typeof(SimGameState), "Update")]
    public static class SimGameState_Update_Patch
    {
        public static void Postfix(SimGameState __instance)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;

            // clear the WarStatus completely
            var hotkeyF10 = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F10);
            if (hotkeyF10)
            {
                foreach (var tag in sim.CompanyTags)
                    if (tag.StartsWith("GalaxyAtWar"))
                        sim.CompanyTags.Remove(tag);

                LogDebug("Setting up new WarStatus");
                Core.WarStatus = new WarStatus();
                Core.WarTick(true, true);
            }

            var hotkeyD = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.D);
            if (hotkeyD)
                using (var writer = new StreamWriter("Mods\\GalaxyAtWar\\SaveDump.json"))
                    writer.Write(JsonConvert.SerializeObject(Core.WarStatus));

            var hotkeyT = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.T);
            if (hotkeyT)
                sim.CompanyTags.Add(new string('=', 50));

            var hotkeyL = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.L);
            if (hotkeyL)
                sim.CompanyTags.Do(LogDebug);
        }
    }
}