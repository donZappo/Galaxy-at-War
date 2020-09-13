// ReSharper disable InconsistentNaming

using System;
using BattleTech;
using Harmony;
using UnityEngine;

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
            var hotkeyT = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                          (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.T);
            if (hotkeyT)
            {
                const int loops = 100;
                Logger.LogDebug($"Running {loops} full ticks.");
                for (var i = 0; i < loops; i++)
                {
                    Logger.LogDebug("Tick " + $"{i,5}...");
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
