using System;

namespace Sokoban.Core
{
    public sealed class LevelFormatException : FormatException
    {
        public LevelFormatException(string message) : base(message) { }
    }
}
