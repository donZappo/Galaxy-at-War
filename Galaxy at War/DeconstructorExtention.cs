using System.Collections.Generic;

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
