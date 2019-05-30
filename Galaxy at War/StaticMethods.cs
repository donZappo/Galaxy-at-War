using System.Collections.Generic;
using System.Linq;
using BattleTech;
using static Logger;

public static class StaticMethods
{
    public static void CalculateNeighbours(SimGameState sim, Dictionary<Faction, int> neighborSystems, string name)
    {
        //neighborSystems = new Dictionary<Faction, int>();
        //LogDebug(neighborSystems.Count.ToString());
        var starSystem = sim.StarSystems.Find(x => x.Name == name);
        //LogDebug(starSystem.Name);
        var neighbors = sim.Starmap.GetAvailableNeighborSystem(starSystem);
        //LogDebug(neighbors.Count.ToString());
        // build a list of all neighbors
        foreach (var neighborSystem in neighbors)
        {
            LogDebug(neighborSystem.Name);
            if (neighborSystems.ContainsKey(neighborSystem.Owner))
                neighborSystems[neighborSystem.Owner] += 1;
            else
                neighborSystems.Add(neighborSystem.Owner, 1);
        }
    }

    public static void CalculateAttackTargets(SimGameState sim, string name)
    {
        LogDebug("CalcAttack");
        var starSystem = sim.StarSystems.Find(x => x.Name == name);
        LogDebug(starSystem.Name);
        // the rest happens only after initial distribution
        // build list of attack targets
        LogDebug("neighorSystems " + sim.Starmap.GetAvailableNeighborSystem(starSystem).Count);
        foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
        {
            LogDebug("\t" + neighborSystem);
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

    public static void CalculateDefenseTargets(SimGameState sim, string name)
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
    
    public static void DistributeInfluence(Dictionary<Faction, float> influenceTracker, Dictionary<Faction, int> neighborSystems, Faction owner, string name)
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

}