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
  public static BlockFacing GetFace(this Edge node) {
    return node switch {
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

  public static Edge GetOpposite(this Edge node) {
    return node switch { Edge.NorthRight => Edge.SouthLeft,
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
public class Network {
  public int Id = 0;
  public bool Source = false;
  [JsonProperty]
  public Scope SourceScope = Scope.Function;
  [JsonProperty]
  public Edge[] Nodes = Array.Empty<Edge>();
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
    foreach (Edge node in Nodes) {
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
  public readonly Dictionary<Edge, Network> Index =
      new Dictionary<Edge, Network>();

  public IndexedNetwork(int firstId, Network[] networks) {
    Networks = networks;
    foreach (var network in networks) {
      network.Id = firstId++;
      foreach (var node in network.Nodes) {
        if (node == Edge.Source) {
          network.Source = true;
        }
        if (node != Edge.Unknown) {
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

  public NetworkMembership GetMembership(bool scopeNetwork, Edge node,
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

  public NetworkMembership GetMembership(bool scopeNetwork, Edge node) {
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
