using System.Collections.Generic;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace LambdaFactory;

public class InventoryOptions {
  public bool RequireTerm;
  public bool RequireConstructor;
  public bool RequireFunction;
  public int MaxSlotStackSize;
  public string DialogTitleLangCode;
  public string DialogDescLangCode;
  public bool HidePerishRate;
  public Dictionary<string, CompositeTexture> FullTextures;

  public bool CanAccept(ItemStack item) {
    BehaviorTerm term = item.Collectible.GetBehavior<BehaviorTerm>();
    if (RequireTerm) {
      if (term == null) {
        return false;
      }
    }
    if (RequireFunction) {
      if (!(term?.IsFunction(item) ?? false)) {
        return false;
      }
    }
    if (RequireConstructor) {
      if (term?.GetConstructs(item) == null) {
        return false;
      }
    }
    return true;
  }
}

// The block entity must inherit from BlockEntityTermContainer for this behavior
// to do anything.
public class BlockBehaviorInventory : BlockBehavior, IInventoryControl {

  public BlockBehaviorInventory(Block block) : base(block) {}

  private InventoryOptions _options;

  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);
    _options = properties.AsObject<InventoryOptions>();
  }

  bool IInventoryControl.GetHidePerishRate() { return _options.HidePerishRate; }

  private bool CanAccept(ItemSlot sourceSlot) {
    return _options.CanAccept(sourceSlot.Itemstack);
  }

  ItemSlot IInventoryControl.GetSlot(InventoryGeneric inventory) {
    return new SelectiveItemSlot(inventory, CanAccept,
                                 _options.MaxSlotStackSize);
  }

  string IInventoryControl.GetTitle() { return _options.DialogTitleLangCode; }

  string IInventoryControl.GetDescription() {
    return _options.DialogDescLangCode;
  }
}