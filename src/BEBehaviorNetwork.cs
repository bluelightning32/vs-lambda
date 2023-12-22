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
  private BlockNodeTemplate _networks;
  private Node[] _nodes;

  public static string Name {
    get { return "Network"; }
  }

  class NetworkNodeAccessor : NodeAccessor {
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
      return behavior._networks;
    }

    public override void SetNode(NodePos pos, in Node node) {
      BEBehaviorNetwork behavior =
          _world.BlockAccessor.GetBlockEntity(pos.Block)
              ?.GetBehavior<BEBehaviorNetwork>();
      behavior._nodes[pos.NodeId] = node;
    }
  }

  public class Manager {
    private readonly NetworkManager _networkManager;

    public bool SingleStep = false;

    public Manager(IWorldAccessor world) {
      _networkManager = new NetworkManager(new NetworkNodeAccessor(world));
    }

    public bool CanPlace(BlockNodeTemplate networks, BlockPos pos,
                         ref string failureCode) {
      return networks.CanPlace(_networkManager, pos, ref failureCode);
    }

    public void ToggleSingleStep() { SingleStep = !SingleStep; }

    public void Step() {}
  }

  public BEBehaviorNetwork(BlockEntity blockentity) : base(blockentity) {}

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    TreeArrayAttribute networks = _networks.ToTreeAttributes(_nodes);
    if (networks != null) {
      tree["networks"] = networks;
    }
  }

  public Node GetNode(bool scopeNetwork, Edge edge) {
    return _networks.GetNode(scopeNetwork, edge, _nodes);
  }

  public static BlockNodeTemplate
  ParseBlockNodeTemplate(ICoreAPI api, JsonObject properties) {
    Dictionary<JsonObject, BlockNodeTemplate> cache =
        ObjectCacheUtil.GetOrCreate(
            api, $"lambdafactory-network-properties",
            () => new Dictionary<JsonObject, BlockNodeTemplate>());
    if (cache.TryGetValue(properties, out BlockNodeTemplate networks)) {
      return networks;
    }
    api.Logger.Notification(
        "lambda: Network properties cache miss. Dict has {0} entries.",
        cache.Count);
    networks = properties.AsObject<BlockNodeTemplate>();
    cache.Add(properties, networks);
    return networks;
  }

  public override void
  FromTreeAttributes(ITreeAttribute tree,
                     IWorldAccessor worldAccessForResolve) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    // FromTreeAttributes is called before Initialize, so ParseNetworks needs to
    // be called before accessing _networks.
    _networks = ParseBlockNodeTemplate(worldAccessForResolve.Api, properties);
    _nodes = _networks.FromTreeAttributes(
        Pos, tree["networks"] as TreeArrayAttribute);
    // No need to update the mesh here. Initialize will be called before the
    // block is rendered.
  }

  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);
    // _networks and _nodes may have already been initialized in
    // `FromTreeAttributes`. Reinitializing it would wipe out the _nodes
    // information.
    if (_networks == null) {
      _networks = ParseBlockNodeTemplate(Api, properties);
      _nodes = _networks.CreateNodes(Pos);
    }
  }

  public override void OnBlockPlaced(ItemStack byItemStack = null) {
    base.OnBlockPlaced(byItemStack);
    _networks.Propagate(Api, Pos, _nodes);
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
      return _networks.GetTexture(textureCode, capi, Block, _nodes) ??
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
