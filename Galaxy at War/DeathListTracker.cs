using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace GalaxyatWar
{
    public class DeathListTracker
    {
        public string Faction;
        public readonly Dictionary<string, float> DeathList = new();
        public List<string> Enemies => DeathList.Where(x => x.Value >= 75).Select(x => x.Key).ToList();
        public List<string> Allies => DeathList.Where(x => x.Value <= 25).Select(x => x.Key).ToList();
        public WarFaction WarFaction;

        [JsonConstructor]
        public DeathListTracker()
        {
            // deser ctor
        }

        public DeathListTracker(string faction)
        {
            Logger.LogDebug($"DeathListTracker ctor: {faction}");

            Faction = faction;
            var factionDef = Globals.Sim.GetFactionDef(faction);

            // TODO comment this
            foreach (var includedFaction in Globals.IncludedFactions)
            {
                var def = Globals.Sim.GetFactionDef(includedFaction);
                if (!Globals.IncludedFactions.Contains(def.FactionValue.Name))
                    continue;
                if (factionDef != def && factionDef.Enemies.Contains(def.FactionValue.Name))
                    DeathList.Add(def.FactionValue.Name, Globals.Settings.KLValuesEnemies);
                else if (factionDef != def && factionDef.Allies.Contains(def.FactionValue.Name))
                    DeathList.Add(def.FactionValue.Name, Globals.Settings.KLValueAllies);
                else if (factionDef != def)
                    DeathList.Add(def.FactionValue.Name, Globals.Settings.KLValuesNeutral);
            }
        }
    }
}
