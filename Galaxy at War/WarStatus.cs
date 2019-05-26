using System.Collections.Generic;
using System.Linq;
using BattleTech;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

public class WarStatus
{
    internal HashSet<SystemStatus> Systems = new HashSet<SystemStatus>();

    // initialize a collection of all planets
    public WarStatus()
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        foreach (var planet in sim.StarSystems)
        {
            Systems.Add(new SystemStatus(planet.Name));
        }
    }
}

public class SystemStatus
{
    // Dictionary to hold each faction's numerical influence
    public readonly Dictionary<string, int> InfluenceMap =
        new Dictionary<string, int>
        {
            {"Steiner", 10},
            {"Kurita", 10},
            {"Davion", 10},
            {"Liao", 10},
            {"Marik", 10},
            {"TaurianConcordat", 10},
            {"MagistracyOfCanopus", 10},
            {"AuriganPirates", 10}
        };

    public readonly string name;
    
    // why the hell is the serializer ignoring these two members?
    internal Dictionary<Faction, int> neighbourSystems;
    internal readonly Faction owner;

    public SystemStatus(string name)
    {
        var sim = UnityGameInstance.BattleTechGame.Simulation;
        this.name = name;
        owner = sim.StarSystems.First(s => s.Name == name).Owner;
        CalculateNeighbours(sim);
    }

    //Find how many friendly and opposing neighbors are present for the star system.
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