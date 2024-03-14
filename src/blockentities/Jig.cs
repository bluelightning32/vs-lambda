using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Lambda.BlockBehaviors;
using Lambda.CollectibleBehaviors;
using Lambda.Token;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Lambda.BlockEntities;

abstract public class Jig : BlockEntityDisplay,
                            IBlockEntityForward,
                            IDropCraftListener,
                            ICollectibleTarget {
  protected enum TermState {
    Invalid,
    CompilationWaiting,
    CompilationRunning,
    CompilationDone,
  }

  protected Cuboidf[] _inventoryBounds = null;
  protected bool _compilationRunning = false;
  protected bool _dropWaiting = false;
  int _hammerHits = 0;
  // `_termState` applies to `_lastTerms`. This is used to check whether the
  // term currently in the inventory changed since the last state update.
  string[] _lastTerms = null;
  protected TermState _termState = TermState.Invalid;

  public override string InventoryClassName => "jig";

  private void OnSlotModified(int slot) {
    if (Api.Side == EnumAppSide.Server) {
      StartCompilation();
    } else {
      updateMeshes();
    }
  }

  public override void Initialize(ICoreAPI api) {
    Inventory.SlotModified += OnSlotModified;
    base.Initialize(api);
    // Set this so that right-click with a block in hand puts the block in the
    // inventory instead of trying to place it. Technically setting this isn't
    // necessary, because the container shouldn't accept blocks (only term
    // items), but this is set anyway for future proofing.
    Block.PlacedPriorityInteract = true;
    if (Api.Side == EnumAppSide.Client) {
      GetOrCreateErrorMesh();
    }
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetInt("termState", (int)_termState);
    if (_lastTerms != null) {
      tree["lastTerms"] = new StringArrayAttribute(_lastTerms);
    }
    tree.SetInt("hammerHits", _hammerHits);
  }

  protected abstract void LoadTermInfo(ITreeAttribute tree,
                                       IWorldAccessor worldForResolving);

  public override void FromTreeAttributes(ITreeAttribute tree,
                                          IWorldAccessor worldForResolving) {
    base.FromTreeAttributes(tree, worldForResolving);

    _termState = (TermState)tree.GetAsInt("termState", (int)TermState.Invalid);
    _lastTerms = (tree["lastTerms"] as StringArrayAttribute)?.value ??
                 Array.Empty<string>();
    _hammerHits = tree.GetInt("hammerHits", 0);
    LoadTermInfo(tree, worldForResolving);

    UpdateInventoryBounds();
    RedrawAfterReceivingTreeAttributes(worldForResolving);
  }

  protected Matrixf GetTransformationMatrix(float startY, float xSize,
                                            float ySize, float zSize,
                                            ItemSlot slot) {
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
    mat.Translate(0, startY, 0);
    // Term meshes are expected to be 8x6x8, centered at the bottom of the
    // cube. If the mesh is already within those bounds, don't translate it.
    // However, if it is outside of those bounds, translate its lowest corner
    // to (4,0,4).
    if (bounds.X1 < (0.4999f - xSize / 2) ||
        bounds.X1 > (0.5001f + xSize / 2) || bounds.Y1 < -0.001f ||
        bounds.Y1 > (ySize + 0.001f) || bounds.Z1 < (0.4999f - zSize / 2) ||
        bounds.Z1 > (0.5001f + zSize / 2)) {
      // 3. Move the mesh to bottom the center of the cube.
      mat.Translate(0.5f, 0, 0.5f);
      // 2. Uniformly shrink the mesh so that it fits in [(-4/16, 0, -4/16),
      //    (4/16, 6, 4/16)].
      float shrink = 1;
      if (bounds.XSize > xSize) {
        shrink = xSize / bounds.XSize;
      }
      if (bounds.YSize > ySize) {
        shrink = Math.Min(shrink, ySize / bounds.YSize);
      }
      if (bounds.ZSize > zSize) {
        shrink = Math.Min(shrink, zSize / bounds.ZSize);
      }
      if (shrink != 1) {
        mat.Scale(shrink, shrink, shrink);
      }
      // 1. Put the mesh on top of the XZ plane, centered at the XZ plane
      // origin.
      mat.Translate(-bounds.MidX, -bounds.Y1, -bounds.MidZ);
    }
    return mat;
  }

  protected MeshData CreateLiquidMesh(ICoreClientAPI capi,
                                      ITexPositionSource contentSource) {
    AssetLocation shapeLocation =
        AssetLocation
            .Create(Block.Attributes["liquidContentShapeLoc"].AsString(),
                    CoreSystem.Domain)
            .WithPathAppendixOnce(".json")
            .WithPathPrefixOnce("shapes/");
    Shape shape = Shape.TryGet(capi, shapeLocation);

    capi.Tesselator.TesselateShape("jig", shape, out MeshData contentMesh,
                                   contentSource);

    // Since this shape is a liquid, it needs its liquid flags set, otherwise
    // the tessellator will fail with a null reference exception. The liquid
    // flag constants are defined in assets/game/shaders/chunkliquid.vsh.
    contentMesh.CustomInts = new CustomMeshDataPartInt(
        contentMesh.FlagsCount) { Count = contentMesh.FlagsCount };
    // Use reduced waves and reduced foam for all of the liquid shape
    // vertices.
    contentMesh.CustomInts.Values.Fill((1 << 26) | (1 << 27));
    // I'm not sure what the floats do. Maybe they are flow vectors? Anyway,
    // the JsonTesselator needs 2 per vertex.
    contentMesh.CustomFloats = new CustomMeshDataPartFloat(
        contentMesh.FlagsCount * 2) { Count = contentMesh.FlagsCount * 2 };
    return contentMesh;
  }

  protected virtual MeshData GetOrCreateErrorMesh() { return null; }

  protected override MeshData getOrCreateMesh(ItemStack stack, int index) {
    if (stack?.Item.MatterState == EnumMatterState.Liquid) {
      string cacheKey = getMeshCacheKey(stack);
      if (MeshCache.TryGetValue(cacheKey, out MeshData result)) {
        return result;
      }

      ICoreClientAPI capi = Api as ICoreClientAPI;
      ITexPositionSource contentSource = BlockBarrel.getContentTexture(
          capi, stack, out float unusedFillHeight);
      MeshData contentMesh = CreateLiquidMesh(capi, contentSource);

      MeshCache[cacheKey] = contentMesh;
      return contentMesh;
    } else {
      return base.getOrCreateMesh(stack, index);
    }
  }

  private bool TryPut(IPlayer player, ref EnumHandling handled) {
    ItemSlot hotbarSlot = player.InventoryManager.ActiveHotbarSlot;
    if (hotbarSlot.Empty) {
      handled = EnumHandling.PassThrough;
      return false;
    }
    if (hotbarSlot.Itemstack.Collectible
            .GetBehavior<CollectibleBehaviors.Term>() == null) {
      (Api as ICoreClientAPI)
          ?.TriggerIngameError(this, "onlyterms", Lang.Get("lambda:onlyterms"));
      handled = EnumHandling.PreventSubsequent;
      return false;
    }
    for (int i = 0; i < Inventory.Count; ++i) {
      if (!Inventory[i].Empty) {
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
      if (hotbarSlot.TryPutInto(Api.World, Inventory[i], 1) > 0) {
        UpdateInventoryBounds();
        MarkDirty();
        handled = EnumHandling.PreventSubsequent;
        return true;
      }
    }
    handled = EnumHandling.PassThrough;
    return false;
  }

  protected virtual void ResetTermState() {
    _dropWaiting = false;
    _lastTerms = null;
    _termState = TermState.Invalid;
    _hammerHits = 0;
  }

  protected virtual bool GetCompilationTerms(out string[] terms,
                                             out string[][] imports) {
    terms = new string[Inventory.Count];
    imports = new string[Inventory.Count][];
    for (int i = 0; i < Inventory.Count; ++i) {
      Term termBhv = Inventory[i].Itemstack?.Collectible.GetBehavior<Term>();
      string term = termBhv?.GetTerm(Inventory[i].Itemstack);
      if (term == null) {
        return false;
      }
      terms[i] = term;
      imports[i] = termBhv?.GetImports(Inventory[i].Itemstack);
    }
    return true;
  }

  protected void StartCompilation() {
    if (!GetCompilationTerms(out string[] terms, out string[][] imports)) {
      ResetTermState();
      return;
    }

    // If the term changed, then reset the state.
    if (_termState == TermState.Invalid || _lastTerms == null ||
        !terms.SequenceEqual(_lastTerms)) {
      _termState = TermState.Invalid;
      _lastTerms = terms;
      _termState = TermState.CompilationWaiting;
    }

    if (_compilationRunning) {
      return;
    }

    if (_termState == TermState.CompilationWaiting) {
      _termState = TermState.CompilationRunning;
      _compilationRunning = true;
      Api.Logger.Notification("Starting compilation for jig {0}. Terms='{1}'",
                              Pos, string.Join(' ', terms));
      TyronThreadPool.QueueLongDurationTask(() => Compile(imports, terms),
                                            "lambda");
    }
  }

  abstract protected void Compile(string[][] imports, string[] terms);

  protected abstract Cuboidf GetItemBounds(int begin, int end);

  private bool TryTake(IPlayer player) {
    for (int i = Inventory.Count - 1; i >= 0; --i) {
      if (Inventory[i].Empty) {
        continue;
      }
      if (Inventory[i].Itemstack.Collectible.MatterState ==
          EnumMatterState.Liquid) {
        continue;
      }
      ItemStack removed = Inventory[i].TakeOutWhole();
      if (!player.InventoryManager.TryGiveItemstack(removed)) {
        Api.World.SpawnItemEntity(removed, Pos.ToVec3d().Add(0.5, 0.5, 0.5),
                                  null);
      }
      UpdateInventoryBounds();
      MarkDirty();
      return true;
    }
    return false;
  }

  protected abstract void UpdateInventoryBounds();

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

  public static string GetInContainerName(CollectibleObject obj) {
    return Lang.Get(
        $"{obj.Code.Domain}:incontainer-{obj.ItemClass.Name()}-{obj.Code.Path}");
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    bool hasContents = false;
    StringBuilder termString = new();
    foreach (ItemSlot slot in Inventory) {
      if (slot.Empty) {
        continue;
      }
      if (slot.Itemstack.Collectible.MatterState == EnumMatterState.Liquid) {
        if (!hasContents) {
          hasContents = true;
          dsc.AppendLine(Lang.Get("Contents:"));
        }
        WaterTightContainableProps props =
            BlockLiquidContainerBase.GetContainableProps(slot.Itemstack);
        float fill = slot.StackSize / props.ItemsPerLitre;
        string incontainerrname =
            GetInContainerName(slot.Itemstack.Collectible);
        dsc.AppendLine(Lang.Get("{0} litres of {1}", fill, incontainerrname));
        slot.Itemstack.Collectible.AppendPerishableInfoText(slot, dsc,
                                                            Api.World);
      } else {
        Term t = slot.Itemstack.Collectible.GetBehavior<Term>();
        if (t != null) {
          if (termString.Length != 0) {
            termString.Append(' ');
          }
          termString.Append(t.GetTerm(slot.Itemstack));
        }
      }
    }
    if (termString.Length != 0) {
      if (!hasContents) {
        hasContents = true;
        dsc.AppendLine(Lang.Get("Contents:"));
      }
      dsc.AppendLine(termString.ToString());
    }
    if (!hasContents) {
      dsc.AppendLine(Lang.Get("Empty"));
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

  protected virtual MeshData GetMesh(int slot, ItemStack stack) {
    return getMesh(stack);
  }

  // Override BlockEntityDisplay.OnTesselation so that the error mesh is shown
  // instead of the liquid mesh when there is an error.
  public override bool OnTesselation(ITerrainMeshPool mesher,
                                     ITesselatorAPI tessThreadTesselator) {
    for (int i = 0; i < Inventory.Count; i++) {
      ItemStack stack = Inventory[i].Itemstack;
      if (stack != null) {
        mesher.AddMeshData(GetMesh(0, stack), tfMatrices[i]);
      }
    }

    bool result = false;
    for (int i = 0; i < Behaviors.Count; i++) {
      result |= Behaviors[i].OnTesselation(mesher, tessThreadTesselator);
    }
    return result;
  }

  protected virtual bool CanCraftItems() {
    if (_termState == TermState.Invalid) {
      return false;
    }
    if (_termState == TermState.CompilationRunning ||
        _termState == TermState.CompilationWaiting) {
      _dropWaiting = true;
      return false;
    }
    return true;
  }

  protected abstract void CraftItems();

  void IDropCraftListener.OnDropCraft(IWorldAccessor world, BlockPos pos,
                                      BlockPos dropper) {
    if (CanCraftItems() && Api.Side == EnumAppSide.Server) {
      CraftItems();
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

    if (!CanCraftItems()) {
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
                                  Callback = () =>
                                      OnHammerStrike(byEntity, slot) });
  }

  private void OnHammerStrike(EntityAgent byEntity, ItemSlot tool) {
    Api.Logger.Notification("Got hammer strike");
    if (GetToolIsHitting(tool) == true) {
      Api.Logger.Notification("Still hitting the jig");

      Api.World.PlaySoundAt(
          new AssetLocation("game:sounds/effect/anvilmergehit"), Pos.X + 0.5f,
          Pos.Y + 0.5f, Pos.Z + 0.5f, (byEntity as EntityPlayer)?.Player, true,
          16, 0.35f);

      if (CanCraftItems()) {
        ++_hammerHits;
        int necessaryHits = Block.Attributes["hammerHits"].AsInt(2);
        if (_hammerHits >= necessaryHits && Api.Side == EnumAppSide.Server) {
          CraftItems();
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
