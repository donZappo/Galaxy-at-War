using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using static Logger;
using Random = System.Random;
using BattleTech.UI;
using UnityEngine;
using UnityEngine.UI;
using BattleTech.Framework;
using BattleTech.Save;
using BattleTech.Save.SaveGameStructure;
using TMPro;
using fastJSON;
using HBS;
using Error = BestHTTP.SocketIO.Error;
using DG.Tweening;
using BattleTech.Data;

namespace Galaxy_at_War
{
    class PiratesAndLocals
    {
        public static float CurrentPAResources;
        public static List<SystemStatus> FullPirateListSystems = new List<SystemStatus>();

        public static void CorrectResources()
        {
            Core.WarStatus.PirateResources -= Core.WarStatus.TempPRGain;
            if (Core.WarStatus.LastPRGain > Core.WarStatus.TempPRGain)
            {
                Core.WarStatus.PirateResources = Core.WarStatus.MinimumPirateResources;
                Core.WarStatus.MinimumPirateResources *= 1.1f;
            }
            else
            {
                Core.WarStatus.MinimumPirateResources /= 1.1f;
                if (Core.WarStatus.MinimumPirateResources < Core.WarStatus.StartingPirateResources)
                    Core.WarStatus.MinimumPirateResources = Core.WarStatus.StartingPirateResources;
            }
            foreach (var warFaction in Core.WarStatus.warFactionTracker)
            {
                warFaction.AttackResources += warFaction.PirateARLoss;
                warFaction.DefensiveResources += warFaction.PirateDRLoss;
            }
            Core.WarStatus.LastPRGain = Core.WarStatus.TempPRGain;
        }
        public static void DefendAgainstPirates()
        {
            Dictionary<WarFaction, bool> FactionEscalateDefense = new Dictionary<WarFaction, bool>();
            Random rand = new Random();
            foreach (var warFaction in Core.WarStatus.warFactionTracker)
            {
                var DefenseValue = 100 * (warFaction.PirateARLoss + warFaction.PirateDRLoss) /
                    (warFaction.AttackResources + warFaction.DefensiveResources + warFaction.PirateARLoss + warFaction.PirateDRLoss);
                if (DefenseValue > 5)
                    FactionEscalateDefense.Add(warFaction, true);
                else
                    FactionEscalateDefense.Add(warFaction, false);
            }

            List<SystemStatus> TempFullPirateListSystems = new List<SystemStatus>(FullPirateListSystems);
            foreach (var system in TempFullPirateListSystems)
            {
                float PAChange = 0.0f;
                var warFaction = Core.WarStatus.warFactionTracker.Find(x => x.faction == system.owner);
                if (FactionEscalateDefense[warFaction])
                    PAChange = (float)(rand.NextDouble() * (system.PirateActivity - system.PirateActivity / 4) + system.PirateActivity / 4);
                else
                {
                    PAChange = (float)(rand.NextDouble() * (system.PirateActivity / 4));
                    if (system.PirateActivity >= 1)
                        PAChange = Math.Max(PAChange, 1);
                }
                

                if (warFaction.DefensiveResources >= PAChange * system.TotalResources / 100)
                {
                    PAChange = Math.Min(PAChange, system.PirateActivity);
                    system.PirateActivity -= PAChange;
                    warFaction.DefensiveResources -= PAChange * system.TotalResources / 100;
                    warFaction.PirateDRLoss += PAChange * system.TotalResources / 100;
                }
                else
                {
                    PAChange = Math.Min(warFaction.DefensiveResources, system.PirateActivity);
                    system.PirateActivity -= PAChange;
                    warFaction.DefensiveResources -= PAChange * system.TotalResources / 100;
                    warFaction.PirateDRLoss += PAChange * system.TotalResources / 100;
                }

                if (system.PirateActivity == 0)
                {
                    FullPirateListSystems.Remove(system);
                    Core.WarStatus.FullPirateSystems.Remove(system.name);
                }
            }
        }
        public static void PiratesStealResources()
        {
            Core.WarStatus.TempPRGain = 0;
            foreach (var warFaction in Core.WarStatus.warFactionTracker)
            {
                warFaction.PirateARLoss = 0;
                warFaction.PirateDRLoss = 0;
            }

            foreach (var system in FullPirateListSystems)
            {
                Core.WarStatus.PirateResources += system.TotalResources * system.PirateActivity / 100;
                Core.WarStatus.TempPRGain += system.TotalResources * system.PirateActivity / 100;

                var warFaction = Core.WarStatus.warFactionTracker.Find(x => x.faction == system.owner);
                var warFARChange = system.AttackResources * system.PirateActivity / 100;
                warFaction.PirateARLoss += warFARChange;
                warFaction.AttackResources -= warFARChange;

                var warFDRChange = system.DefenseResources * system.PirateActivity / 100;
                warFaction.PirateDRLoss += warFDRChange;
                warFaction.DefensiveResources -= warFDRChange;
            }
        }
        public static void DistributePirateResources()
        {
            Random rand = new Random();
            int i = 0;
            while (CurrentPAResources != 0 && i != 1000)
            {
                var RandSystem = rand.Next(0, Core.WarStatus.systems.Count);
                var systemStatus = Core.WarStatus.systems[RandSystem];
                if (systemStatus.owner == "NoFaction" || Core.Settings.ImmuneToWar.Contains(systemStatus.owner)
                    || Core.WarStatus.HotBox.Contains(systemStatus.name))
                    continue;
                float CurrentPA = systemStatus.PirateActivity;
                float basicPA = Core.WarStatus.PirateFlex / systemStatus.TotalResources;

                float bonusPA = CurrentPA * 0.15f;
                float TotalPA = basicPA + bonusPA;
                //Log(systemStatus.name);
                if (CurrentPA + TotalPA <= 100)
                {
                    if (TotalPA <= CurrentPAResources)
                    {
                        systemStatus.PirateActivity += Math.Min(TotalPA, 100 - systemStatus.PirateActivity);
                        CurrentPAResources -= Math.Min(TotalPA, 100 - systemStatus.PirateActivity);
                        i = 0;
                        if (!Core.WarStatus.FullPirateSystems.Contains(systemStatus.name))
                        {
                            Core.WarStatus.FullPirateSystems.Add(systemStatus.name);
                            FullPirateListSystems.Add(systemStatus);
                        }
                    }
                    else
                    {
                        systemStatus.PirateActivity += Math.Min(CurrentPAResources, 100 - systemStatus.PirateActivity);
                        CurrentPAResources = 0;
                        if (!Core.WarStatus.FullPirateSystems.Contains(systemStatus.name))
                        {
                            Core.WarStatus.FullPirateSystems.Add(systemStatus.name);
                            FullPirateListSystems.Add(systemStatus);
                        }
                    }
                }
                else
                {
                    if (100 - systemStatus.PirateActivity <= CurrentPAResources)
                    {
                        systemStatus.PirateActivity += 100 - systemStatus.PirateActivity;
                        CurrentPAResources -= 100 - systemStatus.PirateActivity;
                        i++;
                        if (!Core.WarStatus.FullPirateSystems.Contains(systemStatus.name))
                        {
                            Core.WarStatus.FullPirateSystems.Add(systemStatus.name);
                            FullPirateListSystems.Add(systemStatus);
                        }
                    }
                    else
                    {
                        systemStatus.PirateActivity += Math.Min(CurrentPAResources, 100 - systemStatus.PirateActivity);
                        CurrentPAResources = 0;
                        if (!Core.WarStatus.FullPirateSystems.Contains(systemStatus.name))
                        {
                            Core.WarStatus.FullPirateSystems.Add(systemStatus.name);
                            FullPirateListSystems.Add(systemStatus);
                        }
                    }
                }
            }
        }
    }
}
