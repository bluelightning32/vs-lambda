using Vintagestory.API.Common;

namespace LambdaFactory;

class SelectiveItemSlot : ItemSlot {
  public delegate bool CanAcceptDelegate(ItemSlot source);

  public CanAcceptDelegate CanAccept;
  public SelectiveItemSlot(InventoryBase inventory, CanAcceptDelegate canAccept,
                           int maxSlotStackSize)
      : base(inventory) {
    CanAccept = canAccept;
    MaxSlotStackSize = maxSlotStackSize;
  }

  public override bool
  CanTakeFrom(ItemSlot sourceSlot,
              EnumMergePriority priority = EnumMergePriority.AutoMerge) {
    return base.CanTakeFrom(sourceSlot, priority) && CanAccept(sourceSlot);
  }

  public override bool CanHold(ItemSlot sourceSlot) {
    return base.CanHold(sourceSlot) && CanAccept(sourceSlot);
  }
}