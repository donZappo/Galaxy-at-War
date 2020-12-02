using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GalaxyatWar
{
    static class DeconstructorExtention
    {
        static public void Deconstruct(this KeyValuePair<string,SystemStatus> valuePair ,out string target, out SystemStatus system)
        {
            target = valuePair.Key;
            system = valuePair.Value;
        }
    }
}
