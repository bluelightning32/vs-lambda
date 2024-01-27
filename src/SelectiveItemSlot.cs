using System;

using Vintagestory.API.Common;

namespace Lambda;

class SelectiveItemSlot : ItemSlot {
  public delegate int GetMaxStackForItemDelegate(ItemStack item);

  public GetMaxStackForItemDelegate GetMaxStackForItem;
  public SelectiveItemSlot(InventoryBase inventory,
                           GetMaxStackForItemDelegate getMaxStackForItem)
      : base(inventory) {
    GetMaxStackForItem = getMaxStackForItem;
  }

  public override bool
  CanTakeFrom(ItemSlot sourceSlot,
              EnumMergePriority priority = EnumMergePriority.AutoMerge) {
    return base.CanTakeFrom(sourceSlot, priority) &&
           GetMaxStackForItem(sourceSlot.Itemstack) > 0;
  }

  public override bool CanHold(ItemSlot sourceSlot) {
    return base.CanHold(sourceSlot) &&
           GetMaxStackForItem(sourceSlot.Itemstack) > 0;
  }

  public override int GetRemainingSlotSpace(ItemStack item) {
    return Math.Min(base.GetRemainingSlotSpace(item),
                    Math.Max(0, GetMaxStackForItem(item) - StackSize));
  }

  public override bool TryFlipWith(ItemSlot itemSlot) {
    if (itemSlot.StackSize > GetMaxStackForItem(itemSlot.Itemstack)) {
      return false;
    }
    return base.TryFlipWith(itemSlot);
  }
}