using System.Text;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace LambdaFactory;

public abstract class BEBehaviorAbstractNetwork : BlockEntityBehavior,
                                                  IMeshGenerator {
  protected BlockNodeTemplate _template;
  protected Node[] _nodes;

  protected class NetworkNodeAccessor : NodeAccessor {
    private readonly System
        .Func<BlockPos, BEBehaviorAbstractNetwork> _getBehavior;

    public NetworkNodeAccessor(
        System.Func<BlockPos, BEBehaviorAbstractNetwork> getBehavior) {
      _getBehavior = getBehavior;
    }

    public override BlockNodeTemplate GetBlock(BlockPos pos, out Node[] nodes) {
      BEBehaviorAbstractNetwork behavior = _getBehavior(pos);
      if (behavior == null) {
        nodes = null;
        return null;
      }
      nodes = behavior._nodes;
      return behavior._template;
    }

    public override void SetNode(BlockPos pos, int nodeId, in Node node) {
      BEBehaviorAbstractNetwork behavior = _getBehavior(pos);
      bool redraw = behavior._nodes[nodeId].Scope != node.Scope;
      behavior._nodes[nodeId] = node;
      behavior.MarkDirty(redraw);
    }
  }

  private void MarkDirty(bool redraw) { Blockentity.MarkDirty(redraw); }

  public BEBehaviorAbstractNetwork(BlockEntity blockentity)
      : base(blockentity) {}

  protected abstract string GetNetworkName();
  protected abstract AutoStepNetworkManager GetManager(ICoreAPI api);

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    TreeArrayAttribute nodes = _template.ToTreeAttributes(_nodes);
    if (nodes != null) {
      tree[GetNetworkName()] = nodes;
    }
  }

  public override void
  FromTreeAttributes(ITreeAttribute tree,
                     IWorldAccessor worldAccessForResolve) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    // FromTreeAttributes is called before Initialize, so ParseNetworks needs to
    // be called before accessing _template.
    _template = GetManager(worldAccessForResolve.Api)
                    .ParseBlockNodeTemplate(properties);

    StringBuilder dsc = new StringBuilder();
    for (int i = 0; i < (_nodes?.Length ?? 0); ++i) {
      dsc.AppendLine(
          $"old{GetNetworkName()}[{i}] = {{ {_nodes[i].ToString()} }}");
    }

    if (_template.FromTreeAttributes(
            Pos, tree[GetNetworkName()] as TreeArrayAttribute, ref _nodes) &&
        Api != null) {
      // Only update the mesh here if the behavior was already initialized
      // (indicated by the non-null Api), and the template indicates that the
      // nodes changed significantly enough to require a mesh update.
      (Blockentity as BlockEntityCacheMesh)?.UpdateMesh();
      Blockentity.MarkDirty(true);
    }

    for (int i = 0; i < _nodes.Length; ++i) {
      dsc.AppendLine(
          $"new{GetNetworkName()}[{i}] = {{ {_nodes[i].ToString()} }}");
    }
    worldAccessForResolve.Api.Logger.Debug(
        "FromTreeAttributes on {0} api set {1}: {2}",
        worldAccessForResolve.Api.Side, Api != null, dsc);
  }

  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);
    // _networks and _nodes may have already been initialized in
    // `FromTreeAttributes`. Reinitializing it would wipe out the _nodes
    // information.
    if (_template == null) {
      _template = GetManager(api).ParseBlockNodeTemplate(properties);
      _nodes = _template.CreateNodes(Pos);
    }
  }

  public override void OnBlockPlaced(ItemStack byItemStack = null) {
    base.OnBlockPlaced(byItemStack);
    _template.OnPlaced(Pos, _nodes);
  }

  public override void OnBlockRemoved() {
    base.OnBlockRemoved();
    _template.OnRemoved(Pos, _nodes);
  }

  public object GetKey() { return _template.GetTextureKey(_nodes); }

  public object GetImmutableKey() { return GetKey(); }

  public TextureAtlasPosition GetTexture(string textureCode) {
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
            $"{GetNetworkName()}[{i}] = {{ {_nodes[i].ToString()} }}");
      }
    }
  }
}
