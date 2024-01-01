using System.Collections.Generic;

using Lambda.BlockEntity;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Lambda.BlockBehavior;

using VSBlockBehavior = Vintagestory.API.Common.BlockBehavior;

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
    CollectibleBehavior.Term term =
        item.Collectible.GetBehavior<CollectibleBehavior.Term>();
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

// Controls what kind of item the container can hold. The block entity must
// inherit from BlockEntityTermContainer for this behavior to do anything.
public class Inventory : VSBlockBehavior, IInventoryControl {

  public Inventory(Block block) : base(block) {}

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