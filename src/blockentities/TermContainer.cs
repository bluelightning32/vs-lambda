using Lambda.CollectibleBehaviors;

using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Lambda.BlockEntities;

public class TermContainer : BlockEntityGenericTypedContainer {
  protected override void InitInventory(Block block) {
    base.InitInventory(block);

    // Recreate the slots of the inventory so that the new slots only accept
    // terms. When recreating the slots, copy over the existing contents.
    for (int i = 0; i < Inventory.Count; i++) {
      ItemStack item = Inventory[i].Itemstack;
      Inventory[i] = new SelectiveItemSlot(
          Inventory, GetMaxStackForItem) { Itemstack = item };
    }
  }

  private int GetMaxStackForItem(ItemStack item) {
    if (item.Collectible.HasBehavior<Term>()) {
      return 99999;
    } else {
      return 0;
    }
  }
}
