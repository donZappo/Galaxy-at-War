using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BattleTech;
using Harmony;
using System.IO;
using System.Reflection;
using BattleTech.Framework;


namespace Galaxy_at_War
{
    public class Main
    {
        internal static Helper.ModSettings Settings;
        internal static string modDir;
        public static void Init(string directory, string settings)
        {
            modDir = directory;
            var harmony = HarmonyInstance.Create("io.github.mpstark.SalvageOperations");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Settings = Helper.ModSettings.ReadSettings(settings);
        }

        //Find how many friendly and opposing neighbors are present for the star system.
        public class TargetSystem
        {
            public StarSystem system;
            public Dictionary<Faction, int> factionNeighbours;

            public TargetSystem(StarSystem system, Dictionary<Faction, int> factionNeighbours)
            {
                this.system = system;
                this.factionNeighbours = factionNeighbours;
            }

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
                        this.factionNeighbours.Add(system.Owner, 1);
                    }
                }
            }
        }
    }
}
