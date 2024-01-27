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
  float _finishTime = 0;
  string _errorMessage;
  long _processed_callback = -1;

  public FunctionContainer() {
    // This is just the default class name. `base.Initialize` may override it if
    // the inventoryClassName block attribute is set.
    _inventoryClassName = "function";
  }

  protected override GuiDialogBlockEntity CreateDialog(string title) {
    string description = GetInventoryControl()?.GetDescription();
    Gui.DialogFunctionInventory dialog =
        new(title, description, _progress, _finishTime, _errorMessage,
            Inventory, Pos, Api as ICoreClientAPI, SendInscribePacket);
    InscriptionRecipe recipe =
        InscriptionSystem.GetInstance(Api).GetRecipeForIngredient(
            Inventory[0].Itemstack);
    dialog.SetInscribeEnabled(recipe is not null);
    return dialog;
  }

  public override void FromTreeAttributes(ITreeAttribute tree,
                                          IWorldAccessor worldForResolving) {
    base.FromTreeAttributes(tree, worldForResolving);
    _progress = (float)tree.GetDecimal("progress");
    _finishTime = (float)tree.GetDecimal("finishtime");
    _errorMessage = tree.GetAsString("error");
    if (invDialog is Gui.DialogFunctionInventory dialog) {
      Api.Logger.Debug("FromTreeAttributes is updating the dialog");
      dialog.SetProgress(_progress, _finishTime);
      dialog.ErrorMessage = _errorMessage;
      InscriptionRecipe recipe =
          InscriptionSystem.GetInstance(Api).GetRecipeForIngredient(
              Inventory[0].Itemstack);
      dialog.SetInscribeEnabled(recipe is not null);
    }
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetFloat("progress", _progress);
    tree.SetFloat("finishtime", _finishTime);
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
      if (_processed_callback == -1) {
        _progress = 0;
        _finishTime = 1;
        _processed_callback =
            RegisterDelayedCallback(OnComplete, (int)(_finishTime * 1000));
        MarkDirty(true);
      }
      return;
    }
    base.OnReceivedClientPacket(player, packetid, data);
  }

  protected override void OnSlotModified(int slotId) {
    if (Api.Side == EnumAppSide.Server) {
      _processed_callback = -1;
      _progress = 0;
      _finishTime = 0;
      _errorMessage = null;
      MarkDirty(true);
    }
    base.OnSlotModified(slotId);
  }

  private void OnComplete(float delay) {
    Api.Logger.Debug("Inscribe done on the server side.");
    _processed_callback = -1;
    if (_finishTime != 0) {
      if (++_completed % 2 == 0) {
        _progress = 0;
        _finishTime = 0;
        _errorMessage = null;
        AssetLocation stick = new AssetLocation("game", "stick");
        CollectibleObject replacement;
        if (Inventory[0].Itemstack.Collectible.Code == stick) {
          replacement = Api.World.GetBlock(new AssetLocation("game", "barrel"));
        } else {
          replacement = Api.World.GetItem(stick);
        }
        Inventory[0].Itemstack = new ItemStack(replacement, 1);
        Inventory[0].MarkDirty();
      } else {
        _progress = 0;
        _finishTime = 0;
        _errorMessage = "placeholder error";
      }
    }
    MarkDirty(true);
  }
}