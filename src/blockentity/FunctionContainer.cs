using System;
using System.Collections.Generic;
using System.Linq;

using Lambda.CollectibleBehavior;
using Lambda.Network;
using Lambda.Token;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Lambda.BlockEntity;

// Creates a container with a single slot. The dialog has an inscribe button and
// a status bar. Decisions about whether to show the dialog and what kind of
// items to accept are forwarded to the first behavior that implements
// `IInventoryControl`.
public class FunctionContainer : TermContainer {
  public enum PacketId {
    Inscribe = 2000,
  }

  float _progress = 0;
  float _finishTime = 0;
  long _startTime = 0;
  string _errorMessage;
  bool _running = false;
  long _processedCallback = -1;
  InscriptionRecipe _currentRecipe;
  readonly HashSet<IPlayer> _errorHighlights = new();

  public FunctionContainer() {
    // This is just the default class name. `base.Initialize` may override it if
    // the inventoryClassName block attribute is set.
    _inventoryClassName = "function";
  }

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    _currentRecipe = InscriptionSystem.GetInstance(Api).GetRecipeForIngredient(
        Inventory[0].Itemstack);
  }

  private RichTextComponentBase[] GetDescription() {
    if (Api is not ICoreClientAPI capi) {
      return null;
    }
    if (_currentRecipe is null) {
      return VtmlUtil.Richtextify(capi, GetInventoryControl()?.GetDescription(),
                                  CairoFont.WhiteSmallText());
    } else {
      return InscriptionSystem.GetInstance(Api).GetRecipeDescription(
          _currentRecipe);
    }
  }

  protected override GuiDialogBlockEntity CreateDialog(string title) {
    if (Api is not ICoreClientAPI capi) {
      return null;
    }
    _currentRecipe = InscriptionSystem.GetInstance(Api).GetRecipeForIngredient(
        Inventory[0].Itemstack);
    Gui.DialogFunctionInventory dialog =
        new(title, GetDescription(), _progress, _finishTime, _errorMessage,
            Inventory, Pos, capi, SendInscribePacket);
    dialog.SetInscribeEnabled(_currentRecipe is not null);
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
      if (recipe != _currentRecipe) {
        _currentRecipe = recipe;
        dialog.SetInscribeEnabled(_currentRecipe is not null);
        dialog.Description = GetDescription();
      }
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
      if (!_running && _processedCallback == -1 && _currentRecipe != null) {
        _progress = 0;
        _startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        _finishTime = _currentRecipe.ProcessTime;
        try {
          TokenEmitter emitter =
              GetBehavior<Network.BlockEntityBehavior.TokenEmitter>().Emit();
          _running = true;
          InscriptionRecipe recipe = _currentRecipe;
          TyronThreadPool.QueueLongDurationTask(
              () => Compile(player, emitter, recipe), "lambda");
        } catch (InvalidFormatException e) {
          CompilationDone(player, CoqResult.Error(e));
        } catch (Exception e) {
          CompilationDone(player, CoqResult.Error(e.Message));
        }
        MarkDirty(true);
      }
      return;
    }
    base.OnReceivedClientPacket(player, packetid, data);
  }

  private void Compile(IPlayer player, TokenEmitter emitter,
                       InscriptionRecipe recipe) {
    try {
      emitter.SetPuzzleParameters(recipe.Parameters ?? Array.Empty<string>());
      emitter.PostProcess();
      ServerConfig config = CoreSystem.GetInstance(Api).ServerConfig;
      using CoqSession session = new(config);
      CoqResult result = session.ValidateCoq(emitter);
      Api.Event.EnqueueMainThreadTask(() => CompilationDone(player, result),
                                      "lambda");
    } catch (Exception e) {
      Api.Event.EnqueueMainThreadTask(
          () => CompilationDone(player, CoqResult.Error(e.Message)), "lambda");
    } finally {
      emitter.Dispose();
    }
  }

  private void CompilationDone(IPlayer player, CoqResult result) {
    _running = false;
    long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
    long delay = (long)(_finishTime * 1000) - (now - _startTime);
    if (delay <= 0) {
      OnComplete(player, result);
    } else {
      _processedCallback = RegisterDelayedCallback(
          (float delay) => OnComplete(player, result), (int)delay);
    }
  }

  protected override void OnSlotModified(int slotId) {
    if (Api.Side == EnumAppSide.Server) {
      if (_processedCallback != -1) {
        UnregisterDelayedCallback(_processedCallback);
      }
      _processedCallback = -1;
      _progress = 0;
      _finishTime = 0;
      _errorMessage = null;
      ClearErrorHighlight();
      _currentRecipe =
          InscriptionSystem.GetInstance(Api).GetRecipeForIngredient(
              Inventory[0].Itemstack);
      MarkDirty(true);
    }
    base.OnSlotModified(slotId);
  }

  private ItemStack GetOutputItemStack() {
    InscriptionRecipe recipe =
        InscriptionSystem.GetInstance(Api).GetRecipeForIngredient(
            Inventory[0].Itemstack);
    if (recipe == null) {
      _errorMessage = "no active recipe";
      return null;
    }
    return recipe.Output.ResolvedItemstack.Clone();
  }

  private void OnComplete(IPlayer player, CoqResult result) {
    Api.Logger.Debug("Inscribe done on the server side.");

    if (_finishTime == 0) {
      // Somehow the recipe was reset while it was still inscribing.
      _errorMessage = null;
      ClearErrorHighlight();
    } else if (result.Successful) {
      _errorMessage = null;
      ClearErrorHighlight();
      ItemStack replacement = GetOutputItemStack();
      if (replacement is not null) {
        Inventory[0].Itemstack = replacement;
        Inventory[0].MarkDirty();
      }
    } else {
      Manager manager = NetworkSystem.GetInstance(Api).TokenEmitterManager;
      HashSet<BlockPos> errorLocations =
          manager.ConvertErrorLocations(result.ErrorLocations);
      Api.World.HighlightBlocks(player, MultiblockStructure.HighlightSlotId,
                                errorLocations.ToList());
      _errorHighlights.Add(player);
      _errorMessage = Term.Escape(result.ErrorMessage);
    }
    _progress = 0;
    _finishTime = 0;
    _processedCallback = -1;

    MarkDirty(true);
  }

  private void ClearErrorHighlight() {
    foreach (IPlayer player in _errorHighlights) {
      Api.World.HighlightBlocks(player, MultiblockStructure.HighlightSlotId,
                                new());
    }
    _errorHighlights.Clear();
  }

  public override int GetMaxStackForItem(ItemStack item) {
    if (base.GetMaxStackForItem(item) == 0) {
      // There are none of the attached behaviors accept the item. The block's
      // inventory is likely disabled because a port was added instead.
      return 0;
    }
    int maxStack = 0;
    Dictionary<AssetLocation, List<InscriptionRecipe>> recipes =
        InscriptionSystem.GetInstance(Api).GetRecipesForIngredient(item);
    foreach (List<InscriptionRecipe> group in recipes.Values) {
      foreach (InscriptionRecipe recipe in group) {
        maxStack =
            Math.Max(maxStack, recipe.Ingredient.ResolvedItemstack.StackSize);
      }
    }
    return maxStack;
  }

  public override string GetInventoryTerm() {
    return _currentRecipe?.PuzzleType;
  }
}
