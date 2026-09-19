using System.Collections.Generic;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using Foldables.Models.Items;

namespace SPTFoldablesExtended.Client;

internal static class RemovableContentMover
{
    public static bool TryMove(Item rootItem, CompoundItem compound, InventoryController inventoryController, bool simulate)
    {
        if (rootItem.Parent?.Container?.ParentItem is InventoryEquipment || rootItem.Parent?.Container?.ParentItem is not CompoundItem parent)
        {
            return false;
        }

        var operations = new Stack<OperationResult>();
        if (simulate && rootItem is IFoldable)
        {
            FoldableComponent foldable = rootItem.GetItemComponent<FoldableComponent>();
            if (foldable == null)
            {
                return false;
            }
            OperationResult<FoldResult> fold = ItemManipulator.Fold(foldable, !foldable.Folded, false);
            if (fold.Failed)
            {
                return false;
            }
            operations.Push(fold);
        }

        var items = new Stack<Item>();
        foreach (Grid grid in compound.Grids)
        {
            foreach (Item item in grid.Items)
            {
                items.Push(item);
            }
        }
        foreach (Slot slot in compound.Slots)
        {
            if (!slot.Locked && slot.ContainedItem != null)
            {
                items.Push(slot.ContainedItem);
            }
        }

        bool succeeded = true;
        while (items.TryPop(out Item item))
        {
            OperationResult move = ItemManipulator.QuickFindAppropriatePlace(
                item,
                inventoryController,
                [parent],
                ItemManipulator.EMoveItemOrder.MoveToAnotherSide,
                false);
            if (move.Failed)
            {
                succeeded = false;
                break;
            }
            operations.Push(move);
        }

        if (!simulate && succeeded)
        {
            while (operations.TryPop(out OperationResult move))
            {
                move.Value.RollBack();
                _ = inventoryController.TryRunNetworkTransaction(move);
            }
        }
        else
        {
            while (operations.TryPop(out OperationResult move))
            {
                move.Value.RollBack();
            }
        }

        return succeeded;
    }
}
