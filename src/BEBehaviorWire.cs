using System;
using System.Collections.Generic;

using Newtonsoft.Json;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LambdaFactory;

public class ShapeOverride {
  public HashSet<string> Directions;
  public CompositeShape Shape;
}

[JsonObject(MemberSerialization.OptIn)]
public class WireTemplate {
  [JsonProperty]
  public Dictionary<string, CompositeShape> DirectionShapes;
  public CompositeShape[] IndexedShapes = new CompositeShape[6];
  [JsonProperty]
  public ShapeOverride[] Overrides;
  public Dictionary<int, ShapeOverride> IndexedOverrides = new();

  public WireTemplate(Dictionary<string, CompositeShape> directionShapes,
                      ShapeOverride[] overrides) {
    DirectionShapes = directionShapes ?? new();
    foreach (var shape in DirectionShapes) {
      IndexedShapes[BlockFacing.FromCode(shape.Key).Index] = shape.Value;
    }
    Overrides = overrides;
    foreach (var over in overrides) {
      int dirs = 0;
      foreach (var dir in over.Directions) {
        dirs |= BlockFacing.FromCode(dir).Flag;
      }
      IndexedOverrides[dirs] = over;
    }
  }
}

public class BEBehaviorWire : BlockEntityBehavior,
                              IMeshGenerator,
                              IBlockEntityForward {
  private WireTemplate _template = null;
  private int _directions = 63;
  private Cuboidf[] _selectionBoxes = null;

  public BEBehaviorWire(BlockEntity blockentity) : base(blockentity) {}

  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);
    _template = ParseTemplate(api, properties);
    _selectionBoxes = GetSelectionBoxes(api, _directions);
  }

  public ItemStack OnPickBlock(ref EnumHandling handling) {
    ItemStack stack = new ItemStack(Block, 1);
    stack.Attributes.SetInt("directions", _directions);

    handling = EnumHandling.PreventDefault;
    return stack;
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetInt("directions", _directions);
  }

  public override void OnBlockPlaced(ItemStack byItemStack) {
    base.OnBlockPlaced(byItemStack);
    _directions = byItemStack.Attributes.GetAsInt("directions", _directions);
    _selectionBoxes = GetSelectionBoxes(Api, _directions);
  }

  public override void
  FromTreeAttributes(ITreeAttribute tree,
                     IWorldAccessor worldAccessForResolve) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    _directions = tree.GetInt("directions", _directions);
    // No need to update the selection boxes here. Initialize will be called
    // before the block is rendered.
  }

  private WireTemplate ParseTemplate(ICoreAPI api, JsonObject properties) {
    if (api.Side == EnumAppSide.Server)
      return null;
    Dictionary<JsonObject, WireTemplate> cache = ObjectCacheUtil.GetOrCreate(
        api, $"lambdafactory-wire-mesh",
        () => new Dictionary<JsonObject, WireTemplate>());
    if (cache.TryGetValue(properties, out WireTemplate template)) {
      return template;
    }
    template = properties.AsObject<WireTemplate>(null, Block.Code.Domain);
    cache.Add(properties, template);
    return template;
  }

  private static Cuboidf[] GetSelectionBoxes(ICoreAPI api, int directions) {
    Dictionary<int, Cuboidf[]> cache = ObjectCacheUtil.GetOrCreate(
        api, "lambdafactory-wire-collisionSelectionBoxes",
        () => new Dictionary<int, Cuboidf[]>());
    if (cache.TryGetValue(directions, out Cuboidf[] selectionBoxes)) {
      return selectionBoxes;
    }

    List<Cuboidf> boxes = new();
    Cuboidf center = new Cuboidf(6.5f / 16, 6.5f / 16, 6.5f / 16, 9.5f / 16,
                                 9.5f / 16, 9.5f / 16);
    int remaining = directions;
    for (int i = 0; i < BlockFacing.indexUP; ++i) {
      int j = BlockFacing.ALLFACES[i].Opposite.Index;
      if ((remaining & (1 << i)) != 0 && (remaining & (1 << j)) != 0) {
        center.Expand(BlockFacing.ALLFACES[i], 6.5f / 16);
        center.Expand(BlockFacing.ALLFACES[j], 6.5f / 16);
        boxes.Add(center);
        remaining &= ~((1 << i) | (1 << j));
        center = null;
        break;
      }
    }
    for (int i = 0; i < 6; ++i) {
      if ((remaining & (1 << i)) != 0) {
        if (center != null) {
          center.Expand(BlockFacing.ALLFACES[i], 6.5f / 16);
          boxes.Add(center);
          center = null;
        } else {
          Cuboidf side = new Cuboidf(6.5f / 16, 6.5f / 16, 6.5f / 16, 9.5f / 16,
                                     9.5f / 16, 9.5f / 16);
          side.Expand(BlockFacing.ALLFACES[i], 6.5f / 16);
          side.Expand(BlockFacing.ALLFACES[i].Opposite, -3f / 16);
          boxes.Add(side);
        }
      }
    }
    if (center != null) {
      boxes.Add(center);
    }

    return cache[directions] = boxes.ToArray();
  }

  public void EditMesh(MeshData mesh) {
    if (_template.IndexedOverrides.TryGetValue(_directions,
                                               out ShapeOverride over)) {
      mesh.Clear();
      mesh.AddMeshData(
          (Blockentity as BlockEntityCacheMesh)?.TessellateShape(over.Shape));
      return;
    }
    for (int i = 0; i < 6; ++i) {
      if ((_directions & (1 << i)) != 0 && _template.IndexedShapes[i] != null) {
        mesh.AddMeshData((Blockentity as BlockEntityCacheMesh)
                             ?.TessellateShape(_template.IndexedShapes[i]));
      }
    }
  }

  public Cuboidf[] GetSelectionBoxes(ref EnumHandling handled) {
    handled = EnumHandling.PreventSubsequent;
    return _selectionBoxes;
  }

  public Cuboidf[] GetCollisionBoxes(ref EnumHandling handled) {
    handled = EnumHandling.PreventSubsequent;
    return _selectionBoxes;
  }

  public object GetKey() { return _directions; }

  public object GetImmutableKey() { return _directions; }
}