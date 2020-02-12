using System.Linq;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace GalaxyatWar
{
    [HarmonyPatch(typeof(SimGameState), "Update")]
    public static class SimGameState_Update_Patch
    {
        private static readonly SimGameState Sim = UnityGameInstance.BattleTechGame.Simulation;

        public static void Postfix()
        {
            var hotkeyT = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) && Input.GetKeyDown(KeyCode.T);
            if (hotkeyT)
            {
                Logger.LogDebug(JsonConvert.SerializeObject(
                    Core.WarStatus, new JsonSerializerSettings {ReferenceLoopHandling = ReferenceLoopHandling.Ignore, Formatting = Formatting.Indented}));
            }

            var hotkeyC = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) && Input.GetKeyDown(KeyCode.C);
            if (hotkeyC)
            {
                Sim.CompanyTags
                    .Where(tag => tag.StartsWith("GalaxyAtWar"))
                    .Do(tag => Sim.CompanyTags.Remove(tag));

                Core.WarStatus = null;
            }
        }
    }
}
