

// ReSharper disable InconsistentNaming

namespace GalaxyatWar
{
    //[HarmonyPatch(typeof(SimGameState), "Update")]
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
    }

    //[HarmonyPatch(typeof(CombatGameState), "Update")]
    public static class CombatGameStateUpdatePatch
    {
        //public static void Postfix()
        //{
        //    var hotkeyM = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.M);
        //    if (hotkeyM)
        //    {
        //        Logger.LogDebug("M");
        //        foreach (var combatant in UnityGameInstance.BattleTechGame.Combat.allCombatants)
        //        {
        //            if (combatant is Mech mech)
        //            {
        //                Logger.LogDebug($"mech {mech.DisplayName}");
        //                mech.GetTags().Do(Logger.LogDebug);
        //            }

        //            if (combatant is Vehicle vee)
        //            {
        //                Logger.LogDebug($"vee {vee.DisplayName}");
        //                vee.GetTags().Do(Logger.LogDebug);
        //            }

        //            if (combatant is Turret turret)
        //            {
        //                Logger.LogDebug($"turret {turret.DisplayName}");
        //                turret.GetTags().Do(Logger.LogDebug);
        //            }
        //        }
        //    }
        //}
    }
}
