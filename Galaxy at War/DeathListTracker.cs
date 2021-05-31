using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace GalaxyatWar
{
    public class DeathListTracker
    {
        public string faction;
        public Dictionary<string, float> deathList = new();
        public List<string> Enemies => deathList.Where(x => x.Value >= 75).Select(x => x.Key).ToList();
        public List<string> Allies => deathList.Where(x => x.Value <= 25).Select(x => x.Key).ToList();
        private WarFaction warFactionBackingField;

        internal WarFaction WarFaction
        {
            get
            {
                return warFactionBackingField ?? (warFactionBackingField = Globals.WarStatusTracker.warFactionTracker.Find(x => x.FactionName == faction));
            }
            set => warFactionBackingField = value;
        }

        [JsonConstructor]
        public DeathListTracker()
        {
            // deser ctor
        }

        public DeathListTracker(string faction)
        {
            Logger.LogDebug("DeathListTracker ctor: " + faction);

            this.faction = faction;
            var factionDef = Globals.Sim.GetFactionDef(faction);

            // TODO comment this
            foreach (var includedFaction in Globals.IncludedFactions)
            {
                var def = Globals.Sim.GetFactionDef(includedFaction);
                if (!Globals.IncludedFactions.Contains(def.FactionValue.Name))
                    continue;
                if (factionDef != def && factionDef.Enemies.Contains(def.FactionValue.Name))
                    deathList.Add(def.FactionValue.Name, Globals.Settings.KLValuesEnemies);
                else if (factionDef != def && factionDef.Allies.Contains(def.FactionValue.Name))
                    deathList.Add(def.FactionValue.Name, Globals.Settings.KLValueAllies);
                else if (factionDef != def)
                    deathList.Add(def.FactionValue.Name, Globals.Settings.KLValuesNeutral);
            }
        }
    }
}
