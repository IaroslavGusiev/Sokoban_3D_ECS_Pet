using System;

namespace Sokoban.Presentation
{
    public static class SkinCycle
    {
        private static readonly SkinId[] Values = (SkinId[])Enum.GetValues(typeof(SkinId));

        public static SkinId Next(SkinId skin) => Values[((int)skin + 1) % Values.Length];
    }
}
