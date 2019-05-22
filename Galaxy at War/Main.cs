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

        public static void RefreshResources(SimGameState Sim)
        {
            try
            {
                foreach (KeyValuePair<Faction, FactionDef> pair in Sim.FactionsDict)
                {
                    if (!IsExcluded(pair.Key))
                    {
                        if (Fields.factionResources.Find(x => x.faction == pair.Key) == null)
                        {
                            Fields.factionResources.Add(new FactionResources(pair.Key, 0, 0));
                        }
                        else
                        {
                            FactionResources resources = Fields.factionResources.Find(x => x.faction == pair.Key);
                            resources.offence = 0;
                            resources.defence = 0;
                        }
                    }
                }
                if (Sim.Starmap != null)
                {
                    foreach (StarSystem system in Sim.StarSystems)
                    {
                        FactionResources resources = Fields.factionResources.Find(x => x.faction == system.Owner);
                        if (resources != null)
                        {
                            if (!IsExcluded(resources.faction))
                            {
                                resources.offence += Mathf.RoundToInt(GetOffenceValue(system) * (1 - (Fields.WarFatique[system.Owner] / 100)));
                                resources.defence += Mathf.RoundToInt(GetDefenceValue(system) * (1 - (Fields.WarFatique[system.Owner] / 100)));
                            }
                        }
                    }
                }
                if (Fields.settings.debug)
                {
                    foreach (KeyValuePair<Faction, FactionDef> pair in Sim.FactionsDict)
                    {
                        if (!IsExcluded(pair.Key))
                        {
                            FactionResources resources = Fields.factionResources.Find(x => x.faction == pair.Key);
                            Logger.LogMonthlyReport(Helper.GetFactionName(pair.Key, Sim.DataManager) + " Exhaustion:" + Fields.WarFatique[pair.Key] + " Attack:" + resources.offence + " Defence:" + resources.defence);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
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
