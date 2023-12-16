using System;
using System.Collections.Generic;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LambdaFactory;

public class ScopeCacheKey : IEquatable<ScopeCacheKey>, ICloneable {
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

  public object Clone() { return MemberwiseClone(); }
}

public class BlockEntityScope<Key> : BlockEntity, IBlockEntityForward
    where Key : ScopeCacheKey, IEquatable<Key>, new() {
  private MeshData _mesh;
  protected Key _key = new Key();
  bool _fixedScope = false;

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    string scope = Block.Attributes?["scope"].AsString("any");
    if (scope == null || scope == "any") {
      _fixedScope = false;
    } else {
      _fixedScope = true;
      _key.Scope = ScopeExtension.FromCode(scope);
    }

    UpdateMesh();
  }

  public ItemStack OnPickBlock(ref EnumHandling handling) {
    ItemStack stack = new ItemStack(Block, 1);
    stack.Attributes.SetInt("ScopeFace", _key.ScopeFace);
    if (!_fixedScope) {
      stack.Attributes.SetInt("Scope", (int)_key.Scope);
    }

    handling = EnumHandling.PreventDefault;
    return stack;
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetInt("ScopeFace", _key.ScopeFace);
    if (!_fixedScope) {
      tree.SetInt("Scope", (int)_key.Scope);
    }
  }

  public override void OnBlockPlaced(ItemStack byItemStack) {
    base.OnBlockPlaced(byItemStack);
    _key.ScopeFace =
        byItemStack.Attributes.GetAsInt("ScopeFace", _key.ScopeFace);
    if (!_fixedScope) {
      _key.Scope =
          (Scope)byItemStack.Attributes.GetAsInt("Scope", (int)_key.Scope);
    }

    UpdateMesh();
  }

  public override void
  FromTreeAttributes(ITreeAttribute tree,
                     IWorldAccessor worldAccessForResolve) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    _key.ScopeFace = tree.GetInt("ScopeFace", _key.ScopeFace);
    if (!_fixedScope) {
      _key.Scope = (Scope)tree.GetInt("Scope", (int)_key.Scope);
    }
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
    Api.Logger.Notification(
        "lambda: Cache miss for {0} {1}. Dict has {2} entries.", Block.Code,
        _key, cache.Count);

    _mesh = cache[(Key)_key.Clone()] = GenerateMesh(_key);
  }

  protected virtual MeshData GenerateMesh(ScopeCacheKey key) {
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
    scopeBlend.Base = new AssetLocation(LambdaFactoryModSystem.Domain,
                                        $"scope/{scope.GetCode()}/full");
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
    scopeBlend.Base = new AssetLocation(LambdaFactoryModSystem.Domain,
                                        $"scope/{scope.GetCode()}/full");
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
