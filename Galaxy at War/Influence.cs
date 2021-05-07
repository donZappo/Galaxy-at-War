using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GalaxyatWar
{
    // Currently to be wired directly into SystemStatus.
    // The plan is to have each System manage its own resources and influence.
    // or as close to that as humanly possible.
    // In an attempt to clean up the code and make tracking Influence and
    // resources easier.
    // Influence will be left untouched and unused untill resource management, is
    // more stable. Preferably work on influence will not happen untill core resource
    // code does it's intended job without external influence.
    //
    // with how things are currently, this can't realy be done untill resource code and influence code
    // are untangled from each other.
    //
    // After further inspection, will seperate resource and influence from each other
    // entirely. 
    // All resource processing will be done first.
    // Only after that will any influence be processed.
    class Influence
    {
    }
}
