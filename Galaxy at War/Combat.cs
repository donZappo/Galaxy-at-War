using System;
namespace GalaxyatWar
{
    /***************************************************************************
     * 
     * The intended purpose of this class is to resolve combat between systems.
     * It will use the trackers that are already implimented in WarStatus.
     * It will also resolve the change in resources from combat.
     * 
     * It will not generate resources.
     * It will not change influence in anyway.
     *     
     * The way it works should be Resource sets systems base resources and current usable resources.
     * Combat manages trackers and Resource loss between systems
     * Influence, does Influence processing and nothing else.
     *     
     * Order of use, would reliably be; Resource > Combat > Influence 
     * 
     ***************************************************************************/    
    public class Combat
    {
        public Combat()
        {
        }
    }
}
