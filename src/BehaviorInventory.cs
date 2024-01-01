using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace LambdaFactory;

// The block entity must inherit from BlockEntityTermContainer for this behavior
// to do anything.
public class BlockBehaviorInventory : BlockBehavior, IInventoryControl {
  private bool _requireTerm;
  private bool _requireConstructor;
  private int _maxSlotStackSize;
  private string _dialogTitleLangCode;
  private string _dialogDescLangCode;
  private bool _hidePerishRate;

  public BlockBehaviorInventory(Block block) : base(block) {}

  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);
    _requireTerm = properties["requireTerm"].AsBool(false);
    _requireConstructor = properties["requireConstructor"].AsBool(false);
    _maxSlotStackSize = properties["maxSlotStackSize"].AsInt(999999);
    _dialogTitleLangCode =
        properties["dialogTitleLangCode"].AsString("term-container-title");
    _dialogDescLangCode =
        properties["dialogDescLangCode"].AsString("term-container-description");
    _hidePerishRate = properties["hidePerishRate"].AsBool();
  }

  bool IInventoryControl.GetHidePerishRate() { return _hidePerishRate; }

  private bool CanAccept(ItemSlot sourceSlot) {
    BehaviorTerm term =
        sourceSlot.Itemstack.Collectible.GetBehavior<BehaviorTerm>();
    if (_requireTerm) {
      if (term == null) {
        return false;
      }
    }
    if (_requireConstructor) {
      if (term?.GetConstructs(sourceSlot.Itemstack) == null) {
        return false;
      }
    }
    return true;
  }

  ItemSlot IInventoryControl.GetSlot(InventoryGeneric inventory) {
    return new SelectiveItemSlot(inventory, CanAccept, _maxSlotStackSize);
  }

  string IInventoryControl.GetTitle() { return _dialogTitleLangCode; }

  string IInventoryControl.GetDescription() { return _dialogDescLangCode; }
}