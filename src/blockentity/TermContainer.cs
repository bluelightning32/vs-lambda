using System.Text;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Lambda.BlockEntity;

public interface IInventoryControl {
  public string GetTitle();
  public string GetDescription() { return null; }
  public ItemSlot GetSlot(InventoryGeneric inventory);
  public bool GetHidePerishRate();
  public void OnSlotModified() {}
}

// Creates a container with a single slot. Decisions about format of the dialog
// and what kind of items to accept are forwarded to the first behavior that
// implements `IInventoryControl`.
public class TermContainer : BlockEntityOpenableContainer {
  // Pass in null for the API and inventory class name for now. The correct
  // values will be passed by `BlockEntityOpenableContainer` when it calls
  // LateInitialize from inside of `BlockEntityOpenableContainer.Initialize`.
  private readonly InventoryGeneric _inventory =
      new InventoryGeneric(1, null, null);
  public override InventoryBase Inventory => _inventory;

  private string _inventoryClassName;
  public override string InventoryClassName => _inventoryClassName;

  public TermContainer() { _inventory.SlotModified += OnSlotModified; }

  private IInventoryControl GetInventoryControl() {
    // Don't use `Block.GetInterface`, because that uses the block accessor to
    // look up the block entity at the block position, but `GetInventoryControl`
    // might be called before the block entity is linked to the map.
    {
      if (Block is IInventoryControl result) {
        return result;
      }
    }
    foreach (var behavior in Block.CollectibleBehaviors) {
      if (behavior is IInventoryControl result) {
        return result;
      }
    }
    return GetBehavior<IInventoryControl>();
  }

  private void SetSlot() {
    ItemStack item = _inventory[0].Itemstack;
    _inventory[0] = GetInventoryControl()?.GetSlot(_inventory) ??
                    new ItemSlotOutput(_inventory);
    _inventory[0].Itemstack = item;
  }

  public override void Initialize(ICoreAPI api) {
    // `base.Initialize` will access `InventoryClassName`, so
    // `_inventoryClassName` must be set first.
    _inventoryClassName =
        Block.Attributes["inventoryClassName"].AsString("term");
    base.Initialize(api);

    // The inventory was created with a generic slot. Set the correct slot type
    // now.
    SetSlot();
  }

  public override bool OnPlayerRightClick(IPlayer byPlayer,
                                          BlockSelection blockSel) {
    if (Api.Side == EnumAppSide.Client) {
      string title = GetInventoryControl()?.GetTitle();
      string description = GetInventoryControl()?.GetDescription();
      if (title != null) {
        toggleInventoryDialogClient(byPlayer, () => {
          return new Gui.DialogTermInventory(title, description, Inventory, Pos,
                                             Api as ICoreClientAPI);
        });
      }
    }
    return true;
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    if (GetInventoryControl()?.GetHidePerishRate() ?? false) {
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

  private void OnSlotModified(int slotId) {
    GetInventoryControl()?.OnSlotModified();
  }

  public void InventoryChanged() {
    if (invDialog != null) {
      invDialog?.TryClose();

      invDialog?.Dispose();
      invDialog = null;
    }
    // Recreate the inventory slot.
    SetSlot();
  }
}