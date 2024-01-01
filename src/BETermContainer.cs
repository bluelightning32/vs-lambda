using System.Text;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace LambdaFactory;

public interface IInventoryControl {
  public string GetTitle();
  public string GetDescription() { return null; }
  public ItemSlot GetSlot(InventoryGeneric inventory);
  public bool GetHidePerishRate();
}

// Forwards more methods from the Block to the BlockEntity.
public class BlockEntityTermContainer : BlockEntityOpenableContainer {
  // Pass in null for the API and inventory class name for now. The correct
  // values will be passed by `BlockEntityOpenableContainer` when it calls
  // LateInitialize from inside of `BlockEntityOpenableContainer.Initialize`.
  private readonly InventoryGeneric _inventory =
      new InventoryGeneric(1, null, null);
  public override InventoryBase Inventory => _inventory;

  private string _inventoryClassName;
  public override string InventoryClassName => _inventoryClassName;

  public override void Initialize(ICoreAPI api) {
    // `base.Initialize` will access `InventoryClassName`, so
    // `_inventoryClassName` must be set first.
    _inventoryClassName =
        Block.Attributes["inventoryClassName"].AsString("term");
    base.Initialize(api);

    // The inventory was created with a generic slot. Set the correct slot type
    // now.
    _inventory[0] = Block.GetInterface<IInventoryControl>(Api.World, Pos)
                        ?.GetSlot(_inventory) ??
                    new ItemSlotOutput(_inventory);
  }

  public override bool OnPlayerRightClick(IPlayer byPlayer,
                                          BlockSelection blockSel) {
    if (Api.Side == EnumAppSide.Client) {
      string title =
          Block.GetInterface<IInventoryControl>(Api.World, Pos)?.GetTitle();
      string description =
          Block.GetInterface<IInventoryControl>(Api.World, Pos)
              ?.GetDescription();
      if (title != null) {
        toggleInventoryDialogClient(byPlayer, () => {
          return new GuiDialogTermInventory(title, description, Inventory, Pos,
                                            Api as ICoreClientAPI);
        });
      }
    }
    return true;
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    if (Block.GetInterface<IInventoryControl>(Api.World, Pos)
            ?.GetHidePerishRate() ??
        false) {
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