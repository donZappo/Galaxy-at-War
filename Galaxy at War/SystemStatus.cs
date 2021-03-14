using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Newtonsoft.Json;
//using static GalaxyatWar.Resource;
//using UnityEngine;

namespace GalaxyatWar
{
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

        // TODO wire this up
        internal List<SystemStatus> NeighborSystems = new List<SystemStatus>();
        internal List<float> influenceTrackerDescendingValue;
        //public float TotalResources;
        public bool PriorityDefense = false;
        public bool PriorityAttack = false;
        public List<string> CurrentlyAttackedBy = new List<string>();
        public bool Contended = false;
        public int DifficultyRating;
        public bool BonusSalvage;
        public bool BonusXP;
        public bool BonusCBills;
        public Resource systemResources;
        //public float AttackResources;
        //public float DefenseResources;
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
            systemResources = new Resource(starSystem);
            //AttackResources = Helpers.GetTotalAttackResources(starSystem);
            //DefenseResources = Helpers.GetTotalDefensiveResources(starSystem);
            //TotalResources = systemResources.AttackResources + systemResources.DefenceResources;
            CoreSystemID = system.Def.CoreSystemID;
            BonusCBills = false;
            BonusSalvage = false;
            BonusXP = false;
            /*if (system.Tags.Contains("planet_other_pirate") && !Globals.Settings.HyadesFlashpointSystems.Contains(name))
                if (!Globals.Settings.ISMCompatibility)
                    PirateActivity = Globals.Settings.StartingPirateActivity;
                else
                    PirateActivity = Globals.Settings.StartingPirateActivity_ISM;*/
            //FindNeighbors();
            CalculateSystemInfluence();
            InitializeContracts();
        }

        //Created a horrible loop if FindNeighbors was run in ctor
        //removed FindNeighbors from ctor
        //Find neighbors is now called manually during init of warstatus
        //This is going to be used in extention for finding the systems easier,
        //it is probably overkill but im not currently able to properly think of
        //a easier way to do it, just do a search by name maybe ?
        //current objective get something working that proves concept.
        //TODO after testing decide if there is a better way to do this.
        internal void AddNeigborsToList(StarSystem system)
        {
            var systemStatus = new SystemStatus(system, system.OwnerValue.Name);
            NeighborSystems.Add(systemStatus);
        }

        //will use to call another method that will set a list of SystemStatus for neighbor systems
        public void FindNeighbors()
        {
            try
            {
                neighborSystems.Clear();
                var neighbors = Globals.Sim.Starmap.GetAvailableNeighborSystem(starSystem);

                foreach (var neighborSystem in neighbors)
                {
                    //test line
                    this.AddNeigborsToList(neighborSystem);

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
}
