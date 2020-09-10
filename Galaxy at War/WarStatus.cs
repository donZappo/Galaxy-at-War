using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Newtonsoft.Json;
using static GalaxyatWar.Globals;
using static GalaxyatWar.Helpers;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

namespace GalaxyatWar
{
    public class WarStatus
    {
        public List<SystemStatus> systems = new List<SystemStatus>();
        internal List<SystemStatus> systemsByResources = new List<SystemStatus>();
        public List<DeathListTracker> deathListTracker = new List<DeathListTracker>();
        public List<WarFaction> warFactionTracker = new List<WarFaction>();
        public bool JustArrived = true;
        public bool Escalation = false;
        public bool Deployment = false;
        public WorkOrderEntry_Notification EscalationOrder;
        public int EscalationDays = 0;
        public List<string> PrioritySystems = new List<string>();
        public string CurSystem;
        public bool HotBoxTravelling;
        public bool StartGameInitialized = false;
        public bool FirstTickInitialization = true;
        public List<string> SystemChangedOwners = new List<string>();
        public List<string> HotBox = new List<string>();
        public List<string> LostSystems = new List<string>();
        public bool GaW_Event_PopUp = false;

        public List<string> HomeContendedStrings = new List<string>();
        public List<string> AbandonedSystems = new List<string>();
        public List<string> DeploymentContracts = new List<string>();
        public string DeploymentEmployer = "Marik";
        public double DeploymentInfluenceIncrease = 1.0;
        public bool PirateDeployment = false;

        public Dictionary<string, float> FullHomeContendedSystems = new Dictionary<string, float>();
        public List<string> HomeContendedSystems = new List<string>();
        public Dictionary<string, List<string>> ExternalPriorityTargets = new Dictionary<string, List<string>>();
        public List<string> FullPirateSystems = new List<string>();
        public List<string> PirateHighlight = new List<string>();
        public float PirateResources;
        public float TempPRGain;
        public float MinimumPirateResources;
        public float StartingPirateResources;
        public float LastPRGain;
        public List<string> HyadesRimGeneralPirateSystems = new List<string>();
        public int HyadesRimsSystemsTaken = 0;
        public List<string> InactiveTHRFactions = new List<string>();
        public List<string> FlashpointSystems = new List<string>();
        public List<string> NeverControl = new List<string>();
        public int ComstarCycle = 0;
        public string ComstarAlly = "";

        public WarStatus()
        {
            Logger.LogDebug("WarStatus ctor");
            if (Settings.ISMCompatibility)
                Settings.IncludedFactions = new List<string>(Settings.IncludedFactions_ISM);

            CurSystem = Sim.CurSystem.Name;
            TempPRGain = 0;
            HotBoxTravelling = false;
            HotBox = new List<string>();
            if (Settings.HyadesRimCompatible)
            {
                InactiveTHRFactions = Settings.HyadesAppearingPirates;
                FlashpointSystems = Settings.HyadesFlashpointSystems;
                NeverControl = Settings.HyadesNeverControl;
            }

            //initialize all WarFactions, DeathListTrackers, and SystemStatuses
            foreach (var faction in Settings.IncludedFactions)
            {
                var warFaction = new WarFaction(faction);
                var d = new DeathListTracker(faction)
                {
                    WarFaction = warFaction
                };
                warFaction.DeathListTracker = d;
                warFactionTracker.Add(warFaction);
                deathListTracker.Add(d);
            }

            for (var index = 0; index < Sim.StarSystems.Count; index++)
            {
                var system = Sim.StarSystems[index];
                if (system.OwnerValue.Name == "NoFaction" || system.OwnerValue.Name == "AuriganPirates")
                    AbandonedSystems.Add(system.Name);
                var warFaction = warFactionTracker.Find(x => x.faction == system.OwnerValue.Name);
                if (Settings.DefensiveFactions.Contains(warFaction.faction) && Settings.DefendersUseARforDR)
                    warFaction.DefensiveResources += GetTotalAttackResources(system);
                else
                    warFaction.AttackResources += GetTotalAttackResources(system);
                warFaction.DefensiveResources += GetTotalDefensiveResources(system);
            }

            var maxAR = warFactionTracker.Select(x => x.AttackResources).Max();
            var maxDR = warFactionTracker.Select(x => x.DefensiveResources).Max();

            foreach (var faction in Settings.IncludedFactions)
            {
                var warFaction = warFactionTracker.Find(x => x.faction == faction);
                if (Settings.DefensiveFactions.Contains(faction) && Settings.DefendersUseARforDR)
                {
                    if (!Settings.ISMCompatibility)
                        warFaction.DefensiveResources = maxAR + maxDR + Settings.BonusAttackResources[faction] +
                                                        Settings.BonusDefensiveResources[faction];
                    else
                        warFaction.DefensiveResources = maxAR + maxDR + Settings.BonusAttackResources_ISM[faction] +
                                                        Settings.BonusDefensiveResources_ISM[faction];

                    warFaction.AttackResources = 0;
                }
                else
                {
                    if (!Settings.ISMCompatibility)
                    {
                        warFaction.AttackResources = maxAR + Settings.BonusAttackResources[faction];
                        warFaction.DefensiveResources = maxDR + Settings.BonusDefensiveResources[faction];
                    }
                    else
                    {
                        warFaction.AttackResources = maxAR + Settings.BonusAttackResources_ISM[faction];
                        warFaction.DefensiveResources = maxDR + Settings.BonusDefensiveResources_ISM[faction];
                    }
                }
            }

            if (!Settings.ISMCompatibility)
                PirateResources = maxAR * Settings.FractionPirateResources + Settings.BonusPirateResources;
            else
                PirateResources = maxAR * Settings.FractionPirateResources_ISM + Settings.BonusPirateResources_ISM;

            MinimumPirateResources = PirateResources;
            StartingPirateResources = PirateResources;
            for (var index = 0; index < Sim.StarSystems.Count; index++)
            {
                var system = Sim.StarSystems[index];
                var systemStatus = new SystemStatus(system, system.OwnerValue.Name);
                systems.Add(systemStatus);
                if (system.Tags.Contains("planet_other_pirate") && !system.Tags.Contains("planet_region_hyadesrim"))
                {
                    FullPirateSystems.Add(system.Name);
                    PiratesAndLocals.FullPirateListSystems.Add(systemStatus);
                }

                if (system.Tags.Contains("planet_region_hyadesrim") && !FlashpointSystems.Contains(system.Name)
                                                                    && (system.OwnerValue.Name == "NoFaction" || system.OwnerValue.Name == "Locals"))
                    HyadesRimGeneralPirateSystems.Add(system.Name);
            }

            systems = systems.OrderBy(x => x.name).ToList();
            systemsByResources = systems.OrderBy(x => x.TotalResources).ToList();
        }

        [JsonConstructor]
        public WarStatus(bool fake = false)
        {
        }
    }

    public class SystemStatus
    {
        public string name;
        public string owner;

        public Dictionary<string, int> neighborSystems = new Dictionary<string, int>();
        public Dictionary<string, float> influenceTracker = new Dictionary<string, float>();
        public float TotalResources;
        public bool PriorityDefense = false;
        public bool PriorityAttack = false;
        public List<string> CurrentlyAttackedBy = new List<string>();
        public bool Contended = false;
        public int DifficultyRating;
        public bool BonusSalvage;
        public bool BonusXP;
        public bool BonusCBills;
        public float AttackResources;
        public float DefenseResources;
        public float PirateActivity;
        public string CoreSystemID;
        public int DeploymentTier = 0;
        public string OriginalOwner = null;
        private StarSystem starSystemBackingField;

        internal StarSystem starSystem
        {
            get
            {
                return starSystemBackingField ?? (starSystemBackingField = Sim.StarSystems.Find(s => s.Name == name));
            }
            private set => starSystemBackingField = value;
        }

        [JsonConstructor]
        public SystemStatus()
        {
            // don't want our ctor running at deserialization
        }

        public SystemStatus(StarSystem system, string faction)
        {
            //  LogDebug("SystemStatus ctor");
            name = system.Name;
            owner = faction;
            starSystem = system;
            // warFaction = Mod.SystemStatus.warFactionTracker.Find(x => x.faction == owner);
            AttackResources = GetTotalAttackResources(starSystem);
            DefenseResources = GetTotalDefensiveResources(starSystem);
            TotalResources = AttackResources + DefenseResources;
            CoreSystemID = system.Def.CoreSystemID;
            BonusCBills = false;
            BonusSalvage = false;
            BonusXP = false;
            if (system.Tags.Contains("planet_other_pirate") && !Settings.HyadesFlashpointSystems.Contains(name))
                if (!Settings.ISMCompatibility)
                    PirateActivity = Settings.StartingPirateActivity;
                else
                    PirateActivity = Settings.StartingPirateActivity_ISM;
            FindNeighbors();
            CalculateSystemInfluence();
            InitializeContracts();
        }

        public void FindNeighbors()
        {
            try
            {
                neighborSystems.Clear();
                var neighbors = Sim.Starmap.GetAvailableNeighborSystem(starSystem);
                foreach (var neighborSystem in neighbors)
                {
                    if (neighborSystems.ContainsKey(neighborSystem.OwnerValue.Name))
                        neighborSystems[neighborSystem.OwnerValue.Name] += 1;
                    else
                        neighborSystems.Add(neighborSystem.OwnerValue.Name, 1);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        // determine starting influence based on neighboring systems
        public void CalculateSystemInfluence()
        {
            influenceTracker.Clear();
            if (!Settings.HyadesRimCompatible)
            {
                if (owner == "NoFaction")
                    influenceTracker.Add("NoFaction", 100);
                if (owner == "Locals")
                    influenceTracker.Add("Locals", 100);

                if (owner != "NoFaction" && owner != "Locals")
                {
                    influenceTracker.Add(owner, Settings.DominantInfluence);
                    var remainingInfluence = Settings.MinorInfluencePool;

                    if (!(neighborSystems.Keys.Count == 1 && neighborSystems.Keys.Contains(owner)) && neighborSystems.Keys.Count != 0)
                    {
                        while (remainingInfluence > 0)
                        {
                            foreach (var faction in neighborSystems.Keys)
                            {
                                if (faction != owner)
                                {
                                    var influenceDelta = neighborSystems[faction];
                                    remainingInfluence -= influenceDelta;
                                    if (Settings.DefensiveFactions.Contains(faction))
                                        continue;
                                    if (influenceTracker.ContainsKey(faction))
                                        influenceTracker[faction] += influenceDelta;
                                    else
                                        influenceTracker.Add(faction, influenceDelta);
                                }
                            }
                        }
                    }
                }

                foreach (var faction in IncludedFactions)
                {
                    if (!influenceTracker.Keys.Contains(faction))
                        influenceTracker.Add(faction, 0);
                }

                // need percentages from InfluenceTracker data 
                var totalInfluence = influenceTracker.Values.Sum();
                var tempDict = new Dictionary<string, float>();
                foreach (var kvp in influenceTracker)
                {
                    tempDict[kvp.Key] = kvp.Value / totalInfluence * 100;
                }

                influenceTracker = tempDict;
            }
            else
            {
                if (owner == "NoFaction" && !starSystem.Tags.Contains("planet_region_hyadesrim"))
                    influenceTracker.Add("NoFaction", 100);
                if (owner == "Locals" && !starSystem.Tags.Contains("planet_region_hyadesrim"))
                    influenceTracker.Add("Locals", 100);
                if ((owner == "NoFaction" || owner == "Locals") && starSystem.Tags.Contains("planet_region_hyadesrim"))
                {
                    foreach (var pirateFaction in starSystem.Def.ContractEmployerIDList)
                    {
                        if (Settings.HyadesNeverControl.Contains(pirateFaction))
                            continue;

                        if (!influenceTracker.Keys.Contains(pirateFaction))
                            influenceTracker.Add(pirateFaction, Settings.MinorInfluencePool);
                    }

                    foreach (var pirateFaction in starSystem.Def.ContractTargetIDList)
                    {
                        if (Settings.HyadesNeverControl.Contains(pirateFaction))
                            continue;

                        if (!influenceTracker.Keys.Contains(pirateFaction))
                            influenceTracker.Add(pirateFaction, Settings.MinorInfluencePool);
                    }
                }


                if (owner != "NoFaction" && owner != "Locals")
                {
                    influenceTracker.Add(owner, Settings.DominantInfluence);
                    var remainingInfluence = Settings.MinorInfluencePool;

                    if (!(neighborSystems.Keys.Count == 1 && neighborSystems.Keys.Contains(owner)) && neighborSystems.Keys.Count != 0)
                    {
                        while (remainingInfluence > 0)
                        {
                            foreach (var faction in neighborSystems.Keys)
                            {
                                if (faction != owner)
                                {
                                    var influenceDelta = neighborSystems[faction];
                                    remainingInfluence -= influenceDelta;
                                    if (Settings.DefensiveFactions.Contains(faction))
                                        continue;
                                    if (influenceTracker.ContainsKey(faction))
                                        influenceTracker[faction] += influenceDelta;
                                    else
                                        influenceTracker.Add(faction, influenceDelta);
                                }
                            }
                        }
                    }
                }

                foreach (var faction in IncludedFactions)
                {
                    if (!influenceTracker.Keys.Contains(faction))
                        influenceTracker.Add(faction, 0);
                }

                // need percentages from InfluenceTracker data 
                var totalInfluence = influenceTracker.Values.Sum();
                var tempDict = new Dictionary<string, float>();
                foreach (var kvp in influenceTracker)
                {
                    tempDict[kvp.Key] = kvp.Value / totalInfluence * 100;
                }

                influenceTracker = tempDict;
            }
        }

        public void InitializeContracts()
        {
            if (Settings.HyadesRimCompatible && starSystem.Tags.Contains("planet_region_hyadesrim") && (owner == "NoFaction" || owner == "Locals" || Settings.HyadesFlashpointSystems.Contains(name)))
                return;

            var ContractEmployers = starSystem.Def.ContractEmployerIDList;
            var ContractTargets = starSystem.Def.ContractTargetIDList;

            ContractEmployers.Clear();
            ContractTargets.Clear();
            ContractEmployers.Add(owner);

            foreach (var EF in Settings.DefensiveFactions)
            {
                if (Settings.ImmuneToWar.Contains(EF))
                    continue;
                ContractTargets.Add(EF);
            }

            if (!ContractTargets.Contains(owner))
                ContractTargets.Add(owner);

            foreach (var systemNeighbor in neighborSystems.Keys)
            {
                if (Settings.ImmuneToWar.Contains(systemNeighbor))
                    continue;
                if (!ContractEmployers.Contains(systemNeighbor) && !Settings.DefensiveFactions.Contains(systemNeighbor))
                    ContractEmployers.Add(systemNeighbor);

                if (!ContractTargets.Contains(systemNeighbor) && !Settings.DefensiveFactions.Contains(systemNeighbor))
                    ContractTargets.Add(systemNeighbor);
            }
        }
    }

    public class WarFaction
    {
        public string faction;
        public bool GainedSystem;
        public bool LostSystem;
        public float DaysSinceSystemAttacked;
        public float DaysSinceSystemLost;
        public float AttackResources;
        public float DefensiveResources;
        public int MonthlySystemsChanged;
        public int TotalSystemsChanged;
        public float PirateARLoss;
        public float PirateDRLoss;
        public float AR_Against_Pirates = 0;
        public float DR_Against_Pirates = 0;
        public bool ComstarSupported = false;
        public float AR_PerPlanet = 0;
        public float DR_PerPlanet = 0;

        // removing this will break saves 
        public int NumberOfSystems
        {
            get
            {
                return Sim.StarSystems.Count(system => system.OwnerDef == Sim.factions[faction]);
            }
        }

        public Dictionary<string, float> warFactionAttackResources = new Dictionary<string, float>();
        public Dictionary<string, List<string>> attackTargets = new Dictionary<string, List<string>>();
        public List<string> defenseTargets = new List<string>();
        public Dictionary<string, bool> IncreaseAggression = new Dictionary<string, bool>();
        public List<string> adjacentFactions = new List<string>();
        private DeathListTracker deathListTrackerBackingField;

        internal DeathListTracker DeathListTracker
        {
            get => deathListTrackerBackingField ?? (deathListTrackerBackingField = WarStatusTracker.deathListTracker.Find(x => x.faction == faction));
            set => deathListTrackerBackingField = value;
        }

        [JsonConstructor]
        public WarFaction()
        {
            // deser ctor
        }

        public WarFaction(string faction)
        {
            Logger.LogDebug("WarFaction ctor: " + faction);
            this.faction = faction;
            GainedSystem = false;
            LostSystem = false;
            DaysSinceSystemAttacked = 0;
            DaysSinceSystemLost = 0;
            MonthlySystemsChanged = 0;
            TotalSystemsChanged = 0;
            PirateARLoss = 0;
            PirateDRLoss = 0;
            foreach (var startFaction in IncludedFactions)
                IncreaseAggression.Add(startFaction, false);
        }
    }

    public class DeathListTracker
    {
        public string faction;
        public Dictionary<string, float> deathList = new Dictionary<string, float>();
        public List<string> Enemies => deathList.Where(x => x.Value >= 75).Select(x => x.Key).ToList();
        public List<string> Allies => deathList.Where(x => x.Value <= 25).Select(x => x.Key).ToList();
        private WarFaction warFactionBackingField;

        internal WarFaction WarFaction
        {
            get
            {
                return warFactionBackingField ?? (warFactionBackingField = WarStatusTracker.warFactionTracker.Find(x => x.faction == faction));
            }
            set => warFactionBackingField = value;
        }

        [JsonConstructor]
        public DeathListTracker()
        {
            // deser ctor
        }

        public DeathListTracker(string faction)
        {
            Logger.LogDebug("DeathListTracker ctor: " + faction);

            this.faction = faction;
            var factionDef = Sim.GetFactionDef(faction);

            // TODO comment this
            foreach (var includedFaction in IncludedFactions)
            {
                var def = Sim.GetFactionDef(includedFaction);
                if (!IncludedFactions.Contains(def.FactionValue.Name))
                    continue;
                if (factionDef != def && factionDef.Enemies.Contains(def.FactionValue.Name))
                    deathList.Add(def.FactionValue.Name, Settings.KLValuesEnemies);
                else if (factionDef != def && factionDef.Allies.Contains(def.FactionValue.Name))
                    deathList.Add(def.FactionValue.Name, Settings.KLValueAllies);
                else if (factionDef != def)
                    deathList.Add(def.FactionValue.Name, Settings.KLValuesNeutral);
            }
        }
    }
}
