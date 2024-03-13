using System;
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

public class DestructionJig : BlockEntityDisplay, IBlockEntityForward {
  // Pass in null for the API and inventory class name for now. The correct
  // values will be passed by `BlockEntityContainer.Initialize` when it calls
  // `Inventory.LateInitialize`.
  private readonly InventoryGeneric _inventory =
      new(2, null, null, (int slot, InventoryGeneric inv) => {
        if (slot == 0)
          return new ItemSlotLiquidOnly(inv, 9999);
        else
          return new ItemSlotSurvival(inv);
      });
  public override InventoryBase Inventory => _inventory;
  private Cuboidf[] _inventoryBounds = null;
  private bool _compilationOutdated = true;
  TermInfo _termInfo = null;

  public override string InventoryClassName => "destructionjig";

  public override void Initialize(ICoreAPI api) {
    // Set CapacityLitres before calling the base, because the base calls back
    // into the DestructionJig to initialize the meshes, and the DestructionJig
    // mesh generator expects the capacity to be initialized.
    if (Block.Attributes?["capacityLitres"].Exists == true) {
      ((ItemSlotLiquidOnly)_inventory[0]).CapacityLitres =
          Block.Attributes["capacityLitres"].AsFloat();
    }
    base.Initialize(api);
    // Set this so that right-click with a block in hand puts the block in the
    // inventory instead of trying to place it. Technically setting this isn't
    // necessary, because the container shouldn't accept blocks (only term
    // items), but this is set anyway for future proofing.
    Block.PlacedPriorityInteract = true;
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    if (!_compilationOutdated) {
      _termInfo?.ToTreeAttributes(tree);
    }
  }

  public override void FromTreeAttributes(ITreeAttribute tree,
                                          IWorldAccessor worldForResolving) {
    base.FromTreeAttributes(tree, worldForResolving);

    _termInfo ??= new();
    _termInfo.FromTreeAttributes(tree);
    _compilationOutdated =
        _termInfo.ErrorMessage == null && _termInfo.Term == null;
    if (_compilationOutdated) {
      _termInfo = null;
    }

    SetInventoryBounds(!_inventory[1].Empty);
    RedrawAfterReceivingTreeAttributes(worldForResolving);
  }

  private Matrixf GetTransformationMatrix(float startY, ItemSlot slot) {
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
    if (bounds.X1 < 3.999f / 16f || bounds.X1 > 12.001f / 16f ||
        bounds.Y1 < -0.001f / 16f || bounds.Y1 > 6.001f / 16f ||
        bounds.Z1 < 3.999f / 16f || bounds.Z1 > 12.001f / 16f) {
      float xzSize = Math.Max(bounds.XSize, bounds.ZSize);
      // 3. Move the mesh to bottom the center of the cube.
      mat.Translate(0.5f, 0, 0.5f);
      // 2. Uniformly shrink the mesh so that it fits in [(-4/16, 0, -4/16),
      //    (4/16, 6, 4/16)].
      float shrink = 1;
      if (xzSize > 8f / 16f) {
        shrink = (8f / 16f) / xzSize;
      }
      if (bounds.YSize > 6f / 16f) {
        shrink = Math.Min(shrink, (6f / 16f) / bounds.YSize);
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

  protected override MeshData getOrCreateMesh(ItemStack stack, int index) {
    if (Inventory[index] is ItemSlotLiquidOnly slot) {
      string cacheKey = getMeshCacheKey(stack);
      if (MeshCache.TryGetValue(cacheKey, out MeshData result)) {
        return result;
      }

      ICoreClientAPI capi = Api as ICoreClientAPI;
      ITexPositionSource contentSource = BlockBarrel.getContentTexture(
          capi, stack, out float unusedFillHeight);
      AssetLocation shapeLocation =
          AssetLocation
              .Create(Block.Attributes["liquidContentShapeLoc"].AsString(),
                      CoreSystem.Domain)
              .WithPathAppendixOnce(".json")
              .WithPathPrefixOnce("shapes/");
      Shape shape = Shape.TryGet(capi, shapeLocation);

      capi.Tesselator.TesselateShape("distructionjig", shape,
                                     out MeshData contentMesh, contentSource);

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

      MeshCache[cacheKey] = contentMesh;
      return contentMesh;
    } else {
      return base.getOrCreateMesh(stack, index);
    }
  }

  protected override float[][] genTransformationMatrices() {
    float[][] matrices = new float [_inventory.Count][];
    float fill = 0;
    float capacity = 1;
    ItemSlot liquidSlot = Inventory[0];
    if (!liquidSlot.Empty) {
      WaterTightContainableProps props =
          BlockLiquidContainerBase.GetContainableProps(liquidSlot.Itemstack);
      fill = liquidSlot.StackSize / props.ItemsPerLitre;
      capacity = ((ItemSlotLiquidOnly)liquidSlot).CapacityLitres;
    }
    const float liquidContainerHeight = 2f / 16f;
    const float liquidContainerBase = 0.5f + 6 / 16f;
    Matrixf liquidMat = GetTransformationMatrix(
        liquidContainerBase + liquidContainerHeight * fill / capacity,
        liquidSlot);
    matrices[0] = liquidMat.Values;
    for (int i = 1; i < _inventory.Count; ++i) {
      ItemSlot slot = Inventory[i];
      Matrixf mat = GetTransformationMatrix(0.5f + i * 6f / 16f, slot);
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
            .GetBehavior<CollectibleBehaviors.Term>() == null) {
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
        SetInventoryBounds(true);
        MarkDirty();
        handled = EnumHandling.PreventSubsequent;
        MarkTermInfoDirty();
        return true;
      }
    }
    handled = EnumHandling.PassThrough;
    return false;
  }

  private void MarkTermInfoDirty() {
    _compilationOutdated = true;
    _termInfo = null;
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
      if (_inventory[i].Itemstack.Collectible.MatterState ==
          EnumMatterState.Liquid) {
        continue;
      }
      ItemStack removed = _inventory[i].TakeOutWhole();
      if (!player.InventoryManager.TryGiveItemstack(removed)) {
        Api.World.SpawnItemEntity(removed, Pos.ToVec3d().Add(0.5, 0.5, 0.5),
                                  null);
      }
      SetInventoryBounds(false);
      MarkTermInfoDirty();
      MarkDirty();
      return true;
    }
    return false;
  }

  private void SetInventoryBounds(bool hasItem) {
    if (hasItem) {
      _inventoryBounds = new Cuboidf[1] { GetItemBounds(1, 2) };
    } else {
      _inventoryBounds = null;
    }
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

  public static string GetInContainerName(CollectibleObject obj) {
    return Lang.Get(
        $"{obj.Code.Domain}:incontainer-{obj.ItemClass.Name()}-{obj.Code.Path}");
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    bool hasContents = false;
    ItemSlot liquidSlot = Inventory[0];
    if (!liquidSlot.Empty) {
      hasContents = true;
      dsc.AppendLine(Lang.Get("Contents:"));
      WaterTightContainableProps props =
          BlockLiquidContainerBase.GetContainableProps(liquidSlot.Itemstack);
      float fill = liquidSlot.StackSize / props.ItemsPerLitre;
      string incontainerrname =
          GetInContainerName(liquidSlot.Itemstack.Collectible);
      dsc.AppendLine(Lang.Get("{0} litres of {1}", fill, incontainerrname));
      liquidSlot.Itemstack.Collectible.AppendPerishableInfoText(liquidSlot, dsc,
                                                                Api.World);
    }
    if (!_inventory[1].Empty) {
      Term t = _inventory[1].Itemstack.Collectible.GetBehavior<Term>();
      if (t != null) {
        if (!hasContents) {
          dsc.AppendLine(Lang.Get("Contents:"));
          hasContents = true;
        }
        dsc.AppendLine(t.GetTerm(_inventory[1].Itemstack));
      }
    }
    if (!hasContents) {
      dsc.AppendLine(Lang.Get("Empty"));
    }
    string error = _termInfo?.ErrorMessage;
    if (error != null) {
      dsc.AppendLine(Term.Escape(error));
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

  public override bool OnTesselation(ITerrainMeshPool mesher,
                                     ITesselatorAPI tessThreadTesselator) {
    return base.OnTesselation(mesher, tessThreadTesselator);
  }
}
