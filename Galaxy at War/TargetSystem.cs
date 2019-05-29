using System.Collections.Generic;
using BattleTech;

// ReSharper disable MemberCanBePrivate.Global

// deprecated - merged into WarStatus.cs
//public class TargetSystem
//{
//    public readonly StarSystem system;
//    public Dictionary<Faction, int> neighbourSystems;
//
//    public TargetSystem(StarSystem system, Dictionary<Faction, int> neighbourSystems)
//    {
//        this.system = system;
//        this.neighbourSystems = neighbourSystems;
//    }
//
//    //Find how many friendly and opposing neighbors are present for the star system.
//    public void CalculateNeighbours(SimGameState Sim)
//    {
//        neighbourSystems = new Dictionary<Faction, int>();
//        foreach (StarSystem system in Sim.Starmap.GetAvailableNeighborSystem(this.system))
//        {
//            if (neighbourSystems.ContainsKey(system.Owner))
//            {
//                neighbourSystems[system.Owner] += 1;
//            }
//            else
//            {
//                neighbourSystems.Add(system.Owner, 1);
//            }
//        }
//    }
//