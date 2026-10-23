using System;

namespace Sokoban.Presentation
{
    public readonly struct ViewKey : IEquatable<ViewKey>
    {
        public ViewKind Kind { get; }
        public SkinId Skin { get; }

        public ViewKey(ViewKind kind, SkinId skin)
        {
            Kind = kind;
            Skin = skin;
        }

        public bool Equals(ViewKey other) => Kind == other.Kind && Skin == other.Skin;
        public override bool Equals(object obj) => obj is ViewKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Kind, Skin);
        public static bool operator ==(ViewKey left, ViewKey right) => left.Equals(right);
        public static bool operator !=(ViewKey left, ViewKey right) => !left.Equals(right);
        public override string ToString() => Skin == SkinId.Default ? Kind.ToString() : $"{Kind}/{Skin}";
    }
}
