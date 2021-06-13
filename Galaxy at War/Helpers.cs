using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using BattleTech;
using BattleTech.Framework;
using Harmony;
using UnityEngine;
using static GalaxyatWar.Logger;

// ReSharper disable StringLiteralTypo
// ReSharper disable ClassNeverInstantiated.Global

namespace GalaxyatWar
{
    public static class Helpers
    {
        internal static void CopySettingsToState()
        {
            if (Globals.Settings.ISMCompatibility)
                Globals.IncludedFactions = new List<string>(Globals.Settings.IncludedFactions_ISM);
            else
                Globals.IncludedFactions = new List<string>(Globals.Settings.IncludedFactions);

            Globals.OffensiveFactions = Globals.IncludedFactions.Except(Globals.Settings.DefensiveFactions).ToList();
        }

        public static void SystemDifficulty()
        {
            var factionSystems = new Dictionary<string, List<SystemStatus>>();
            foreach (var system in Globals.WarStatusTracker.Systems)
            {
                //Define the original owner of the system for revolt purposes.
                if (system.OriginalOwner == null)
                    system.OriginalOwner = system.Owner;

                if (!factionSystems.ContainsKey(system.OriginalOwner))
                {
                    factionSystems.Add(system.OriginalOwner, new List<SystemStatus>{ system });
                }
                else
                {
                    factionSystems[system.OriginalOwner].Add(system);
                }
            }

            foreach (var faction in factionSystems.Keys)
            {
                {
                    var systemCount = factionSystems[faction].Count;
                    var difficultyCutoff = systemCount / 10;
                    var i = 0;
                    var orderedSystems = factionSystems[faction].OrderBy(v => v.TotalResources);
                    foreach (var systemStatus in orderedSystems)
                    {
                        try
                        {
                            if (Globals.Settings.ChangeDifficulty && !systemStatus.StarSystem.Tags.Contains("planet_start_world"))
                            {
                                Globals.Sim.Constants.Story.ContractDifficultyMod = 0;
                                Globals.Sim.CompanyStats.Set<float>("Difficulty", 0);
                                if (i <= difficultyCutoff)
                                {
                                    systemStatus.DifficultyRating = 1;
                                }

                                if (i <= difficultyCutoff * 2 && i > difficultyCutoff)
                                {
                                    systemStatus.DifficultyRating = 2;
                                }

                                if (i <= difficultyCutoff * 3 && i > 2 * difficultyCutoff)
                                {
                                    systemStatus.DifficultyRating = 3;
                                }

                                if (i <= difficultyCutoff * 4 && i > 3 * difficultyCutoff)
                                {
                                    systemStatus.DifficultyRating = 4;
                                }

                                if (i <= difficultyCutoff * 5 && i > 4 * difficultyCutoff)
                                {
                                    systemStatus.DifficultyRating = 5;
                                }

                                if (i <= difficultyCutoff * 6 && i > 5 * difficultyCutoff)
                                {
                                    systemStatus.DifficultyRating = 6;
                                }

                                if (i <= difficultyCutoff * 7 && i > 6 * difficultyCutoff)
                                {
                                    systemStatus.DifficultyRating = 7;
                                }

                                if (i <= difficultyCutoff * 8 && i > 7 * difficultyCutoff)
                                {
                                    systemStatus.DifficultyRating = 8;
                                }

                                if (i <= difficultyCutoff * 9 && i > 8 * difficultyCutoff)
                                {
                                    systemStatus.DifficultyRating = 9;
                                }

                                if (i > 9 * difficultyCutoff)
                                {
                                    systemStatus.DifficultyRating = 10;
                                }

                                i++;

                                var amount = systemStatus.DifficultyRating;
                                var difficultyList = new List<int> { amount, amount };
                                systemStatus.StarSystem.Def.DifficultyList = difficultyList;
                                systemStatus.StarSystem.Def.DefaultDifficulty = amount;
                                Logger.LogDebug("System: " + systemStatus.Name + ", Faction: " + systemStatus.StarSystem.OwnerDef.Name +
                                    ", Difficulty: " + systemStatus.StarSystem.Def.DefaultDifficulty);
                            }
                            else
                            {
                                systemStatus.DifficultyRating = systemStatus.StarSystem.Def.DefaultDifficulty;
                                i++;
                            }

                            if (systemStatus.StarSystem.Def.OwnerValue.Name != "NoFaction" && systemStatus.StarSystem.Def.SystemShopItems.Count == 0)
                            {
                                var tempList = new List<string>
                        {
                            "itemCollection_minor_Locals"
                        };
                                systemStatus.StarSystem.Def.SystemShopItems = tempList;
                                if (Globals.Sim.CurSystem.Name == systemStatus.StarSystem.Def.Description.Name)
                                {
                                    var refreshShop = Shop.RefreshType.RefreshIfEmpty;
                                    systemStatus.StarSystem.SystemShop.Rehydrate(Globals.Sim, systemStatus.StarSystem, systemStatus.StarSystem.Def.SystemShopItems, refreshShop,
                                        Shop.ShopType.System);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Error(ex);
                        }
                    }
                }
            }
        }

        public static float GetTotalAttackResources(StarSystem system)
        {
            float result = 0;
            if (system.Tags.Contains("planet_industry_poor"))
                result += Globals.Settings.planet_industry_poor;
            if (system.Tags.Contains("planet_industry_mining"))
                result += Globals.Settings.planet_industry_mining;
            if (system.Tags.Contains("planet_industry_rich"))
                result += Globals.Settings.planet_industry_rich;
            if (system.Tags.Contains("planet_industry_manufacturing"))
                result += Globals.Settings.planet_industry_manufacturing;
            if (system.Tags.Contains("planet_industry_research"))
                result += Globals.Settings.planet_industry_research;
            if (system.Tags.Contains("planet_other_starleague"))
                result += Globals.Settings.planet_other_starleague;

            return result;
        }

        public static float GetTotalDefensiveResources(StarSystem system)
        {
            float result = 0;
            if (system.Tags.Contains("planet_industry_agriculture"))
                result += Globals.Settings.planet_industry_agriculture;
            if (system.Tags.Contains("planet_industry_aquaculture"))
                result += Globals.Settings.planet_industry_aquaculture;
            if (system.Tags.Contains("planet_other_capital"))
                result += Globals.Settings.planet_other_capital;
            if (system.Tags.Contains("planet_other_megacity"))
                result += Globals.Settings.planet_other_megacity;
            if (system.Tags.Contains("planet_pop_large"))
                result += Globals.Settings.planet_pop_large;
            if (system.Tags.Contains("planet_pop_medium"))
                result += Globals.Settings.planet_pop_medium;
            if (system.Tags.Contains("planet_pop_none"))
                result += Globals.Settings.planet_pop_none;
            if (system.Tags.Contains("planet_pop_small"))
                result += Globals.Settings.planet_pop_small;
            if (system.Tags.Contains("planet_other_hub"))
                result += Globals.Settings.planet_other_hub;
            if (system.Tags.Contains("planet_other_comstar"))
                result += Globals.Settings.planet_other_comstar;
            return result;
        }

        internal static void CalculateComstarSupport()
        {
            if (Globals.WarStatusTracker.ComstarCycle < Globals.Settings.GaW_Police_SupportTime)
            {
                Globals.WarStatusTracker.ComstarCycle++;
                return;
            }

            Globals.WarStatusTracker.ComstarCycle = 1;
            var warFactionList = new List<WarFaction>();
            var omit = Globals.Settings.DefensiveFactions.Concat(Globals.Settings.HyadesPirates)
                .Concat(Globals.Settings.NoOffensiveContracts).Concat(new[] {"AuriganPirates"}).ToList();
            foreach (var warFarTemp in Globals.WarStatusTracker.WarFactionTracker)
            {
                warFarTemp.ComstarSupported = false;
                if (omit.Contains(warFarTemp.FactionName))
                    continue;
                warFactionList.Add(warFarTemp);
            }

            var warFactionHolder = warFactionList.OrderBy(x => x.TotalSystemsChanged).ElementAt(0);
            var warFactionListTrimmed = warFactionList.FindAll(x => x.TotalSystemsChanged == warFactionHolder.TotalSystemsChanged);
            warFactionListTrimmed.Shuffle();
            var warFaction = Globals.WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == warFactionListTrimmed.ElementAt(0).FactionName);
            warFaction.ComstarSupported = true;
            Globals.WarStatusTracker.ComstarAlly = warFaction.FactionName;
            var factionDef = Globals.Sim.GetFactionDef(warFaction.FactionName);
            if (Globals.Settings.GaW_PoliceSupport && !factionDef.Allies.Contains(Globals.Settings.GaW_Police))
            {
                var tempList = factionDef.Allies.ToList();
                tempList.Add(Globals.Settings.GaW_Police);
                Traverse.Create(factionDef).Property("Allies").SetValue(tempList.ToArray());
            }

            if (Globals.Settings.GaW_PoliceSupport && factionDef.Enemies.Contains(Globals.Settings.GaW_Police))
            {
                var tempList = factionDef.Enemies.ToList();
                tempList.Remove(Globals.Settings.GaW_Police);
                Traverse.Create(factionDef).Property("Enemies").SetValue(tempList.ToArray());
            }
        }

        public static void CalculateAttackAndDefenseTargets(StarSystem starSystem)
        {
            var warFac = Globals.WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == starSystem.OwnerValue.Name);
            if (warFac == null)
                return;
            var isFlashpointSystem = Globals.WarStatusTracker.FlashpointSystems.Contains(starSystem.Name);
            var warSystem = Globals.WarStatusTracker.Systems.Find(x => x.StarSystem == starSystem);
            var ownerNeighborSystems = warSystem.NeighborSystems;
            ownerNeighborSystems.Clear();
            if (Globals.Sim.Starmap.GetAvailableNeighborSystem(starSystem).Count == 0)
                return;
            var availableNeighbors = Globals.Sim.Starmap.GetAvailableNeighborSystem(starSystem)
                .Where(x => !Globals.Settings.ImmuneToWar.Contains(x.OwnerValue.Name));
            foreach (var neighborSystem in availableNeighbors)
            {
                if (neighborSystem.OwnerValue.Name != starSystem.OwnerValue.Name &&
                    !isFlashpointSystem)
                {
                    var neighborSystemStatus = Globals.WarStatusTracker.Systems.Find(s => s.StarSystem == neighborSystem);
                    if (!warFac.AttackTargets.ContainsKey(neighborSystem.OwnerValue.Name))
                    {
                        var tempList = new List<SystemStatus>
                        {
                            neighborSystemStatus
                        };
                        warFac.AttackTargets.Add(neighborSystem.OwnerValue.Name, tempList);
                    }
                    else if (warFac.AttackTargets.ContainsKey(neighborSystem.OwnerValue.Name)
                             && !warFac.AttackTargets[neighborSystem.OwnerValue.Name].Contains(neighborSystemStatus))
                    {
                        // bug should Locals be here?
                        warFac.AttackTargets[neighborSystem.OwnerValue.Name].Add(neighborSystemStatus);
                    }

                    //if (!warFac.defenseTargets.Contains(starSystem.Name))
                    //{
                    //    warFac.defenseTargets.Add(starSystem.Name);
                    //}
                    if (!warFac.AdjacentFactions.Contains(starSystem.OwnerValue.Name) && !Globals.Settings.DefensiveFactions.Contains(starSystem.OwnerValue.Name))
                        warFac.AdjacentFactions.Add(starSystem.OwnerValue.Name);
                }

                RefreshNeighbors(ownerNeighborSystems, neighborSystem);
            }
        }

        public static void RefreshNeighbors(Dictionary<string, int> starSystem, StarSystem neighborSystem)
        {
            if (Globals.WarStatusTracker.FlashpointSystems.Contains(neighborSystem.Name))
                return;

            var neighborSystemOwner = neighborSystem.OwnerValue.Name;

            if (starSystem.ContainsKey(neighborSystemOwner))
                starSystem[neighborSystemOwner] += 1;
            else
                starSystem.Add(neighborSystemOwner, 1);
        }

        //public static void CalculateDefenseTargets(StarSystem starSystem)
        //{
        //    foreach (var neighborSystem in Globals.Sim.Starmap.GetAvailableNeighborSystem(starSystem))
        //    {
        //        var warFac = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == starSystem.Owner);
        //        if (warFac == null)
        //        {
        //            return;
        //        }

        //        if ((neighborSystem.Owner != starSystem.Owner) && !warFac.defenseTargets.Contains(starSystem))
        //        {
        //            warFac.defenseTargets.Add(starSystem);
        //        }
        //    }
        //}


        public static void CalculateDefensiveSystems()
        {
            foreach (var warFaction in Globals.WarStatusTracker.WarFactionTracker)
                warFaction.DefenseTargets.Clear();

            foreach (var system in Globals.WarStatusTracker.Systems)
            {
                if (Globals.WarStatusTracker.FlashpointSystems.Contains(system.Name))
                    continue;

                var totalInfluence = system.InfluenceTracker.Values.Sum();
                //if ((totalInfluence - 100) / 100 > Globals.Settings.SystemDefenseCutoff)
                if (totalInfluence - 100 > 0)
                {
                    var warFaction = Globals.WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == system.Owner);
                    warFaction.DefenseTargets.Add(system);
                }
            }

            //foreach (var warFaction in Globals.WarStatusTracker.warFactionTracker)
            //{
            //    Log("=============");
            //    Log(warFaction.faction);
            //    foreach (var system in warFaction.defenseTargets)
            //        Log("   " + system);
            //}
        }


        public static void ChangeSystemOwnership(StarSystem system, string faction, bool forceFlip)
        {
            if (faction != system.OwnerValue.Name || forceFlip)
            {
                // todo test
                if (Globals.Settings.ImmuneToWar.Contains(faction))
                {
                    return;
                }

                var oldFaction = system.OwnerValue;
                if ((oldFaction.Name == "NoFaction" || oldFaction.Name == "Locals") && system.Def.Tags.Contains("planet_region_hyadesrim") && !forceFlip)
                {
                    if (Globals.WarStatusTracker.HyadesRimGeneralPirateSystems.Contains(system.Name))
                        Globals.WarStatusTracker.HyadesRimGeneralPirateSystems.Remove(system.Name);
                    Globals.WarStatusTracker.HyadesRimsSystemsTaken++;
                }

                if (system.Def.Tags.Contains(Globals.Settings.FactionTags[oldFaction.Name]))
                    system.Def.Tags.Remove(Globals.Settings.FactionTags[oldFaction.Name]);
                system.Def.Tags.Add(Globals.Settings.FactionTags[faction]);

                if (!Globals.WarStatusTracker.AbandonedSystems.Contains(system.Name))
                {
                    if (system.Def.SystemShopItems.Count != 0)
                    {
                        var tempList = system.Def.SystemShopItems;
                        tempList.Add(Globals.Settings.FactionShops[system.OwnerValue.Name]);
                        system.Def.SystemShopItems = tempList;
                    }

                    if (system.Def.FactionShopItems != null)
                    {
                        system.Def.FactionShopOwnerValue = Globals.FactionValues.Find(x => x.Name == faction);
                        system.Def.factionShopOwnerID = faction;
                        var factionShops = system.Def.FactionShopItems;
                        if (factionShops.Contains(Globals.Settings.FactionShopItems[system.Def.OwnerValue.Name]))
                            factionShops.Remove(Globals.Settings.FactionShopItems[system.Def.OwnerValue.Name]);
                        factionShops.Add(Globals.Settings.FactionShopItems[faction]);
                        system.Def.FactionShopItems = factionShops;
                    }
                }

                var systemStatus = Globals.WarStatusTracker.Systems.Find(x => x.StarSystem == system);
                var oldOwner = systemStatus.Owner;
                systemStatus.Owner = faction;
                system.Def.factionShopOwnerID = faction;
                system.Def.OwnerValue = Globals.FactionValues.Find(x => x.Name == faction);

                //Change the Kill List for the factions.
                var totalAR = GetTotalAttackResources(system);
                var totalDr = GetTotalDefensiveResources(system);

                var wfWinner = Globals.WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == faction);
                wfWinner.GainedSystem = true;
                wfWinner.MonthlySystemsChanged += 1;
                wfWinner.TotalSystemsChanged += 1;
                if (Globals.Settings.DefendersUseARforDR && Globals.Settings.DefensiveFactions.Contains(wfWinner.FactionName))
                {
                    wfWinner.DefensiveResources += totalAR;
                    wfWinner.DefensiveResources += totalDr;
                }
                else
                {
                    wfWinner.AttackResources += totalAR;
                    wfWinner.DefensiveResources += totalDr;
                }

                var wfLoser = Globals.WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == oldFaction.Name);
                wfLoser.LostSystem = true;
                wfLoser.MonthlySystemsChanged -= 1;
                wfLoser.TotalSystemsChanged -= 1;
                RemoveAndFlagSystems(wfLoser, system);
                if (Globals.Settings.DefendersUseARforDR && Globals.Settings.DefensiveFactions.Contains(wfWinner.FactionName))
                {
                    wfLoser.DefensiveResources -= totalAR;
                    wfLoser.DefensiveResources -= totalDr;
                }
                else
                {
                    wfLoser.AttackResources -= totalAR;
                    wfLoser.DefensiveResources -= totalDr;
                }

                if (wfLoser.AttackResources < 0)
                    wfLoser.AttackResources = 0;
                if (wfLoser.DefensiveResources < 0)
                    wfLoser.DefensiveResources = 0;

                if (!Globals.WarStatusTracker.SystemChangedOwners.Contains(system.Name))
                    Globals.WarStatusTracker.SystemChangedOwners.Add(system.Name);

                if (forceFlip)
                {
                    RecalculateSystemInfluence(systemStatus, faction, oldOwner);
                    systemStatus.PirateActivity = 0;
                }

                foreach (var neighbor in Globals.Sim.Starmap.GetAvailableNeighborSystem(system))
                {
                    if (!Globals.WarStatusTracker.SystemChangedOwners.Contains(neighbor.Name))
                        Globals.WarStatusTracker.SystemChangedOwners.Add(neighbor.Name);
                }
            }
        }

        public static void ChangeDeathListFromAggression(StarSystem system, string faction, string oldFaction)
        {
            var totalAR = GetTotalAttackResources(system);
            var totalDr = GetTotalDefensiveResources(system);
            var systemValue = totalAR + totalDr;
            var killListDelta = Math.Max(10, systemValue);
            // g - commented out 9/9/20 - deathListTracker will have all factions now
            //if (Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == oldFaction) == null)
            //    return;

            try
            {
                var factionTracker = Globals.WarStatusTracker.DeathListTracker.Find(x => x.Faction == oldFaction);
                if (factionTracker.DeathList.ContainsKey(faction))
                {
                    if (factionTracker.DeathList[faction] < 50)
                        factionTracker.DeathList[faction] = 50;

                    factionTracker.DeathList[faction] += killListDelta;
                }
                else
                {
                    LogDebug($"factionTracker missing faction {faction}, ignoring.");
                }
            }
            catch (Exception ex)
            {
                Error(ex);
            }

            try
            {
                //Allies are upset that their friend is being beaten up.
                if (!Globals.Settings.DefensiveFactions.Contains(oldFaction))
                {
                    foreach (var ally in Globals.Sim.GetFactionDef(oldFaction).Allies)
                    {
                        if (!Globals.IncludedFactions.Contains(ally) || faction == ally || Globals.WarStatusTracker.DeathListTracker.Find(x => x.Faction == ally) == null)
                            continue;
                        var factionAlly = Globals.WarStatusTracker.DeathListTracker.Find(x => x.Faction == ally);
                        factionAlly.DeathList[faction] += killListDelta / 2;
                    }

                    //Enemies of the target faction are happy with the faction doing the beating. 
                    foreach (var enemy in Globals.Sim.GetFactionDef(oldFaction).Enemies)
                    {
                        if (!Globals.IncludedFactions.Contains(enemy) || enemy == faction || Globals.WarStatusTracker.DeathListTracker.Find(x => x.Faction == enemy) == null)
                            continue;
                        var factionEnemy = Globals.WarStatusTracker.DeathListTracker.Find(x => x.Faction == enemy);
                        factionEnemy.DeathList[faction] -= killListDelta / 2;
                    }
                }
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        public static void CalculateHatred()
        {
            foreach (var faction in Globals.WarStatusTracker.WarFactionTracker)
            {
                var attackCount = new Dictionary<string, int>();
                foreach (var target in faction.AttackTargets)
                {
                    if (Globals.Settings.DefensiveFactions.Contains(target.Key) || Globals.Settings.ImmuneToWar.Contains(target.Key))
                        continue;
                    attackCount.Add(target.Key, target.Value.Count);
                }

                var i = 0;
                var topHalf = attackCount.Count / 2;
                foreach (var attackTarget in attackCount.OrderByDescending(x => x.Value))
                {
                    var warFaction = Globals.WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == attackTarget.Key);
                    if (i < topHalf)
                        warFaction.IncreaseAggression[warFaction.FactionName] = true;
                    else
                        warFaction.IncreaseAggression[warFaction.FactionName] = false;
                    i++;
                }
            }
        }

        private static void RemoveAndFlagSystems(WarFaction oldOwner, StarSystem system)
        {
            //OldOwner.defenseTargets.Remove(system.Name);
            if (!Globals.WarStatusTracker.SystemChangedOwners.Contains(system.Name))
                Globals.WarStatusTracker.SystemChangedOwners.Add(system.Name);
            foreach (var neighborSystem in Globals.Sim.Starmap.GetAvailableNeighborSystem(system))
            {
                var systemStatus = Globals.WarStatusTracker.Systems.Find(s => s.StarSystem == neighborSystem);
                var attackTargets = Globals.WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == neighborSystem.OwnerValue.Name).AttackTargets;
                if (attackTargets.Keys.Contains(oldOwner.FactionName) && attackTargets[oldOwner.FactionName].Contains(systemStatus))
                    attackTargets[oldOwner.FactionName].Remove(systemStatus);
            }
        }

        internal static void UpdateInfluenceFromAttacks(bool checkForSystemChange)
        {
            if (checkForSystemChange)
                Globals.WarStatusTracker.LostSystems.Clear();

            //LogDebug($"Updating influence for {Globals.WarStatusTracker.SystemStatuses.Count.ToString()} systems");
            foreach (var systemStatus in Globals.WarStatusTracker.Systems)
            {
                var tempDict = new Dictionary<string, float>();
                var totalInfluence = systemStatus.InfluenceTracker.Values.Sum();
                var highest = 0f;
                var highestFaction = systemStatus.Owner;
                foreach (var kvp in systemStatus.InfluenceTracker)
                {
                    tempDict.Add(kvp.Key, kvp.Value / totalInfluence * 100);
                    if (kvp.Value > highest)
                    {
                        highest = kvp.Value;
                        highestFaction = kvp.Key;
                    }
                }

                systemStatus.InfluenceTracker = tempDict;
                var diffStatus = systemStatus.InfluenceTracker[highestFaction] - systemStatus.InfluenceTracker[systemStatus.Owner];
                var starSystem = systemStatus.StarSystem;

                if (highestFaction != systemStatus.Owner &&
                    !Globals.WarStatusTracker.FlashpointSystems.Contains(systemStatus.Name) &&
                    diffStatus > Globals.Settings.TakeoverThreshold &&
                    !Globals.WarStatusTracker.HotBox.Contains(systemStatus) &&
                    (!Globals.Settings.DefensiveFactions.Contains(highestFaction) || highestFaction == "Locals") &&
                    !Globals.Settings.ImmuneToWar.Contains(starSystem.OwnerValue.Name))
                {
                    if (!systemStatus.Contested)
                    {
                        systemStatus.Contested = true;
                        ChangeDeathListFromAggression(starSystem, highestFaction, starSystem.OwnerValue.Name);
                    }
                    else if (checkForSystemChange)
                    {
                        ChangeSystemOwnership(starSystem, highestFaction, false);
                        systemStatus.Contested = false;
                        Globals.WarStatusTracker.LostSystems.Add(starSystem.Name);
                    }
                }

                //Local Government can take a system.
                if (systemStatus.Owner != "Locals" && systemStatus.OriginalOwner == "Locals" && highestFaction == "Locals" && systemStatus.InfluenceTracker[highestFaction] >= 75)
                {
                    ChangeSystemOwnership(starSystem, "Locals", true);
                    systemStatus.Contested = false;
                    Globals.WarStatusTracker.LostSystems.Add(starSystem.Name);
                }
            }

            CalculateHatred();
            foreach (var deathListTracker in Globals.WarStatusTracker.DeathListTracker)
            {
                AdjustDeathList(deathListTracker, false);
            }
        }

        public static string MonthlyWarReport()
        {
            var combinedString = "";
            foreach (var faction in Globals.IncludedFactions)
            {
                var warFaction = Globals.WarStatusTracker.WarFactionTracker.Find(x => x.FactionName == faction);
                combinedString = combinedString + "<b><u>" + Globals.Settings.FactionNames[faction] + "</b></u>\n";
                var summaryString = "Monthly Change in Systems: " + warFaction.MonthlySystemsChanged + "\n";
                var summaryString2 = "Overall Change in Systems: " + warFaction.TotalSystemsChanged + "\n\n";

                combinedString = combinedString + summaryString + summaryString2;
                warFaction.MonthlySystemsChanged = 0;
            }

            combinedString = combinedString.TrimEnd('\n');
            return combinedString;
        }

        public static void RefreshContractsEmployersAndTargets(SystemStatus systemStatus)
        {
            var starSystem = systemStatus.StarSystem;
            //LogDebug("RefreshContracts for " + starSystem.Name);
            if (Globals.WarStatusTracker.HotBox.Contains(systemStatus) || starSystem.Tags.Contains("planet_region_hyadesrim") &&
                (starSystem.OwnerDef.Name == "Locals" || starSystem.OwnerDef.Name == "NoFaction"))
            {
                LogDebug("Skipping HotBox or THR Neutrals");
                return;
            }

            var contractEmployers = starSystem.Def.contractEmployerIDs;
            var contractTargets = starSystem.Def.contractTargetIDs;
            var owner = starSystem.OwnerValue;
            contractEmployers.Clear();
            contractTargets.Clear();

            contractEmployers.Add("Locals");
            contractTargets.Add("Locals");

            if (starSystem.Tags.Contains("planet_other_pirate") || Globals.WarStatusTracker.AbandonedSystems.Contains(starSystem.Name))
            {
                contractEmployers.Add("AuriganPirates");
                contractTargets.Add("AuriganPirates");
            }

            if (!Equals(owner, Globals.FactionValues.FirstOrDefault(f => f.Name == "NoFaction")) &&
                !Equals(owner, Globals.FactionValues.FirstOrDefault(f => f.Name == "Locals")))
            {
                contractEmployers.Add(owner.Name);
                contractTargets.Add(owner.Name);
            }

            if (Globals.Settings.GaW_PoliceSupport && Globals.WarStatusTracker.ComstarAlly == owner.Name)
            {
                contractEmployers.Add(Globals.Settings.GaW_Police);
                contractTargets.Add(Globals.Settings.GaW_Police);
            }

            var neighborSystems = systemStatus.NeighborSystems;
            foreach (var systemNeighbor in neighborSystems.Keys)
            {
                if (Globals.Settings.ImmuneToWar.Contains(systemNeighbor) || Globals.Settings.DefensiveFactions.Contains(systemNeighbor))
                    continue;

                if (!contractEmployers.Contains(systemNeighbor))
                    contractEmployers.Add(systemNeighbor);

                if (!contractTargets.Contains(systemNeighbor))
                    contractTargets.Add(systemNeighbor);
            }

            if (systemStatus.PirateActivity > 0 && !contractEmployers.Contains("AuriganPirates"))
            {
                contractEmployers.Add("AuriganPirates");
                contractTargets.Add("AuriganPirates");
            }

            if (contractEmployers.Count == 1)
            {
                var faction = Globals.OffensiveFactions[Globals.Rng.Next(Globals.OffensiveFactions.Count)];
                contractEmployers.Add(faction);
                if (!contractTargets.Contains(faction))
                    contractTargets.Add(faction);
            }

            if (starSystem.Tags.Contains("planet_region_hyadesrim") && Globals.Settings.HyadesRimCompatible)
            {
                foreach (var alliedFaction in owner.FactionDef.Allies)
                {
                    if (!contractEmployers.Contains(alliedFaction) && !Globals.Settings.HyadesTargetsOnly.Contains(alliedFaction))
                        contractEmployers.Add(alliedFaction);
                }

                foreach (var enemyFaction in owner.FactionDef.Enemies)
                {
                    if (!contractTargets.Contains(enemyFaction) && !Globals.Settings.HyadesEmployersOnly.Contains(enemyFaction))
                        contractTargets.Add(enemyFaction);
                }
            }

            var tempContractEmployers = new List<string>(contractEmployers);
            foreach (var tempEmployer in tempContractEmployers)
            {
                if (Globals.Settings.NoOffensiveContracts.Contains(tempEmployer))
                    contractEmployers.Remove(tempEmployer);
            }
        }

        internal static double DeltaInfluence(StarSystem system, double contractDifficulty, string contractTypeID, string defenseFaction, bool piratesInvolved)
        {
            var targetSystem = Globals.WarStatusTracker.Systems.Find(x => x.StarSystem == system);
            if (targetSystem == null)
            {
                LogDebug($"null systemStatus {system.Name} at DeltaInfluence");
            }

            float maximumInfluence;

            if (piratesInvolved && defenseFaction == "AuriganPirates")
                maximumInfluence = targetSystem.PirateActivity;
            else if (piratesInvolved)
                maximumInfluence = 100 - targetSystem.PirateActivity;
            else
                maximumInfluence = targetSystem.InfluenceTracker[defenseFaction];

            double influenceChange;
            contractDifficulty = Mathf.Max((int) contractDifficulty, targetSystem.DifficultyRating);

            //If contracts are not properly designed, this provides a failsafe.
            try
            {
                influenceChange = (11 + contractDifficulty - 2 * targetSystem.DifficultyRating) * Globals.Settings.ContractImpact[contractTypeID] / Globals.Settings.InfluenceDivisor;
            }
            catch
            {
                influenceChange = (11 + contractDifficulty - 2 * targetSystem.DifficultyRating) / Globals.Settings.InfluenceDivisor;
            }

            //Log("System Delta Influence");
            //Log(TargetSystem.name);
            //Log(Globals.WarStatusTracker.DeploymentInfluenceIncrease.ToString());
            //Log(contractDifficulty.ToString());
            //Log(TargetSystem.DifficultyRating.ToString());
            //Log(Globals.Settings.InfluenceDivisor.ToString());
            //Log(InfluenceChange.ToString());


            if (piratesInvolved)
                influenceChange *= 2;
            influenceChange = Globals.WarStatusTracker.DeploymentInfluenceIncrease * Math.Max(influenceChange, 0.5);
            if (influenceChange > maximumInfluence && !piratesInvolved)
            {
                Globals.AttackerInfluenceHolder = influenceChange;
                Globals.AttackerInfluenceHolder = Math.Round(Globals.AttackerInfluenceHolder, 1);
                Globals.InfluenceMaxed = true;
            }
            else
                Globals.InfluenceMaxed = false;

            influenceChange = Math.Min(influenceChange, maximumInfluence);
            influenceChange = Math.Round(influenceChange, 1);
            //Log(InfluenceChange.ToString());
            //Log("--------------------------");
            return influenceChange;
        }

        internal static bool WillSystemFlip(StarSystem system, string winner, string loser, double deltaInfluence, bool preBattle)
        {
            var warSystem = Globals.WarStatusTracker.Systems.Find(x => x.StarSystem == system);
            if (warSystem == null)
            {
                LogDebug($"null systemStatus {system.Name} at WillSystemFlip");
            }

            var tempIt = new Dictionary<string, float>(warSystem.InfluenceTracker);

            if (preBattle && !Globals.InfluenceMaxed)
            {
                tempIt[winner] += (float) deltaInfluence;
                tempIt[loser] -= (float) deltaInfluence;
            }
            else if (preBattle && Globals.InfluenceMaxed)
            {
                tempIt[winner] += (float) Math.Min(Globals.AttackerInfluenceHolder, 100 - tempIt[winner]);
                tempIt[loser] -= (float) deltaInfluence;
            }

            var highKey = tempIt.OrderByDescending(x => x.Value).Select(x => x.Key).First();
            var highValue = tempIt.OrderByDescending(x => x.Value).Select(x => x.Value).First();
            tempIt.Remove(highKey);
            var secondValue = tempIt.OrderByDescending(x => x.Value).Select(x => x.Value).First();

            if (highKey != warSystem.Owner &&
                highKey == winner &&
                highValue - secondValue > Globals.Settings.TakeoverThreshold &&
                !Globals.WarStatusTracker.FlashpointSystems.Contains(system.Name) &&
                !Globals.Settings.DefensiveFactions.Contains(winner) &&
                !Globals.Settings.ImmuneToWar.Contains(loser))
                return true;
            return false;
        }

        internal static int CalculateFlipMissions(string attacker, StarSystem system)
        {
            var warSystem = Globals.WarStatusTracker.Systems.Find(x => x.StarSystem == system);
            var tempIt = new Dictionary<string, float>(warSystem.InfluenceTracker);
            var missionCounter = 0;
            var influenceDifference = 0.0f;
            double contractDifficulty = warSystem.DifficultyRating;
            var deploymentIfHolder = Globals.WarStatusTracker.DeploymentInfluenceIncrease;
            Globals.WarStatusTracker.DeploymentInfluenceIncrease = 1;

            while (influenceDifference <= Globals.Settings.TakeoverThreshold)
            {
                var defenseFaction = "";
                foreach (var faction in tempIt.OrderByDescending(x => x.Value))
                {
                    if (faction.Key != attacker)
                    {
                        defenseFaction = faction.Key;
                        break;
                    }
                }

                var influenceChange = DeltaInfluence(system, contractDifficulty, "CaptureBase", defenseFaction, false);
                tempIt[attacker] += (float) influenceChange;
                tempIt[defenseFaction] -= (float) influenceChange;
                influenceDifference = tempIt[attacker] - tempIt[defenseFaction];
                Globals.WarStatusTracker.DeploymentInfluenceIncrease *= Globals.Settings.DeploymentEscalationFactor;
                missionCounter++;
            }

            Globals.WarStatusTracker.DeploymentInfluenceIncrease = deploymentIfHolder;
            return missionCounter;
        }

        public static void RecalculateSystemInfluence(SystemStatus systemStatus, string newOwner, string oldOwner)
        {
            systemStatus.InfluenceTracker.Clear();
            systemStatus.InfluenceTracker.Add(newOwner, Globals.Settings.DominantInfluence);
            systemStatus.InfluenceTracker.Add(oldOwner, Globals.Settings.MinorInfluencePool);

            foreach (var faction in Globals.IncludedFactions)
            {
                if (!systemStatus.InfluenceTracker.Keys.Contains(faction))
                    systemStatus.InfluenceTracker.Add(faction, 0);
            }
        }

        public static void AdjustDeathList(DeathListTracker deathListTracker, bool reloadFromSave)
        {
            //LogDebug($"AdjustDeathList: {deathListTracker.Faction}.");
            var trackerDeathList = deathListTracker.DeathList;
            var trackerFaction = deathListTracker.Faction;
            var trackerFactionDef = Globals.Sim.GetFactionDef(trackerFaction);
            var trackerFactionEnemies = new List<string>(trackerFactionDef.Enemies);
            var trackerFactionAllies = new List<string>(trackerFactionDef.Allies);

            if (Globals.WarStatusTracker.InactiveTHRFactions.Contains(trackerFaction) || Globals.WarStatusTracker.NeverControl.Contains(trackerFaction))
                return;

            //Check to see if it is an ally or enemy of itself and remove it if so.
            if (trackerDeathList.ContainsKey(trackerFaction))
            {
                trackerDeathList.Remove(trackerFaction);
                if (trackerFactionAllies.Contains(trackerFaction))
                {
                    trackerFactionAllies.Remove(trackerFaction);
                    trackerFactionDef.Allies = trackerFactionAllies.ToArray();
                }

                if (trackerFactionEnemies.Contains(trackerFaction))
                {
                    trackerFactionEnemies.Remove(trackerFaction);
                    trackerFactionDef.Enemies = trackerFactionEnemies.ToArray();
                }
            }

            var deathListOffensiveFactions = new List<string>(trackerDeathList.Keys.Except(Globals.Settings.DefensiveFactions));
            var warFaction = deathListTracker.WarFaction;
            if (warFaction is null)
            {
                LogDebug($"{deathListTracker.Faction} has a null WarFaction.");
                SaveHandling.StarmapPopulateMapPatch.Spawn();
                return;
            }

            var hasEnemy = false;
            //Defensive Only factions are always neutral

            Globals.Settings.DefensiveFactions.Do(fac => trackerDeathList[fac] = 50);
            if (Globals.Settings.GaW_PoliceSupport && warFaction.ComstarSupported)
                trackerDeathList[Globals.Settings.GaW_Police] = 99;

            foreach (var offensiveFaction in deathListOffensiveFactions)
            {
                if (Globals.WarStatusTracker.InactiveTHRFactions.Contains(offensiveFaction) || Globals.WarStatusTracker.NeverControl.Contains(offensiveFaction))
                    continue;

                //Check to see if factions are always allied with each other.
                if (Globals.Settings.FactionsAlwaysAllies.Keys.Contains(warFaction.FactionName) && Globals.Settings.FactionsAlwaysAllies[warFaction.FactionName].Contains(offensiveFaction))
                {
                    trackerDeathList[offensiveFaction] = 99;
                    continue;
                }

                if (!reloadFromSave)
                {
                    //Factions adjust hatred based upon how much they are being attacked. But there is diminishing returns further from 50.
                    var direction = -1;
                    if (warFaction.IncreaseAggression.Keys.Contains(offensiveFaction) && warFaction.IncreaseAggression[offensiveFaction])
                        direction = 1;
                    {
                        if (trackerDeathList[offensiveFaction] > 50)
                            trackerDeathList[offensiveFaction] += direction * (1 - (trackerDeathList[offensiveFaction] - 50) / 50);
                        else if (trackerDeathList[offensiveFaction] <= 50)
                            trackerDeathList[offensiveFaction] += direction * (1 - (50 - trackerDeathList[offensiveFaction]) / 50);
                    }

                    //Ceiling and floor for faction enmity. 
                    if (trackerDeathList[offensiveFaction] > 99)
                        trackerDeathList[offensiveFaction] = 99;

                    if (trackerDeathList[offensiveFaction] < 1)
                        trackerDeathList[offensiveFaction] = 1;
                }

                // If the faction target is Pirates, the faction always hates them. Alternatively, if the faction we are checking
                // the DeathList for is Pirates themselves, we must set everybody else to be an enemy.
                if (offensiveFaction == "AuriganPirates")
                    trackerDeathList[offensiveFaction] = 80;

                if (trackerFaction == "AuriganPirates")
                    trackerDeathList[offensiveFaction] = 80;

                if (trackerDeathList[offensiveFaction] > 75)
                {
                    if (offensiveFaction != "AuriganPirates")
                        hasEnemy = true;
                    if (!trackerFactionEnemies.Contains(offensiveFaction))
                    {
                        trackerFactionEnemies.Add(offensiveFaction);
                        if (trackerFactionEnemies.Contains(trackerFaction))
                            trackerFactionEnemies.Remove(trackerFaction);
                        if (trackerFactionEnemies.Contains("AuriganDirectorate"))
                            trackerFactionEnemies.Remove("AuriganDirectorate");
                        trackerFactionDef.Enemies = trackerFactionEnemies.ToArray();
                    }

                    if (trackerFactionAllies.Contains(offensiveFaction))
                    {
                        trackerFactionAllies.Remove(offensiveFaction);
                        if (trackerFactionAllies.Contains(trackerFaction))
                            trackerFactionAllies.Remove(trackerFaction);
                        if (trackerFactionAllies.Contains("AuriganDirectorate"))
                            trackerFactionAllies.Remove("AuriganDirectorate");
                        trackerFactionDef.Allies = trackerFactionAllies.ToArray();
                    }
                }
                else if (trackerDeathList[offensiveFaction] <= 75 && trackerDeathList[offensiveFaction] > 25)
                {
                    if (trackerFactionEnemies.Contains(offensiveFaction))
                    {
                        trackerFactionEnemies.Remove(offensiveFaction);
                        if (trackerFactionEnemies.Contains(trackerFaction))
                            trackerFactionEnemies.Remove(trackerFaction);
                        if (trackerFactionEnemies.Contains("AuriganDirectorate"))
                            trackerFactionEnemies.Remove("AuriganDirectorate");
                        trackerFactionDef.Enemies = trackerFactionEnemies.ToArray();
                    }


                    if (trackerFactionAllies.Contains(offensiveFaction))
                    {
                        trackerFactionAllies.Remove(offensiveFaction);
                        if (trackerFactionAllies.Contains(trackerFaction))
                            trackerFactionAllies.Remove(trackerFaction);
                        if (trackerFactionAllies.Contains("AuriganDirectorate"))
                            trackerFactionAllies.Remove("AuriganDirectorate");
                        trackerFactionDef.Allies = trackerFactionAllies.ToArray();
                    }
                }
                else if (trackerDeathList[offensiveFaction] <= 25)
                {
                    if (!trackerFactionAllies.Contains(offensiveFaction))
                    {
                        trackerFactionAllies.Add(offensiveFaction);
                        if (trackerFactionAllies.Contains(trackerFaction))
                            trackerFactionAllies.Remove(trackerFaction);
                        if (trackerFactionAllies.Contains("AuriganDirectorate"))
                            trackerFactionAllies.Remove("AuriganDirectorate");
                        trackerFactionDef.Allies = trackerFactionAllies.ToArray();
                    }

                    if (trackerFactionEnemies.Contains(offensiveFaction))
                    {
                        trackerFactionEnemies.Remove(offensiveFaction);
                        if (trackerFactionEnemies.Contains(trackerFaction))
                            trackerFactionEnemies.Remove(trackerFaction);
                        if (trackerFactionEnemies.Contains("AuriganDirectorate"))
                            trackerFactionEnemies.Remove("AuriganDirectorate");
                        trackerFactionDef.Enemies = trackerFactionEnemies.ToArray();
                    }
                }
            }

            if (!hasEnemy)
            {
                var rand = Globals.Rng.Next(0, Globals.IncludedFactions.Count);
                var newEnemy = Globals.IncludedFactions[rand];

                while (newEnemy == trackerFaction || Globals.Settings.ImmuneToWar.Contains(newEnemy) || Globals.Settings.DefensiveFactions.Contains(newEnemy))
                {
                    rand = Globals.Rng.Next(0, Globals.IncludedFactions.Count);
                    newEnemy = Globals.IncludedFactions[rand];
                }

                if (warFaction.AdjacentFactions.Count != 0)
                {
                    rand = Globals.Rng.Next(0, warFaction.AdjacentFactions.Count);
                    newEnemy = warFaction.AdjacentFactions[rand];
                }

                if (trackerFactionAllies.Contains(newEnemy))
                {
                    trackerFactionAllies.Remove(newEnemy);
                    if (trackerFactionAllies.Contains(trackerFaction))
                        trackerFactionAllies.Remove(trackerFaction);
                    if (trackerFactionAllies.Contains("AuriganDirectorate"))
                        trackerFactionAllies.Remove("AuriganDirectorate");
                    trackerFactionDef.Allies = trackerFactionAllies.ToArray();
                }

                if (!trackerFactionEnemies.Contains(newEnemy))
                {
                    trackerFactionEnemies.Add(newEnemy);
                    if (trackerFactionEnemies.Contains(trackerFaction))
                        trackerFactionEnemies.Remove(trackerFaction);
                    if (trackerFactionEnemies.Contains("AuriganDirectorate"))
                        trackerFactionEnemies.Remove("AuriganDirectorate");
                    trackerFactionDef.Enemies = trackerFactionEnemies.ToArray();
                }

                trackerDeathList[newEnemy] = 80;
            }
        }

        public static void GaW_Notification()
        {
            //82 characters per line. 
            //Globals.SimGameResultAction Globals.SimGameResultAction = new Globals.SimGameResultAction();
            //Globals.SimGameResultAction.Type = Globals.SimGameResultAction.ActionType.System_ShowSummaryOverlay;
            //Globals.SimGameResultAction.value = Strings.T("Galaxy at War");
            //Globals.SimGameResultAction.additionalValues = new string[1];
            //Globals.SimGameResultAction.additionalValues[0] = Strings.T("In Galaxy at War, the Great Houses of the Inner Sphere will not Globals.Simply wait for a wedding invitation" +
            //                                                    " to show their disdain for each other. To that end, war will break out as petty bickering turns into all out conflict. Your reputation with the factions" +
            //                                                    " is key - the more they like you, the more they'll bring you to the front lines and the greater the rewards. Perhaps an enterprising mercenary could make their" +
            //                                                    " fortune changing the tides of battle and helping a faction dominate the Inner Sphere.\n\n <b>New features in Galaxy at War:</b>" +
            //                                                    "\n• Each planet generates Attack Resources and Defensive Resources that they will be constantly " +
            //                                                    "spending to spread their influence and protect their own systems." +
            //                                                    "\n• Planetary Resources and Faction Influence can be seen on the Star Map by hovering over any system." +
            //                                                    "\n• Successfully completing missions will swing the influence towards the Faction granting the contract." +
            //                                                    "\n• Target Acquisition Missions & Attack and Defend Missions will give a permanent bonus to the winning faction's Attack Resources and a permanent deduction to the losing faction's Defensive Resources." +
            //                                                    "\n• If you accept a travel contract the Faction will blockade the system for 30 days. A bonus will be granted for every mission you complete within that system during that time." +
            //                                                    "\n• Pirates are active and will reduce Resources in a system. High Pirate activity will be highlighted in red." +
            //                                                    "\n• Sumire will flag the systems in purple on the Star Map that are the most valuable local targets." +
            //                                                    "\n• Sumire will also highlight systems in yellow that have changed ownership during the previous month." +
            //                                                    "\n• Hitting Control-R will bring up a summary of the Faction's relationships and their overall war status." +
            //                                                    "\n\n****Press Enter to Continue****");


            //Globals.SimGameState.ApplyEventAction(Globals.SimGameResultAction, null);
            //UnityGameInstance.BattleTechGame.Globals.Simulation.StopPlayMode();
        }

        public static void GenerateMonthlyContracts()
        {
            var contracts = new List<Contract>();
            var system = Globals.Sim.CurSystem;
            var systemStatus = Globals.WarStatusTracker.Systems.Find(x => x.StarSystem == system);
            var influenceTracker = systemStatus.InfluenceTracker;
            var owner = influenceTracker.First().Key;
            var second = influenceTracker.Skip(1).First().Key;
            LogDebug(0);
            var contract = Contracts.GenerateContract(system, 2, 2, owner);
            contracts.Add(contract);
            LogDebug(1);
            contract = Contracts.GenerateContract(system, 4, 4, owner);
            contracts.Add(contract);
            LogDebug(2);
            contract = Contracts.GenerateContract(system, 2, 2, second);
            contracts.Add(contract);
            LogDebug(3);
            contract = Contracts.GenerateContract(system, 4, 4, second);
            contracts.Add(contract);
            LogDebug(4);
            contract = Contracts.GenerateContract(system, 6, 6, Globals.Settings.IncludedFactions.Where(x => x != "NoFaction").GetRandomElement());
            contracts.Add(contract);
            LogDebug(5);
            Globals.Sim.CurSystem.activeSystemContracts = contracts;
        }

        internal static void BackFillContracts()
        {
            const int ownerContracts = 5;
            const int secondContracts = 3;
            const int otherContracts = 2;

            //var loops = 10 - ownerContracts + secondContracts;
            for (var i = 0; i < 10; i++)
            {
                int variedDifficulty;
                var isTravel = Globals.Rng.Next(0, 2) == 0;
                var isPriority = Globals.Rng.Next(0, 11) == 0;
                var system = isTravel ? Globals.GaWSystems.Where(x => x.JumpDistance < 10).GetRandomElement() : Globals.Sim.CurSystem;
                var systemStatus = Globals.WarStatusTracker.Systems.Find(x => x.StarSystem == system);
                var owner = systemStatus.InfluenceTracker.OrderByDescending(x => x.Value).Select(x => x.Key).First();
                var second = systemStatus.InfluenceTracker.OrderByDescending(x =>
                    x.Value).Select(x => x.Key).Skip(1).Take(1).First();
                var currentOwnerContracts = Globals.Sim.GetAllCurrentlySelectableContracts().Count(x => x.Override.employerTeam.faction == owner);
                var currentSecondContracts = Globals.Sim.GetAllCurrentlySelectableContracts().Count(x => x.Override.employerTeam.faction == second);
                var currentOtherContracts = Globals.Sim.GetAllCurrentlySelectableContracts().Count - currentOwnerContracts - currentSecondContracts;
                if (currentOwnerContracts < ownerContracts)
                {
                    variedDifficulty = Variance(system);
                    AddContract(system, variedDifficulty, owner, isTravel, isPriority);
                }
                else if (currentSecondContracts < secondContracts)
                {
                    variedDifficulty = Variance(system);
                    AddContract(system, variedDifficulty, second, isTravel, isPriority);
                }
                else if (currentOtherContracts < otherContracts)
                {
                    variedDifficulty = Variance(system);
                    AddContract(system, variedDifficulty, systemStatus.InfluenceTracker.Select(x => x.Key).GetRandomElement(), isTravel, isPriority);
                }
            }
        }

        private static void AddContract(StarSystem system, int variedDifficulty, string employer, bool isTravel, bool isPriority)
        {
            var contract = Contracts.GenerateContract(system, variedDifficulty, variedDifficulty, employer, null, isTravel);
            if (isPriority)
            {
                contract.Override.contractDisplayStyle = ContractDisplayStyle.BaseCampaignStory;
            }

            system.activeSystemContracts.Add(contract);
        }

        internal static int Variance(StarSystem starSystem)
        {
            const int variance = 2;
            return starSystem.Def.GetDifficulty(SimGameState.SimGameType.CAREER) + Globals.Rng.Next(-variance, variance + 1);
        }

        internal static SystemStatus FindSystemStatus(this StarSystem starSystem)
            => Globals.WarStatusTracker.Systems.Find(system => system.StarSystem == starSystem);

        internal static void DumpCSV()
        {
            var date = UnityGameInstance.BattleTechGame.Simulation.CurrentDate;
            var filename = Directory.GetParent(Assembly.GetExecutingAssembly().Location)?.FullName + $"/dump-{date:MM-yyyy}.csv";
            var table = new System.Data.DataTable();
            table.Columns.Add("DayMonthYear");
            table.Columns.Add("name");
            table.Columns.Add("owner");
            table.Columns.Add("OriginalOwner");
            table.Columns.Add("TotalResources");
            table.Columns.Add("PriorityDefense");
            table.Columns.Add("PriorityAttack");
            table.Columns.Add("DefenseResources");
            table.Columns.Add("AttackResources");
            table.Columns.Add("PirateActivity");

            table.Rows.Add(
                "DayMonthYear",
                "name",
                "owner",
                "OriginalOwner",
                "TotalResources",
                "PriorityDefense",
                "PriorityAttack",
                "DefenseResources",
                "AttackResources",
                "PirateActivity");

            foreach (var system in Globals.WarStatusTracker.Systems)
            {
                //LogDebug("Building table row for " + system.name);
                table.Rows.Add($"{date:MM-yyyy}", system.Name, system.Owner, system.OriginalOwner, system.TotalResources, system.PriorityDefense, system.PriorityAttack,
                    system.DefenseResources, system.AttackResources, system.PirateActivity);
            }

            const int col = 0;
            using var sw = new StreamWriter(filename, true);
            for (var row = 0; row < table.Rows.Count; row++)
            {
                //LogDebug($"Adding row {row}");
                sw.WriteLine($"{table.Rows[row][col]},{table.Rows[row][col + 1]},{table.Rows[row][col + 2]},{table.Rows[row][col + 3]}" +
                             $",{table.Rows[row][col + 4]},{table.Rows[row][col + 5]},{table.Rows[row][col + 6]},{table.Rows[row][col + 7]}" +
                             $",{table.Rows[row][col + 8]},{table.Rows[row][col + 9]}");
            }
        }

        // https://stackoverflow.com/questions/7343465/compression-decompression-string-with-c-sharp
        private static void CopyTo(Stream src, Stream dest)
        {
            var bytes = new byte[4096];
            int cnt;

            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
            {
                dest.Write(bytes, 0, cnt);
            }
        }

        internal static byte[] Zip(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);

            using var msi = new MemoryStream(bytes);
            using var mso = new MemoryStream();
            using (var gs = new GZipStream(mso, CompressionMode.Compress))
            {
                //msi.CopyTo(gs);
                CopyTo(msi, gs);
            }

            return mso.ToArray();
        }

        internal static string Unzip(byte[] bytes)
        {
            using var msi = new MemoryStream(bytes);
            using var mso = new MemoryStream();
            using (var gs = new GZipStream(msi, CompressionMode.Decompress))
            {
                //gs.CopyTo(mso);
                CopyTo(gs, mso);
            }

            return Encoding.UTF8.GetString(mso.ToArray());
        }
    }
}
