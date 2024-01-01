using System.Text;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace LambdaFactory;

// Forwards more methods from the Block to the BlockEntity.
public class BlockEntityTermContainer : BlockEntityOpenableContainer {
  private InventoryGeneric _inventory;
  public override InventoryBase Inventory => _inventory;

  private string _inventoryClassName;
  public override string InventoryClassName => _inventoryClassName;

  // `CreateBehaviors` is the first function to receive the block. It is called
  // before `Initialize` and `FromTreeAttributes`.
  public override void CreateBehaviors(Block block,
                                       IWorldAccessor worldForResolve) {
    base.CreateBehaviors(block, worldForResolve);
    _inventoryClassName =
        block.Attributes["inventoryClassName"].AsString("term");
    // Pass in null for the API and inventory class name for now. The correct
    // values will be passed by `BlockEntityOpenableContainer` when it calls
    // LateInitialize later.
    _inventory = new InventoryGeneric(1, null, null, CreateSlot);
  }

  private bool CanAccept(ItemSlot sourceSlot) {
    BehaviorTerm term =
        sourceSlot.Itemstack.Collectible.GetBehavior<BehaviorTerm>();
    if (Block.Attributes["requireTerm"].AsBool()) {
      if (term == null) {
        return false;
      }
    }
    if (Block.Attributes["requireConstructor"].AsBool()) {
      if (term?.GetConstructs(sourceSlot.Itemstack) == null) {
        return false;
      }
    }
    return true;
  }

  private int GetMaxSlotStackSize() {
    return Block.Attributes["maxSlotStackSize"].AsInt(999999);
  }

  private ItemSlot CreateSlot(int slotId, InventoryGeneric self) {
    return new SelectiveItemSlot(self, CanAccept, GetMaxSlotStackSize);
  }

  public override bool OnPlayerRightClick(IPlayer byPlayer,
                                          BlockSelection blockSel) {
    if (Api.Side == EnumAppSide.Client) {
      toggleInventoryDialogClient(byPlayer, () => {
        return new GuiDialogTermInventory(
            Block.Attributes["dialogTitleLangCode"].AsString(
                "term-container-title"),
            Block.Attributes["dialogDescLangCode"].AsString(
                "term-container-description"),
            Inventory, Pos, Api as ICoreClientAPI);
      });
    }
    return true;
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    if (Block.Attributes["hidePerishRate"].AsBool()) {
      // BlockEntityOpenableContainer adds the message that we're trying to
      // hide. So skip calling the base class, and call the behaviors directly
      // instead.
      foreach (var behavior in Behaviors) {
        behavior.GetBlockInfo(forPlayer, dsc);
      }
      return;
    }
    base.GetBlockInfo(forPlayer, dsc);
  }
}