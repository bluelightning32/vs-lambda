using System;
using System.Collections.Generic;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LambdaFactory;

public class BEBehaviorPortHole : BlockEntityBehavior,
                                  IBlockEntityForward,
                                  IMeshGenerator {
  private int _portedSides = 0;

  public BEBehaviorPortHole(BlockEntity blockentity) : base(blockentity) {}

  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);
    SetKey();
  }

  public override void OnBlockPlaced(ItemStack byItemStack) {
    base.OnBlockPlaced(byItemStack);
    SetKey();
  }

  protected virtual void SetKey() {
    Block[] decors = Api.World.BlockAccessor.GetDecors(Pos);
    _portedSides = 0;
    for (int i = 0; i < BlockFacing.ALLFACES.Length; ++i) {
      if (decors[i] != null) {
        _portedSides |= 1 << i;
      }
    }
  }

  public void GenerateMesh(ref MeshData mesh) {
    bool blockDefault = false;
    if (mesh == null) {
      mesh = ((ICoreClientAPI)Api).TesselatorManager.GetDefaultBlockMesh(Block);
      if (mesh == null) {
        return;
      }
      blockDefault = true;
    }
    mesh = CutPortHoles(_portedSides, mesh, !blockDefault);
  }

  public MeshData CutPortHoles(int sides, MeshData mesh, bool inPlace) {
    if (mesh.VerticesPerFace != 4 || mesh.IndicesPerFace != 6) {
      throw new Exception("Unexpected VerticesPerFace or IndicesPerFace");
    }
    if (sides == 0) {
      return mesh;
    }
    Cuboidf faceBounds = new Cuboidf();
    int origFaceCount = mesh.VerticesCount / mesh.VerticesPerFace;
    MeshData copy = mesh;
    if (!inPlace) {
      copy = copy.Clone();
    }
    for (int face = 0; face < origFaceCount; face++) {
      MeshUtil.GetFaceBounds(faceBounds, mesh.xyz, face * mesh.VerticesPerFace,
                             (face + 1) * mesh.VerticesPerFace);
      faceBounds.OmniGrowBy(0.001f);
      for (int i = 0; i < 6; ++i) {
        if ((sides & (1 << i)) == 0) {
          continue;
        }
        BlockFacing facing = BlockFacing.ALLFACES[i];
        if (faceBounds[(int)facing.Axis + 3] - faceBounds[(int)facing.Axis] <
                0.1f &&
            faceBounds.Contains(facing.PlaneCenter.X, facing.PlaneCenter.Y,
                                facing.PlaneCenter.Z)) {
          MeshUtil.AddFaceHole(copy, facing.Axis, face, facing);
        }
      }
    }
    return copy;
  }

  void IBlockEntityForward.OnNeighbourBlockChange(
      Vintagestory.API.MathTools.BlockPos neibpos,
      ref Vintagestory.API.Common.EnumHandling handling) {
    int oldPortedSides = _portedSides;
    SetKey();
    if (oldPortedSides != _portedSides) {
      (Blockentity as BlockEntityCacheMesh)?.UpdateMesh();
    }
  }

  public object GetKey() { return _portedSides; }

  public object GetClonedKey() { return _portedSides; }
}
