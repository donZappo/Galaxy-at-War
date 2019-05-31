using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Newtonsoft.Json;
using static Logger;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
namespace GalaxyAtWar
{
    public class WarStatus
    {
        public List<SystemStatus> systems = new List<SystemStatus>();
        public RelationTracker relationTracker = new RelationTracker(UnityGameInstance.BattleTechGame.Simulation);
        public List<WarFaction> factionTracker = new List<WarFaction>();
        public Dictionary<Faction, Dictionary<Faction, float>> attackResources = new Dictionary<Faction, Dictionary<Faction, float>>();
    }

    public class SystemStatus
    {
        public string name;
        public Dictionary<Faction, float> influenceTracker = new Dictionary<Faction, float>();
        public Faction owner = Faction.Unknown;

        [JsonConstructor]
        public SystemStatus()
        {
            // don't want our ctor running at deserialization
        }

        public SystemStatus(SimGameState sim, string systemName)
        {
            Log($"new SystemStatus: {systemName}");
            name = systemName;
            owner = sim.StarSystems.First(s => s.Name == name).Owner;

            Globals.neighborSystems.Clear();
            try
            {
                StaticMethods.CalculateNeighbours(sim, systemName);
            }
            catch (Exception ex)
            {
                Error(ex);
            }


            StaticMethods.DistributeInfluence(influenceTracker, Globals.neighborSystems, owner, name);
            StaticMethods.CalculateAttackTargets(sim, name);
            StaticMethods.CalculateDefenseTargets(sim, name);
        }

        // Find how many friendly and opposing neighbors are present for the star system.
        // thanks to WarTech by Morphyum

        public void CalculateAttackTargets(SimGameState sim)
        {
            var starSystem = sim.StarSystems.Find(x => x.Name == name);
            // the rest happens only after initial distribution
            // build list of attack targets
            foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
            {
                if (!Globals.attackTargets.ContainsKey(neighborSystem.Owner) &&
                    neighborSystem.Owner != starSystem.Owner)
                {
                    var tempList = new List<StarSystem> {starSystem};
                    Globals.attackTargets.Add(neighborSystem.Owner, tempList);
                }
                else if (Globals.attackTargets.ContainsKey(neighborSystem.Owner) &&
                         !Globals.attackTargets[neighborSystem.Owner].Contains(starSystem) &&
                         (neighborSystem.Owner != starSystem.Owner))
                {
                    Globals.attackTargets[neighborSystem.Owner].Add(starSystem);
                }
            }
        }

        public void CalculateDefenseTargets(SimGameState sim)
        {
            var starSystem = sim.StarSystems.Find(x => x.Name == name);
            // build list of defense targets
            foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
            {
                if (!Globals.defenseTargets.ContainsKey(starSystem.Owner) &&
                    neighborSystem.Owner != starSystem.Owner)
                {
                    var tempList = new List<StarSystem> {starSystem};
                    Globals.defenseTargets.Add(starSystem.Owner, tempList);
                }
                else if (Globals.defenseTargets.ContainsKey(neighborSystem.Owner) &&
                         !Globals.defenseTargets[starSystem.Owner].Contains(starSystem) &&
                         neighborSystem.Owner != starSystem.Owner)
                {
                    Globals.defenseTargets[starSystem.Owner].Add(starSystem);
                }
            }
        }
    }
}