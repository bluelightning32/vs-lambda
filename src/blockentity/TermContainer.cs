using System.Text;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Lambda.BlockEntity;

public interface IInventoryControl {
  public string GetTitle();
  public string GetDescription() { return null; }
  public int GetMaxStackForItem(ItemStack item);
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

  protected string _inventoryClassName = "term";
  public override string InventoryClassName => _inventoryClassName;

  public TermContainer() { _inventory.SlotModified += OnSlotModified; }

  protected IInventoryControl GetInventoryControl() {
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

  public virtual int GetMaxStackForItem(ItemStack item) {
    return GetInventoryControl()?.GetMaxStackForItem(item) ?? 0;
  }

  private void SetSlot() {
    ItemStack item = _inventory[0].Itemstack;
    _inventory[0] = new SelectiveItemSlot(_inventory, GetMaxStackForItem);
    _inventory[0].Itemstack = item;
  }

  public virtual string GetInventoryTerm() {
    ItemStack item = _inventory[0].Itemstack;
    CollectibleBehavior.Term term =
        item?.Collectible.GetBehavior<CollectibleBehavior.Term>();
    return term?.GetTerm(item);
  }

  public override void Initialize(ICoreAPI api) {
    // `base.Initialize` will access `InventoryClassName`, so
    // `_inventoryClassName` must be set first.
    _inventoryClassName =
        Block.Attributes["inventoryClassName"].AsString(_inventoryClassName);
    base.Initialize(api);

    // The inventory was created with a generic slot. Set the correct slot type
    // now.
    SetSlot();
  }

  protected virtual GuiDialogBlockEntity CreateDialog(string title) {
    string description = GetInventoryControl()?.GetDescription();
    return new Gui.DialogTermInventory(title, description, Inventory, Pos,
                                       Api as ICoreClientAPI);
  }

  public override bool OnPlayerRightClick(IPlayer byPlayer,
                                          BlockSelection blockSel) {
    if (Api.Side == EnumAppSide.Client) {
      string title = GetInventoryControl()?.GetTitle();
      if (title != null) {
        toggleInventoryDialogClient(byPlayer, () => CreateDialog(title));
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

  protected virtual void OnSlotModified(int slotId) {
    GetInventoryControl()?.OnSlotModified();
  }

  // Update the slot and dialog options.
  // The behavior that's providing the inventory control should call this when
  // it changes its inventory options.
  public void InventoryChanged() {
    if (invDialog != null) {
      invDialog?.TryClose();

      invDialog?.Dispose();
      invDialog = null;
    }
    // Recreate the inventory slot.
    SetSlot();
  }

  public override void OnBlockPlaced(ItemStack byItemStack) {
    base.OnBlockPlaced(byItemStack);
    // `BlockEntityContainer.OnBlockPlaced` has a bug where it doesn't call the
    // block entity behaviors. So call them here to work around the bug.
    foreach (var behavior in Behaviors) {
      behavior.OnBlockPlaced(byItemStack);
    }
  }
}