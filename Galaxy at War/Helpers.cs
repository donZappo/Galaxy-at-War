using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Harmony;
using UnityEngine;
using static GalaxyatWar.Globals;
using static GalaxyatWar.Logger;

// ReSharper disable StringLiteralTypo
// ReSharper disable ClassNeverInstantiated.Global

namespace GalaxyatWar
{
    public class Helpers
    {
        internal static void CopySettingsToState()
        {
            if (Settings.ISMCompatibility)
                IncludedFactions = new List<string>(Settings.IncludedFactions_ISM);
            else
                IncludedFactions = new List<string>(Settings.IncludedFactions);

            OffensiveFactions = IncludedFactions.Except(Settings.DefensiveFactions).ToList();
        }

        public static void SystemDifficulty()
        {
            var totalSystems = WarStatusTracker.systems.Count;
            var difficultyCutoff = totalSystems / 10;
            var i = 0;

            foreach (var systemStatus in WarStatusTracker.systemsByResources)
            {
                try
                {
                    //Define the original owner of the system for revolt purposes.
                    if (systemStatus.OriginalOwner == null)
                        systemStatus.OriginalOwner = systemStatus.owner;

                    if (Settings.ChangeDifficulty && !systemStatus.starSystem.Tags.Contains("planet_start_world"))
                    {
                        Sim.Constants.Story.ContractDifficultyMod = 0;
                        Sim.CompanyStats.Set<float>("Difficulty", 0);
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
                        LogDebug("SystemDifficulty fix entry " + T.Elapsed);
                        var tempList = new List<string>
                        {
                            "itemCollection_minor_Locals"
                        };
                        systemStatus.starSystem.Def.SystemShopItems = tempList;
                        if (Sim.CurSystem.Name == systemStatus.starSystem.Def.Description.Name)
                        {
                            var refreshShop = Shop.RefreshType.RefreshIfEmpty;
                            systemStatus.starSystem.SystemShop.Rehydrate(Sim, systemStatus.starSystem, systemStatus.starSystem.Def.SystemShopItems, refreshShop,
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
                result += Settings.planet_industry_poor;
            if (system.Tags.Contains("planet_industry_mining"))
                result += Settings.planet_industry_mining;
            if (system.Tags.Contains("planet_industry_rich"))
                result += Settings.planet_industry_rich;
            if (system.Tags.Contains("planet_industry_manufacturing"))
                result += Settings.planet_industry_manufacturing;
            if (system.Tags.Contains("planet_industry_research"))
                result += Settings.planet_industry_research;
            if (system.Tags.Contains("planet_other_starleague"))
                result += Settings.planet_other_starleague;

            return result;
        }

        public static float GetTotalDefensiveResources(StarSystem system)
        {
            float result = 0;
            if (system.Tags.Contains("planet_industry_agriculture"))
                result += Settings.planet_industry_agriculture;
            if (system.Tags.Contains("planet_industry_aquaculture"))
                result += Settings.planet_industry_aquaculture;
            if (system.Tags.Contains("planet_other_capital"))
                result += Settings.planet_other_capital;
            if (system.Tags.Contains("planet_other_megacity"))
                result += Settings.planet_other_megacity;
            if (system.Tags.Contains("planet_pop_large"))
                result += Settings.planet_pop_large;
            if (system.Tags.Contains("planet_pop_medium"))
                result += Settings.planet_pop_medium;
            if (system.Tags.Contains("planet_pop_none"))
                result += Settings.planet_pop_none;
            if (system.Tags.Contains("planet_pop_small"))
                result += Settings.planet_pop_small;
            if (system.Tags.Contains("planet_other_hub"))
                result += Settings.planet_other_hub;
            if (system.Tags.Contains("planet_other_comstar"))
                result += Settings.planet_other_comstar;
            return result;
        }

        internal static void CalculateComstarSupport()
        {
            if (WarStatusTracker.ComstarCycle < Settings.GaW_Police_SupportTime)
            {
                WarStatusTracker.ComstarCycle++;
                return;
            }

            WarStatusTracker.ComstarCycle = 1;
            var warFactionList = new List<WarFaction>();
            var omit = Settings.DefensiveFactions.Concat(Settings.HyadesPirates)
                .Concat(Settings.NoOffensiveContracts).Concat(new[] {"AuriganPirates"}).ToList();
            foreach (var warFarTemp in WarStatusTracker.warFactionTracker)
            {
                warFarTemp.ComstarSupported = false;
                if (omit.Contains(warFarTemp.faction))
                    continue;
                warFactionList.Add(warFarTemp);
            }

            var warFactionHolder = warFactionList.OrderBy(x => x.TotalSystemsChanged).ElementAt(0);
            var warFactionListTrimmed = warFactionList.FindAll(x => x.TotalSystemsChanged == warFactionHolder.TotalSystemsChanged);
            warFactionListTrimmed.Shuffle();
            var warFaction = WarStatusTracker.warFactionTracker.Find(x => x.faction == warFactionListTrimmed.ElementAt(0).faction);
            warFaction.ComstarSupported = true;
            WarStatusTracker.ComstarAlly = warFaction.faction;
            var factionDef = Sim.GetFactionDef(warFaction.faction);
            if (Settings.GaW_PoliceSupport && !factionDef.Allies.Contains(Settings.GaW_Police))
            {
                var tempList = factionDef.Allies.ToList();
                tempList.Add(Settings.GaW_Police);
                Traverse.Create(factionDef).Property("Allies").SetValue(tempList.ToArray());
            }

            if (Settings.GaW_PoliceSupport && factionDef.Enemies.Contains(Settings.GaW_Police))
            {
                var tempList = factionDef.Enemies.ToList();
                tempList.Remove(Settings.GaW_Police);
                Traverse.Create(factionDef).Property("Enemies").SetValue(tempList.ToArray());
            }
        }

        public static void CalculateAttackAndDefenseTargets(StarSystem starSystem)
        {
            var warFac = WarStatusTracker.warFactionTracker.Find(x => x.faction == starSystem.OwnerValue.Name);
            if (warFac == null)
                return;
            var isFlashpointSystem = WarStatusTracker.FlashpointSystems.Contains(starSystem.Name);
            var warSystem = WarStatusTracker.systems.Find(x => x.starSystem == starSystem);
            var ownerNeighborSystems = warSystem.neighborSystems;
            ownerNeighborSystems.Clear();
            if (Sim.Starmap.GetAvailableNeighborSystem(starSystem).Count == 0)
                return;

            foreach (var neighborSystem in Sim.Starmap.GetAvailableNeighborSystem(starSystem))
            {
                if (neighborSystem.OwnerValue.Name != starSystem.OwnerValue.Name &&
                    !isFlashpointSystem &&
                    !Settings.ImmuneToWar.Contains(neighborSystem.OwnerValue.Name))
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
                    if (!warFac.adjacentFactions.Contains(starSystem.OwnerValue.Name) && !Settings.DefensiveFactions.Contains(starSystem.OwnerValue.Name))
                        warFac.adjacentFactions.Add(starSystem.OwnerValue.Name);
                }

                RefreshNeighbors(ownerNeighborSystems, neighborSystem);
            }
        }

        public static void RefreshNeighbors(Dictionary<string, int> starSystem, StarSystem neighborSystem)
        {
            if (WarStatusTracker.FlashpointSystems.Contains(neighborSystem.Name))
                return;

            var neighborSystemOwner = neighborSystem.OwnerValue.Name;

            if (starSystem.ContainsKey(neighborSystemOwner))
                starSystem[neighborSystemOwner] += 1;
            else
                starSystem.Add(neighborSystemOwner, 1);
        }

        //public static void CalculateDefenseTargets(StarSystem starSystem)
        //{
        //    foreach (var neighborSystem in Sim.Starmap.GetAvailableNeighborSystem(starSystem))
        //    {
        //        var warFac = WarStatusTracker.warFactionTracker.Find(x => x.faction == starSystem.Owner);
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
            foreach (var warFaction in WarStatusTracker.warFactionTracker)
                warFaction.defenseTargets.Clear();

            foreach (var system in WarStatusTracker.systems)
            {
                if (WarStatusTracker.FlashpointSystems.Contains(system.name))
                    continue;

                var totalInfluence = system.influenceTracker.Values.Sum();
                if ((totalInfluence - 100) / 100 > Settings.SystemDefenseCutoff)
                {
                    var warFaction = WarStatusTracker.warFactionTracker.Find(x => x.faction == system.owner);
                    warFaction.defenseTargets.Add(system.name);
                }
            }

            //foreach (var warFaction in WarStatusTracker.warFactionTracker)
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
                var oldFaction = system.OwnerValue;
                if ((oldFaction.Name == "NoFaction" || oldFaction.Name == "Locals") && system.Def.Tags.Contains("planet_region_hyadesrim") && !forceFlip)
                {
                    if (WarStatusTracker.HyadesRimGeneralPirateSystems.Contains(system.Name))
                        WarStatusTracker.HyadesRimGeneralPirateSystems.Remove(system.Name);
                    WarStatusTracker.HyadesRimsSystemsTaken++;
                }

                if (system.Def.Tags.Contains(Settings.FactionTags[oldFaction.Name]))
                    system.Def.Tags.Remove(Settings.FactionTags[oldFaction.Name]);
                system.Def.Tags.Add(Settings.FactionTags[faction]);

                if (!WarStatusTracker.AbandonedSystems.Contains(system.Name))
                {
                    if (system.Def.SystemShopItems.Count != 0)
                    {
                        var tempList = system.Def.SystemShopItems;
                        tempList.Add(Settings.FactionShops[system.OwnerValue.Name]);
                        Traverse.Create(system.Def).Property("SystemShopItems").SetValue(tempList);
                    }

                    if (system.Def.FactionShopItems != null)
                    {
                        Traverse.Create(system.Def).Property("FactionShopOwnerValue").SetValue(FactionValues.Find(x => x.Name == faction));
                        Traverse.Create(system.Def).Property("FactionShopOwnerID").SetValue(faction);
                        var factionShops = system.Def.FactionShopItems;
                        if (factionShops.Contains(Settings.FactionShopItems[system.Def.OwnerValue.Name]))
                            factionShops.Remove(Settings.FactionShopItems[system.Def.OwnerValue.Name]);
                        factionShops.Add(Settings.FactionShopItems[faction]);
                        Traverse.Create(system.Def).Property("FactionShopItems").SetValue(factionShops);
                    }
                }

                var systemStatus = WarStatusTracker.systems.Find(x => x.starSystem == system);
                var oldOwner = systemStatus.owner;
                systemStatus.owner = faction;
                Traverse.Create(system.Def).Property("OwnerID").SetValue(faction);
                Traverse.Create(system.Def).Property("OwnerValue").SetValue(FactionValues.Find(x => x.Name == faction));

                //Change the Kill List for the factions.
                var totalAR = GetTotalAttackResources(system);
                var totalDr = GetTotalDefensiveResources(system);

                var wfWinner = WarStatusTracker.warFactionTracker.Find(x => x.faction == faction);
                wfWinner.GainedSystem = true;
                wfWinner.MonthlySystemsChanged += 1;
                wfWinner.TotalSystemsChanged += 1;
                if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(wfWinner.faction))
                {
                    wfWinner.DefensiveResources += totalAR;
                    wfWinner.DefensiveResources += totalDr;
                }
                else
                {
                    wfWinner.AttackResources += totalAR;
                    wfWinner.DefensiveResources += totalDr;
                }

                var wfLoser = WarStatusTracker.warFactionTracker.Find(x => x.faction == oldFaction.Name);
                wfLoser.LostSystem = true;
                wfLoser.MonthlySystemsChanged -= 1;
                wfLoser.TotalSystemsChanged -= 1;
                RemoveAndFlagSystems(wfLoser, system);
                if (Settings.DefendersUseARforDR && Settings.DefensiveFactions.Contains(wfWinner.faction))
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

                if (!WarStatusTracker.SystemChangedOwners.Contains(system.Name))
                    WarStatusTracker.SystemChangedOwners.Add(system.Name);

                if (forceFlip)
                {
                    RecalculateSystemInfluence(systemStatus, faction, oldOwner);
                    systemStatus.PirateActivity = 0;
                }

                foreach (var neighbor in Sim.Starmap.GetAvailableNeighborSystem(system))
                {
                    if (!WarStatusTracker.SystemChangedOwners.Contains(neighbor.Name))
                        WarStatusTracker.SystemChangedOwners.Add(neighbor.Name);
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
            //if (WarStatusTracker.deathListTracker.Find(x => x.faction == oldFaction) == null)
            //    return;

            try
            {
                var factionTracker = WarStatusTracker.deathListTracker.Find(x => x.faction == oldFaction);
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
                if (!Settings.DefensiveFactions.Contains(oldFaction))
                {
                    foreach (var ally in Sim.GetFactionDef(oldFaction).Allies)
                    {
                        if (!IncludedFactions.Contains(ally) || faction == ally || WarStatusTracker.deathListTracker.Find(x => x.faction == ally) == null)
                            continue;
                        var factionAlly = WarStatusTracker.deathListTracker.Find(x => x.faction == ally);
                        factionAlly.deathList[faction] += killListDelta / 2;
                    }

                    //Enemies of the target faction are happy with the faction doing the beating. 
                    foreach (var enemy in Sim.GetFactionDef(oldFaction).Enemies)
                    {
                        if (!IncludedFactions.Contains(enemy) || enemy == faction || WarStatusTracker.deathListTracker.Find(x => x.faction == enemy) == null)
                            continue;
                        var factionEnemy = WarStatusTracker.deathListTracker.Find(x => x.faction == enemy);
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
            foreach (var faction in WarStatusTracker.warFactionTracker)
            {
                var attackCount = new Dictionary<string, int>();
                foreach (var target in faction.attackTargets)
                {
                    if (Settings.DefensiveFactions.Contains(target.Key) || Settings.ImmuneToWar.Contains(target.Key))
                        continue;
                    attackCount.Add(target.Key, target.Value.Count);
                }

                var i = 0;
                var topHalf = attackCount.Count / 2;
                foreach (var attackTarget in attackCount.OrderByDescending(x => x.Value))
                {
                    var warFaction = WarStatusTracker.warFactionTracker.Find(x => x.faction == attackTarget.Key);
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
            if (!WarStatusTracker.SystemChangedOwners.Contains(system.Name))
                WarStatusTracker.SystemChangedOwners.Add(system.Name);
            foreach (var neighborSystem in UnityGameInstance.BattleTechGame.Simulation.Starmap.GetAvailableNeighborSystem(system))
            {
                var wfat = WarStatusTracker.warFactionTracker.Find(x => x.faction == neighborSystem.OwnerValue.Name).attackTargets;
                if (wfat.Keys.Contains(oldOwner.faction) && wfat[oldOwner.faction].Contains(system.Name))
                    wfat[oldOwner.faction].Remove(system.Name);
            }
        }

        internal static void UpdateInfluenceFromAttacks(bool checkForSystemChange)
        {
            if (checkForSystemChange)
                WarStatusTracker.LostSystems.Clear();

            //LogDebug($"Updating influence for {WarStatusTracker.SystemStatuses.Count.ToString()} systems");
            foreach (var systemStatus in WarStatusTracker.systems)
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

                if (highestFaction != systemStatus.owner && !WarStatusTracker.FlashpointSystems.Contains(systemStatus.name) &&
                    (diffStatus > Settings.TakeoverThreshold && !WarStatusTracker.HotBox.Contains(systemStatus.name)
                                                             && (!Settings.DefensiveFactions.Contains(highestFaction) || highestFaction == "Locals") &&
                                                             !Settings.ImmuneToWar.Contains(starSystem.OwnerValue.Name)))
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
                        WarStatusTracker.LostSystems.Add(starSystem.Name);
                    }
                }

                //Local Government can take a system.
                if (systemStatus.owner != "Locals" && systemStatus.OriginalOwner == "Locals" &&
                    (highestFaction == "Locals" && systemStatus.influenceTracker[highestFaction] >= 75))
                {
                    ChangeSystemOwnership(starSystem, "Locals", true);
                    systemStatus.Contended = false;
                    WarStatusTracker.LostSystems.Add(starSystem.Name);
                }
            }

            CalculateHatred();
            foreach (var deathListTracker in WarStatusTracker.deathListTracker)
            {
                AdjustDeathList(deathListTracker, false);
            }
        }

        public static string MonthlyWarReport()
        {
            var combinedString = "";
            foreach (var faction in IncludedFactions)
            {
                var warFaction = WarStatusTracker.warFactionTracker.Find(x => x.faction == faction);
                combinedString = combinedString + "<b><u>" + Settings.FactionNames[faction] + "</b></u>\n";
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
            if (WarStatusTracker.HotBox.Contains(starSystem.Name) || (starSystem.Tags.Contains("planet_region_hyadesrim") &&
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

            if (starSystem.Tags.Contains("planet_other_pirate") || WarStatusTracker.AbandonedSystems.Contains(starSystem.Name))
            {
                contractEmployers.Add("AuriganPirates");
                contractTargets.Add("AuriganPirates");
            }

            if (!Equals(owner, FactionValues.FirstOrDefault(f => f.Name == "NoFaction")) &&
                !Equals(owner, FactionValues.FirstOrDefault(f => f.Name == "Locals")))
            {
                contractEmployers.Add(owner.Name);
                contractTargets.Add(owner.Name);
            }

            if (Settings.GaW_PoliceSupport && WarStatusTracker.ComstarAlly == owner.Name)
            {
                contractEmployers.Add(Settings.GaW_Police);
                contractTargets.Add(Settings.GaW_Police);
            }

            var neighborSystems = systemStatus.neighborSystems;
            foreach (var systemNeighbor in neighborSystems.Keys)
            {
                if (Settings.ImmuneToWar.Contains(systemNeighbor) || Settings.DefensiveFactions.Contains(systemNeighbor))
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
                var faction = OffensiveFactions[Rng.Next(OffensiveFactions.Count)];
                contractEmployers.Add(faction);
                if (!contractTargets.Contains(faction))
                    contractTargets.Add(faction);
            }

            if (starSystem.Tags.Contains("planet_region_hyadesrim") && Settings.HyadesRimCompatible)
            {
                foreach (var alliedFaction in owner.FactionDef.Allies)
                {
                    if (!contractEmployers.Contains(alliedFaction) && !Settings.HyadesTargetsOnly.Contains(alliedFaction))
                        contractEmployers.Add(alliedFaction);
                }

                foreach (var enemyFaction in owner.FactionDef.Enemies)
                {
                    if (!contractTargets.Contains(enemyFaction) && !Settings.HyadesEmployersOnly.Contains(enemyFaction))
                        contractTargets.Add(enemyFaction);
                }
            }

            var tempContractEmployers = new List<string>(contractEmployers);
            foreach (var tempEmployer in tempContractEmployers)
            {
                if (Settings.NoOffensiveContracts.Contains(tempEmployer))
                    contractEmployers.Remove(tempEmployer);
            }
        }

        internal static double DeltaInfluence(StarSystem system, double contractDifficulty, string contractTypeID, string defenseFaction, bool piratesInvolved)
        {
            var targetSystem = WarStatusTracker.systems.Find(x => x.starSystem == system);
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
                influenceChange = (11 + contractDifficulty - 2 * targetSystem.DifficultyRating) * Settings.ContractImpact[contractTypeID] / Settings.InfluenceDivisor;
            }
            catch
            {
                influenceChange = (11 + contractDifficulty - 2 * targetSystem.DifficultyRating) / Settings.InfluenceDivisor;
            }

            //Log("System Delta Influence");
            //Log(TargetSystem.name);
            //Log(WarStatusTracker.DeploymentInfluenceIncrease.ToString());
            //Log(contractDifficulty.ToString());
            //Log(TargetSystem.DifficultyRating.ToString());
            //Log(Settings.InfluenceDivisor.ToString());
            //Log(InfluenceChange.ToString());


            if (piratesInvolved)
                influenceChange *= 2;
            influenceChange = WarStatusTracker.DeploymentInfluenceIncrease * Math.Max(influenceChange, 0.5);
            if (influenceChange > maximumInfluence && !piratesInvolved)
            {
                AttackerInfluenceHolder = influenceChange;
                AttackerInfluenceHolder = Math.Round(AttackerInfluenceHolder, 1);
                InfluenceMaxed = true;
            }
            else
                InfluenceMaxed = false;

            influenceChange = Math.Min(influenceChange, maximumInfluence);
            influenceChange = Math.Round(influenceChange, 1);
            //Log(InfluenceChange.ToString());
            //Log("--------------------------");
            return influenceChange;
        }

        internal static bool WillSystemFlip(StarSystem system, string winner, string loser, double deltaInfluence, bool preBattle)
        {
            var warSystem = WarStatusTracker.systems.Find(x => x.starSystem == system);
            var tempIt = new Dictionary<string, float>(warSystem.influenceTracker);

            if (preBattle && !InfluenceMaxed)
            {
                tempIt[winner] += (float) deltaInfluence;
                tempIt[loser] -= (float) deltaInfluence;
            }
            else if (preBattle && InfluenceMaxed)
            {
                tempIt[winner] += (float) Math.Min(AttackerInfluenceHolder, 100 - tempIt[winner]);
                tempIt[loser] -= (float) deltaInfluence;
            }

            var highKey = tempIt.OrderByDescending(x => x.Value).Select(x => x.Key).First();
            var highValue = tempIt.OrderByDescending(x => x.Value).Select(x => x.Value).First();
            tempIt.Remove(highKey);
            var secondValue = tempIt.OrderByDescending(x => x.Value).Select(x => x.Value).First();

            if (highKey != warSystem.owner && highKey == winner && highValue - secondValue > Settings.TakeoverThreshold
                && !WarStatusTracker.FlashpointSystems.Contains(system.Name) &&
                (!Settings.DefensiveFactions.Contains(winner) && !Settings.ImmuneToWar.Contains(loser)))
                return true;
            return false;
        }

        internal static int CalculateFlipMissions(string attacker, StarSystem system)
        {
            var warSystem = WarStatusTracker.systems.Find(x => x.starSystem == system);
            var tempIt = new Dictionary<string, float>(warSystem.influenceTracker);
            var missionCounter = 0;
            var influenceDifference = 0.0f;
            double contractDifficulty = warSystem.DifficultyRating;
            var deploymentIfHolder = WarStatusTracker.DeploymentInfluenceIncrease;
            WarStatusTracker.DeploymentInfluenceIncrease = 1;

            while (influenceDifference <= Settings.TakeoverThreshold)
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
                WarStatusTracker.DeploymentInfluenceIncrease *= Settings.DeploymentEscalationFactor;
                missionCounter++;
            }

            WarStatusTracker.DeploymentInfluenceIncrease = deploymentIfHolder;
            return missionCounter;
        }

        public static void RecalculateSystemInfluence(SystemStatus systemStatus, string newOwner, string oldOwner)
        {
            systemStatus.influenceTracker.Clear();
            systemStatus.influenceTracker.Add(newOwner, Settings.DominantInfluence);
            systemStatus.influenceTracker.Add(oldOwner, Settings.MinorInfluencePool);

            foreach (var faction in IncludedFactions)
            {
                if (!systemStatus.influenceTracker.Keys.Contains(faction))
                    systemStatus.influenceTracker.Add(faction, 0);
            }
        }

        public static void AdjustDeathList(DeathListTracker deathListTracker, bool reloadFromSave)
        {
            var trackerDeathList = deathListTracker.deathList;
            var trackerFaction = deathListTracker.faction;
            var trackerFactionDef = Sim.GetFactionDef(trackerFaction);
            var trackerFactionEnemies = new List<string>(trackerFactionDef.Enemies);
            var trackerFactionAllies = new List<string>(trackerFactionDef.Allies);

            if (WarStatusTracker.InactiveTHRFactions.Contains(trackerFaction) || WarStatusTracker.NeverControl.Contains(trackerFaction))
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

            var deathListOffensiveFactions = new List<string>(trackerDeathList.Keys.Except(Settings.DefensiveFactions));
            var warFaction = deathListTracker.WarFaction;
            var hasEnemy = false;
            //Defensive Only factions are always neutral
            Settings.DefensiveFactions.Do(x => trackerDeathList[x] = 50);
            if (Settings.GaW_PoliceSupport && warFaction.ComstarSupported)
                trackerDeathList[Settings.GaW_Police] = 99;

            foreach (var offensiveFaction in deathListOffensiveFactions)
            {
                if (WarStatusTracker.InactiveTHRFactions.Contains(offensiveFaction) || WarStatusTracker.NeverControl.Contains(offensiveFaction))
                    continue;

                //Check to see if factions are always allied with each other.
                if (Settings.FactionsAlwaysAllies.Keys.Contains(warFaction.faction) && Settings.FactionsAlwaysAllies[warFaction.faction].Contains(offensiveFaction))
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
                var rand = Rng.Next(0, IncludedFactions.Count);
                var newEnemy = IncludedFactions[rand];

                while (newEnemy == trackerFaction || Settings.ImmuneToWar.Contains(newEnemy) || Settings.DefensiveFactions.Contains(newEnemy))
                {
                    rand = Rng.Next(0, IncludedFactions.Count);
                    newEnemy = IncludedFactions[rand];
                }

                if (warFaction.adjacentFactions.Count != 0)
                {
                    rand = Rng.Next(0, warFaction.adjacentFactions.Count);
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
            //SimGameResultAction simGameResultAction = new SimGameResultAction();
            //simGameResultAction.Type = SimGameResultAction.ActionType.System_ShowSummaryOverlay;
            //simGameResultAction.value = Strings.T("Galaxy at War");
            //simGameResultAction.additionalValues = new string[1];
            //simGameResultAction.additionalValues[0] = Strings.T("In Galaxy at War, the Great Houses of the Inner Sphere will not simply wait for a wedding invitation" +
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


            //SimGameState.ApplyEventAction(simGameResultAction, null);
            //UnityGameInstance.BattleTechGame.Simulation.StopPlayMode();
        }
    }
}
