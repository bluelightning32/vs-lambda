using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;

using LambdaFactory;

using Newtonsoft.Json;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LambdaFactory;

public enum Edge {
  [EnumMember(Value = "north-right")] NorthRight = 0,
  [EnumMember(Value = "north-up")] NorthUp = 1,
  [EnumMember(Value = "north-left")] NorthLeft = 2,
  [EnumMember(Value = "north-down")] NorthDown = 3,
  [EnumMember(Value = "north-center")] NorthCenter = 4,
  [EnumMember(Value = "east-right")] EastRight = 5,
  [EnumMember(Value = "east-up")] EastUp = 6,
  [EnumMember(Value = "east-left")] EastLeft = 7,
  [EnumMember(Value = "east-down")] EastDown = 8,
  [EnumMember(Value = "east-center")] EastCenter = 9,
  [EnumMember(Value = "south-right")] SouthRight = 10,
  [EnumMember(Value = "south-up")] SouthUp = 11,
  [EnumMember(Value = "south-left")] SouthLeft = 12,
  [EnumMember(Value = "south-down")] SouthDown = 13,
  [EnumMember(Value = "south-center")] SouthCenter = 14,
  [EnumMember(Value = "west-right")] WestRight = 15,
  [EnumMember(Value = "west-up")] WestUp = 16,
  [EnumMember(Value = "west-left")] WestLeft = 17,
  [EnumMember(Value = "west-down")] WestDown = 18,
  [EnumMember(Value = "west-center")] WestCenter = 19,
  [EnumMember(Value = "up-right")] UpRight = 20,
  [EnumMember(Value = "up-up")] UpUp = 21,
  [EnumMember(Value = "up-left")] UpLeft = 22,
  [EnumMember(Value = "up-down")] UpDown = 23,
  [EnumMember(Value = "up-center")] UpCenter = 24,
  [EnumMember(Value = "down-right")] DownRight = 25,
  [EnumMember(Value = "down-up")] DownUp = 26,
  [EnumMember(Value = "down-left")] DownLeft = 27,
  [EnumMember(Value = "down-down")] DownDown = 28,
  [EnumMember(Value = "down-center")] DownCenter = 29,
  [EnumMember(Value = "source")] Source = 30,
  [EnumMember(Value = "unknown")] Unknown = 100,
}

static class EdgeExtension {
  public static BlockFacing GetFace(this Edge edge) {
    return edge switch {
      Edge.NorthRight or Edge.NorthUp or Edge.NorthLeft or
          Edge.NorthDown or Edge.NorthCenter => BlockFacing.NORTH,
      Edge.EastRight or Edge.EastUp or Edge.EastLeft or Edge.EastDown or
          Edge.EastCenter => BlockFacing.EAST,
      Edge.SouthRight or Edge.SouthUp or Edge.SouthLeft or
          Edge.SouthDown or Edge.SouthCenter => BlockFacing.SOUTH,
      Edge.WestRight or Edge.WestUp or Edge.WestLeft or Edge.WestDown or
          Edge.WestCenter => BlockFacing.WEST,
      Edge.UpRight or Edge.UpUp or Edge.UpLeft or Edge.UpDown or
          Edge.UpCenter => BlockFacing.UP,
      Edge.DownRight or Edge.DownUp or Edge.DownLeft or Edge.DownDown or
          Edge.DownCenter => BlockFacing.DOWN,
      _ => null
    };
  }

  public static Edge GetOpposite(this Edge edge) {
    return edge switch { Edge.NorthRight => Edge.SouthLeft,
                         Edge.EastRight => Edge.WestLeft,
                         Edge.SouthRight => Edge.NorthLeft,
                         Edge.WestRight => Edge.EastLeft,

                         Edge.NorthLeft => Edge.SouthRight,
                         Edge.EastLeft => Edge.WestRight,
                         Edge.SouthLeft => Edge.NorthRight,
                         Edge.WestLeft => Edge.EastRight,

                         Edge.NorthUp => Edge.SouthUp,
                         Edge.EastUp => Edge.WestUp,
                         Edge.SouthUp => Edge.NorthUp,
                         Edge.WestUp => Edge.EastUp,

                         Edge.NorthDown => Edge.SouthDown,
                         Edge.EastDown => Edge.WestDown,
                         Edge.SouthDown => Edge.NorthDown,
                         Edge.WestDown => Edge.EastDown,

                         Edge.UpUp => Edge.DownUp,
                         Edge.UpDown => Edge.DownDown,
                         Edge.UpLeft => Edge.DownRight,
                         Edge.UpRight => Edge.DownLeft,

                         Edge.DownUp => Edge.UpUp,
                         Edge.DownDown => Edge.UpDown,
                         Edge.DownLeft => Edge.UpRight,
                         Edge.DownRight => Edge.UpLeft,

                         Edge.NorthCenter => Edge.SouthCenter,
                         Edge.EastCenter => Edge.WestCenter,
                         Edge.SouthCenter => Edge.NorthCenter,
                         Edge.WestCenter => Edge.EastCenter,
                         Edge.UpCenter => Edge.DownCenter,
                         Edge.DownCenter => Edge.UpCenter,

                         _ => Edge.Unknown };
  }
}

[JsonObject(MemberSerialization.OptIn)]
public class NodeTemplate {
  public int Id = 0;
  public bool Source = false;
  [JsonProperty]
  public Scope SourceScope = Scope.Function;
  [JsonProperty]
  public Edge[] Edges = Array.Empty<Edge>();
  [JsonProperty]
  public string[] Textures = Array.Empty<string>();

  public NodeTemplate() {}

  public Scope GetScope(Node[] nodes) {
    if (Source) {
      return SourceScope;
    } else {
      return nodes[Id].Scope;
    }
  }

  public void Propagate(ICoreAPI api, BlockPos pos, bool scopeNetwork,
                        ref Node node) {
    foreach (Edge edge in Edges) {
      BlockFacing face = edge.GetFace();
      if (face == null) {
        continue;
      }
      BlockEntity neighborEntity =
          api.World.BlockAccessor.GetBlockEntity(pos.AddCopy(face));
      if (neighborEntity == null) {
        continue;
      }
      BEBehaviorNetwork neighbor =
          neighborEntity.GetBehavior<BEBehaviorNetwork>();
      if (neighbor != null) {
        Scope neighborScope =
            neighbor.GetNode(scopeNetwork, edge.GetOpposite()).Scope;
        if (neighborScope != Scope.None) {
          node.Scope = neighborScope;
        }
      }
    }
  }
}

[JsonArray]
public class BlockNodeCategory {
  public readonly NodeTemplate[] Networks;
  public readonly Dictionary<Edge, NodeTemplate> Index =
      new Dictionary<Edge, NodeTemplate>();

  public BlockNodeCategory(int firstId, NodeTemplate[] networks) {
    Networks = networks;
    foreach (var network in networks) {
      network.Id = firstId++;
      foreach (var edge in network.Edges) {
        if (edge == Edge.Source) {
          network.Source = true;
        }
        if (edge != Edge.Unknown) {
          Index.Add(edge, network);
        }
      }
    }
  }

  public void Propagate(ICoreAPI api, BlockPos pos, bool scopeNetwork,
                        Node[] nodes) {
    foreach (var network in Networks) {
      network.Propagate(api, pos, scopeNetwork, ref nodes[network.Id]);
    }
  }
}

[JsonObject(MemberSerialization.OptIn)]
public class BlockNodeTemplate {
  [JsonProperty]
  private readonly BlockNodeCategory _scope;
  [JsonProperty]
  private readonly BlockNodeCategory _match;

  private readonly Dictionary<string, NodeTemplate> _textures =
      new Dictionary<string, NodeTemplate>();

  [JsonConstructor]
  public BlockNodeTemplate(NodeTemplate[] scope, NodeTemplate[] match) {
    _scope = new BlockNodeCategory(0, scope ?? Array.Empty<NodeTemplate>());
    _match = new BlockNodeCategory(_scope.Networks.Length,
                                   match ?? Array.Empty<NodeTemplate>());
    foreach (NodeTemplate network in _scope.Networks) {
      foreach (string texture in network.Textures) {
        _textures.Add(texture, network);
      }
    }
    foreach (NodeTemplate network in _match.Networks) {
      foreach (string texture in network.Textures) {
        _textures.Add(texture, network);
      }
    }
  }

  public TreeArrayAttribute ToTreeAttributes(Node[] nodes) {
    List<TreeAttribute> info = new List<TreeAttribute>(Count);
    foreach (var network in _scope.Networks) {
      info.Add(nodes[network.Id].ToTreeAttributes());
    }
    foreach (var network in _match.Networks) {
      info.Add(nodes[network.Id].ToTreeAttributes());
    }
    if (info.Count == 0) {
      return null;
    }

    return new TreeArrayAttribute(info.ToArray());
  }

  public int Count {
    get { return _scope.Networks.Length + _match.Networks.Length; }
  }

  public Node[] FromTreeAttributes(TreeArrayAttribute info) {
    Node[] nodes = new Node[Count];
    if (info == null) {
      SetSourceScope(nodes);
      return nodes;
    }
    int index = 0;
    foreach (var network in _scope.Networks) {
      nodes[network.Id] = Node.FromTreeAttributes(info.value[index++]);
    }
    foreach (var network in _match.Networks) {
      nodes[network.Id] = Node.FromTreeAttributes(info.value[index++]);
    }
    return nodes;
  }

  public TextureAtlasPosition GetTexture(string name, ICoreClientAPI capi,
                                         Block block, Node[] nodes) {
    if (!_textures.TryGetValue(name, out NodeTemplate network)) {
      return null;
    }
    CompositeTexture composite;
    if (!block.Textures.TryGetValue(name, out composite)) {
      return null;
    }
    Scope scope = network.GetScope(nodes);
    if (scope != Scope.None) {
      composite = composite.Clone();
      BlendedOverlayTexture scopeBlend = new BlendedOverlayTexture();
      scopeBlend.Base =
          new AssetLocation(LambdaFactoryModSystem.Domain,
                            $"scope/{ScopeExtension.GetCode(scope)}");
      scopeBlend.BlendMode = EnumColorBlendMode.ColorBurn;
      composite.BlendedOverlays =
          composite.BlendedOverlays?.Append(scopeBlend) ??
          new BlendedOverlayTexture[] { scopeBlend };
    }
    composite.Bake(capi.Assets);
    ITextureAtlasAPI atlas = capi.BlockTextureAtlas;
    atlas.GetOrInsertTexture(
        composite.Baked.BakedName, out int id, out TextureAtlasPosition tex,
        () => atlas.LoadCompositeBitmap(composite.Baked.BakedName));
    return tex;
  }

  private void SetSourceScope(Node[] nodes) {
    foreach (var network in _scope.Networks) {
      if (network.Source) {
        nodes[network.Id].Scope = network.SourceScope;
      }
    }
    foreach (var network in _match.Networks) {
      if (network.Source) {
        nodes[network.Id].Scope = network.SourceScope;
      }
    }
  }

  public Node[] CreateNodes() {
    Node[] nodes = new Node[Count];
    SetSourceScope(nodes);
    return nodes;
  }

  public Node GetNode(bool scopeNetwork, Edge edge, Node[] nodes) {
    BlockNodeCategory network = scopeNetwork ? _scope : _match;
    if (!network.Index.TryGetValue(edge, out NodeTemplate n)) {
      return new Node();
    }
    return nodes[n.Id];
  }

  public void Propagate(ICoreAPI api, BlockPos pos, Node[] nodes) {
    _scope.Propagate(api, pos, true, nodes);
    _match.Propagate(api, pos, false, nodes);
  }
}

public struct Node {
  public Scope Scope = Scope.None;

  public Node() {}

  public TreeAttribute ToTreeAttributes() {
    TreeAttribute tree = new TreeAttribute();
    tree.SetInt("scope", (int)Scope);
    return tree;
  }

  public static Node FromTreeAttributes(TreeAttribute tree) {
    Node node = new Node();
    node.Scope = (Scope)tree.GetAsInt("scope");
    return node;
  }
}

public class BEBehaviorNetwork : BlockEntityBehavior,
                                 IMeshGenerator,
                                 ITexPositionSource {
  private BlockNodeTemplate _networks;
  private Node[] _nodes;

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

  private void ParseNetworks(JsonObject properties, ICoreAPI api) {
    Dictionary<JsonObject, BlockNodeTemplate> cache =
        ObjectCacheUtil.GetOrCreate(
            api, $"lambdafactory-networks-{Block.Code}",
            () => new Dictionary<JsonObject, BlockNodeTemplate>());
    if (cache.TryGetValue(properties, out _networks)) {
      return;
    }
    api.Logger.Notification(
        "lambda: Properties cache miss for {0}. Dict has {1} entries.",
        Block.Code, cache.Count);
    _networks = properties.AsObject<BlockNodeTemplate>();
    cache.Add(properties, _networks);
  }

  public override void
  FromTreeAttributes(ITreeAttribute tree,
                     IWorldAccessor worldAccessForResolve) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    // FromTreeAttributes is called before Initialize, so ParseNetworks needs to
    // be called before accessing _networks.
    ParseNetworks(properties, worldAccessForResolve.Api);
    _nodes =
        _networks.FromTreeAttributes(tree["networks"] as TreeArrayAttribute);
    // No need to update the mesh here. Initialize will be called before the
    // block is rendered.
  }

  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);
    // _networks and _nodes may have already been initialized in
    // `FromTreeAttributes`. Reinitializing it would wipe out the _nodes
    // information.
    if (_networks == null) {
      ParseNetworks(properties, api);
      _nodes = _networks.CreateNodes();
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
}
