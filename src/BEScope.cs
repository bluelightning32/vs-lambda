using System;
using System.Collections.Generic;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LambdaFactory;

public enum Scope { Function = 0, Case = 1, Forall = 2, Matchin = 3 }

public class ScopeCacheKey : IEquatable<ScopeCacheKey> {
  public int PortedSides = 0;
  public int ScopeFace = -1;
  public Scope Scope;

  public bool Equals(ScopeCacheKey other) {
    if (other == null) {
      return false;
    }
    if (PortedSides != other.PortedSides) {
      return false;
    }
    if (ScopeFace != other.ScopeFace) {
      return false;
    }
    if (Scope != other.Scope) {
      return false;
    }
    return true;
  }

  public override int GetHashCode() {
    return HashCode.Combine(PortedSides, ScopeFace, Scope);
  }

  public override bool Equals(object obj) {
    return Equals(obj as ScopeCacheKey);
  }
}

public class BlockEntityScope<Key> : BlockEntity, IBlockEntityForward
    where Key : ScopeCacheKey, new() {
  private MeshData _mesh;
  protected Key _key = new Key();

  public override void Initialize(ICoreAPI api) {
    api.Logger.Notification($"lambda: Initialize {GetHashCode()}");
    base.Initialize(api);
    UpdateMesh();
  }

  static private Dictionary<Key, MeshData> GetMeshCache(ICoreAPI api,
                                                        Block block) {
    return ObjectCacheUtil.GetOrCreate(api,
                                       $"lambdafactory-bescope-{block.Code}",
                                       () => new Dictionary<Key, MeshData>());
  }

  protected virtual void UpdateKey() {
    Block[] decors = Api.World.BlockAccessor.GetDecors(Pos);
    _key.PortedSides = 0;
    for (int i = 0; i < BlockFacing.ALLFACES.Length; ++i) {
      if (decors[i] != null) {
        _key.PortedSides |= 1 << i;
      }
    }
  }

  private void UpdateMesh() {
    if (Api.Side == EnumAppSide.Server)
      return;
    UpdateKey();
    Dictionary<Key, MeshData> cache = GetMeshCache(Api, Block);
    if (cache.TryGetValue(_key, out _mesh)) {
      return;
    }
    _mesh = cache[_key] = GenerateMesh(_key);
  }

  protected virtual MeshData GenerateMesh(ScopeCacheKey key) {
    MeshData mesh =
        ((ICoreClientAPI)Api).TesselatorManager.GetDefaultBlockMesh(Block);
    return CutPortHoles(key.PortedSides, mesh);
  }

  public MeshData CutPortHoles(int sides, MeshData mesh) {
    if (mesh.VerticesPerFace != 4 || mesh.IndicesPerFace != 6) {
      throw new Exception("Unexpected VerticesPerFace or IndicesPerFace");
    }
    if (sides == 0) {
      return mesh;
    }
    Cuboidf faceBounds = new Cuboidf();
    int origFaceCount = mesh.VerticesCount / mesh.VerticesPerFace;
    MeshData copy = mesh.Clone();
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

  public override bool OnTesselation(ITerrainMeshPool mesher,
                                     ITesselatorAPI tessThreadTesselator) {
    Api.Logger.Notification($"lambda: OnTesselation {GetHashCode()}");
    mesher.AddMeshData(_mesh);
    return true;
  }

  public override void OnBlockPlaced(ItemStack byItemStack = null) {
    Api.Logger.Notification($"lambda: OnBlockPlaced {GetHashCode()}");
    base.OnBlockPlaced(byItemStack);
  }

  public override void OnExchanged(Block block) {
    Api.Logger.Notification($"lambda: OnExchanged {GetHashCode()}");
    base.OnExchanged(block);
  }

  void IBlockEntityForward.OnNeighbourBlockChange(
      Vintagestory.API.MathTools.BlockPos neibpos,
      ref Vintagestory.API.Common.EnumHandling handling) {
    UpdateMesh();
  }
}
