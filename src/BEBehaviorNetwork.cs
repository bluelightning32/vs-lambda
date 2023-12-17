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

public enum Node {
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

static class NodeExtension {
  public static BlockFacing GetFace(this Node node) {
    return node switch {
      Node.NorthRight or Node.NorthUp or Node.NorthLeft or
          Node.NorthDown or Node.NorthCenter => BlockFacing.NORTH,
      Node.EastRight or Node.EastUp or Node.EastLeft or Node.EastDown or
          Node.EastCenter => BlockFacing.EAST,
      Node.SouthRight or Node.SouthUp or Node.SouthLeft or
          Node.SouthDown or Node.SouthCenter => BlockFacing.SOUTH,
      Node.WestRight or Node.WestUp or Node.WestLeft or Node.WestDown or
          Node.WestCenter => BlockFacing.WEST,
      Node.UpRight or Node.UpUp or Node.UpLeft or Node.UpDown or
          Node.UpCenter => BlockFacing.UP,
      Node.DownRight or Node.DownUp or Node.DownLeft or Node.DownDown or
          Node.DownCenter => BlockFacing.DOWN,
      _ => null
    };
  }

  public static Node GetOpposite(this Node node) {
    return node switch { Node.NorthRight => Node.SouthLeft,
                         Node.EastRight => Node.WestLeft,
                         Node.SouthRight => Node.NorthLeft,
                         Node.WestRight => Node.EastLeft,

                         Node.NorthLeft => Node.SouthRight,
                         Node.EastLeft => Node.WestRight,
                         Node.SouthLeft => Node.NorthRight,
                         Node.WestLeft => Node.EastRight,

                         Node.NorthUp => Node.SouthUp,
                         Node.EastUp => Node.WestUp,
                         Node.SouthUp => Node.NorthUp,
                         Node.WestUp => Node.EastUp,

                         Node.NorthDown => Node.SouthDown,
                         Node.EastDown => Node.WestDown,
                         Node.SouthDown => Node.NorthDown,
                         Node.WestDown => Node.EastDown,

                         Node.UpUp => Node.DownUp,
                         Node.UpDown => Node.DownDown,
                         Node.UpLeft => Node.DownRight,
                         Node.UpRight => Node.DownLeft,

                         Node.DownUp => Node.UpUp,
                         Node.DownDown => Node.UpDown,
                         Node.DownLeft => Node.UpRight,
                         Node.DownRight => Node.UpLeft,

                         Node.NorthCenter => Node.SouthCenter,
                         Node.EastCenter => Node.WestCenter,
                         Node.SouthCenter => Node.NorthCenter,
                         Node.WestCenter => Node.EastCenter,
                         Node.UpCenter => Node.DownCenter,
                         Node.DownCenter => Node.UpCenter,

                         _ => Node.Unknown };
  }
}

[JsonObject(MemberSerialization.OptIn)]
public class Network {
  public int Id = 0;
  public bool Source = false;
  [JsonProperty]
  public Scope SourceScope = Scope.Function;
  [JsonProperty]
  public Node[] Nodes = Array.Empty<Node>();
  [JsonProperty]
  public string[] Textures = Array.Empty<string>();

  public Network() {}

  public TreeAttribute ToTreeAttributes(NetworkMembership membership) {
    TreeAttribute tree = new TreeAttribute();
    tree.SetInt("scope", (int)membership.Scope);
    return tree;
  }

  public NetworkMembership FromTreeAttributes(TreeAttribute tree) {
    NetworkMembership membership = new NetworkMembership();
    membership.Scope = (Scope)tree.GetAsInt("scope");
    return membership;
  }

  public Scope GetScope(NetworkMembership[] membership) {
    if (Source) {
      return SourceScope;
    } else {
      return membership[Id].Scope;
    }
  }

  public void Propagate(ICoreAPI api, BlockPos pos, bool scopeNetwork,
                        ref NetworkMembership networkMembership) {
    foreach (Node node in Nodes) {
      BlockFacing face = node.GetFace();
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
            neighbor.GetMembership(scopeNetwork, node.GetOpposite()).Scope;
        if (neighborScope != Scope.None) {
          networkMembership.Scope = neighborScope;
        }
      }
    }
  }
}

[JsonArray]
public class IndexedNetwork {
  public readonly Network[] Networks;
  public readonly Dictionary<Node, Network> Index =
      new Dictionary<Node, Network>();

  public IndexedNetwork(int firstId, Network[] networks) {
    Networks = networks;
    foreach (var network in networks) {
      network.Id = firstId++;
      foreach (var node in network.Nodes) {
        if (node == Node.Source) {
          network.Source = true;
        }
        if (node != Node.Unknown) {
          Index.Add(node, network);
        }
      }
    }
  }

  public void Propagate(ICoreAPI api, BlockPos pos, bool scopeNetwork,
                        NetworkMembership[] membership) {
    foreach (var network in Networks) {
      network.Propagate(api, pos, scopeNetwork, ref membership[network.Id]);
    }
  }
}

[JsonObject(MemberSerialization.OptIn)]
public class Networks {
  [JsonProperty]
  private readonly IndexedNetwork _scope;
  [JsonProperty]
  private readonly IndexedNetwork _match;

  private readonly Dictionary<string, Network> _textures =
      new Dictionary<string, Network>();

  [JsonConstructor]
  public Networks(Network[] scope, Network[] match) {
    _scope = new IndexedNetwork(0, scope ?? Array.Empty<Network>());
    _match = new IndexedNetwork(_scope.Networks.Length,
                                match ?? Array.Empty<Network>());
    foreach (Network network in _scope.Networks) {
      foreach (string texture in network.Textures) {
        _textures.Add(texture, network);
      }
    }
    foreach (Network network in _match.Networks) {
      foreach (string texture in network.Textures) {
        _textures.Add(texture, network);
      }
    }
  }

  public TreeArrayAttribute ToTreeAttributes(NetworkMembership[] membership) {
    List<TreeAttribute> info = new List<TreeAttribute>(Count);
    foreach (var network in _scope.Networks) {
      if (network.Source) {
        continue;
      }
      info.Add(network.ToTreeAttributes(membership[network.Id]));
    }
    foreach (var network in _match.Networks) {
      if (network.Source) {
        continue;
      }
      info.Add(network.ToTreeAttributes(membership[network.Id]));
    }
    if (info.Count == 0) {
      return null;
    }

    return new TreeArrayAttribute(info.ToArray());
  }

  public int Count {
    get { return _scope.Networks.Length + _match.Networks.Length; }
  }

  public NetworkMembership[] FromTreeAttributes(TreeArrayAttribute info) {
    NetworkMembership[] membership = new NetworkMembership[Count];
    if (info == null) {
      SetSourceMembership(membership);
      return membership;
    }
    int index = 0;
    foreach (var network in _scope.Networks) {
      if (network.Source) {
        membership[network.Id].Scope = network.SourceScope;
      } else {
        membership[network.Id] =
            network.FromTreeAttributes(info.value[index++]);
      }
    }
    foreach (var network in _match.Networks) {
      if (network.Source) {
        membership[network.Id].Scope = network.SourceScope;
      } else {
        membership[network.Id] =
            network.FromTreeAttributes(info.value[index++]);
      }
    }
    return membership;
  }

  public TextureAtlasPosition GetTexture(string name, ICoreClientAPI capi,
                                         Block block,
                                         NetworkMembership[] membership) {
    if (!_textures.TryGetValue(name, out Network network)) {
      return null;
    }
    CompositeTexture composite;
    if (!block.Textures.TryGetValue(name, out composite)) {
      return null;
    }
    Scope scope = network.GetScope(membership);
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

  private void SetSourceMembership(NetworkMembership[] membership) {
    foreach (var network in _scope.Networks) {
      if (network.Source) {
        membership[network.Id].Scope = network.SourceScope;
      }
    }
    foreach (var network in _match.Networks) {
      if (network.Source) {
        membership[network.Id].Scope = network.SourceScope;
      }
    }
  }

  public NetworkMembership[] CreateMembership() {
    NetworkMembership[] membership = new NetworkMembership[Count];
    SetSourceMembership(membership);
    return membership;
  }

  public NetworkMembership GetMembership(bool scopeNetwork, Node node,
                                         NetworkMembership[] membership) {
    IndexedNetwork network = scopeNetwork ? _scope : _match;
    if (!network.Index.TryGetValue(node, out Network n)) {
      return new NetworkMembership();
    }
    return membership[n.Id];
  }

  public void Propagate(ICoreAPI api, BlockPos pos,
                        NetworkMembership[] membership) {
    _scope.Propagate(api, pos, true, membership);
    _match.Propagate(api, pos, false, membership);
  }
}

public struct NetworkMembership {
  public Scope Scope = Scope.None;

  public NetworkMembership() {}
}

public class BEBehaviorNetwork : BlockEntityBehavior,
                                 IMeshGenerator,
                                 ITexPositionSource {
  private Networks _networks;
  private NetworkMembership[] _membership;

  public BEBehaviorNetwork(BlockEntity blockentity) : base(blockentity) {}

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    TreeArrayAttribute networks = _networks.ToTreeAttributes(_membership);
    if (networks != null) {
      tree["networks"] = networks;
    }
  }

  public NetworkMembership GetMembership(bool scopeNetwork, Node node) {
    return _networks.GetMembership(scopeNetwork, node, _membership);
  }

  private void ParseNetworks(JsonObject properties, ICoreAPI api) {
    Dictionary<JsonObject, Networks> cache = ObjectCacheUtil.GetOrCreate(
        api, $"lambdafactory-networks-{Block.Code}",
        () => new Dictionary<JsonObject, Networks>());
    if (cache.TryGetValue(properties, out _networks)) {
      return;
    }
    api.Logger.Notification(
        "lambda: Properties cache miss for {0}. Dict has {1} entries.",
        Block.Code, cache.Count);
    _networks = properties.AsObject<Networks>();
    cache.Add(properties, _networks);
  }

  public override void
  FromTreeAttributes(ITreeAttribute tree,
                     IWorldAccessor worldAccessForResolve) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    // FromTreeAttributes is called before Initialize, so ParseNetworks needs to
    // be called before accessing _networks.
    ParseNetworks(properties, worldAccessForResolve.Api);
    _membership =
        _networks.FromTreeAttributes(tree["networks"] as TreeArrayAttribute);
    // No need to update the mesh here. Initialize will be called before the
    // block is rendered.
  }

  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);
    // _networks and _membership may have already been initialized in
    // `FromTreeAttributes`. Reinitializing it would wipe out the membership
    // information.
    if (_networks == null) {
      ParseNetworks(properties, api);
      _membership = _networks.CreateMembership();
    }
  }

  public override void OnBlockPlaced(ItemStack byItemStack = null) {
    base.OnBlockPlaced(byItemStack);
    _networks.Propagate(Api, Pos, _membership);
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
    if (_membership.Length > 64 / 3) {
      throw new Exception(
          $"Block {Block.Code} has more than the max supported networks.");
    }

    ulong key = 0;
    foreach (NetworkMembership membership in _membership) {
      key <<= 3;
      key |= (ulong)membership.Scope;
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
      return _networks.GetTexture(textureCode, capi, Block, _membership) ??
             capi.Tesselator.GetTextureSource(Block)[textureCode];
    }
  }
}
