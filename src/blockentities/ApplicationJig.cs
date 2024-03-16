using System;
using System.IO;
using System.Linq;
using System.Text;

using Lambda.BlockBehaviors;
using Lambda.CollectibleBehaviors;
using Lambda.Network;
using Lambda.Token;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Lambda.BlockEntities;

public class ApplicationJig : Jig {
  // Pass in null for the API and inventory class name for now. The correct
  // values will be passed by `BlockEntityContainer.Initialize` when it calls
  // `Inventory.LateInitialize`.
  private readonly InventoryGeneric _inventory = new(2, null, null);
  public override InventoryBase Inventory => _inventory;
  TermInfo _termInfo = null;

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    if (_termState == TermState.CompilationDone ||
        _termInfo?.ErrorMessage != null) {
      _termInfo?.ToTreeAttributes(tree);
    }
  }

  protected override void LoadTermInfo(ITreeAttribute tree,
                                       IWorldAccessor worldForResolving) {
    _termInfo ??= new();
    _termInfo.FromTreeAttributes(tree);
    if (_termState != TermState.CompilationDone &&
        _termInfo?.ErrorMessage == null) {
      _termInfo = null;
    }
  }

  private Matrixf GetTransformationMatrix(float startY, ItemSlot slot) {
    return GetTransformationMatrix(startY, 6f / 16f, 6f / 16f, 6f / 16f, slot);
  }

  protected override float[][] genTransformationMatrices() {
    float[][] matrices = new float [_inventory.Count][];
    for (int i = 0; i < _inventory.Count; ++i) {
      ItemSlot slot = Inventory[i];
      Matrixf mat = GetTransformationMatrix(0.5f + i * 6f / 16f, slot);
      matrices[i] = mat.Values;
    }
    return matrices;
  }

  protected override void ResetTermState() {
    base.ResetTermState();
    // Keep the term info if it has an error and the first term is still there.
    if (_termInfo?.ErrorMessage == null || _inventory[0].Itemstack == null) {
      _termInfo = null;
    }
  }

  protected override void Compile(string[][] imports, string[] terms) {
    NodePos nodePos = new(Pos, 0);
    App app = new(nodePos, nodePos, nodePos);
    Constant applicandConstant = new(nodePos, imports[0], terms[0]);
    applicandConstant.AddSink(app.Applicand);
    Constant argumentConstant = new(nodePos, imports[1], terms[1]);
    argumentConstant.AddSink(app.Argument);

    using MemoryStream ms = new();
    CoqEmitter emitter = new(ms);
    app.GatherConstructImports(emitter);
    app.EmitConstruct(emitter, false);
    ms.Seek(0, SeekOrigin.Begin);
    StreamReader reader = new(ms);
    string term = reader.ReadToEnd();

    ServerConfig config = CoreSystem.GetInstance(Api).ServerConfig;
    using CoqSession session = new(config);

    TermInfo info =
        session.GetTermInfo(Pos, emitter.Imports.Keys.ToArray(), term);

    Api.Event.EnqueueMainThreadTask(() => CompilationDone(info), "lambda");
  }

  protected override bool CanCraftItems() {
    if (!base.CanCraftItems()) {
      return false;
    }
    if (_termInfo.ErrorMessage != null) {
      return false;
    }
    return true;
  }

  private void CompilationDone(TermInfo info) {
    if (info.ErrorMessage != null) {
      Api.Logger.Notification(
          "Compilation for application jig {0} completed with an error '{1}'.",
          Pos, info.ErrorMessage);
    } else {
      Api.Logger.Notification(
          "Compilation for application jig {0} completed successfully.", Pos);
    }

    _compilationRunning = false;
    if (_termState != TermState.CompilationRunning) {
      if (_termState == TermState.CompilationWaiting) {
        StartCompilation();
      }
      return;
    }
    _termInfo = info;
    _termState = TermState.CompilationDone;
    if (info.ErrorMessage != null) {
      _dropWaiting = false;
      if (!_inventory[1].Empty) {
        ItemStack removed = _inventory[1].TakeOutWhole();
        Api.World.SpawnItemEntity(removed, Pos.ToVec3d().Add(0.5, 0.5, 0.5),
                                  null);
        UpdateInventoryBounds();
        MarkDirty();
      }
    } else if (_dropWaiting) {
      _dropWaiting = false;
      if (CanCraftItems() && Api.Side == EnumAppSide.Server) {
        CraftItems();
      }
    } else {
      MarkDirty();
    }
  }

  protected override void CraftItems() {
    if (Api.Side != EnumAppSide.Server) {
      return;
    }
    TermInfo termInfo = _termInfo;
    _termState = TermState.Invalid;
    _termInfo = null;
    for (int i = 0; i < _inventory.Count; ++i) {
      _inventory[i].TakeOutWhole();
    }
    ItemStack combined = Term.Find(Api, termInfo);
    _inventory[0].Itemstack = combined;
    _inventory[0].MarkDirty();
    MarkDirty();
  }

  protected override Cuboidf GetItemBounds(int begin, int end) {
    return new(5f / 16f, 0.5f + begin * 6f / 16f, 5f / 16f, 11f / 16f,
               0.5f + end * 6f / 16f, 11f / 16f);
  }

  protected override void UpdateInventoryBounds() {
    int used = _inventory.Count;
    while (used > 0 && _inventory[used - 1].Empty) {
      --used;
    }

    Cuboidf bounds = GetItemBounds(0, used);
    if (bounds.Y2 > 1) {
      _inventoryBounds = new Cuboidf[1] { bounds };
    } else {
      _inventoryBounds = null;
    }
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    string error = _termInfo?.ErrorMessage;
    if (error != null) {
      dsc.AppendLine(Term.Escape(error));
    }
  }
}
