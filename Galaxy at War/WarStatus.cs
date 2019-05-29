using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using fastJSON;
using HBS.Util;
using Newtonsoft.Json;
using static Logger;
using static Core;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

public class WarStatus
{
    public List<SystemStatus> systems = new List<SystemStatus>();
    public RelationTracker relationTracker = new RelationTracker(UnityGameInstance.BattleTechGame.Simulation);
    public List<WarFaction> factionTracker = new List<WarFaction>();
    internal Dictionary<Faction, List<StarSystem>> attackTargets = new Dictionary<Faction, List<StarSystem>>();
    internal Dictionary<Faction, List<StarSystem>> defenseTargets = new Dictionary<Faction, List<StarSystem>>();
    public Dictionary<Faction, Dictionary<Faction, float>> attackResources = new Dictionary<Faction, Dictionary<Faction, float>>();

    [JsonConstructor]
    public WarStatus()
    {
        // need an empty ctor for deserialization
    }

    // initialize a collection of all planets
    public WarStatus(bool nothing = false)
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        if (systems.Count == 0)
        {
            Log(">>> Initialize systems");
            foreach (var starSystem in sim.StarSystems)
            {
                systems.Add(new SystemStatus(starSystem.Name));
                //ChangeSystemOwnership(sim, planet, planet.Owner, true);
            }
        }
    }
    public class SystemStatus
    {
        public string name;
        public Dictionary<Faction, float> influenceTracker = new Dictionary<Faction, float>();
        public Dictionary<Faction, int> neighborSystems;
        public Faction owner;

        public SystemStatus()
        {
            // don't want our ctor running at deserialization
        }

        public SystemStatus(string systemName)
        {
            Log($"new SystemStatus: {systemName}");
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            name = systemName;
            //starSystem = sim.StarSystems.Find(x => x.Name == name);
            owner = sim.StarSystems.First(s => s.Name == name).Owner;

            CalculateNeighbours(sim);
            DistributeInfluence();
            CalculateAttackTargets(sim);
            CalculateDefenseTargets(sim);
        }

        private void DistributeInfluence()
        {
            Log(">>> DistributeInfluence: " + name);
            // determine starting influence based on neighboring systems
            influenceTracker.Add(owner, Core.Settings.DominantInfluence);
            int remainingInfluence = Core.Settings.MinorInfluencePool;
            //Log("\nremainingInfluence: " + remainingInfluence);
            //Log("=====================================================");
            while (remainingInfluence > 0)
            {
                foreach (var faction in neighborSystems.Keys)
                {
                    var influenceDelta = neighborSystems[faction];
                    remainingInfluence -= influenceDelta;
                    //Log($"{faction.ToString(),-20} gains {influenceDelta,2}, leaving {remainingInfluence}");
                    if (influenceTracker.ContainsKey(faction))
                        influenceTracker[faction] += influenceDelta;
                    else
                        influenceTracker.Add(faction, influenceDelta);
                }
            }

            var totalInfluence = influenceTracker.Values.Sum();
            Log($"\ntotalInfluence for {name}: {totalInfluence}");
            Log("=====================================================");
            // need percentages from InfluenceTracker data 
            var tempDict = new Dictionary<Faction, float>();
            foreach (var kvp in influenceTracker)
            {
                tempDict.Add(kvp.Key, kvp.Value / totalInfluence * 100);
                Log($"{kvp.Key}: {tempDict[kvp.Key]}");
            }

            influenceTracker = tempDict;
        }

        // Find how many friendly and opposing neighbors are present for the star system.
        // thanks to WarTech by Morphyum
        public void CalculateNeighbours(SimGameState sim)
        {
            neighborSystems = new Dictionary<Faction, int>();
            var starSystem = sim.StarSystems.Find(x => x.Name == name);
            var neighbors = sim.Starmap.GetAvailableNeighborSystem(starSystem);
            // build a list of all neighbors
            foreach (var neighborSystem in neighbors)
            {
                if (neighborSystems.ContainsKey(neighborSystem.Owner))
                    neighborSystems[neighborSystem.Owner] += 1;
                else
                    neighborSystems.Add(neighborSystem.Owner, 1);
            }
        }

        public void CalculateAttackTargets(SimGameState sim)
        {
            var starSystem = sim.StarSystems.Find(x => x.Name == name);
            // the rest happens only after initial distribution
            // build list of attack targets
            foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
            {
                if (!Core.WarStatus.attackTargets.ContainsKey(neighborSystem.Owner) &&
                    neighborSystem.Owner != starSystem.Owner)
                {
                    var tempList = new List<StarSystem> {starSystem};
                    Core.WarStatus.attackTargets.Add(neighborSystem.Owner, tempList);
                }
                else if (Core.WarStatus.attackTargets.ContainsKey(neighborSystem.Owner) &&
                         !Core.WarStatus.attackTargets[neighborSystem.Owner].Contains(starSystem) &&
                         (neighborSystem.Owner != starSystem.Owner))
                {
                    Core.WarStatus.attackTargets[neighborSystem.Owner].Add(starSystem);
                }
            }
        }

        public void CalculateDefenseTargets(SimGameState sim)
        {
            var starSystem = sim.StarSystems.Find(x => x.Name == name);
            // build list of defense targets
            foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
            {
                if (!Core.WarStatus.defenseTargets.ContainsKey(starSystem.Owner) &&
                    neighborSystem.Owner != starSystem.Owner)
                {
                    var tempList = new List<StarSystem> {starSystem};
                    Core.WarStatus.defenseTargets.Add(starSystem.Owner, tempList);
                }
                else if (Core.WarStatus.defenseTargets.ContainsKey(neighborSystem.Owner) &&
                         !Core.WarStatus.defenseTargets[starSystem.Owner].Contains(starSystem) &&
                         neighborSystem.Owner != starSystem.Owner)
                {
                    Core.WarStatus.defenseTargets[starSystem.Owner].Add(starSystem);
                }
            }
        }
    }
}