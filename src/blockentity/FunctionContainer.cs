using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Lambda.BlockEntity;

// Creates a container with a single slot. The dialog has an inscribe button and
// a status bar. Decisions about whether to show the dialog and what kind of
// items to accept are forwarded to the first behavior that implements
// `IInventoryControl`.
public class FunctionContainer : TermContainer {
  public enum PacketId {
    Inscribe = 2000,
  }

  int _completed = 0;
  float _progress = 0;
  string _errorMessage;

  public FunctionContainer() {
    // This is just the default class name. `base.Initialize` may override it if
    // the inventoryClassName block attribute is set.
    _inventoryClassName = "function";
  }

  protected override GuiDialogBlockEntity CreateDialog(string title) {
    string description = GetInventoryControl()?.GetDescription();
    return new Gui.DialogFunctionInventory(
        title, description, _progress, _errorMessage, Inventory, Pos,
        Api as ICoreClientAPI, SendInscribePacket);
  }

  public override void FromTreeAttributes(ITreeAttribute tree,
                                          IWorldAccessor worldForResolving) {
    base.FromTreeAttributes(tree, worldForResolving);
    _progress = (float)tree.GetDecimal("progress");
    _errorMessage = tree.GetAsString("error");
    if (invDialog is Gui.DialogFunctionInventory dialog) {
      Api.Logger.Debug("FromTreeAttributes is updating the dialog");
      dialog.Progress = _progress;
      dialog.ErrorMessage = _errorMessage;
    }
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetFloat("progress", _progress);
    if (_errorMessage != null) {
      tree.SetString("error", _errorMessage);
    }
  }

  private void SendInscribePacket() {
    ((ICoreClientAPI)Api)
        .Network.SendBlockEntityPacket(Pos, (int)PacketId.Inscribe);
  }

  public override void OnReceivedClientPacket(IPlayer player, int packetid,
                                              byte[] data) {
    if (packetid == (int)PacketId.Inscribe) {
      Api.Logger.Debug("Server got inscribe packet");
      RegisterDelayedCallback(OnComplete, 1000);
      return;
    }
    base.OnReceivedClientPacket(player, packetid, data);
  }

  private void OnComplete(float delay) {
    Api.Logger.Debug("Inscribe done on the server side.");

    if (++_completed % 2 == 0) {
      _progress = 90;
      _errorMessage = null;
    } else {
      _progress = 0;
      _errorMessage = "placeholder error";
    }
    MarkDirty(true);
  }
}