#nullable enable
using System;

namespace SPTFoldablesExtended;

public readonly struct FoldedSize : IEquatable<FoldedSize>
{
    public FoldedSize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }
    public int Area => Width * Height;

    public bool Equals(FoldedSize other) => Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is FoldedSize other && Equals(other);
    public override int GetHashCode() => (Width * 397) ^ Height;
    public static bool operator ==(FoldedSize left, FoldedSize right) => left.Equals(right);
    public static bool operator !=(FoldedSize left, FoldedSize right) => !left.Equals(right);
}

public static class FoldedGeometry
{
    public static FoldedSize ForArmor(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Armor dimensions must be positive.");
        }

        int cells = (int)Math.Ceiling(width * height / 4d);
        if (width == height)
        {
            int side = (int)Math.Ceiling(Math.Sqrt(cells));
            return new FoldedSize(side, (int)Math.Ceiling(cells / (double)side));
        }

        return new FoldedSize(cells, 1);
    }

    public static FoldedSize ForHelmet(int width, int height)
    {
        if (width * height != 4)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Only four-cell helmets are supported.");
        }

        return new FoldedSize(1, 2);
    }

    public static FoldedSize ForPoster(int width, int height)
    {
        if (width * height != 4)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Only four-cell poster packs are supported.");
        }

        return new FoldedSize(1, 1);
    }
}
