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
        public static List<SystemStatus> systems = new List<SystemStatus>();
        public static RelationTracker relationTracker = new RelationTracker(UnityGameInstance.BattleTechGame.Simulation);
        public static List<WarFaction> factionTracker = new List<WarFaction>();
        public static Dictionary<Faction, Dictionary<Faction, float>> attackResources = new Dictionary<Faction, Dictionary<Faction, float>>();
        internal static Dictionary<Faction, List<StarSystem>> attackTargets = new Dictionary<Faction, List<StarSystem>>();
        internal static Dictionary<Faction, List<StarSystem>> defenseTargets = new Dictionary<Faction, List<StarSystem>>();
        internal static Dictionary<Faction, int> neighborSystems = new Dictionary<Faction, int>();
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
        }

        // Find how many friendly and opposing neighbors are present for the star system.
        // thanks to WarTech by Morphyum
        public void CalculateAttackTargets(SimGameState sim)
        {
            Log("A");
            var starSystem = sim.StarSystems.Find(x => x.Name == name);
            Log("B");
            // the rest happens only after initial distribution
            // build list of attack targets
            foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
            {
                Log("C");
                Log(neighborSystem.Name);
                Log(neighborSystem.Owner.ToString());
                Log(starSystem.Owner.ToString());
                if (!WarStatus.attackTargets.ContainsKey(neighborSystem.Owner) &&
                    neighborSystem.Owner != starSystem.Owner)
                {
                    Log("D");
                    var tempList = new List<StarSystem> {starSystem};
                    WarStatus.attackTargets.Add(neighborSystem.Owner, tempList);
                }
                else if (WarStatus.attackTargets.ContainsKey(neighborSystem.Owner) &&
                         !WarStatus.attackTargets[neighborSystem.Owner].Contains(starSystem) &&
                         (neighborSystem.Owner != starSystem.Owner))
                {
                    Log("E");
                    WarStatus.attackTargets[neighborSystem.Owner].Add(starSystem);
                }

                Log("F");
            }
        }

        public void CalculateDefenseTargets(SimGameState sim)
        {
            var starSystem = sim.StarSystems.Find(x => x.Name == name);
            // build list of defense targets
            foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
            {
                if (!WarStatus.defenseTargets.ContainsKey(starSystem.Owner) &&
                    neighborSystem.Owner != starSystem.Owner)
                {
                    var tempList = new List<StarSystem> {starSystem};
                    WarStatus.defenseTargets.Add(starSystem.Owner, tempList);
                }
                else if (WarStatus.defenseTargets.ContainsKey(neighborSystem.Owner) &&
                         !WarStatus.defenseTargets[starSystem.Owner].Contains(starSystem) &&
                         neighborSystem.Owner != starSystem.Owner)
                {
                    WarStatus.defenseTargets[starSystem.Owner].Add(starSystem);
                }
            }
        }
    }
}