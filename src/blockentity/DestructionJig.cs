using System;
using System.Text;

using Lambda.BlockBehavior;
using Lambda.CollectibleBehavior;
using Lambda.Token;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Lambda.BlockEntity;

public class DestructionJig : BlockEntityDisplay, IBlockEntityForward {
  // Pass in null for the API and inventory class name for now. The correct
  // values will be passed by `BlockEntityContainer.Initialize` when it calls
  // `Inventory.LateInitialize`.
  private readonly InventoryGeneric _inventory =
      new(2, null, null, (int slot, InventoryGeneric inv) => {
        if (slot == 0)
          return new ItemSlotLiquidOnly(inv, 10);
        else
          return new ItemSlotSurvival(inv);
      });
  public override InventoryBase Inventory => _inventory;
  private Cuboidf[] _inventoryBounds = null;
  private bool _compilationOutdated = true;
  TermInfo _termInfo = null;

  public override string InventoryClassName => "destructionjig";

  public override void Initialize(ICoreAPI api) {
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
      if (bounds.X1 < 5.001f / 16f || bounds.X1 > 11.001f / 16f ||
          bounds.Y1 < -0.001f / 16f || bounds.Y1 > 6.001f / 16f ||
          bounds.Z1 < 5.001f / 16f || bounds.Z1 > 11.001f / 16f) {
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
          dsc.Append("Contents: ");
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
}