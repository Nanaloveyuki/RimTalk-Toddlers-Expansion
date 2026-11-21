using System;

namespace RimTalk_ToddlersExpansion.Core
{
    // Save data stores the numeric category IDs.
    public enum ToddlerPlayCategory
    {
        None = 0,
        SoloPlay = 1,
        SocialPlay = 2,
        ToyPlay = 3,
        Observation = 4,
        Media = 5,
        Passive = 6,
        Exploration = 7,
        Creative = 8,

        // External mods can register categories starting at this value.
        Custom = 100
    }
}
