using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BattleTech;
using BattleTech.BinkMedia;
using BattleTech.Rendering;
using Harmony;
using Newtonsoft.Json;

namespace GalaxyatWar
{
    public class SystemStatus
    {
        public string name;
        public string owner;

        public Dictionary<string, int> NeighborSystems = new();
        public Dictionary<string, float> InfluenceTracker = new();
        private float trackerSum = 42;
        private int trackerSumHash;

        // [00:08:06.050]  CACHE HIT 
        // [00:08:06.051]  CACHE MISS
        // [00:08:06.051]  CACHE MISS
        // [00:08:06.051]  CACHE MISS
        // [00:08:06.051]  CACHE HIT 
        // [00:08:06.051]  CACHE MISS
        // [00:08:06.052]  CACHE MISS
        // [00:08:06.052]  CACHE MISS
        // [00:08:06.052]  CACHE HIT 
        // [00:08:06.052]  CACHE MISS
        internal float TrackerSum
        {
            get
            {
                if (trackerSumHash == trackerSum.GetHashCode())
                {
                    return trackerSum;
                }

                trackerSum = InfluenceTracker.Values.Sum();
                trackerSumHash = trackerSum.GetHashCode();
                return trackerSum;
            }
        }

        public float TotalResources;
        public bool PriorityDefense = false;
        public bool PriorityAttack = false;
        public List<string> CurrentlyAttackedBy = new();
        public bool Contested = false;
        public int DifficultyRating;
        public bool BonusSalvage;
        public bool BonusXP;
        public bool BonusCBills;
        private float defenseResources;
        public string CoreSystemID;
        public int DeploymentTier = 0;
        public string OriginalOwner = null;
        private StarSystem starSystemBackingField;

        public float PirateActivity { get; set; }
        public float AttackResources { get; set; }

        public float DefenseResources
        {
            get => defenseResources;
            set
            {
                if (value < -50000 || value > 50000)
                {
                    Logger.LogDebug(value);
                    Logger.LogDebug(new StackTrace());
                }

                defenseResources = value;
            }
        }

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
            AttackResources = Helpers.GetTotalAttackResources(starSystem);
            DefenseResources = Helpers.GetTotalDefensiveResources(starSystem);
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
                NeighborSystems.Clear();
                var neighbors = Globals.Sim.Starmap.GetAvailableNeighborSystem(starSystem);
                foreach (var neighborSystem in neighbors)
                {
                    if (NeighborSystems.ContainsKey(neighborSystem.OwnerValue.Name))
                        NeighborSystems[neighborSystem.OwnerValue.Name] += 1;
                    else
                        NeighborSystems.Add(neighborSystem.OwnerValue.Name, 1);
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
            InfluenceTracker.Clear();
            if (!Globals.Settings.HyadesRimCompatible)
            {
                if (owner == "NoFaction")
                    InfluenceTracker.Add("NoFaction", 100);
                if (owner == "Locals")
                    InfluenceTracker.Add("Locals", 100);

                if (owner != "NoFaction" && owner != "Locals")
                {
                    InfluenceTracker.Add(owner, Globals.Settings.DominantInfluence);
                    var remainingInfluence = Globals.Settings.MinorInfluencePool;

                    if (!(NeighborSystems.Keys.Count == 1 && NeighborSystems.Keys.Contains(owner)) && NeighborSystems.Keys.Count != 0)
                    {
                        while (remainingInfluence > 0)
                        {
                            foreach (var faction in NeighborSystems.Keys)
                            {
                                if (faction != owner)
                                {
                                    var influenceDelta = NeighborSystems[faction];
                                    remainingInfluence -= influenceDelta;
                                    if (Globals.Settings.DefensiveFactions.Contains(faction))
                                        continue;
                                    if (InfluenceTracker.ContainsKey(faction))
                                        InfluenceTracker[faction] += influenceDelta;
                                    else
                                        InfluenceTracker.Add(faction, influenceDelta);
                                }
                            }
                        }
                    }
                }

                foreach (var faction in Globals.IncludedFactions)
                {
                    if (!InfluenceTracker.Keys.Contains(faction))
                        InfluenceTracker.Add(faction, 0);
                }

                // need percentages from InfluenceTracker data 
                var totalInfluence = InfluenceTracker.Values.Sum();
                var tempDict = new Dictionary<string, float>();
                foreach (var kvp in InfluenceTracker)
                {
                    tempDict[kvp.Key] = kvp.Value / totalInfluence * 100;
                }

                InfluenceTracker = tempDict;
            }
            else
            {
                if (owner == "NoFaction" && !starSystem.Tags.Contains("planet_region_hyadesrim"))
                    InfluenceTracker.Add("NoFaction", 100);
                if (owner == "Locals" && !starSystem.Tags.Contains("planet_region_hyadesrim"))
                    InfluenceTracker.Add("Locals", 100);
                if ((owner == "NoFaction" || owner == "Locals") && starSystem.Tags.Contains("planet_region_hyadesrim"))
                {
                    foreach (var pirateFaction in starSystem.Def.ContractEmployerIDList)
                    {
                        if (Globals.Settings.HyadesNeverControl.Contains(pirateFaction))
                            continue;

                        if (!InfluenceTracker.Keys.Contains(pirateFaction))
                            InfluenceTracker.Add(pirateFaction, Globals.Settings.MinorInfluencePool);
                    }

                    foreach (var pirateFaction in starSystem.Def.ContractTargetIDList)
                    {
                        if (Globals.Settings.HyadesNeverControl.Contains(pirateFaction))
                            continue;

                        if (!InfluenceTracker.Keys.Contains(pirateFaction))
                            InfluenceTracker.Add(pirateFaction, Globals.Settings.MinorInfluencePool);
                    }
                }


                if (owner != "NoFaction" && owner != "Locals")
                {
                    InfluenceTracker.Add(owner, Globals.Settings.DominantInfluence);
                    var remainingInfluence = Globals.Settings.MinorInfluencePool;

                    if (!(NeighborSystems.Keys.Count == 1 && NeighborSystems.Keys.Contains(owner)) && NeighborSystems.Keys.Count != 0)
                    {
                        while (remainingInfluence > 0)
                        {
                            foreach (var faction in NeighborSystems.Keys)
                            {
                                if (faction != owner)
                                {
                                    var influenceDelta = NeighborSystems[faction];
                                    remainingInfluence -= influenceDelta;
                                    if (Globals.Settings.DefensiveFactions.Contains(faction))
                                        continue;
                                    if (InfluenceTracker.ContainsKey(faction))
                                        InfluenceTracker[faction] += influenceDelta;
                                    else
                                        InfluenceTracker.Add(faction, influenceDelta);
                                }
                            }
                        }
                    }
                }

                foreach (var faction in Globals.IncludedFactions)
                {
                    if (!InfluenceTracker.Keys.Contains(faction))
                        InfluenceTracker.Add(faction, 0);
                }

                // need percentages from InfluenceTracker data 
                var totalInfluence = InfluenceTracker.Values.Sum();
                var tempDict = new Dictionary<string, float>();
                foreach (var kvp in InfluenceTracker)
                {
                    tempDict[kvp.Key] = kvp.Value / totalInfluence * 100;
                }

                InfluenceTracker = tempDict;
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

            foreach (var systemNeighbor in NeighborSystems.Keys)
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
}
