using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BattleTech;
using Newtonsoft.Json;

namespace GalaxyatWar
{
    public class SystemStatus
    {
        public string Name;
        public string Owner;
        public Dictionary<string, int> NeighborSystems = new();
        public Dictionary<string, float> InfluenceTracker = new();
        public float TotalResources;
        public float TotalOriginalResources;
        public bool PriorityDefense = false;
        public bool PriorityAttack = false;
        public List<string> CurrentlyAttackedBy = new();
        public bool Contested = false;
        public int DifficultyRating;
        public bool BonusSalvage;
        public bool BonusXP;
        public bool BonusCBills;
        public string CoreSystemID;
        public int DeploymentTier = 0;
        public string OriginalOwner = null;
        public float PirateActivity;
        public float AttackResources;
        public float AttackResourcesOriginal;
        public float DefenseResources;
        public float DefenseResourcesOriginal;
        private float trackerSum = -1;
        private StarSystem starSystem;

        internal StarSystem StarSystem
        {
            get
            {
                return starSystem ??= Globals.Sim.StarSystems.Find(s => s.Name == Name);
            }
            private set => starSystem = value;
        }

        internal float TrackerSum
        {
            get
            {
                if (trackerSum > -1)
                {
                    return trackerSum;
                }

                trackerSum = InfluenceTracker.Values.Sum();
                return trackerSum;
            }
            set => trackerSum = value;
        }

        [JsonConstructor]
        public SystemStatus()
        {
            // don't want our ctor running at deserialization
        }

        public SystemStatus(StarSystem system, string faction)
        {
            //  LogDebug("SystemStatus ctor");
            Name = system.Name;
            Owner = faction;
            StarSystem = system;
            AttackResources = Helpers.GetTotalAttackResources(StarSystem);
            DefenseResources = Helpers.GetTotalDefensiveResources(StarSystem);
            TotalResources = AttackResources + DefenseResources;
            CoreSystemID = system.Def.CoreSystemID;
            BonusCBills = false;
            BonusSalvage = false;
            BonusXP = false;
            if (system.Tags.Contains("planet_other_pirate") && !Globals.Settings.HyadesFlashpointSystems.Contains(Name))
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
                var neighbors = Globals.Sim.Starmap.GetAvailableNeighborSystem(StarSystem);
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
                if (Owner == "NoFaction")
                    InfluenceTracker.Add("NoFaction", 100);
                if (Owner == "Locals")
                    InfluenceTracker.Add("Locals", 100);

                if (Owner != "NoFaction" && Owner != "Locals")
                {
                    InfluenceTracker.Add(Owner, Globals.Settings.DominantInfluence);
                    var remainingInfluence = Globals.Settings.MinorInfluencePool;

                    if (!(NeighborSystems.Keys.Count == 1 && NeighborSystems.Keys.Contains(Owner)) && NeighborSystems.Keys.Count != 0)
                    {
                        while (remainingInfluence > 0)
                        {
                            foreach (var faction in NeighborSystems.Keys)
                            {
                                if (faction != Owner)
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
                if (Owner == "NoFaction")
                    InfluenceTracker.Add("NoFaction", 100);
                if (Owner == "Locals")
                    InfluenceTracker.Add("Locals", 100);
                //if ((Owner == "NoFaction" || Owner == "Locals") && StarSystem.Tags.Contains("planet_region_hyadesrim"))
                //{
                //    foreach (var pirateFaction in StarSystem.Def.ContractEmployerIDList)
                //    {
                //        if (Globals.Settings.HyadesNeverControl.Contains(pirateFaction))
                //            continue;

                //        if (!InfluenceTracker.Keys.Contains(pirateFaction))
                //            InfluenceTracker.Add(pirateFaction, Globals.Settings.MinorInfluencePool);
                //    }

                //    foreach (var pirateFaction in StarSystem.Def.ContractTargetIDList)
                //    {
                //        if (Globals.Settings.HyadesNeverControl.Contains(pirateFaction))
                //            continue;

                //        if (!InfluenceTracker.Keys.Contains(pirateFaction))
                //            InfluenceTracker.Add(pirateFaction, Globals.Settings.MinorInfluencePool);
                //    }
                //}


                if (Owner != "NoFaction" && Owner != "Locals")
                {
                    InfluenceTracker.Add(Owner, Globals.Settings.DominantInfluence);
                    var remainingInfluence = Globals.Settings.MinorInfluencePool;

                    if (!(NeighborSystems.Keys.Count == 1 && NeighborSystems.Keys.Contains(Owner)) && NeighborSystems.Keys.Count != 0)
                    {
                        while (remainingInfluence > 0)
                        {
                            foreach (var faction in NeighborSystems.Keys)
                            {
                                if (faction != Owner)
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
            //if (Globals.Settings.HyadesRimCompatible && StarSystem.Tags.Contains("planet_region_hyadesrim") && (Owner == "NoFaction" || Owner == "Locals" || Globals.Settings.HyadesFlashpointSystems.Contains(Name)))
            //    return;

            var ContractEmployers = StarSystem.Def.ContractEmployerIDList;
            var ContractTargets = StarSystem.Def.ContractTargetIDList;

            ContractEmployers.Clear();
            ContractTargets.Clear();
            ContractEmployers.Add(Owner);

            foreach (var EF in Globals.Settings.DefensiveFactions)
            {
                if (Globals.Settings.ImmuneToWar.Contains(EF))
                    continue;
                ContractTargets.Add(EF);
            }

            if (!ContractTargets.Contains(Owner))
                ContractTargets.Add(Owner);

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
