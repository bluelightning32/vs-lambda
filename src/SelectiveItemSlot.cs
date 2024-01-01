using Vintagestory.API.Common;

namespace LambdaFactory;

class SelectiveItemSlot : ItemSlot {
  public delegate bool CanAcceptDelegate(ItemSlot source);
  public delegate int GetMaxSlotStackSizeDelegate();

  public CanAcceptDelegate CanAccept;
  public GetMaxSlotStackSizeDelegate GetMaxSlotStackSize;
  public SelectiveItemSlot(InventoryBase inventory, CanAcceptDelegate canAccept,
                           GetMaxSlotStackSizeDelegate getMaxSlotStackSize)
      : base(inventory) {
    CanAccept = canAccept;
    GetMaxSlotStackSize = getMaxSlotStackSize;
  }

  public override int MaxSlotStackSize { get => GetMaxSlotStackSize(); }

  public override bool
  CanTakeFrom(ItemSlot sourceSlot,
              EnumMergePriority priority = EnumMergePriority.AutoMerge) {
    return base.CanTakeFrom(sourceSlot, priority) && CanAccept(sourceSlot);
  }

  public override bool CanHold(ItemSlot sourceSlot) {
    return base.CanHold(sourceSlot) && CanAccept(sourceSlot);
  }
}