using System.Linq;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using UnityEngine;

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
                FileLog.Log(JsonConvert.SerializeObject(
                    Core.WarStatus, new JsonSerializerSettings {ReferenceLoopHandling = ReferenceLoopHandling.Ignore, Formatting = Formatting.Indented}));
            }

            var hotkeyR = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) && Input.GetKeyDown(KeyCode.C);
            if (hotkeyR)
            {
                Sim.CompanyTags
                    .Where(tag => tag.StartsWith("GalaxyAtWar"))
                    .Do(tag => Sim.CompanyTags.Remove(tag));

                Core.WarStatus = null;

                FileLog.Log(Sim.CompanyTags.ToString());
            }
        }
    }
}
