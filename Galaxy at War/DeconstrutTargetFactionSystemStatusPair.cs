using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GalaxyatWar
{
    public static class DeconstrutTargetFactionSystemStatusPair
    {
        public static void Deconstruct(this KeyValuePair<string, SystemStatus> randElement, out string target, out SystemStatus system)
        {            
            target = randElement.Key;
            system = randElement.Value;
        }
    }
}
