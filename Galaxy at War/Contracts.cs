using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.Data;
using BattleTech.Framework;
using Harmony;

namespace GalaxyatWar
{
    public static class Contracts
    {
        private static int min;
        private static int max;
        private static int actualDifficulty;

        internal static Contract GenerateContract(StarSystem system, int minDiff, int maxDiff, string employer = null,
            List<string> opFor = null, bool usingBreadcrumbs = false, bool includeOwnershipCheck = false)
        {
            min = minDiff;
            max = maxDiff;
            actualDifficulty = Globals.Rng.Next(min, max + 1);
            var difficultyRange = new SimGameState.ContractDifficultyRange(
                actualDifficulty, actualDifficulty, ContractDifficulty.Easy, ContractDifficulty.Easy);
            var potentialContracts = GetSinglePlayerProceduralContractOverrides(difficultyRange)
                .Where(x => x.Value.Any(c => c.finalDifficulty + c.difficultyUIModifier <= actualDifficulty))
                .ToDictionary(k => k.Key, v => v.Value);
            var playableMaps = GetSinglePlayerProceduralPlayableMaps(system, includeOwnershipCheck);
            var validParticipants = GetValidParticipants(system, employer, opFor);
            var source = playableMaps.Select(map => map.Map.Weight);
            var activeMaps = new WeightedList<MapAndEncounters>(WeightedListType.SimpleRandom, playableMaps.ToList(), source.ToList());
            var next = activeMaps.GetNext();
            var mapEncounterContractData = Globals.Sim.FillMapEncounterContractData(system, difficultyRange, potentialContracts, validParticipants, next);
            system.SetCurrentContractFactions();
            var gameContext = new GameContext(Globals.Sim.Context);
            gameContext.SetObject(GameContextObjectTagEnum.TargetStarSystem, system);
            if (mapEncounterContractData.FlatContracts.rootList == null ||
                mapEncounterContractData.FlatContracts.rootList.Count < 1)
            {
                return GenerateContract(system, minDiff, maxDiff, employer, opFor, usingBreadcrumbs, includeOwnershipCheck);
            }

            var proceduralContract = CreateProceduralContract(system, usingBreadcrumbs, next, mapEncounterContractData, gameContext);
            return proceduralContract;
        }

        private static Contract CreateProceduralContract(
            StarSystem system,
            bool usingBreadcrumbs,
            MapAndEncounters level,
            SimGameState.MapEncounterContractData MapEncounterContractData,
            GameContext gameContext)
        {
            var flatContracts = MapEncounterContractData.FlatContracts;
            Globals.Sim.FilterContracts(flatContracts);
            var next = flatContracts.GetNext();
            var id = next.contractOverride.ContractTypeValue.ID;
            MapEncounterContractData.Encounters[id].Shuffle();
            var encounterLayerGuid = MapEncounterContractData.Encounters[id][0].EncounterLayerGUID;
            var contractOverride = next.contractOverride;
            var employer = next.employer;
            var target = next.target;
            var employerAlly = next.employerAlly;
            var targetAlly = next.targetAlly;
            var neutralToAll = next.NeutralToAll;
            var hostileToAll = next.HostileToAll;
            var contract = usingBreadcrumbs
                ? CreateTravelContract(
                    level.Map.MapName,
                    level.Map.MapPath,
                    encounterLayerGuid,
                    next.contractOverride.ContractTypeValue,
                    contractOverride,
                    gameContext,
                    employer,
                    target,
                    targetAlly,
                    employerAlly,
                    neutralToAll,
                    hostileToAll,
                    false,
                    actualDifficulty)
                : new Contract(
                    level.Map.MapName,
                    level.Map.MapPath,
                    encounterLayerGuid,
                    next.contractOverride.ContractTypeValue,
                    Globals.Sim.BattleTechGame,
                    contractOverride,
                    gameContext,
                    true,
                    actualDifficulty);

            Globals.Sim.mapDiscardPile.Add(level.Map.MapID);
            Globals.Sim.contractDiscardPile.Add(contractOverride.ID);
            PrepContract(contract,
                employer,
                employerAlly,
                target,
                targetAlly,
                neutralToAll,
                hostileToAll,
                level.Map.BiomeSkinEntry.BiomeSkin,
                contract.Override.travelSeed,
                system);
            return contract;
        }

        public static Contract CreateTravelContract(
            string mapName,
            string mapPath,
            string encounterGuid,
            ContractTypeValue contractTypeValue,
            ContractOverride ovr,
            GameContext context,
            FactionValue employer,
            FactionValue target,
            FactionValue targetsAlly,
            FactionValue employersAlly,
            FactionValue neutralToAll,
            FactionValue hostileToAll,
            bool isGlobal,
            int difficulty)
        {
            Logger.Log("CreateTravelContract");
            var starSystem = context.GetObject(GameContextObjectTagEnum.TargetStarSystem) as StarSystem;
            var seed = Globals.Rng.Next(0, int.MaxValue);
            ovr.FullRehydrate();
            var contractOverride = new ContractOverride();
            contractOverride.CopyContractTypeData(ovr);
            contractOverride.contractName = ovr.contractName;
            contractOverride.difficulty = ovr.difficulty;
            contractOverride.longDescription = ovr.longDescription;
            contractOverride.shortDescription = ovr.shortDescription;
            contractOverride.travelOnly = true;
            contractOverride.useTravelCostPenalty = !isGlobal;
            contractOverride.disableNegotations = ovr.disableNegotations;
            contractOverride.disableAfterAction = ovr.disableAfterAction;
            contractOverride.salvagePotential = ovr.salvagePotential;
            contractOverride.contractRewardOverride = ovr.contractRewardOverride;
            contractOverride.travelSeed = seed;
            contractOverride.difficultyUIModifier = ovr.difficultyUIModifier;
            var simGameEventResult = new SimGameEventResult();
            var gameResultAction = new SimGameResultAction();
            var length = 14;
            gameResultAction.Type = SimGameResultAction.ActionType.System_StartNonProceduralContract;
            gameResultAction.value = mapName;
            gameResultAction.additionalValues = new string[length];
            gameResultAction.additionalValues[0] = starSystem.ID;
            gameResultAction.additionalValues[1] = mapPath;
            gameResultAction.additionalValues[2] = encounterGuid;
            gameResultAction.additionalValues[3] = ovr.ID;
            gameResultAction.additionalValues[4] = isGlobal.ToString();
            gameResultAction.additionalValues[5] = employer.Name;
            gameResultAction.additionalValues[6] = target.Name;
            gameResultAction.additionalValues[7] = difficulty.ToString();
            gameResultAction.additionalValues[8] = "true";
            gameResultAction.additionalValues[9] = targetsAlly.Name;
            gameResultAction.additionalValues[10] = seed.ToString();
            gameResultAction.additionalValues[11] = employersAlly.Name;
            gameResultAction.additionalValues[12] = neutralToAll.Name;
            gameResultAction.additionalValues[13] = hostileToAll.Name;
            Logger.LogDebug("-");
            Logger.LogDebug(gameResultAction);
            Logger.LogDebug("--");
            gameResultAction.additionalValues.Do(Logger.LogDebug);
            Logger.LogDebug("---");
            simGameEventResult.Actions = new SimGameResultAction[1];
            simGameEventResult.Actions[0] = gameResultAction;
            contractOverride.OnContractSuccessResults.Add(simGameEventResult);
            Logger.LogDebug(contractOverride.OnContractSuccessResults[0]);
            return new Contract(mapName, mapPath, encounterGuid, contractTypeValue, Globals.Sim.BattleTechGame, contractOverride, context, true, actualDifficulty)
            {
                Override =
                {
                    travelSeed = seed
                }
            };
        }

        private static void PrepContract(
            Contract contract,
            FactionValue employer,
            FactionValue employersAlly,
            FactionValue target,
            FactionValue targetsAlly,
            FactionValue NeutralToAll,
            FactionValue HostileToAll,
            Biome.BIOMESKIN skin,
            int presetSeed,
            StarSystem system)
        {
            if (presetSeed != 0 && !contract.IsPriorityContract)
            {
                var diff = Globals.Rng.Next(min, max + 1);
                contract.SetFinalDifficulty(diff);
            }

            var unitFactionValue1 = FactionEnumeration.GetPlayer1sMercUnitFactionValue();
            var unitFactionValue2 = FactionEnumeration.GetPlayer2sMercUnitFactionValue();
            contract.AddTeamFaction("bf40fd39-ccf9-47c4-94a6-061809681140", unitFactionValue1.ID);
            contract.AddTeamFaction("757173dd-b4e1-4bb5-9bee-d78e623cc867", unitFactionValue2.ID);
            contract.AddTeamFaction("ecc8d4f2-74b4-465d-adf6-84445e5dfc230", employer.ID);
            contract.AddTeamFaction("70af7e7f-39a8-4e81-87c2-bd01dcb01b5e", employersAlly.ID);
            contract.AddTeamFaction("be77cadd-e245-4240-a93e-b99cc98902a5", target.ID);
            contract.AddTeamFaction("31151ed6-cfc2-467e-98c4-9ae5bea784cf", targetsAlly.ID);
            contract.AddTeamFaction("61612bb3-abf9-4586-952a-0559fa9dcd75", NeutralToAll.ID);
            contract.AddTeamFaction("3c9f3a20-ab03-4bcb-8ab6-b1ef0442bbf0", HostileToAll.ID);
            contract.SetupContext();
            var finalDifficulty = contract.Override.finalDifficulty;
            var cbills = SimGameState.RoundTo(contract.Override.contractRewardOverride < 0
                ? Globals.Sim.CalculateContractValueByContractType(contract.ContractTypeValue, finalDifficulty,
                    Globals.Sim.Constants.Finances.ContractPricePerDifficulty, Globals.Sim.Constants.Finances.ContractPriceVariance, presetSeed)
                : (float) contract.Override.contractRewardOverride, 1000);
            contract.SetInitialReward(cbills);
            contract.SetBiomeSkin(skin);
        }

        private static Dictionary<int, List<ContractOverride>> GetSinglePlayerProceduralContractOverrides(SimGameState.ContractDifficultyRange diffRange)
        {
            return MetadataDatabase.Instance.GetContractsByDifficultyRangeAndScopeAndOwnership(
                    (int) diffRange.MinDifficultyClamped,
                    (int) diffRange.MaxDifficultyClamped,
                    Globals.Sim.ContractScope, true)
                .Where(c => c.ContractTypeRow.IsSinglePlayerProcedural)
                .GroupBy(c =>
                    (int) c.ContractTypeRow.ContractTypeID, c => c.ContractID)
                .ToDictionary(c => c.Key, c => c.Select(ci => Globals.Sim.DataManager.ContractOverrides.Get(ci))
                    .ToList());
        }

        private static WeightedList<MapAndEncounters> GetSinglePlayerProceduralPlayableMaps(StarSystem system, bool includeOwnershipCheck)
        {
            return MetadataDatabase.Instance.GetReleasedMapsAndEncountersBySinglePlayerProceduralContractTypeAndTags(
                    system.Def.MapRequiredTags,
                    system.Def.MapExcludedTags,
                    system.Def.SupportedBiomes,
                    includeOwnershipCheck)
                .ToWeightedList(WeightedListType.SimpleRandom);
        }

        private static Dictionary<string, WeightedList<SimGameState.ContractParticipants>> GetValidParticipants(StarSystem system, string employer, List<string> opFor)
        {
            var employers = system.Def.ContractEmployerIDList.Where(e => !Globals.Sim.ignoredContractEmployers.Contains(e));
            FactionDef employerDef = default;
            if (!string.IsNullOrEmpty(employer))
            {
                employers = new[] {employer};
                employerDef = FactionEnumeration.GetFactionByName(employer).FactionDef;
            }

            var result = employers.Select(e => new
                {
                    Employer = employer ?? e,
                    Participants = GenerateContractParticipants(employerDef ?? Globals.Sim.factions[e], system.Def, opFor)
                })
                .Where(e => e.Participants.Any()).ToDictionary(e => e.Employer, t => t.Participants);
            return result;
        }

        private static WeightedList<SimGameState.ContractParticipants> GenerateContractParticipants(FactionDef employer, StarSystemDef system, List<string> opFor)
        {
            var weightedList1 = new WeightedList<SimGameState.ContractParticipants>(WeightedListType.PureRandom);
            var enemies = opFor?.Count > 0
                ? opFor
                : employer.Enemies.Where(t =>
                    system.ContractTargetIDList.Contains(t) &&
                    !Globals.Sim.IgnoredContractTargets.Contains(t) &&
                    !Globals.Sim.IsFactionAlly(FactionEnumeration.GetFactionByName(t))).ToList();
            var neutrals = FactionEnumeration.PossibleNeutralToAllList.Where(f =>
                !employer.FactionValue.Equals(f) &&
                !Globals.Sim.IgnoredContractTargets.Contains(f.Name)).ToList();
            var hostiles = FactionEnumeration.PossibleHostileToAllList.Where(f =>
                !employer.FactionValue.Equals(f) &&
                !Globals.Sim.IgnoredContractTargets.Contains(f.Name)).ToList();
            var allies = FactionEnumeration.PossibleAllyFallbackList.Where(f =>
                !employer.FactionValue.Equals(f) &&
                !Globals.Sim.IgnoredContractTargets.Contains(f.Name)).ToList();
            foreach (var str in enemies)
            {
                var target = str;
                var targetFactionDef = Globals.Sim.factions[target];
                var mercenariesFactionValue = FactionEnumeration.GetHostileMercenariesFactionValue();
                var defaultHostileFaction = Globals.Sim.GetDefaultHostileFaction(employer.FactionValue, targetFactionDef.FactionValue);
                var defaultTargetAlly = allies.Where(f =>
                    !targetFactionDef.Enemies.Contains(f.Name) &&
                    !employer.Allies.Contains(f.Name) &&
                    target != f.Name).DefaultIfEmpty(targetFactionDef.FactionValue).GetRandomElement(Globals.Sim.NetworkRandom);
                var randomElement = allies.Where(f =>
                    !employer.Enemies.Contains(f.Name) &&
                    !targetFactionDef.Allies.Contains(f.Name) &&
                    defaultTargetAlly != f && target != f.Name).DefaultIfEmpty(employer.FactionValue).GetRandomElement(Globals.Sim.NetworkRandom);
                var weightedList2 = targetFactionDef.Allies.Select(FactionEnumeration.GetFactionByName).Where(f =>
                    !employer.Allies.Contains(f.Name) &&
                    !Globals.Sim.IgnoredContractTargets.Contains(f.Name)).DefaultIfEmpty(defaultTargetAlly).ToWeightedList(WeightedListType.PureRandom);
                var weightedList3 = employer.Allies.Select(FactionEnumeration.GetFactionByName).Where(f =>
                    !targetFactionDef.Allies.Contains(f.Name) &&
                    !Globals.Sim.IgnoredContractTargets.Contains(f.Name)).DefaultIfEmpty(randomElement).ToWeightedList(WeightedListType.PureRandom);
                var list2 = neutrals.Where(f =>
                    target != f.Name &&
                    !targetFactionDef.Enemies.Contains(f.Name) &&
                    !employer.Enemies.Contains(f.Name)).DefaultIfEmpty(mercenariesFactionValue).ToList();
                var list3 = hostiles.Where(f =>
                    target != f.Name &&
                    !targetFactionDef.Allies.Contains(f.Name) &&
                    !employer.Allies.Contains(f.Name)).DefaultIfEmpty(defaultHostileFaction).ToList();
                weightedList1.Add(new SimGameState.ContractParticipants(targetFactionDef.FactionValue, weightedList2, weightedList3, list2, list3));
            }

            return weightedList1;
        }

        [HarmonyPatch(typeof(ContractOverride), "GetUIDifficulty")]
        public class ContractOverrideGetUIDifficultyPatch
        {
            private static void Postfix(ContractOverride __instance, ref int __result)
            {
                __result = __instance.contract.Difficulty != __result
                    ? __instance.contract.Difficulty
                    : __result;
            }
        }

        [HarmonyPatch(typeof(SimGameState), "FillMapEncounterContractData")]
        public class SimGameStateFillMapEncounterContractDataPatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
            {
                var from = AccessTools.Method(typeof(SimGameState), nameof(SimGameState.CreatePotentialContract));
                var to = AccessTools.Method(typeof(SimGameStateFillMapEncounterContractDataPatch), nameof(CreatePotentialContract));
                return ins.MethodReplacer(from, to);
            }

            private static bool CreatePotentialContract(
                StarSystem system,
                SimGameState.ContractDifficultyRange diffRange,
                ContractOverride contractOvr,
                Dictionary<string, WeightedList<SimGameState.ContractParticipants>> validTargets,
                MapAndEncounters level,
                int encounterContractTypeID,
                out SimGameState.PotentialContract returnContract)
            {
                returnContract = new SimGameState.PotentialContract();
                if (Globals.Sim.GetValidFaction(system, validTargets, contractOvr.requirementList, out var chosenContractParticipants))
                {
                    system.SetCurrentContractFactions(chosenContractParticipants.Employer, chosenContractParticipants.Target);
                    if (Globals.Sim.DoesContractMeetRequirements(system, level, contractOvr))
                    {
                        returnContract = new SimGameState.PotentialContract
                        {
                            contractOverride = contractOvr,
                            difficulty = actualDifficulty,
                            employer = chosenContractParticipants.Employer,
                            target = chosenContractParticipants.Target,
                            employerAlly = chosenContractParticipants.EmployersAlly,
                            targetAlly = chosenContractParticipants.TargetsAlly,
                            NeutralToAll = chosenContractParticipants.NeutralToAll,
                            HostileToAll = chosenContractParticipants.HostileToAll
                        };
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
