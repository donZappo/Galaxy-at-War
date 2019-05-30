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
    }
}