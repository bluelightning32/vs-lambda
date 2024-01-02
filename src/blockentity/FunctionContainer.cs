using System.Text;

using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Lambda.BlockEntity;

// Creates a container with a single slot. The dialog has an inscribe button and
// a status bar. Decisions about whether to show the dialog and what kind of
// items to accept are forwarded to the first behavior that implements
// `IInventoryControl`.
public class FunctionContainer : TermContainer {
  // Pass in null for the API and inventory class name for now. The correct
  // values will be passed by `BlockEntityOpenableContainer` when it calls
  // LateInitialize from inside of `BlockEntityOpenableContainer.Initialize`.

  public FunctionContainer() {
    // This is just the default class name. `base.Initialize` may override it if
    // the inventoryClassName block attribute is set.
    _inventoryClassName = "function";
  }

  protected override GuiDialogBlockEntity CreateDialog(string title) {
    string description = GetInventoryControl()?.GetDescription();
    return new Gui.DialogFunctionInventory(title, description, Inventory, Pos,
                                           Api as ICoreClientAPI);
  }
}