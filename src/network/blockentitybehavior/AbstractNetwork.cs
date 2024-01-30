using System.Text;

using Lambda.BlockEntityBehavior;
using Lambda.CollectibleBehavior;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Lambda.Network.BlockEntityBehavior;

using VSBlockEntityBehavior = Vintagestory.API.Common.BlockEntityBehavior;
using VSBlockEntity = Vintagestory.API.Common.BlockEntity;

public abstract class AbstractNetwork : VSBlockEntityBehavior, IMeshGenerator {
  protected BlockNodeTemplate _template;
  protected Node[] _nodes;

  protected class NetworkNodeAccessor : NodeAccessor {
    private readonly System.Func<BlockPos, AbstractNetwork> _getBehavior;

    public NetworkNodeAccessor(
        System.Func<BlockPos, AbstractNetwork> getBehavior) {
      _getBehavior = getBehavior;
    }

    public override BlockNodeTemplate GetBlock(BlockPos pos, out Node[] nodes) {
      AbstractNetwork behavior = _getBehavior(pos);
      if (behavior == null) {
        nodes = null;
        return null;
      }
      nodes = behavior._nodes;
      return behavior._template;
    }

    public override void SetNode(BlockPos pos, int nodeId, in Node node) {
      AbstractNetwork behavior = _getBehavior(pos);
      bool redraw = behavior._nodes[nodeId].Scope != node.Scope;
      behavior._nodes[nodeId] = node;
      behavior.MarkDirty(redraw);
    }
  }

  private void MarkDirty(bool redraw) { Blockentity.MarkDirty(redraw); }

  public AbstractNetwork(VSBlockEntity blockentity) : base(blockentity) {}

  protected abstract AutoStepManager GetManager(ICoreAPI api);

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    TreeArrayAttribute nodes = _template.ToTreeAttributes(_nodes);
    if (nodes != null) {
      tree[_template.GetNetworkName()] = nodes;
    }
  }

  protected virtual BlockNodeTemplate
  ParseBlockNodeTemplate(IWorldAccessor world, JsonObject properties) {
    return GetManager(world.Api).ParseBlockNodeTemplate(properties, 0, 0);
  }

  public override void
  FromTreeAttributes(ITreeAttribute tree,
                     IWorldAccessor worldAccessForResolve) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    // FromTreeAttributes is called before Initialize, so ParseNetworks needs to
    // be called before accessing _template.
    _template = ParseBlockNodeTemplate(worldAccessForResolve, properties);

    StringBuilder dsc = new StringBuilder();
    for (int i = 0; i < (_nodes?.Length ?? 0); ++i) {
      dsc.AppendLine(
          $"old{_template.GetNetworkName()}[{i}] = {{ {_nodes[i].ToString()} }}");
    }

    if (_template.FromTreeAttributes(
            Pos, tree[_template.GetNetworkName()] as TreeArrayAttribute,
            ref _nodes) &&
        Api != null) {
      // Only update the mesh here if the behavior was already initialized
      // (indicated by the non-null Api), and the template indicates that the
      // nodes changed significantly enough to require a mesh update.
      Blockentity.GetBehavior<CacheMesh>()?.UpdateMesh();
      Blockentity.MarkDirty(true);
    }

    for (int i = 0; i < _nodes.Length; ++i) {
      dsc.AppendLine(
          $"new{_template.GetNetworkName()}[{i}] = {{ {_nodes[i].ToString()} }}");
    }
  }

  public virtual void Initialize(ICoreAPI api) {
    Api = api;
    // _networks and _nodes may have already been initialized in
    // `FromTreeAttributes`. Reinitializing it would wipe out the _nodes
    // information.
    if (_template == null) {
      _template = ParseBlockNodeTemplate(api.World, properties);
      _nodes = _template.CreateNodes(Pos);
    }
  }

  public override void OnBlockPlaced(ItemStack byItemStack = null) {
    base.OnBlockPlaced(byItemStack);
    if (_template.OnPlaced(Pos, _nodes)) {
      Blockentity.GetBehavior<CacheMesh>()?.UpdateMesh();
    }
  }

  public override void OnBlockRemoved() {
    base.OnBlockRemoved();
    _template.OnRemoved(Pos, _nodes);
  }

  public virtual object GetKey() { return _template.GetTextureKey(_nodes); }

  public virtual object GetImmutableKey() { return GetKey(); }

  public virtual TextureAtlasPosition GetTexture(string textureCode) {
    ICoreClientAPI capi = (ICoreClientAPI)Api;
    return _template.GetTexture(textureCode, capi, Block, _nodes);
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    if ((Api as ICoreClientAPI)?.Settings.Bool["extendedDebugInfo"] ?? false) {
      for (int i = 0; i < _nodes.Length; ++i) {
        if (i == 13) {
          dsc.AppendLine($"...");
          break;
        }
        dsc.AppendLine(
            $"[{i}] = {{network={_template.GetNodeTemplate(i).Network.GetCode()}, {Term.Escape(_nodes[i].ToString())} }}");
      }
    }
  }

  public virtual void EditMesh(MeshData mesh) {}
}
