using Comfort.Common;
using Diz.LanguageExtensions;
using EFT;
using EFT.InventoryLogic;
using Foldables.Models;
using Foldables.Models.Items;
using Foldables.Models.Templates;
using JetBrains.Annotations;

namespace SPTFoldablesExtended.Client;

public sealed class FoldableArmorTemplate : ArmorTemplate, IExtendedFoldableComponentTemplate
{
    public bool Foldable { get; set; }
    public string FoldedSlot { get; set; }
    public int SizeReduceRight { get; set; }
    public int SizeReduceDown { get; set; }
    public float FoldingTime { get; set; }
}

public sealed class FoldableArmor : Armor, IFoldable
{
    [Component, UsedImplicitly]
    public readonly ExtendedFoldableComponent Foldable;

    public bool Folded => Foldable is { Folded: true };
    public int SizeReduceRight => GetTemplate<FoldableArmorTemplate>().SizeReduceRight;
    public int SizeReduceDown => GetTemplate<FoldableArmorTemplate>().SizeReduceDown;
    public float FoldingTime => GetTemplate<FoldableArmorTemplate>().FoldingTime;

    public FoldableArmor(string id, FoldableArmorTemplate template) : base(id, template)
    {
        if (template.Foldable)
        {
            Foldable = new ExtendedFoldableComponent(this, template);
            Components.Add(Foldable);
        }
    }

    public override OperationResult Apply(ItemController itemController, Item item, int count, bool simulate)
        => Folded ? new Foldables.Models.FoldedInsertError(item) : base.Apply(itemController, item, count, simulate);

    public override int GetHashSum() => Foldable == null ? base.GetHashSum() : base.GetHashSum() * 27 + Foldable.Folded.GetHashCode();
}

public sealed class FoldableHeadwearTemplate : HeadwearTemplate, IExtendedFoldableComponentTemplate
{
    public bool Foldable { get; set; }
    public string FoldedSlot { get; set; }
    public int SizeReduceRight { get; set; }
    public int SizeReduceDown { get; set; }
    public float FoldingTime { get; set; }
}

public sealed class FoldableHeadwear : Headwear, IFoldable
{
    [Component, UsedImplicitly]
    public readonly ExtendedFoldableComponent Foldable;

    public bool Folded => Foldable is { Folded: true };
    public int SizeReduceRight => GetTemplate<FoldableHeadwearTemplate>().SizeReduceRight;
    public int SizeReduceDown => GetTemplate<FoldableHeadwearTemplate>().SizeReduceDown;
    public float FoldingTime => GetTemplate<FoldableHeadwearTemplate>().FoldingTime;

    public FoldableHeadwear(string id, FoldableHeadwearTemplate template) : base(id, template)
    {
        if (template.Foldable)
        {
            Foldable = new ExtendedFoldableComponent(this, template);
            Components.Add(Foldable);
        }
    }

    public override OperationResult Apply(ItemController itemController, Item item, int count, bool simulate)
        => Folded ? new Foldables.Models.FoldedInsertError(item) : base.Apply(itemController, item, count, simulate);

    public override int GetHashSum() => Foldable == null ? base.GetHashSum() : base.GetHashSum() * 27 + Foldable.Folded.GetHashCode();
}

public sealed class FoldableFaceCoverTemplate : FaceCoverTemplate, IExtendedFoldableComponentTemplate
{
    public bool Foldable { get; set; }
    public string FoldedSlot { get; set; }
    public int SizeReduceRight { get; set; }
    public int SizeReduceDown { get; set; }
    public float FoldingTime { get; set; }
}

public sealed class FoldableFaceCover : FaceCover, IFoldable
{
    [Component, UsedImplicitly]
    public readonly ExtendedFoldableComponent Foldable;

    public bool Folded => Foldable is { Folded: true };
    public int SizeReduceRight => GetTemplate<FoldableFaceCoverTemplate>().SizeReduceRight;
    public int SizeReduceDown => GetTemplate<FoldableFaceCoverTemplate>().SizeReduceDown;
    public float FoldingTime => GetTemplate<FoldableFaceCoverTemplate>().FoldingTime;

    public FoldableFaceCover(string id, FoldableFaceCoverTemplate template) : base(id, template)
    {
        if (template.Foldable)
        {
            Foldable = new ExtendedFoldableComponent(this, template);
            Components.Add(Foldable);
        }
    }

    public override OperationResult Apply(ItemController itemController, Item item, int count, bool simulate)
        => Folded ? new Foldables.Models.FoldedInsertError(item) : base.Apply(itemController, item, count, simulate);

    public override int GetHashSum() => Foldable == null ? base.GetHashSum() : base.GetHashSum() * 27 + Foldable.Folded.GetHashCode();
}

public sealed class FoldablePosterTemplate : CompoundItemTemplate, IExtendedFoldableComponentTemplate
{
    public bool Foldable { get; set; }
    public string FoldedSlot { get; set; }
    public int SizeReduceRight { get; set; }
    public int SizeReduceDown { get; set; }
    public float FoldingTime { get; set; }
}

public sealed class FoldablePoster : CompoundItem, IFoldable
{
    [Component, UsedImplicitly]
    public readonly ExtendedFoldableComponent Foldable;

    public bool Folded => Foldable is { Folded: true };
    public int SizeReduceRight => GetTemplate<FoldablePosterTemplate>().SizeReduceRight;
    public int SizeReduceDown => GetTemplate<FoldablePosterTemplate>().SizeReduceDown;
    public float FoldingTime => GetTemplate<FoldablePosterTemplate>().FoldingTime;

    public FoldablePoster(string id, FoldablePosterTemplate template) : base(id, template)
    {
        // SPT 4.1.6 can materialize custom templates before
        // extension-data fold geometry has been bound. The Folded flag then
        // changes correctly while both reductions remain zero, leaving a 4-cell
        // poster at its open size. Posters have one fixed contract, so restore
        // that geometry from the native dimensions at the client boundary.
        if (template.Width * template.Height == 4)
        {
            template.SizeReduceRight = System.Math.Max(0, template.Width - 1);
            template.SizeReduceDown = System.Math.Max(0, template.Height - 1);
        }

        if (template.Foldable)
        {
            Foldable = new ExtendedFoldableComponent(this, template);
            Components.Add(Foldable);
        }
    }

    public override int GetHashSum() => Foldable == null ? base.GetHashSum() : base.GetHashSum() * 27 + Foldable.Folded.GetHashCode();
}
