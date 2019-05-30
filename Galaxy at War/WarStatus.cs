using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using static Logger;

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

            try
            {
                CalculateNeighbours(sim);
            }
            catch (Exception ex)
            {
                Error(ex);
            }

            StaticMethods.DistributeInfluence(influenceTracker, neighborSystems, owner, name);
            StaticMethods.CalculateAttackTargets(sim, name);
            StaticMethods.CalculateDefenseTargets(sim, name);
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
            Log("A");
            var starSystem = sim.StarSystems.Find(x => x.Name == name);
            Log("B");
            // the rest happens only after initial distribution
            // build list of attack targets
            foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
            {
                var warstatus = new WarStatus();
                Log("C");
                Log(neighborSystem.Name);
                Log(neighborSystem.Owner.ToString());
                Log(starSystem.Owner.ToString());
                if (!warstatus.attackTargets.ContainsKey(neighborSystem.Owner) &&
                    neighborSystem.Owner != starSystem.Owner)
                {
                    Log("D");
                    var tempList = new List<StarSystem> { starSystem };
                    warstatus.attackTargets.Add(neighborSystem.Owner, tempList);
                }
                else if (warstatus.attackTargets.ContainsKey(neighborSystem.Owner) &&
                         !warstatus.attackTargets[neighborSystem.Owner].Contains(starSystem) &&
                         (neighborSystem.Owner != starSystem.Owner))
                {
                    Log("E");
                    warstatus.attackTargets[neighborSystem.Owner].Add(starSystem);
                }
                Log("F");
            }
        }

        public void CalculateDefenseTargets(SimGameState sim)
        {
            var starSystem = sim.StarSystems.Find(x => x.Name == name);
            var warstatus = new WarStatus();
            // build list of defense targets
            foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
            {
                if (!warstatus.defenseTargets.ContainsKey(starSystem.Owner) &&
                    neighborSystem.Owner != starSystem.Owner)
                {
                    var tempList = new List<StarSystem> {starSystem};
                    warstatus.defenseTargets.Add(starSystem.Owner, tempList);
                }
                else if (warstatus.defenseTargets.ContainsKey(neighborSystem.Owner) &&
                         !warstatus.defenseTargets[starSystem.Owner].Contains(starSystem) &&
                         neighborSystem.Owner != starSystem.Owner)
                {
                    warstatus.defenseTargets[starSystem.Owner].Add(starSystem);
                }
            }
        }
    }
}