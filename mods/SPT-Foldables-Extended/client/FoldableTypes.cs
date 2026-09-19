using Comfort.Common;
using Diz.LanguageExtensions;
using EFT;
using EFT.InventoryLogic;
using Foldables.Models;
using Foldables.Models.Items;
using Foldables.Models.Templates;
using JetBrains.Annotations;

namespace SPTFoldablesExtended.Client;

public abstract class ExtendedFoldableItemBase
{
    protected static ExtendedFoldableComponent Create(Item item, IExtendedFoldableComponentTemplate template)
        => new(item, template);
}

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

public sealed class FoldablePosterTemplate : ItemTemplate, IExtendedFoldableComponentTemplate
{
    public bool Foldable { get; set; }
    public string FoldedSlot { get; set; }
    public int SizeReduceRight { get; set; }
    public int SizeReduceDown { get; set; }
    public float FoldingTime { get; set; }
}

public sealed class FoldablePoster : Item, IFoldable
{
    [Component, UsedImplicitly]
    public readonly ExtendedFoldableComponent Foldable;

    public bool Folded => Foldable is { Folded: true };
    public int SizeReduceRight => GetTemplate<FoldablePosterTemplate>().SizeReduceRight;
    public int SizeReduceDown => GetTemplate<FoldablePosterTemplate>().SizeReduceDown;
    public float FoldingTime => GetTemplate<FoldablePosterTemplate>().FoldingTime;

    public FoldablePoster(string id, FoldablePosterTemplate template) : base(id, template)
    {
        if (template.Foldable)
        {
            Foldable = new ExtendedFoldableComponent(this, template);
            Components.Add(Foldable);
        }
    }

    public override int GetHashSum() => Foldable == null ? base.GetHashSum() : base.GetHashSum() * 27 + Foldable.Folded.GetHashCode();
}
