using System;
using System.Collections.Generic;
using System.Linq;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LambdaFactory;

interface IMeshGenerator {
  public object GetKey();
  public object GetImmutableKey() { return ((ICloneable)GetKey()).Clone(); }
  public void EditMesh(MeshData mesh) {}

  public TextureAtlasPosition GetTexture(string textureCode) { return null; }
}

public class CacheMeshTextureSource : ITexPositionSource {
  private readonly BlockEntityCacheMesh _cache;
  private readonly ITexPositionSource _def;

  public CacheMeshTextureSource(BlockEntityCacheMesh cache,
                                ITexPositionSource def) {
    _cache = cache;
    _def = def;
  }

  public TextureAtlasPosition this[string textureCode] {
    get { return _cache.GetTexture(textureCode) ?? _def[textureCode]; }
  }

  public Size2i AtlasSize => _def.AtlasSize;
}

public class BlockEntityCacheMesh : BlockEntity {
  private MeshData _mesh;

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    UpdateMesh();
  }

  public override void OnBlockPlaced(ItemStack byItemStack) {
    base.OnBlockPlaced(byItemStack);
    UpdateMesh();
  }

  static private Dictionary<List<object>, MeshData> GetMeshCache(ICoreAPI api,
                                                                 Block block) {
    return ObjectCacheUtil.GetOrCreate(
        api, $"lambdafactory-mesh-{block.Code}",
        () => new Dictionary<List<object>, MeshData>(
            new ListEqualityComparer<object>()));
  }

  private List<object> GetKey() {
    List<object> result = new List<object>();
    foreach (var behavior in Behaviors) {
      IMeshGenerator generator = behavior as IMeshGenerator;
      if (generator == null) {
        continue;
      }
      result.Add(generator.GetKey());
    }
    return result;
  }

  private List<object> GetClonedKey() {
    List<object> result = new List<object>();
    foreach (var behavior in Behaviors) {
      IMeshGenerator generator = behavior as IMeshGenerator;
      if (generator == null) {
        continue;
      }
      result.Add(generator.GetImmutableKey());
    }
    return result;
  }

  public MeshData TessellateShape(CompositeShape shape) {
    ((ICoreClientAPI)Api)
        .Tesselator.TesselateShape(
            "cachemesh", Block.Code, shape, out MeshData mesh,
            new CacheMeshTextureSource(
                this,
                ((ICoreClientAPI)Api).Tesselator.GetTextureSource(Block)));
    return mesh;
  }

  public void UpdateMesh() {
    if (Api.Side == EnumAppSide.Server)
      return;
    List<object> key = GetKey();
    Dictionary<List<object>, MeshData> cache = GetMeshCache(Api, Block);
    if (cache.TryGetValue(key, out _mesh)) {
      return;
    }
    Api.Logger.Notification(
        "lambda: Cache miss for {0} {1}. Dict has {2} entries.", Block.Code,
        ListEqualityComparer<object>.GetString(key), cache.Count);

    _mesh = TessellateShape(Block.Shape);
    EditMesh(_mesh);
    List<object> cloned = GetClonedKey();
    cache[cloned] = _mesh;
  }

  public virtual void EditMesh(MeshData mesh) {
    foreach (var behavior in Behaviors) {
      (behavior as IMeshGenerator)?.EditMesh(mesh);
    }
  }

  public virtual TextureAtlasPosition GetTexture(string textureCode) {
    foreach (var behavior in Behaviors) {
      TextureAtlasPosition result =
          (behavior as IMeshGenerator)?.GetTexture(textureCode);
      if (result != null) {
        return result;
      }
    }
    return null;
  }

  public override bool OnTesselation(ITerrainMeshPool mesher,
                                     ITesselatorAPI tessThreadTesselator) {
    if (_mesh == null) {
      return false;
    }
    mesher.AddMeshData(_mesh);
    return true;
  }
}
