using System.IO;
using System.Linq;
using BattleTech;
using BattleTech.Save.Test;
using Harmony;
using Newtonsoft.Json;
using UnityEngine;
using static Logger;
using BattleTech.Save;

public static class SaveHandling
{
    [HarmonyPatch(typeof(SimGameState), "_OnAttachUXComplete")]
    public static class SimGameState__OnAttachUXComplete_Patch
    {
        public static void Postfix()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Core.WarStatus == null)
            {
                Core.WarStatus = new WarStatus();
                Core.WarTick();

                //Galaxy_at_War.HotSpots.ProcessHotSpots();
                //var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
                //sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
                StarmapMod.SetupRelationPanel();
                //Traverse.Create(sim.CurSystem).Property("CurMaxContracts").SetValue(8f);
                SerializeWar();
            }
            else
            {
                foreach (var system in sim.StarSystems)
                {
                    Core.CalculateAttackTargets(system);
                    Core.CalculateDefenseTargets(system);
                    Core.RefreshNeighbors(system);
                    Core.RefreshContracts(system);
                }
                Galaxy_at_War.HotSpots.ProcessHotSpots();
            }

            //Log("Setting up new WarStatus");
            //Core.WarStatus = new WarStatus();
            //Core.WarTick();
            //Galaxy_at_War.HotSpots.ProcessHotSpots();
            //StarmapMod.SetupRelationPanel();
            //var cmdCenter = UnityGameInstance.BattleTechGame.Simulation.RoomManager.CmdCenterRoom;
            //sim.CurSystem.GenerateInitialContracts(() => Traverse.Create(cmdCenter).Method("OnContractsFetched"));
        }
    }

    [HarmonyPatch(typeof(SimGameState), "Rehydrate")]
    public static class SimGameState_Rehydrate_Patch
    {
        static void Postfix(SimGameState __instance, GameInstanceSave gameInstanceSave)
        {
            DeserializeWar();
        }
    }
    internal static void DeserializeWar()
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        //foreach (var tag in sim.CompanyTags)
        //    LogDebug(tag);

        Core.WarStatus = JsonConvert.DeserializeObject<WarStatus>(sim.CompanyTags.First(x => x.StartsWith("GalaxyAtWarSave{")).Substring(15));
        LogDebug(">>> Deserialization complete");
        LogDebug($"Size after load: {JsonConvert.SerializeObject(Core.WarStatus).Length / 1024}kb");
    }

    [HarmonyPatch(typeof(SimGameState), "Dehydrate")]
    public static class SimGameState_Dehydrate_Patch
    {
        public static void Prefix()
        {
            SerializeWar();
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

    //[HarmonyPatch(typeof(SerializableReferenceContainer), "Load")]
    //public static class SerializableReferenceContainer_Load_Patch
    //{
    //    // get rid of tags before loading because vanilla behaviour doesn't purge them
    //    public static void Prefix()
    //    {
    //        var sim = UnityGameInstance.BattleTechGame.Simulation;
    //        if (sim == null) return;
    //        LogDebug("Clearing GaW save tags");
    //        sim.CompanyTags.Where(tag => tag.StartsWith("GalaxyAtWar")).Do(x => sim.CompanyTags.Remove(x));
    //    }
    //}



    


    //Hotkeys
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
                Core.WarTick();
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