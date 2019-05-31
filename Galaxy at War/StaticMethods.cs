using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Newtonsoft.Json;
using static Logger;

public static class StaticMethods
{
    public static void CalculateNeighbours(SimGameState sim, string name)
    {
        var starSystem = sim.StarSystems.Find(x => x.Name == name);
        var neighbors = sim.Starmap.GetAvailableNeighborSystem(starSystem);
        // build a list of all neighbors
        foreach (var neighborSystem in neighbors)
        {
            if (Globals.neighborSystems.ContainsKey(neighborSystem.Owner))
                Globals.neighborSystems[neighborSystem.Owner] += 1;
            else
                Globals.neighborSystems.Add(neighborSystem.Owner, 1);
        }
    }

    public static void CalculateAttackTargets(SimGameState sim, string name)
    {
        LogDebug("Calculate Potential Attack Targets");

        var starSystem = sim.StarSystems.Find(x => x.Name == name);
        LogDebug(starSystem.Name + ": " + starSystem.Owner.ToString());

        // the rest happens only after initial distribution
        // build list of attack targets
        LogDebug("Under attack by:");
        foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
        {
            if (!Globals.attackTargets.ContainsKey(neighborSystem.Owner) &&
                (neighborSystem.Owner != starSystem.Owner))
            {
                var tempList = new List<StarSystem> {starSystem};
                Globals.attackTargets.Add(neighborSystem.Owner, tempList);
                LogDebug("\t" + neighborSystem.Name + ": " + neighborSystem.Owner.ToString());
            }
            else if (Globals.attackTargets.ContainsKey(neighborSystem.Owner) &&
                     !Globals.attackTargets[neighborSystem.Owner].Contains(starSystem) &&
                     (neighborSystem.Owner != starSystem.Owner))
            {
                Globals.attackTargets[neighborSystem.Owner].Add(starSystem);
                LogDebug("\t" + neighborSystem.Name + ": " + neighborSystem.Owner.ToString());
            }
        }
    }

    public static void CalculateDefenseTargets(SimGameState sim, string name)
    {
        LogDebug("Calculate Potential Defendable Systems");
        var starSystem = sim.StarSystems.Find(x => x.Name == name);
        LogDebug(starSystem.Name);
        LogDebug("Needs defense:");
        // build list of defense targets
        foreach (var neighborSystem in sim.Starmap.GetAvailableNeighborSystem(starSystem))
        {
            if (!Globals.defenseTargets.ContainsKey(starSystem.Owner) &&
                (neighborSystem.Owner != starSystem.Owner))
            {
                var tempList = new List<StarSystem> {starSystem};
                Globals.defenseTargets.Add(starSystem.Owner, tempList);
                LogDebug("\t" + starSystem.Name + ": " + starSystem.Owner.ToString());
            }
            else if (Globals.defenseTargets.ContainsKey(starSystem.Owner) &&
                     !Globals.defenseTargets[starSystem.Owner].Contains(starSystem) &&
                     (neighborSystem.Owner != starSystem.Owner))
            {
                Globals.defenseTargets[starSystem.Owner].Add(starSystem);
                LogDebug("\t" + starSystem.Name + ": " + starSystem.Owner.ToString());
            }
        }
    }

    public static void DistributeInfluence(Dictionary<Faction, float> influenceTracker, Faction owner, string name)
    {
        Log(">>> DistributeInfluence: " + name);
        // determine starting influence based on neighboring systems
        influenceTracker.Add(owner, Core.Settings.DominantInfluence);
        int remainingInfluence = Core.Settings.MinorInfluencePool;

        if (Globals.neighborSystems.Keys.Count() != 1)
        {
            while (remainingInfluence > 0)
            {
                foreach (var faction in Globals.neighborSystems.Keys)
                {
                    if (faction != owner)
                    {
                        var influenceDelta = Globals.neighborSystems[faction];
                        remainingInfluence -= influenceDelta;
                        if (influenceTracker.ContainsKey(faction))
                            influenceTracker[faction] += influenceDelta;
                        else
                            influenceTracker.Add(faction, influenceDelta);
                    }
                }
            }
        }

        var totalInfluence = influenceTracker.Values.Sum();
        LogDebug($"\ntotalInfluence for {name}");
        LogDebug("=====================================================");
        // need percentages from InfluenceTracker data 
        var tempDict = new Dictionary<Faction, float>();
        foreach (var kvp in influenceTracker)
        {
            tempDict[kvp.Key] = kvp.Value / totalInfluence * 100;
            LogDebug($"{kvp.Key}: {tempDict[kvp.Key]}");
        }

        LogDebug("=====================================================");
        influenceTracker = tempDict;
    }
}