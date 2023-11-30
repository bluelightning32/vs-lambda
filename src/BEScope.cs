using System;
using System.Collections.Generic;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LambdaFactory;

public enum Scope { Function = 0, Case = 1, Forall = 2, Matchin = 3 }

public static class ScopeHelper {
  public static string GetCode(Scope scope) {
    switch (scope) {
    case Scope.Function:
      return "function";
    case Scope.Case:
      return "case";
    case Scope.Forall:
      return "forall";
    case Scope.Matchin:
      return "matchin";
    default:
      return "unknown";
    }
  }
}

public class ScopeCacheKey : IEquatable<ScopeCacheKey> {
  public int PortedSides = 0;
  public int ScopeFace = -1;
  public Scope Scope = Scope.Function;

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

  public override string ToString() {
    return $"{PortedSides} {ScopeFace} {Scope}";
  }
}

public class BlockEntityScope<Key> : BlockEntity, IBlockEntityForward
    where Key : ScopeCacheKey, new() {
  private MeshData _mesh;
  protected Key _key = new Key();

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    UpdateMesh();
  }

  public ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos,
                               ref EnumHandling handling) {
    ItemStack stack = new ItemStack(Block, 1);
    stack.Attributes.SetInt("ScopeFace", _key.ScopeFace);

    handling = EnumHandling.PreventDefault;
    return stack;
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    Console.WriteLine("lambda: ToTreeAttributes {0}", tree.ToJsonToken());
    base.ToTreeAttributes(tree);
    tree.SetInt("ScopeFace", _key.ScopeFace);
  }

  public override void OnBlockPlaced(ItemStack byItemStack) {
    base.OnBlockPlaced(byItemStack);
    _key.ScopeFace =
        byItemStack.Attributes.GetAsInt("ScopeFace", _key.ScopeFace);
    UpdateMesh();
    Api.Logger.Notification("lambda: OnBlockPlaced {0} - {1} - {2}",
                            GetHashCode(), _key.ScopeFace,
                            byItemStack.Attributes.ToJsonToken());
  }

  public override void
  FromTreeAttributes(ITreeAttribute tree,
                     IWorldAccessor worldAccessForResolve) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    _key.ScopeFace = tree.GetInt("ScopeFace", _key.ScopeFace);
    worldAccessForResolve.Logger.Notification(
        "lambda: FromTreeAttributes {0} - {1}", GetHashCode(), _key.ScopeFace);
    // No need to update the mesh here. Initialize will be called before the
    // block is rendered.
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
    Api.Logger.Notification("lambda: GenerateMesh {0} {1}", Block.Code, key);
    MeshData original =
        ((ICoreClientAPI)Api).TesselatorManager.GetDefaultBlockMesh(Block);
    if (original == null) {
      return original;
    }
    MeshData mesh = original;

    mesh = ColorScopeFace(key.ScopeFace, key.Scope, mesh,
                          !Object.ReferenceEquals(mesh, original));
    mesh = ColorScopeTopEdge(key.ScopeFace, key.Scope, mesh,
                             !Object.ReferenceEquals(mesh, original));
    mesh = CutPortHoles(key.PortedSides, mesh,
                        !Object.ReferenceEquals(mesh, original));
    return mesh;
  }

  public MeshData ColorScopeFace(int scopeFace, Scope scope, MeshData mesh,
                                 bool copied) {
    if (scopeFace == -1) {
      return mesh;
    }
    MeshData copy = mesh;
    if (!copied) {
      copy = copy.Clone();
    }
    ICoreClientAPI capi = (ICoreClientAPI)Api;
    BlockFacing facing = BlockFacing.ALLFACES[scopeFace];
    ITextureAtlasAPI atlas = capi.BlockTextureAtlas;
    TextureAtlasPosition original =
        atlas
            .Positions[Block.TexturesInventory[facing.Code].Baked.TextureSubId];
    CompositeTexture compositeReplacement =
        Block.TexturesInventory[facing.Code].Clone();
    Array.Resize(ref compositeReplacement.BlendedOverlays,
                 (compositeReplacement.BlendedOverlays?.Length ?? 0) + 1);
    BlendedOverlayTexture scopeBlend = new BlendedOverlayTexture();
    scopeBlend.Base =
        new AssetLocation(LambdaFactoryModSystem.Domain,
                          $"scope/{ScopeHelper.GetCode(scope)}/full");
    scopeBlend.BlendMode = EnumColorBlendMode.ColorBurn;
    compositeReplacement
        .BlendedOverlays[compositeReplacement.BlendedOverlays.Length - 1] =
        scopeBlend;
    compositeReplacement.Bake(capi.Assets);
    atlas.GetOrInsertTexture(
        compositeReplacement.Baked.BakedName, out int replacementId,
        out TextureAtlasPosition replacement,
        () => atlas.LoadCompositeBitmap(compositeReplacement.Baked.BakedName));

    MeshUtil.ReplaceTexture(copy, facing, 2.1f / 16, original, replacement);
    return copy;
  }

  public MeshData CutPortHoles(int sides, MeshData mesh, bool copied) {
    if (mesh.VerticesPerFace != 4 || mesh.IndicesPerFace != 6) {
      throw new Exception("Unexpected VerticesPerFace or IndicesPerFace");
    }
    if (sides == 0) {
      return mesh;
    }
    Cuboidf faceBounds = new Cuboidf();
    int origFaceCount = mesh.VerticesCount / mesh.VerticesPerFace;
    MeshData copy = mesh;
    if (!copied) {
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

  public MeshData ColorScopeTopEdge(int scopeFace, Scope scope, MeshData mesh,
                                    bool copied) {
    if (scopeFace == -1) {
      return mesh;
    }
    if (!BlockFacing.ALLFACES[scopeFace].IsHorizontal) {
      return mesh;
    }
    MeshData copy = mesh;
    if (!copied) {
      copy = copy.Clone();
    }

    ICoreClientAPI capi = (ICoreClientAPI)Api;
    ITextureAtlasAPI atlas = capi.BlockTextureAtlas;
    TextureAtlasPosition original =
        atlas.Positions[Block.TexturesInventory["up"].Baked.TextureSubId];
    CompositeTexture compositeReplacement =
        Block.TexturesInventory["up"].Clone();
    Array.Resize(ref compositeReplacement.BlendedOverlays,
                 (compositeReplacement.BlendedOverlays?.Length ?? 0) + 1);
    BlendedOverlayTexture scopeBlend = new BlendedOverlayTexture();
    scopeBlend.Base =
        new AssetLocation(LambdaFactoryModSystem.Domain,
                          $"scope/{ScopeHelper.GetCode(scope)}/full");
    scopeBlend.BlendMode = EnumColorBlendMode.ColorBurn;
    compositeReplacement
        .BlendedOverlays[compositeReplacement.BlendedOverlays.Length - 1] =
        scopeBlend;
    compositeReplacement.Bake(capi.Assets);
    atlas.GetOrInsertTexture(
        compositeReplacement.Baked.BakedName, out int replacementId,
        out TextureAtlasPosition replacement,
        () => atlas.LoadCompositeBitmap(compositeReplacement.Baked.BakedName));

    BlockFacing facing = BlockFacing.ALLFACES[scopeFace];
    Cuboidf bounds =
        new Cuboidf(-0.1f, 1.0f - 2.1f / 16, -0.1f, 1.1f, 1.1f, 1.1f);
    bounds[(int)facing.Axis] = facing.PlaneCenter[(int)facing.Axis] - 1f / 16;
    bounds[(int)facing.Axis + 3] =
        facing.PlaneCenter[(int)facing.Axis] + 1f / 16;

    MeshUtil.ReplaceTextureInBounds(copy, BlockFacing.UP, bounds, original,
                                    replacement);

    return copy;
  }

  public override bool OnTesselation(ITerrainMeshPool mesher,
                                     ITesselatorAPI tessThreadTesselator) {
    if (_mesh == null) {
      return false;
    }
    mesher.AddMeshData(_mesh);
    return true;
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
