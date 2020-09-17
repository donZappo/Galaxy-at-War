using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Newtonsoft.Json;
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
            if (Globals.Settings.ISMCompatibility)
                Globals.Settings.IncludedFactions = new List<string>(Globals.Settings.IncludedFactions_ISM);

            CurSystem = Globals.Sim.CurSystem.Name;
            TempPRGain = 0;
            HotBoxTravelling = false;
            HotBox = new List<string>();
            if (Globals.Settings.HyadesRimCompatible)
            {
                InactiveTHRFactions = Globals.Settings.HyadesAppearingPirates;
                FlashpointSystems = Globals.Settings.HyadesFlashpointSystems;
                NeverControl = Globals.Settings.HyadesNeverControl;
            }

            //initialize all WarFactions, DeathListTrackers, and SystemStatuses
            foreach (var faction in Globals.Settings.IncludedFactions)
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

            foreach (var system in Globals.GaWSystems)
            {
                if (system.OwnerValue.Name == "NoFaction" || system.OwnerValue.Name == "AuriganPirates")
                    AbandonedSystems.Add(system.Name);
                var warFaction = warFactionTracker.Find(x => x.faction == system.OwnerValue.Name);
                if (Globals.Settings.DefensiveFactions.Contains(warFaction.faction) && Globals.Settings.DefendersUseARforDR)
                    warFaction.DefensiveResources += GetTotalAttackResources(system);
                else
                    warFaction.AttackResources += GetTotalAttackResources(system);
                warFaction.DefensiveResources += GetTotalDefensiveResources(system);
            }

            Logger.LogDebug("WarFaction AR/DR set.");
            var maxAR = warFactionTracker.Select(x => x.AttackResources).Max();
            var maxDR = warFactionTracker.Select(x => x.DefensiveResources).Max();

            foreach (var faction in Globals.Settings.IncludedFactions)
            {
                var warFaction = warFactionTracker.Find(x => x.faction == faction);
                if (Globals.Settings.DefensiveFactions.Contains(faction) && Globals.Settings.DefendersUseARforDR)
                {
                    if (!Globals.Settings.ISMCompatibility)
                        warFaction.DefensiveResources = maxAR + maxDR + Globals.Settings.BonusAttackResources[faction] +
                                                        Globals.Settings.BonusDefensiveResources[faction];
                    else
                        warFaction.DefensiveResources = maxAR + maxDR + Globals.Settings.BonusAttackResources_ISM[faction] +
                                                        Globals.Settings.BonusDefensiveResources_ISM[faction];

                    warFaction.AttackResources = 0;
                }
                else
                {
                    if (!Globals.Settings.ISMCompatibility)
                    {
                        warFaction.AttackResources = maxAR + Globals.Settings.BonusAttackResources[faction];
                        warFaction.DefensiveResources = maxDR + Globals.Settings.BonusDefensiveResources[faction];
                    }
                    else
                    {
                        warFaction.AttackResources = maxAR + Globals.Settings.BonusAttackResources_ISM[faction];
                        warFaction.DefensiveResources = maxDR + Globals.Settings.BonusDefensiveResources_ISM[faction];
                    }
                }
            }

            Logger.LogDebug("WarFaction bonus AR/DR set.");
            if (!Globals.Settings.ISMCompatibility)
                PirateResources = maxAR * Globals.Settings.FractionPirateResources + Globals.Settings.BonusPirateResources;
            else
                PirateResources = maxAR * Globals.Settings.FractionPirateResources_ISM + Globals.Settings.BonusPirateResources_ISM;

            MinimumPirateResources = PirateResources;
            StartingPirateResources = PirateResources;
            Logger.LogDebug("SystemStatus mass creation...");
            systems = new List<SystemStatus>(Globals.GaWSystems.Count);
            for (var index = 0; index < Globals.Sim.StarSystems.Count; index++)
            {
                var system = Globals.Sim.StarSystems[index];
                if (Globals.Settings.ImmuneToWar.Contains(system.OwnerValue.Name))
                {
                    continue;
                }
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

            Logger.LogDebug("Full pirate systems created.");
            systems = systems.OrderBy(x => x.name).ToList();
            systemsByResources = systems.OrderBy(x => x.TotalResources).ToList();
            PrioritySystems = new List<string>(systems.Count);
            Logger.LogDebug("SystemStatus ordered lists created.");
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
        internal Dictionary<string, float> influenceTrackerBf = new Dictionary<string, float>();

        public Dictionary<string, float> influenceTracker
        {
            get => influenceTrackerBf;
            set
            {
                influenceTrackerBf = value;
                influenceTrackerDescendingValue = influenceTrackerBf.Values.OrderByDescending(x => x).ToList();
            }
        }

        internal List<float> influenceTrackerDescendingValue;
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
                return starSystemBackingField ?? (starSystemBackingField = Globals.Sim.StarSystems.Find(s => s.Name == name));
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
            if (system.Tags.Contains("planet_other_pirate") && !Globals.Settings.HyadesFlashpointSystems.Contains(name))
                if (!Globals.Settings.ISMCompatibility)
                    PirateActivity = Globals.Settings.StartingPirateActivity;
                else
                    PirateActivity = Globals.Settings.StartingPirateActivity_ISM;
            FindNeighbors();
            CalculateSystemInfluence();
            InitializeContracts();
        }

        public void FindNeighbors()
        {
            try
            {
                neighborSystems.Clear();
                var neighbors = Globals.Sim.Starmap.GetAvailableNeighborSystem(starSystem);
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
            if (!Globals.Settings.HyadesRimCompatible)
            {
                if (owner == "NoFaction")
                    influenceTracker.Add("NoFaction", 100);
                if (owner == "Locals")
                    influenceTracker.Add("Locals", 100);

                if (owner != "NoFaction" && owner != "Locals")
                {
                    influenceTracker.Add(owner, Globals.Settings.DominantInfluence);
                    var remainingInfluence = Globals.Settings.MinorInfluencePool;

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
                                    if (Globals.Settings.DefensiveFactions.Contains(faction))
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

                foreach (var faction in Globals.IncludedFactions)
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
                        if (Globals.Settings.HyadesNeverControl.Contains(pirateFaction))
                            continue;

                        if (!influenceTracker.Keys.Contains(pirateFaction))
                            influenceTracker.Add(pirateFaction, Globals.Settings.MinorInfluencePool);
                    }

                    foreach (var pirateFaction in starSystem.Def.ContractTargetIDList)
                    {
                        if (Globals.Settings.HyadesNeverControl.Contains(pirateFaction))
                            continue;

                        if (!influenceTracker.Keys.Contains(pirateFaction))
                            influenceTracker.Add(pirateFaction, Globals.Settings.MinorInfluencePool);
                    }
                }


                if (owner != "NoFaction" && owner != "Locals")
                {
                    influenceTracker.Add(owner, Globals.Settings.DominantInfluence);
                    var remainingInfluence = Globals.Settings.MinorInfluencePool;

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
                                    if (Globals.Settings.DefensiveFactions.Contains(faction))
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

                foreach (var faction in Globals.IncludedFactions)
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
            if (Globals.Settings.HyadesRimCompatible && starSystem.Tags.Contains("planet_region_hyadesrim") && (owner == "NoFaction" || owner == "Locals" || Globals.Settings.HyadesFlashpointSystems.Contains(name)))
                return;

            var ContractEmployers = starSystem.Def.ContractEmployerIDList;
            var ContractTargets = starSystem.Def.ContractTargetIDList;

            ContractEmployers.Clear();
            ContractTargets.Clear();
            ContractEmployers.Add(owner);

            foreach (var EF in Globals.Settings.DefensiveFactions)
            {
                if (Globals.Settings.ImmuneToWar.Contains(EF))
                    continue;
                ContractTargets.Add(EF);
            }

            if (!ContractTargets.Contains(owner))
                ContractTargets.Add(owner);

            foreach (var systemNeighbor in neighborSystems.Keys)
            {
                if (Globals.Settings.ImmuneToWar.Contains(systemNeighbor))
                    continue;
                if (!ContractEmployers.Contains(systemNeighbor) && !Globals.Settings.DefensiveFactions.Contains(systemNeighbor))
                    ContractEmployers.Add(systemNeighbor);

                if (!ContractTargets.Contains(systemNeighbor) && !Globals.Settings.DefensiveFactions.Contains(systemNeighbor))
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
                return Globals.GaWSystems.Count(system => system.OwnerDef == Globals.Sim.factions[faction]);
            }
        }

        public Dictionary<string, float> warFactionAttackResources = new Dictionary<string, float>();
        public Dictionary<string, List<string>> attackTargets= new Dictionary<string, List<string>>();
        internal Dictionary<string, List<StarSystem>> systemTargets = new Dictionary<string, List<StarSystem>>();
        public List<string> defenseTargets = new List<string>();
        public Dictionary<string, bool> IncreaseAggression = new Dictionary<string, bool>();
        public List<string> adjacentFactions = new List<string>();
        private DeathListTracker deathListTrackerBackingField;

        internal DeathListTracker DeathListTracker
        {
            get => deathListTrackerBackingField ?? (deathListTrackerBackingField = Globals.WarStatusTracker.deathListTracker.Find(x => x.faction == faction));
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
            foreach (var startFaction in Globals.IncludedFactions)
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
                return warFactionBackingField ?? (warFactionBackingField = Globals.WarStatusTracker.warFactionTracker.Find(x => x.faction == faction));
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
            var factionDef = Globals.Sim.GetFactionDef(faction);

            // TODO comment this
            foreach (var includedFaction in Globals.IncludedFactions)
            {
                var def = Globals.Sim.GetFactionDef(includedFaction);
                if (!Globals.IncludedFactions.Contains(def.FactionValue.Name))
                    continue;
                if (factionDef != def && factionDef.Enemies.Contains(def.FactionValue.Name))
                    deathList.Add(def.FactionValue.Name, Globals.Settings.KLValuesEnemies);
                else if (factionDef != def && factionDef.Allies.Contains(def.FactionValue.Name))
                    deathList.Add(def.FactionValue.Name, Globals.Settings.KLValueAllies);
                else if (factionDef != def)
                    deathList.Add(def.FactionValue.Name, Globals.Settings.KLValuesNeutral);
            }
        }
    }
}
