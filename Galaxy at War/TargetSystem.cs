using System.Collections.Generic;
using BattleTech;

public class TargetSystem
{
    public StarSystem system;
    public Dictionary<Faction, int> factionNeighbours;

    public TargetSystem(StarSystem system, Dictionary<Faction, int> factionNeighbours)
    {
        this.system = system;
        this.factionNeighbours = factionNeighbours;
    }

    //Find how many friendly and opposing neighbors are present for the star system.
    public void CalculateNeighbours(SimGameState Sim)
    {
        this.factionNeighbours = new Dictionary<Faction, int>();
        foreach (StarSystem system in Sim.Starmap.GetAvailableNeighborSystem(this.system))
        {
            if (this.factionNeighbours.ContainsKey(system.Owner))
            {
                this.factionNeighbours[system.Owner] += 1;
            }
            else
            {
                factionNeighbours.Add(system.Owner, 1);
            }
        }
    }
}