using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using static Logger;
using static Core;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

public class WarStatus
{
    public HashSet<SystemStatus> Systems = new HashSet<SystemStatus>();
    public RelationTracker RelationTracker = new RelationTracker();

    // initialize a collection of all planets
    public WarStatus()
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        foreach (var planet in sim.StarSystems)
        {
            Systems.Add(new SystemStatus(planet.Name));
            Core.ChangeSystemOwnership(sim, planet, planet.Owner, true);
        }
    }
}

public class SystemStatus
{
    public readonly string name;

    // Dictionary to hold each faction's numerical influence
    public Dictionary<string, float> InfluenceTracker = new Dictionary<string, float>();

    // why the hell is the serializer ignoring these two members?
    public Dictionary<Faction, int> neighbourSystems;
    public readonly Faction owner;
    public string ownerName;

    public SystemStatus(string systemName)
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        name = systemName;
        owner = sim.StarSystems.First(s => s.Name == name).Owner;
        ownerName = owner.ToString();
        CalculateNeighbours(sim);
        DistributeResources();
    }

    private void DistributeResources()
    {
        InfluenceTracker.Add(owner.ToString(), Core.Settings.DominantInfluence);
        int remainingInfluence = Core.Settings.MinorInfluencePool;
        Log("\nremainingInfluence: " + remainingInfluence);
        Log("=====================================================");
        while (remainingInfluence > 0)
        {
            foreach (var faction in neighbourSystems.Keys)
            {
                var influenceDelta = neighbourSystems[faction];
                remainingInfluence -= influenceDelta;
                LogDebug($"{faction.ToString(),-20} gains {influenceDelta,2}, leaving {remainingInfluence}");
                if (InfluenceTracker.ContainsKey(faction.ToString()))
                    InfluenceTracker[faction.ToString()] += influenceDelta;
                else
                    InfluenceTracker.Add(faction.ToString(), influenceDelta);
            }
        }

        var totalInfluence = InfluenceTracker.Values.Sum();
        Log($"\ntotalInfluence for {name}: {totalInfluence}");
        Log("=====================================================");
        // need percentages from InfluenceTracker data 
        var tempDict = new Dictionary<string, float>();
        foreach (var kvp in InfluenceTracker)
        {
            //Log($"{kvp.Key}: {kvp.Value}");
            tempDict.Add(kvp.Key, kvp.Value / totalInfluence * 100);
        }

        InfluenceTracker = tempDict;
    }

    // Find how many friendly and opposing neighbors are present for the star system.
    // thanks to WarTech by Morphyum
    public void CalculateNeighbours(SimGameState sim)
    {
        var thisSystem = sim.StarSystems.First(s => s.Name == name);
        neighbourSystems = new Dictionary<Faction, int>();
        foreach (var system in sim.Starmap.GetAvailableNeighborSystem(thisSystem))
        {
            if (neighbourSystems.ContainsKey(system.Owner))
                neighbourSystems[system.Owner] += 1;
            else
                neighbourSystems.Add(system.Owner, 1);
        }
    }
}