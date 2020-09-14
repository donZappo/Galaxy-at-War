using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BattleTech;
using BattleTech.Framework;
using BattleTech.UI;
using Harmony;
using Newtonsoft.Json;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace GalaxyatWar
{
    [HarmonyPatch(typeof(UnityGameInstance), "Update")]
    public static class SimGameStateUpdatePatch
    {
        //public static void Postfix()
        //{
        //    var hotkeyT = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.T);
        //    if (hotkeyT)
        //    {
        //        Logger.LogDebug(JsonConvert.SerializeObject(
        //            Mod.WarStatus, new JsonSerializerSettings {ReferenceLoopHandling = ReferenceLoopHandling.Ignore, Formatting = Formatting.Indented}));
        //    }

        //    var hotkeyC = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.C);
        //    if (hotkeyC)
        //    {
        //        Sim.CompanyTags
        //            .Where(tag => tag.StartsWith("GalaxyAtWar"))
        //            .Do(tag => Sim.CompanyTags.Remove(tag));

        //        Mod.WarStatus = null;
        //    }
        //}

        public static void Postfix()
        {
            var hotkeyC = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.C);
            if (hotkeyC)
            {
                try
                {
                    Logger.LogDebug("Hotkey C");
                    var contracts = new List<Contract>();
                    contracts.Add(Contracts.GenerateContract(Globals.Sim.CurSystem, 2, 2, "Locals", new List<string> {"AuriganPirates"}));
                    contracts.Add(Contracts.GenerateContract(Globals.Sim.CurSystem, 4, 4, "Locals"));
                    contracts.Add(Contracts.GenerateContract(Globals.Sim.CurSystem, 6, 6, "Locals", new List<string>{"Steiner"}));
                    contracts.Add(Contracts.GenerateContract(Globals.Sim.StarSystems.GetRandomElement(), 6, 6, "Kurita"));
                    contracts.Add(Contracts.GenerateContract(Globals.Sim.StarSystems.GetRandomElement(), 8, 8, "Steiner"));
                    var deployment = Contracts.GenerateContract(Globals.Sim.StarSystems.GetRandomElement(), 10, 10, "TaurianConcordat");
                    deployment.Override.contractDisplayStyle = ContractDisplayStyle.BaseCampaignStory;
                    contracts.Add(deployment);
                    
                    //for (var j = 0; j < 2; j++)
                    //{
                    //    for (var i = 2; i <= 10; i += 2)
                    //    {
                    //        var system = Globals.Sim.StarSystems.GetRandomElement();
                    //        var contract = Contracts.GenerateContract(system, i, i, "Davion", new List<string> {"Kurita", "Steiner"});
                    //        if (contract == null)
                    //        {
                    //            Logger.LogDebug($"Couldn't find contract for {system.Name,-20} system is {system.Def.GetDifficulty(SimGameState.SimGameType.CAREER),-2} and contract attempt is rank {i,-2}");
                    //            continue;
                    //        }
                    //
                    //        contract.Override.contractDisplayStyle = ContractDisplayStyle.BaseCampaignStory;
                    //        contracts.Add(contract);
                    //    }
                    //}

                    Globals.Sim.CurSystem.activeSystemBreadcrumbs = contracts;
                    Globals.Sim.CurSystem.activeSystemContracts = contracts;
                    var cmdCenter = Globals.Sim.RoomManager.CmdCenterRoom;
                    cmdCenter.contractsWidget.ListContracts(contracts, cmdCenter.contractDisplayAutoSelect);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }

            var hotkeyT = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                          (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.T);
            if (hotkeyT)
            {
                const int loops = 100;
                Logger.LogDebug($"Running {loops} full ticks.");
                for (var i = 0; i < loops; i++)
                {
                    Logger.LogDebug("Tick " + $"{i,3}...");
                    try
                    {
                        WarTick.Tick(true, true);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
                    }
                }
            }
        }
    }
}
