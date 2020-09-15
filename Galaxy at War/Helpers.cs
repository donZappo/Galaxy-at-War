using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Harmony;
using UnityEngine;
using static GalaxyatWar.Logger;

// ReSharper disable StringLiteralTypo
// ReSharper disable ClassNeverInstantiated.Global

namespace GalaxyatWar
{
    public class Helpers
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
            var totalSystems = Globals.WarStatusTracker.systems.Count;
            var difficultyCutoff = totalSystems / 10;
            var i = 0;

            foreach (var systemStatus in Globals.WarStatusTracker.systemsByResources)
            {
                try
                {
                    //Define the original owner of the system for revolt purposes.
                    if (systemStatus.OriginalOwner == null)
                        systemStatus.OriginalOwner = systemStatus.owner;

                    if (Globals.Settings.ChangeDifficulty && !systemStatus.starSystem.Tags.Contains("planet_start_world"))
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
                        var difficultyList = new List<int> {amount, amount};
                        systemStatus.starSystem.Def.DifficultyList = difficultyList;
                        systemStatus.starSystem.Def.DefaultDifficulty = amount;
                    }
                    else
                    {
                        systemStatus.DifficultyRating = systemStatus.starSystem.Def.DefaultDifficulty;
                        i++;
                    }

                    if (systemStatus.starSystem.Def.OwnerValue.Name != "NoFaction" && systemStatus.starSystem.Def.SystemShopItems.Count == 0)
                    {
                        var tempList = new List<string>
                        {
                            "itemCollection_minor_Locals"
                        };
                        systemStatus.starSystem.Def.SystemShopItems = tempList;
                        if (Globals.Sim.CurSystem.Name == systemStatus.starSystem.Def.Description.Name)
                        {
                            var refreshShop = Shop.RefreshType.RefreshIfEmpty;
                            systemStatus.starSystem.SystemShop.Rehydrate(Globals.Sim, systemStatus.starSystem, systemStatus.starSystem.Def.SystemShopItems, refreshShop,
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
            foreach (var warFarTemp in Globals.WarStatusTracker.warFactionTracker)
            {
                warFarTemp.ComstarSupported = false;
                if (omit.Contains(warFarTemp.faction))
                    continue;
                warFactionList.Add(warFarTemp);
            }

            var warFactionHolder = warFactionList.OrderBy(x => x.TotalSystemsChanged).ElementAt(0);
            var warFactionListTrimmed = warFactionList.FindAll(x => x.TotalSystemsChanged == warFactionHolder.TotalSystemsChanged);
            warFactionListTrimmed.Shuffle();
            var warFaction = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == warFactionListTrimmed.ElementAt(0).faction);
            warFaction.ComstarSupported = true;
            Globals.WarStatusTracker.ComstarAlly = warFaction.faction;
            var factionDef = Globals.Sim.GetFactionDef(warFaction.faction);
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
            var warFac = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == starSystem.OwnerValue.Name);
            if (warFac == null)
                return;
            var isFlashpointSystem = Globals.WarStatusTracker.FlashpointSystems.Contains(starSystem.Name);
            var warSystem = Globals.WarStatusTracker.systems.Find(x => x.starSystem == starSystem);
            var ownerNeighborSystems = warSystem.neighborSystems;
            ownerNeighborSystems.Clear();
            if (Globals.Sim.Starmap.GetAvailableNeighborSystem(starSystem).Count == 0)
                return;

            foreach (var neighborSystem in Globals.Sim.Starmap.GetAvailableNeighborSystem(starSystem))
            {
                if (neighborSystem.OwnerValue.Name != starSystem.OwnerValue.Name &&
                    !isFlashpointSystem &&
                    !Globals.Settings.ImmuneToWar.Contains(neighborSystem.OwnerValue.Name))
                {
                    if (!warFac.attackTargets.ContainsKey(neighborSystem.OwnerValue.Name))
                    {
                        var tempList = new List<string> {neighborSystem.Name};
                        //var tempList = new List<string>(warFac.attackTargets[neighborSystem.OwnerValue.Name]);
                        warFac.attackTargets.Add(neighborSystem.OwnerValue.Name, tempList);
                    }
                    else if (warFac.attackTargets.ContainsKey(neighborSystem.OwnerValue.Name)
                             && !warFac.attackTargets[neighborSystem.OwnerValue.Name].Contains(neighborSystem.Name))
                    {
                        // bug should Locals be here?
                        warFac.attackTargets[neighborSystem.OwnerValue.Name].Add(neighborSystem.Name);
                    }

                    //if (!warFac.defenseTargets.Contains(starSystem.Name))
                    //{
                    //    warFac.defenseTargets.Add(starSystem.Name);
                    //}
                    if (!warFac.adjacentFactions.Contains(starSystem.OwnerValue.Name) && !Globals.Settings.DefensiveFactions.Contains(starSystem.OwnerValue.Name))
                        warFac.adjacentFactions.Add(starSystem.OwnerValue.Name);
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
            foreach (var warFaction in Globals.WarStatusTracker.warFactionTracker)
                warFaction.defenseTargets.Clear();

            foreach (var system in Globals.WarStatusTracker.systems)
            {
                if (Globals.WarStatusTracker.FlashpointSystems.Contains(system.name))
                    continue;

                var totalInfluence = system.influenceTracker.Values.Sum();
                if ((totalInfluence - 100) / 100 > Globals.Settings.SystemDefenseCutoff)
                {
                    var warFaction = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == system.owner);
                    warFaction.defenseTargets.Add(system.name);
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

                var systemStatus = Globals.WarStatusTracker.systems.Find(x => x.starSystem == system);
                var oldOwner = systemStatus.owner;
                systemStatus.owner = faction;
                system.Def.factionShopOwnerID = faction;
                system.Def.OwnerValue = Globals.FactionValues.Find(x => x.Name == faction);

                //Change the Kill List for the factions.
                var totalAR = GetTotalAttackResources(system);
                var totalDr = GetTotalDefensiveResources(system);

                var wfWinner = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == faction);
                wfWinner.GainedSystem = true;
                wfWinner.MonthlySystemsChanged += 1;
                wfWinner.TotalSystemsChanged += 1;
                if (Globals.Settings.DefendersUseARforDR && Globals.Settings.DefensiveFactions.Contains(wfWinner.faction))
                {
                    wfWinner.DefensiveResources += totalAR;
                    wfWinner.DefensiveResources += totalDr;
                }
                else
                {
                    wfWinner.AttackResources += totalAR;
                    wfWinner.DefensiveResources += totalDr;
                }

                var wfLoser = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == oldFaction.Name);
                wfLoser.LostSystem = true;
                wfLoser.MonthlySystemsChanged -= 1;
                wfLoser.TotalSystemsChanged -= 1;
                RemoveAndFlagSystems(wfLoser, system);
                if (Globals.Settings.DefendersUseARforDR && Globals.Settings.DefensiveFactions.Contains(wfWinner.faction))
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
                var factionTracker = Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == oldFaction);
                if (factionTracker.deathList.ContainsKey(faction))
                {
                    if (factionTracker.deathList[faction] < 50)
                        factionTracker.deathList[faction] = 50;

                    factionTracker.deathList[faction] += killListDelta;
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
                        if (!Globals.IncludedFactions.Contains(ally) || faction == ally || Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == ally) == null)
                            continue;
                        var factionAlly = Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == ally);
                        factionAlly.deathList[faction] += killListDelta / 2;
                    }

                    //Enemies of the target faction are happy with the faction doing the beating. 
                    foreach (var enemy in Globals.Sim.GetFactionDef(oldFaction).Enemies)
                    {
                        if (!Globals.IncludedFactions.Contains(enemy) || enemy == faction || Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == enemy) == null)
                            continue;
                        var factionEnemy = Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == enemy);
                        factionEnemy.deathList[faction] -= killListDelta / 2;
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
            foreach (var faction in Globals.WarStatusTracker.warFactionTracker)
            {
                var attackCount = new Dictionary<string, int>();
                foreach (var target in faction.attackTargets)
                {
                    if (Globals.Settings.DefensiveFactions.Contains(target.Key) || Globals.Settings.ImmuneToWar.Contains(target.Key))
                        continue;
                    attackCount.Add(target.Key, target.Value.Count);
                }

                var i = 0;
                var topHalf = attackCount.Count / 2;
                foreach (var attackTarget in attackCount.OrderByDescending(x => x.Value))
                {
                    var warFaction = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == attackTarget.Key);
                    if (i < topHalf)
                        warFaction.IncreaseAggression[warFaction.faction] = true;
                    else
                        warFaction.IncreaseAggression[warFaction.faction] = false;
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
                var wfat = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == neighborSystem.OwnerValue.Name).attackTargets;
                if (wfat.Keys.Contains(oldOwner.faction) && wfat[oldOwner.faction].Contains(system.Name))
                    wfat[oldOwner.faction].Remove(system.Name);
            }
        }

        internal static void UpdateInfluenceFromAttacks(bool checkForSystemChange)
        {
            if (checkForSystemChange)
                Globals.WarStatusTracker.LostSystems.Clear();

            //LogDebug($"Updating influence for {Globals.WarStatusTracker.SystemStatuses.Count.ToString()} systems");
            foreach (var systemStatus in Globals.WarStatusTracker.systems)
            {
                var tempDict = new Dictionary<string, float>();
                var totalInfluence = systemStatus.influenceTracker.Values.Sum();
                var highest = 0f;
                var highestFaction = systemStatus.owner;
                foreach (var kvp in systemStatus.influenceTracker)
                {
                    tempDict.Add(kvp.Key, kvp.Value / totalInfluence * 100);
                    if (kvp.Value > highest)
                    {
                        highest = kvp.Value;
                        highestFaction = kvp.Key;
                    }
                }

                systemStatus.influenceTracker = tempDict;
                var diffStatus = systemStatus.influenceTracker[highestFaction] - systemStatus.influenceTracker[systemStatus.owner];
                var starSystem = systemStatus.starSystem;

                if (highestFaction != systemStatus.owner && !Globals.WarStatusTracker.FlashpointSystems.Contains(systemStatus.name) &&
                    (diffStatus > Globals.Settings.TakeoverThreshold && !Globals.WarStatusTracker.HotBox.Contains(systemStatus.name)
                                                                     && (!Globals.Settings.DefensiveFactions.Contains(highestFaction) || highestFaction == "Locals") &&
                                                                     !Globals.Settings.ImmuneToWar.Contains(starSystem.OwnerValue.Name)))
                {
                    if (!systemStatus.Contended)
                    {
                        systemStatus.Contended = true;
                        ChangeDeathListFromAggression(starSystem, highestFaction, starSystem.OwnerValue.Name);
                    }
                    else if (checkForSystemChange)
                    {
                        ChangeSystemOwnership(starSystem, highestFaction, false);
                        systemStatus.Contended = false;
                        Globals.WarStatusTracker.LostSystems.Add(starSystem.Name);
                    }
                }

                //Local Government can take a system.
                if (systemStatus.owner != "Locals" && systemStatus.OriginalOwner == "Locals" &&
                    (highestFaction == "Locals" && systemStatus.influenceTracker[highestFaction] >= 75))
                {
                    ChangeSystemOwnership(starSystem, "Locals", true);
                    systemStatus.Contended = false;
                    Globals.WarStatusTracker.LostSystems.Add(starSystem.Name);
                }
            }

            CalculateHatred();
            foreach (var deathListTracker in Globals.WarStatusTracker.deathListTracker)
            {
                AdjustDeathList(deathListTracker, false);
            }
        }

        public static string MonthlyWarReport()
        {
            var combinedString = "";
            foreach (var faction in Globals.IncludedFactions)
            {
                var warFaction = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == faction);
                combinedString = combinedString + "<b><u>" + Globals.Settings.FactionNames[faction] + "</b></u>\n";
                var summaryString = "Monthly Change in Systems: " + warFaction.MonthlySystemsChanged + "\n";
                var summaryString2 = "Overall Change in Systems: " + warFaction.TotalSystemsChanged + "\n\n";

                combinedString = combinedString + summaryString + summaryString2;
                warFaction.MonthlySystemsChanged = 0;
            }

            combinedString = combinedString.TrimEnd('\n');
            return combinedString;
        }

        public static void RefreshContracts(SystemStatus systemStatus)
        {
            var starSystem = systemStatus.starSystem;
            //LogDebug("RefreshContracts for " + starSystem.Name);
            if (Globals.WarStatusTracker.HotBox.Contains(starSystem.Name) || (starSystem.Tags.Contains("planet_region_hyadesrim") &&
                                                                              (starSystem.OwnerDef.Name == "Locals" || starSystem.OwnerDef.Name == "NoFaction")))
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

            var neighborSystems = systemStatus.neighborSystems;
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
            var targetSystem = Globals.WarStatusTracker.systems.Find(x => x.starSystem == system);
            float maximumInfluence;

            if (piratesInvolved && defenseFaction == "AuriganPirates")
                maximumInfluence = targetSystem.PirateActivity;
            else if (piratesInvolved)
                maximumInfluence = 100 - targetSystem.PirateActivity;
            else
                maximumInfluence = targetSystem.influenceTracker[defenseFaction];

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
            var warSystem = Globals.WarStatusTracker.systems.Find(x => x.starSystem == system);
            var tempIt = new Dictionary<string, float>(warSystem.influenceTracker);

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

            if (highKey != warSystem.owner && highKey == winner && highValue - secondValue > Globals.Settings.TakeoverThreshold
                && !Globals.WarStatusTracker.FlashpointSystems.Contains(system.Name) &&
                (!Globals.Settings.DefensiveFactions.Contains(winner) && !Globals.Settings.ImmuneToWar.Contains(loser)))
                return true;
            return false;
        }

        internal static int CalculateFlipMissions(string attacker, StarSystem system)
        {
            var warSystem = Globals.WarStatusTracker.systems.Find(x => x.starSystem == system);
            var tempIt = new Dictionary<string, float>(warSystem.influenceTracker);
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
            systemStatus.influenceTracker.Clear();
            systemStatus.influenceTracker.Add(newOwner, Globals.Settings.DominantInfluence);
            systemStatus.influenceTracker.Add(oldOwner, Globals.Settings.MinorInfluencePool);

            foreach (var faction in Globals.IncludedFactions)
            {
                if (!systemStatus.influenceTracker.Keys.Contains(faction))
                    systemStatus.influenceTracker.Add(faction, 0);
            }
        }

        public static void AdjustDeathList(DeathListTracker deathListTracker, bool reloadFromSave)
        {
            var trackerDeathList = deathListTracker.deathList;
            var trackerFaction = deathListTracker.faction;
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
            var hasEnemy = false;
            //Defensive Only factions are always neutral
            Globals.Settings.DefensiveFactions.Do(x => trackerDeathList[x] = 50);
            if (Globals.Settings.GaW_PoliceSupport && warFaction.ComstarSupported)
                trackerDeathList[Globals.Settings.GaW_Police] = 99;

            foreach (var offensiveFaction in deathListOffensiveFactions)
            {
                if (Globals.WarStatusTracker.InactiveTHRFactions.Contains(offensiveFaction) || Globals.WarStatusTracker.NeverControl.Contains(offensiveFaction))
                    continue;

                //Check to see if factions are always allied with each other.
                if (Globals.Settings.FactionsAlwaysAllies.Keys.Contains(warFaction.faction) && Globals.Settings.FactionsAlwaysAllies[warFaction.faction].Contains(offensiveFaction))
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

                if (warFaction.adjacentFactions.Count != 0)
                {
                    rand = Globals.Rng.Next(0, warFaction.adjacentFactions.Count);
                    newEnemy = warFaction.adjacentFactions[rand];
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
            LogDebug(0);
            var system = Globals.Sim.CurSystem;
            var systemStatus = Globals.WarStatusTracker.systems.Find(x => x.starSystem == system);
            var influenceTracker = systemStatus.influenceTracker;
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
    }
}
