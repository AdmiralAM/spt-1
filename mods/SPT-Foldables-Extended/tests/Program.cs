using SPTFoldablesExtended;

Assert(FoldedGeometry.ForArmor(3, 4), new(3, 1), "3x4 armor");
Assert(FoldedGeometry.ForArmor(4, 4), new(2, 2), "4x4 armor");
Assert(FoldedGeometry.ForArmor(3, 5), new(4, 1), "3x5 armor");
Assert(FoldedGeometry.ForHelmet(2, 2), new(1, 2), "four-cell headwear");
Assert(FoldedGeometry.ForFaceCover(2, 1), new(1, 1), "two-cell face cover");
Assert(FoldedGeometry.ForFaceCover(2, 2), new(1, 2), "four-cell face cover");
Assert(FoldedGeometry.ForPoster(2, 2), new(1, 1), "2x2 poster");

Console.WriteLine("Foldables Extended geometry contract: PASS");

static void Assert(FoldedSize actual, FoldedSize expected, string name)
{
    if (actual != expected)
    {
        throw new InvalidOperationException($"{name}: expected {expected.Width}x{expected.Height}, got {actual.Width}x{actual.Height}");
    }
}
