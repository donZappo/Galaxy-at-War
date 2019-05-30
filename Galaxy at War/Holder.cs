using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BattleTech;

public static class Globals
{
    public static Dictionary<Faction, List<StarSystem>> attackTargets = new Dictionary<Faction, List<StarSystem>>();
    public static Dictionary<Faction, List<StarSystem>> defenseTargets = new Dictionary<Faction, List<StarSystem>>();
    public static Dictionary<Faction, int> neighborSystems = new Dictionary<Faction, int>();
}
