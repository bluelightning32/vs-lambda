using System.Collections.Generic;

using Lambda.BlockEntity;
using Lambda.Network;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Lambda.BlockBehavior;

using VSBlockBehavior = Vintagestory.API.Common.BlockBehavior;

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

  string IInventoryControl.GetTitle() { return _options.DialogTitleLangCode; }

  string IInventoryControl.GetDescription() {
    return _options.DialogDescLangCode;
  }

  int IInventoryControl.GetMaxStackForItem(ItemStack item) {
    return _options.GetMaxStackForItem(item);
  }
}