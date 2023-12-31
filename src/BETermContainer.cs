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
    _inventory = new InventoryGeneric(1, null, null, null);
  }

  public override bool OnPlayerRightClick(IPlayer byPlayer,
                                          BlockSelection blockSel) {
    if (Api.Side == EnumAppSide.Client) {
      toggleInventoryDialogClient(byPlayer, () => {
        return new GuiDialogBlockEntityInventory(
            Lang.Get(Block.Attributes["title"].AsString("term-container")),
            Inventory, Pos, 1, Api as ICoreClientAPI);
      });
    }
    return true;
  }
}