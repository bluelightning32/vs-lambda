using System;
using System.IO;
using System.Linq;
using System.Text;

using Lambda.BlockBehavior;
using Lambda.CollectibleBehavior;
using Lambda.Network;
using Lambda.Token;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Lambda.BlockEntity;

public class ApplicationJig : BlockEntityDisplay,
                              IBlockEntityForward,
                              IDropCraftListener,
                              ICollectibleTarget {
  // Pass in null for the API and inventory class name for now. The correct
  // values will be passed by `BlockEntityContainer.Initialize` when it calls
  // `Inventory.LateInitialize`.
  private readonly InventoryGeneric _inventory = new(2, null, null);
  public override InventoryBase Inventory => _inventory;
  private Cuboidf[] _inventoryBounds = null;
  private bool _compilationOutdated = true;
  private bool _compilationRunning = false;
  TermInfo _termInfo = null;
  bool _dropWaiting = false;
  int _hammerHits = 0;

  public override string InventoryClassName => "applicationjig";

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    // Set this so that right-click with a block in hand puts the block in the
    // inventory instead of trying to place it. Technically setting this isn't
    // necessary, because the container shouldn't accept blocks (only term
    // items), but this is set anyway for future proofing.
    Block.PlacedPriorityInteract = true;

    if (Api.Side == EnumAppSide.Server) {
      StartCompilation();
    }
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    if (!_compilationOutdated) {
      _termInfo?.ToTreeAttributes(tree);
    }
    tree.SetInt("hammerHits", _hammerHits);
  }

  public override void FromTreeAttributes(ITreeAttribute tree,
                                          IWorldAccessor worldForResolving) {
    base.FromTreeAttributes(tree, worldForResolving);

    _termInfo ??= new();
    _termInfo.FromTreeAttributes(tree);
    _hammerHits = tree.GetInt("hammerHits", 0);
    _compilationOutdated =
        _termInfo.ErrorMessage == null && _termInfo.Term == null;
    if (_compilationOutdated) {
      _termInfo = null;
    }

    int used = _inventory.Count;
    while (used > 0 && _inventory[used - 1].Empty) {
      --used;
    }
    SetInventoryBounds(used);
    RedrawAfterReceivingTreeAttributes(worldForResolving);
  }

  protected override float[][] genTransformationMatrices() {
    float[][] matrices = new float [_inventory.Count][];
    for (int i = 0; i < _inventory.Count; ++i) {
      ItemSlot slot = Inventory[i];
      MeshData mesh = null;
      if (!slot.Empty) {
        mesh = getMesh(slot.Itemstack);
      }
      // GetMeshBounds handles nulls.
      Cuboidf bounds = MeshUtil.GetMeshBounds(mesh);
      Matrixf mat = Matrixf.Create();
      // 4. Move the mesh up from the bottom of the cube to its appropriate
      //    vertical position. The first inventory item starts half way up the
      //    cube. The next inventory item is 6/16 above it.
      mat.Translate(0, 0.5f + i * 6f / 16f, 0);
      // Term meshes are expected to be 6x6x6, centered at the bottom of the
      // cube. If the mesh is already within those bounds, don't translate it.
      // However, if it is outside of those bounds, translate its lowest corner
      // to (5,0,5).
      if (bounds.X1 < 4.999f / 16f || bounds.X1 > 11.001f / 16f ||
          bounds.Y1 < -0.001f / 16f || bounds.Y1 > 6.001f / 16f ||
          bounds.Z1 < 4.999f / 16f || bounds.Z1 > 11.001f / 16f) {
        float size =
            Math.Max(Math.Max(bounds.XSize, bounds.YSize), bounds.ZSize);
        // 3. Move the mesh to bottom the center of the cube.
        mat.Translate(0.5f, 0, 0.5f);
        // 2. Uniformly shrink the mesh so that it fits in [(-3/16, 0, -3/16),
        //    (3/16, 6, 3/16)].
        if (size > 6f / 16f) {
          float shrink = (6f / 16f) / size;
          mat.Scale(shrink, shrink, shrink);
        }
        // 1. Put the mesh on top of the XZ plane, centered at the XZ plane
        // origin.
        mat.Translate(-bounds.MidX, -bounds.Y1, -bounds.MidZ);
      }
      matrices[i] = mat.Values;
    }
    return matrices;
  }

  private bool TryPut(IPlayer player, ref EnumHandling handled) {
    ItemSlot hotbarSlot = player.InventoryManager.ActiveHotbarSlot;
    if (hotbarSlot.Empty) {
      handled = EnumHandling.PassThrough;
      return false;
    }
    if (hotbarSlot.Itemstack.Collectible
            .GetBehavior<CollectibleBehavior.Term>() == null) {
      (Api as ICoreClientAPI)
          ?.TriggerIngameError(this, "onlyterms", Lang.Get("lambda:onlyterms"));
      handled = EnumHandling.PreventSubsequent;
      return false;
    }
    for (int i = 0; i < _inventory.Count; ++i) {
      if (!_inventory[i].Empty) {
        continue;
      }
      // These are the bounds of where the item will be rendered.
      Cuboidf itemBounds = GetItemBounds(i, i + 1);
      if (itemBounds.Y2 > 1.01f) {
        // Prevent the new item from being reported as intersecting back with
        // this block.
        itemBounds.Y1 = 1.01f;
        if (Api.World.CollisionTester.IsColliding(
                Api.World.BlockAccessor, itemBounds, Pos.ToVec3d(), false)) {
          (Api as ICoreClientAPI)
              ?.TriggerIngameError(this, "nospaceabove",
                                   Lang.Get("lambda:nospaceabove"));
          handled = EnumHandling.PreventSubsequent;
          return false;
        }
      }
      if (hotbarSlot.TryPutInto(Api.World, _inventory[i], 1) > 0) {
        SetInventoryBounds(i + 1);
        MarkDirty();
        handled = EnumHandling.PreventSubsequent;
        MarkTermInfoDirty();
        if (i == 1 && Api.Side == EnumAppSide.Server) {
          StartCompilation();
        }
        return true;
      }
    }
    handled = EnumHandling.PassThrough;
    return false;
  }

  private void MarkTermInfoDirty() {
    _compilationOutdated = true;
    _termInfo = null;
    _hammerHits = 0;
  }

  private void StartCompilation() {
    if (_compilationRunning) {
      return;
    }
    string[] terms = new string[2];
    string[][] imports = new string[2][];
    for (int i = 0; i < 2; ++i) {
      Term termBhv = _inventory[i].Itemstack?.Collectible.GetBehavior<Term>();
      string term = termBhv?.GetTerm(_inventory[i].Itemstack);
      if (term == null) {
        _compilationOutdated = false;
        _dropWaiting = false;
        return;
      }
      terms[i] = term;
      imports[i] = termBhv?.GetImports(_inventory[i].Itemstack);
    }

    _compilationRunning = true;
    _compilationOutdated = false;
    Api.Logger.Notification(
        "Starting compilation for application jig {0}. Applicand='{1}' argument='{2}'",
        Pos, terms[0], terms[1]);
    TyronThreadPool.QueueLongDurationTask(
        () => Compile(imports[0], terms[0], imports[1], terms[1]), "lambda");
  }

  private void Compile(string[] applicandImports, string applicand,
                       string[] argumentImports, string argument) {
    NodePos nodePos = new(Pos, 0);
    App app = new(nodePos, nodePos, nodePos);
    Constant applicandConstant = new(nodePos, applicandImports, applicand);
    applicandConstant.AddSink(app.Applicand);
    Constant argumentConstant = new(nodePos, argumentImports, argument);
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

  private void CompilationDone(TermInfo info) {
    _compilationRunning = false;
    if (_compilationOutdated) {
      StartCompilation();
      return;
    }
    _termInfo = info;
    if (info.ErrorMessage != null) {
      _dropWaiting = false;
      if (!_inventory[1].Empty) {
        ItemStack removed = _inventory[1].TakeOutWhole();
        Api.World.SpawnItemEntity(removed, Pos.ToVec3d().Add(0.5, 0.5, 0.5),
                                  null);
        SetInventoryBounds(1);
        MarkDirty();
      }
    } else if (_dropWaiting) {
      _dropWaiting = false;
      ((IDropCraftListener)this).OnDropCraft(Api.World, Pos, Pos);
    }
  }

  private void CreateCombinedTerm() {
    if (Api.Side != EnumAppSide.Server) {
      return;
    }
    if (_compilationRunning) {
      _dropWaiting = true;
      return;
    }
    if (_termInfo == null || _termInfo.Term == null) {
      return;
    }
    TermInfo termInfo = _termInfo;
    MarkTermInfoDirty();
    for (int i = 0; i < _inventory.Count; ++i) {
      _inventory[i].TakeOutWhole();
    }
    ItemStack combined = Term.Find(Api, termInfo);
    _inventory[0].Itemstack = combined;
    _inventory[0].MarkDirty();
    MarkDirty();
  }

  void IDropCraftListener.OnDropCraft(IWorldAccessor world, BlockPos pos,
                                      BlockPos dropper) {
    CreateCombinedTerm();
  }

  private static Cuboidf GetItemBounds(int begin, int end) {
    return new(5f / 16f, 0.5f + begin * 6f / 16f, 5f / 16f, 11f / 16f,
               0.5f + end * 6f / 16f, 11f / 16f);
  }

  private bool TryTake(IPlayer player) {
    for (int i = _inventory.Count - 1; i >= 0; --i) {
      if (_inventory[i].Empty) {
        continue;
      }
      ItemStack removed = _inventory[i].TakeOutWhole();
      if (!player.InventoryManager.TryGiveItemstack(removed)) {
        Api.World.SpawnItemEntity(removed, Pos.ToVec3d().Add(0.5, 0.5, 0.5),
                                  null);
      }
      SetInventoryBounds(i);
      MarkTermInfoDirty();
      MarkDirty();
      return true;
    }
    return false;
  }

  private void SetInventoryBounds(int usedSlots) {
    Cuboidf bounds = GetItemBounds(0, usedSlots);
    if (bounds.Y2 > 1) {
      _inventoryBounds = new Cuboidf[1] { bounds };
    } else {
      _inventoryBounds = null;
    }
  }

  bool IBlockEntityForward.OnBlockInteractStart(IPlayer byPlayer,
                                                BlockSelection blockSel,
                                                ref EnumHandling handled) {
    ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
    if (!hotbarSlot.Empty) {
      return TryPut(byPlayer, ref handled);
    } else {
      if (TryTake(byPlayer)) {
        handled = EnumHandling.PreventSubsequent;
        // Return true to sync the action with the server
        return true;
      }
    }
    // Let other behaviors handle the interaction.
    return true;
  }

  public Cuboidf[] GetCollisionBoxes(ref EnumHandling handled) {
    if (_inventoryBounds != null) {
      handled = EnumHandling.Handled;
    }
    return _inventoryBounds;
  }

  public Cuboidf[] GetSelectionBoxes(ref EnumHandling handled) {
    if (_inventoryBounds != null) {
      handled = EnumHandling.Handled;
    }
    return _inventoryBounds;
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    bool hasTerms = false;
    foreach (ItemSlot slot in _inventory) {
      if (slot.Empty) {
        continue;
      }
      Term t = slot.Itemstack.Collectible.GetBehavior<Term>();
      if (t != null) {
        if (!hasTerms) {
          dsc.Append(Lang.Get("Contents: "));
          hasTerms = true;
        } else {
          dsc.Append(' ');
        }
        dsc.Append(t.GetTerm(slot.Itemstack));
      }
    }
    if (hasTerms) {
      dsc.AppendLine();
    }
    string error = _termInfo?.ErrorMessage;
    if (error != null) {
      dsc.AppendLine(Term.Escape(error));
    }
  }

  void ICollectibleTarget.OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity,
                                            BlockSelection blockSel,
                                            EntitySelection entitySel,
                                            ref EnumHandHandling handHandling,
                                            ref EnumHandling handled) {
    Api.Logger.Notification("Got OnHeldAttackStart");

    handHandling = EnumHandHandling.PreventDefault;
    handled = EnumHandling.PreventSubsequent;

    if (_compilationRunning || _termInfo == null || _termInfo.Term == null) {
      // There's no easy way to prevent the attack animation from playing. This
      // method doesn't start the animation, but it will later get started by
      // EntityPlayer.HandleHandAnimations.
      //
      // Return with handHandling set to `EnumHandHandling.PreventDefault`. This
      // way the block won't get broken in creative mode. Although, the hammer's
      // nimationAuthoritative behavior would have set handHandling to that
      // anyway, if `handled` allowed it to run.
      return;
    }
    MarkToolAsHitting(slot, true);
    string anim =
        slot.Itemstack.Collectible.GetHeldTpHitAnimation(slot, byEntity);
    float framesound =
        CollectibleBehaviorAnimationAuthoritative.getSoundAtFrame(byEntity,
                                                                  anim);

    byEntity.AnimManager.RegisterFrameCallback(
        new AnimFrameCallback() { Animation = anim, Frame = framesound,
                                  Callback = () => OnHammerStrike(slot) });
  }

  private void OnHammerStrike(ItemSlot tool) {
    Api.Logger.Notification("Got hammer strike");
    if (GetToolIsHitting(tool) == true) {
      Api.Logger.Notification("Still hitting the jig");
      if (!_compilationRunning && _termInfo?.Term != null) {
        ++_hammerHits;
        int necessaryHits = Block.Attributes["hammerHits"].AsInt(2);
        if (_hammerHits >= necessaryHits) {
          CreateCombinedTerm();
        }
      }
    }
  }

  private void MarkToolAsHitting(ItemSlot tool, bool value) {
    tool.Itemstack.TempAttributes.SetBool($"hitting-{Block.Code}", value);
  }

  private bool GetToolIsHitting(ItemSlot tool) {
    if (tool.Itemstack == null) {
      return false;
    }
    return tool.Itemstack.TempAttributes.GetAsBool($"hitting-{Block.Code}",
                                                   false);
  }

  bool ICollectibleTarget.OnHeldAttackStep(BlockPos originalTarget,
                                           float secondsPassed, ItemSlot slot,
                                           EntityAgent byEntity,
                                           BlockSelection blockSelection,
                                           EntitySelection entitySel,
                                           ref EnumHandling handled) {
    handled = EnumHandling.PreventSubsequent;

    MarkToolAsHitting(slot,
                      blockSelection != null && blockSelection.Position == Pos);

    string animCode =
        slot.Itemstack.Collectible.GetHeldTpHitAnimation(slot, byEntity);
    return byEntity.AnimManager.IsAnimationActive(animCode);
  }

  bool ICollectibleTarget.OnHeldAttackCancel(
      BlockPos originalTarget, float secondsPassed, ItemSlot slot,
      EntityAgent byEntity, BlockSelection blockSelection,
      EntitySelection entitySel, EnumItemUseCancelReason cancelReason,
      ref EnumHandling handled) {
    Api.Logger.Debug("Got OnHeldAttackCancel");
    handled = EnumHandling.PreventSubsequent;
    if (cancelReason == EnumItemUseCancelReason.Death ||
        cancelReason == EnumItemUseCancelReason.Destroyed) {
      MarkToolAsHitting(slot, false);
      return true;
    } else {
      return false;
    }
  }
}
