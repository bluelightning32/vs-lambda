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
  public ICloneable GetKey();
  public void GenerateMesh(ref MeshData mesh);

  public bool UpdatedPickedStack(ItemStack stack);
}

public class BlockEntityCacheMesh : BlockEntity, IBlockEntityForward {
  private MeshData _mesh;

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    UpdateMesh();
  }

  public override void OnBlockPlaced(ItemStack byItemStack) {
    base.OnBlockPlaced(byItemStack);
    UpdateMesh();
  }

  static private Dictionary<List<ICloneable>, MeshData>
  GetMeshCache(ICoreAPI api, Block block) {
    return ObjectCacheUtil.GetOrCreate(
        api, $"lambdafactory-mesh-{block.Code}",
        () => new Dictionary<List<ICloneable>, MeshData>(
            new ListEqualityComparer<ICloneable>()));
  }

  private List<ICloneable> GetKey() {
    List<ICloneable> result = new List<ICloneable>();
    foreach (var behavior in Behaviors) {
      IMeshGenerator generator = behavior as IMeshGenerator;
      if (generator == null) {
        continue;
      }
      result.Add(generator.GetKey());
    }
    return result;
  }

  public void UpdateMesh() {
    if (Api.Side == EnumAppSide.Server)
      return;
    List<ICloneable> key = GetKey();
    Dictionary<List<ICloneable>, MeshData> cache = GetMeshCache(Api, Block);
    if (cache.TryGetValue(key, out _mesh)) {
      return;
    }
    Api.Logger.Notification(
        "lambda: Cache miss for {0} {1}. Dict has {2} entries.", Block.Code,
        ListEqualityComparer<ICloneable>.GetString(key), cache.Count);

    _mesh = null;
    GenerateMesh(ref _mesh);
    List<ICloneable> cloned =
        key.Select(item => (ICloneable)item.Clone()).ToList();
    cache[cloned] = _mesh;
  }

  protected virtual void GenerateMesh(ref MeshData mesh) {
    foreach (var behavior in Behaviors) {
      (behavior as IMeshGenerator)?.GenerateMesh(ref mesh);
    }
  }

  public override bool OnTesselation(ITerrainMeshPool mesher,
                                     ITesselatorAPI tessThreadTesselator) {
    if (_mesh == null) {
      return false;
    }
    mesher.AddMeshData(_mesh);
    return true;
  }

  public virtual bool UpdatedPickedStack(ItemStack stack) {
    bool useResult = false;
    foreach (var behavior in Behaviors) {
      useResult |=
          (behavior as IMeshGenerator)?.UpdatedPickedStack(stack) ?? false;
    }
    return useResult;
  }

  public ItemStack OnPickBlock(ref EnumHandling handling) {
    ItemStack stack = new ItemStack(Block, 1);
    if (UpdatedPickedStack(stack)) {
      handling = EnumHandling.PreventDefault;
    }
    return stack;
  }
}
