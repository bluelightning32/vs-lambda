using System.Collections.Generic;

using Lambda.BlockBehavior;
using Lambda.Network;

using Newtonsoft.Json;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.BlockEntityBehavior;
using VSBlockEntity = Vintagestory.API.Common.BlockEntity;

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

public class Wire : TermNetwork, IBlockEntityForward, IConnectable {
  private WireTemplate _WireTemplate = null;
  private int _directions = 0;
  private Cuboidf[] _selectionBoxes = null;

  public Wire(VSBlockEntity blockentity) : base(blockentity) {}

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    _WireTemplate = ParseTemplate(api, properties);
    _selectionBoxes = GetSelectionBoxes(api, _directions);
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetInt("directions", _directions);
  }

  public override void OnBlockPlaced(ItemStack byItemStack) {
    // Update `_template` before calling `base.OnBlockPlaced`, because it
    // accesses `_template`.
    if (byItemStack != null) {
      _directions = byItemStack.Attributes.GetAsInt("directions", _directions);
    }
    _selectionBoxes = GetSelectionBoxes(Api, _directions);
    _template = ParseBlockNodeTemplate(Api.World, properties);
    base.OnBlockPlaced(byItemStack);
  }

  public override void
  FromTreeAttributes(ITreeAttribute tree,
                     IWorldAccessor worldAccessForResolve) {
    // Set `_directions` before calling `base.FromTreeAttributes`, because it
    // accesses `_directions` in the process of setting `_template`.
    _directions = tree.GetInt("directions", _directions);
    base.FromTreeAttributes(tree, worldAccessForResolve);
    // No need to update the selection boxes here. Initialize will be called
    // before the block is rendered.
  }

  private WireTemplate ParseTemplate(ICoreAPI api, JsonObject properties) {
    if (api.Side == EnumAppSide.Server)
      return null;
    Dictionary<JsonObject, WireTemplate> cache = ObjectCacheUtil.GetOrCreate(
        api, $"lambda-wire-mesh",
        () => new Dictionary<JsonObject, WireTemplate>());
    if (cache.TryGetValue(properties, out WireTemplate template)) {
      return template;
    }
    template = properties.AsObject<WireTemplate>(null, Block.Code.Domain);
    cache.Add(properties, template);
    return template;
  }

  protected override BlockNodeTemplate
  ParseBlockNodeTemplate(IWorldAccessor world, JsonObject properties) {
    return GetManager(world.Api).ParseWireTemplate(properties, _directions);
  }

  private static Cuboidf[] GetSelectionBoxes(ICoreAPI api, int directions) {
    Dictionary<int, Cuboidf[]> cache =
        ObjectCacheUtil.GetOrCreate(api, "lambda-wire-collisionSelectionBoxes",
                                    () => new Dictionary<int, Cuboidf[]>());
    if (cache.TryGetValue(directions, out Cuboidf[] selectionBoxes)) {
      return selectionBoxes;
    }

    List<Cuboidf> boxes = new();
    Cuboidf center = new Cuboidf(6.5f / 16, 6.5f / 16, 6.5f / 16, 9.5f / 16,
                                 9.5f / 16, 9.5f / 16);
    boxes.Add(center);
    for (int i = 0; i < 6; ++i) {
      if ((directions & (1 << i)) != 0) {
        // Start with the center cube.
        Cuboidf side = new Cuboidf(6.5f / 16, 6.5f / 16, 6.5f / 16, 9.5f / 16,
                                   9.5f / 16, 9.5f / 16);
        // Expand the cuboid in the direction that the wire segment faces. After
        // this the cuboid will contain both the wire segment and the center.
        side.Expand(BlockFacing.ALLFACES[i], 6.5f / 16);
        // Now remove the center so that the cuboid contains just the wire
        // segment.
        side.Expand(BlockFacing.ALLFACES[i].Opposite, -3f / 16);
        boxes.Add(side);
      }
    }

    return cache[directions] = boxes.ToArray();
  }

  public override void EditMesh(MeshData mesh) {
    if (_WireTemplate.IndexedOverrides.TryGetValue(_directions,
                                                   out ShapeOverride over)) {
      mesh.Clear();
      mesh.AddMeshData(
          Blockentity.GetBehavior<CacheMesh>()?.TessellateShape(over.Shape));
      return;
    }
    for (int i = 0; i < 6; ++i) {
      if ((_directions & (1 << i)) != 0 &&
          _WireTemplate.IndexedShapes[i] != null) {
        mesh.AddMeshData(Blockentity.GetBehavior<CacheMesh>()?.TessellateShape(
            _WireTemplate.IndexedShapes[i]));
      }
    }
    base.EditMesh(mesh);
  }

  public Cuboidf[] GetSelectionBoxes(ref EnumHandling handled) {
    handled = EnumHandling.PreventSubsequent;
    return _selectionBoxes;
  }

  public Cuboidf[] GetCollisionBoxes(ref EnumHandling handled) {
    handled = EnumHandling.PreventSubsequent;
    return _selectionBoxes;
  }

  public override object GetKey() {
    return _directions + ((_nodes[0].Source.IsSet() ? 1 : 0) << 6);
  }

  public override object GetImmutableKey() { return GetKey(); }

  public bool CanAddEdge(Edge edge, out NodePos source) {
    if (!edge.IsFaceCenter() || (_directions & edge.GetFace().Flag) != 0) {
      source = new();
      return false;
    }
    source = _nodes[0].Source;
    return true;
  }

  public void AddEdge(Edge edge) {
    _directions |= edge.GetFace().Flag;
    _selectionBoxes = GetSelectionBoxes(Api, _directions);
    _template = ParseBlockNodeTemplate(Api.World, properties);
    _template.OnNodeChanged(Pos, 0, ref _nodes[0]);
    Blockentity.GetBehavior<CacheMesh>()?.UpdateMesh();
  }

  Network.Manager IConnectable.GetManager(ICoreAPI api) {
    return GetManager(api);
  }

  public void RemoveEdge(Edge edge) {
    if ((_directions & edge.GetFace().Flag) == 0) {
      // The block doesn't currently have that edge.
      return;
    }
    _directions &= ~edge.GetFace().Flag;
    _selectionBoxes = GetSelectionBoxes(Api, _directions);
    _template = ParseBlockNodeTemplate(Api.World, properties);
    _template.OnNodeChanged(Pos, 0, ref _nodes[0]);
    Blockentity.GetBehavior<CacheMesh>()?.UpdateMesh();
  }
}
