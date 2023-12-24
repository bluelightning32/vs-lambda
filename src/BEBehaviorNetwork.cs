using System;
using System.Collections.Generic;
using System.Text;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LambdaFactory;

public class BEBehaviorNetwork : BlockEntityBehavior,
                                 IMeshGenerator,
                                 ITexPositionSource {
  private BlockNodeTemplate _template;
  private Node[] _nodes;

  public static string Name {
    get { return "Network"; }
  }

  private class NetworkNodeAccessor : NodeAccessor {
    private readonly IWorldAccessor _world;
    public NetworkNodeAccessor(IWorldAccessor world) { _world = world; }

    public override BlockNodeTemplate GetBlock(BlockPos pos, out Node[] nodes) {
      BEBehaviorNetwork behavior = _world.BlockAccessor.GetBlockEntity(pos)
                                       ?.GetBehavior<BEBehaviorNetwork>();
      if (behavior == null) {
        nodes = null;
        return null;
      }
      nodes = behavior._nodes;
      return behavior._template;
    }

    public override void SetNode(BlockPos pos, int nodeId, in Node node) {
      BlockEntity block = _world.BlockAccessor.GetBlockEntity(pos);
      BEBehaviorNetwork behavior = block?.GetBehavior<BEBehaviorNetwork>();
      bool redraw = behavior._nodes[nodeId].Scope != node.Scope;
      behavior._nodes[nodeId] = node;
      block.MarkDirty(redraw);
    }
  }

  private interface IGetNodeAccessor {
    public NodeAccessor Accessor { get; }
  }

  public class Manager : NetworkManager, IGetNodeAccessor {
    public bool SingleStep = false;

    private readonly IEventAPI _eventAPI;
    private bool _stepEnqueued = false;

    NodeAccessor IGetNodeAccessor.Accessor {
      get { return _accessor; }
    }

    public Manager(IWorldAccessor world)
        : base(world.Api.Side, world.Logger, new NetworkNodeAccessor(world)) {
      _eventAPI = world.Api.Event;
    }

    private void MaybeEnqueueStep() {
      if (!_stepEnqueued && !SingleStep) {
        _eventAPI.EnqueueMainThreadTask(() => {
          _stepEnqueued = false;
          if (!SingleStep) {
            Step();
            if (HasPendingWork) {
              MaybeEnqueueStep();
            }
          }
        }, "lambdanetwork");
        _stepEnqueued = true;
      }
    }

    public void ToggleSingleStep() {
      SingleStep = !SingleStep;
      // `MaybeEnqueueStep` checks that SingleStep is false.
      if (HasPendingWork) {
        MaybeEnqueueStep();
      }
    }

    public override void EnqueueNode(Node node, BlockPos pos, int nodeId) {
      base.EnqueueNode(node, pos, nodeId);
      MaybeEnqueueStep();
    }
  }

  public BEBehaviorNetwork(BlockEntity blockentity) : base(blockentity) {}

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    TreeArrayAttribute nodes = _template.ToTreeAttributes(_nodes);
    if (nodes != null) {
      tree["nodes"] = nodes;
    }
  }

  public static BlockNodeTemplate
  ParseBlockNodeTemplate(ICoreAPI api, JsonObject properties) {
    Dictionary<JsonObject, BlockNodeTemplate> cache =
        ObjectCacheUtil.GetOrCreate(
            api, $"lambdafactory-network-properties",
            () => new Dictionary<JsonObject, BlockNodeTemplate>());
    if (cache.TryGetValue(properties, out BlockNodeTemplate block)) {
      return block;
    }
    api.Logger.Notification(
        "lambda: Network properties cache miss. Dict has {0} entries.",
        cache.Count);
    BlockNodeTemplateLoading loading =
        properties.AsObject<BlockNodeTemplateLoading>();
    Manager manager =
        api.ModLoader.GetModSystem<LambdaFactoryModSystem>().NetworkManager;
    block = new BlockNodeTemplate(loading, ((IGetNodeAccessor)manager).Accessor,
                                  manager);

    cache.Add(properties, block);
    return block;
  }

  public override void
  FromTreeAttributes(ITreeAttribute tree,
                     IWorldAccessor worldAccessForResolve) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    // FromTreeAttributes is called before Initialize, so ParseNetworks needs to
    // be called before accessing _networks.
    _template = ParseBlockNodeTemplate(worldAccessForResolve.Api, properties);

    StringBuilder dsc = new StringBuilder();
    for (int i = 0; i < (_nodes?.Length ?? 0); ++i) {
      dsc.AppendLine($"oldnode[{i}] = {{ {_nodes[i].ToString()} }}");
    }

    if (_template.FromTreeAttributes(Pos, tree["nodes"] as TreeArrayAttribute,
                                     ref _nodes) &&
        Api != null) {
      // Only update the mesh here if the behavior was already initialized
      // (indicated by the non-null Api), and the template indicates that the
      // nodes changed significantly enough to require a mesh update.
      (Blockentity as BlockEntityCacheMesh)?.UpdateMesh();
      Blockentity.MarkDirty(true);
    }

    for (int i = 0; i < _nodes.Length; ++i) {
      dsc.AppendLine($"newnode[{i}] = {{ {_nodes[i].ToString()} }}");
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
      _template = ParseBlockNodeTemplate(Api, properties);
      _nodes = _template.CreateNodes(Pos);
    }
  }

  public override void OnBlockPlaced(ItemStack byItemStack = null) {
    base.OnBlockPlaced(byItemStack);
    _template.OnPlaced(Pos, _nodes);
  }

  public void GenerateMesh(ref MeshData mesh) {
    ((ICoreClientAPI)Api)
        .Tesselator.TesselateShape("network", Block.Code, Block.Shape, out mesh,
                                   this);
  }

  public object GetKey() {
    if (Scope.Min < 0 || (int)Scope.Max > 7) {
      throw new Exception("Scope range is too large for 3 bits.");
    }
    if (_nodes.Length > 64 / 3) {
      throw new Exception(
          $"Block {Block.Code} has more than the max supported networks.");
    }

    ulong key = 0;
    foreach (Node node in _nodes) {
      key <<= 3;
      key |= (ulong)node.Scope;
    }

    return key;
  }

  public object GetImmutableKey() { return GetKey(); }

  public Size2i AtlasSize {
    get {
      ITexPositionSource def =
          ((ICoreClientAPI)Api).Tesselator.GetTextureSource(Block);
      return def.AtlasSize;
    }
  }

  public TextureAtlasPosition this[string textureCode] {
    get {
      ICoreClientAPI capi = (ICoreClientAPI)Api;
      return _template.GetTexture(textureCode, capi, Block, _nodes) ??
             capi.Tesselator.GetTextureSource(Block)[textureCode];
    }
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    if ((Api as ICoreClientAPI)?.Settings.Bool["extendedDebugInfo"] ?? false) {
      for (int i = 0; i < _nodes.Length; ++i) {
        dsc.AppendLine($"node[{i}] = {{ {_nodes[i].ToString()} }}");
      }
    }
  }
}
