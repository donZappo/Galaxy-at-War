using System;
using BattleTech;
using Harmony;

// ReSharper disable InconsistentNaming

namespace GalaxyatWar
{
    public class Fixes
    {
        // Terra bombs here, singly.. no idea why
        [HarmonyPatch(typeof(StarSystemDef), "GetDifficulty")]
        public class StarSystemDefGetDifficultyPatch
        {
            static bool Prefix(StarSystemDef __instance, SimGameState.SimGameType type)
            {
                try
                {
                    if (__instance.DifficultyList is null
                        || __instance.DifficultyModes is null)
                    {
                        Logger.LogDebug(null);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex);
                }

                if (__instance.DifficultyList?.Count == 0)
                {
                    Logger.LogDebug($"{__instance.Description?.Name} has nooooooooooooooo Difficulties");
                }

                return __instance.DifficultyList?.Count > 0;
            }
        }
    }
}
