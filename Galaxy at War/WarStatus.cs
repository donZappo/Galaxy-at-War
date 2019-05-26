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
    public WarStatus(bool distribute)
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        foreach (var planet in sim.StarSystems)
        {
            Systems.Add(new SystemStatus(planet.Name, distribute));
            Core.ChangeSystemOwnership(sim, planet, planet.Owner, true);
        }
    }
}

public class SystemStatus
{
    public readonly string name;
    internal static ModSettings Settings;

    // Dictionary to hold each faction's numerical influence
    public Dictionary<string, float> InfluenceTracker = new Dictionary<string, float>();

    // why the hell is the serializer ignoring these two members?
    public Dictionary<Faction, int> neighbourSystems;
    public readonly Faction owner;
    public string ownerName;

    public SystemStatus(string systemName, bool InitialDistribution)
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        name = systemName;
        owner = sim.StarSystems.First(s => s.Name == name).Owner;
        ownerName = owner.ToString();
        CalculateNeighbours(sim, InitialDistribution);
        if(InitialDistribution)
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
    public void CalculateNeighbours(SimGameState sim, bool InitialDistribution)
    {
        var OriginalSystem = sim.StarSystems.First(s => s.Name == name);
        neighbourSystems = new Dictionary<Faction, int>();
        foreach (var neighborsystem in sim.Starmap.GetAvailableNeighborSystem(OriginalSystem))
        {
            if (neighbourSystems.ContainsKey(neighborsystem.Owner))
            {
                neighbourSystems[neighborsystem.Owner] += 1;
            }
            else
                neighbourSystems.Add(neighborsystem.Owner, 1);

            if (!InitialDistribution && !Settings.AttackTargets.ContainsKey(neighborsystem.Owner) &&
                    (neighborsystem.Owner != OriginalSystem.Owner))
            {
                List<StarSystem> TempList = new List<StarSystem>();
                TempList.Add(OriginalSystem);
                Settings.AttackTargets.Add(neighborsystem.Owner, TempList);
                }
            else if (!InitialDistribution && !Settings.AttackTargets[neighborsystem.Owner].Contains(OriginalSystem) &&
                (neighborsystem.Owner != OriginalSystem.Owner))
            {
                Settings.AttackTargets[neighborsystem.Owner].Add(OriginalSystem);
            }

            if (!InitialDistribution && !Settings.DefenseTargets.ContainsKey(OriginalSystem.Owner) &&
                    (neighborsystem.Owner != OriginalSystem.Owner))
            {
                List<StarSystem> TempList = new List<StarSystem>();
                TempList.Add(OriginalSystem);
                Settings.DefenseTargets.Add(OriginalSystem.Owner, TempList);
            }
            else if (!InitialDistribution && !Settings.DefenseTargets[OriginalSystem.Owner].Contains(OriginalSystem) &&
                (neighborsystem.Owner != OriginalSystem.Owner))
            {
                Settings.DefenseTargets[OriginalSystem.Owner].Add(OriginalSystem);
            }
        }
    }
}

public class WarProgress
{
    internal static ModSettings Settings;
    public static WarStatus WarStatus;
    public void PotentialTargets(Faction faction)
    {
        Settings.AttackTargets.Clear();
        WarStatus = new WarStatus(false);
    }
}