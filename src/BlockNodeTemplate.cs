using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LambdaFactory;

[JsonArray]
public class BlockNodeCategory {
  public readonly NodeTemplate[] NodeTemplates;
  public readonly Dictionary<Edge, NodeTemplate> Index =
      new Dictionary<Edge, NodeTemplate>();

  public BlockNodeCategory(int firstId, NodeTemplate[] networks) {
    NodeTemplates = networks;
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

  public void OnPlaced(NetworkManager manager, BlockPos pos, bool scopeNetwork,
                       BlockNodeTemplate[] neighborTemplates,
                       Node[][] neighbors, Node[] nodes) {
    foreach (var template in NodeTemplates) {
      template.OnPlaced(manager, pos, scopeNetwork, neighborTemplates,
                        neighbors, ref nodes[template.Id]);
    }
  }

  public void OnRemoved(NodeAccessor accessor, NetworkManager manager,
                        BlockPos pos, bool scopeNetwork,
                        BlockNodeTemplate[] neighborTemplates,
                        Node[][] neighbors, Node[] nodes) {
    foreach (var template in NodeTemplates) {
      template.OnRemoved(accessor, manager, pos, scopeNetwork,
                         neighborTemplates, neighbors, in nodes[template.Id]);
    }
  }

  public bool CanPlace(NetworkManager manager, BlockPos pos, bool scopeNetwork,
                       BlockNodeTemplate[] neighborTemplates,
                       Node[][] neighbors, ref string failureCode) {
    foreach (var network in NodeTemplates) {
      if (!network.CanPlace(manager, pos, scopeNetwork, neighborTemplates,
                            neighbors, ref failureCode)) {
        return false;
      }
    }
    return true;
  }
}

[JsonObject(MemberSerialization.OptIn)]
public class BlockNodeTemplateLoading {
  [JsonProperty]
  public NodeTemplate[] Scope;
  [JsonProperty]
  public NodeTemplate[] Match;
}

public class BlockNodeTemplate {
  private readonly BlockNodeCategory _scope;
  private readonly BlockNodeCategory _match;

  private readonly Dictionary<string, NodeTemplate> _textures = new();

  private readonly NodeAccessor _accessor;
  private readonly NetworkManager _manager;

  public BlockNodeTemplate(BlockNodeTemplateLoading loading,
                           NodeAccessor accessor, NetworkManager manager) {
    _accessor = accessor;
    _manager = manager;
    _scope =
        new BlockNodeCategory(0, loading.Scope ?? Array.Empty<NodeTemplate>());
    _match =
        new BlockNodeCategory(_scope.NodeTemplates.Length,
                              loading.Match ?? Array.Empty<NodeTemplate>());
    foreach (NodeTemplate network in _scope.NodeTemplates) {
      AddTexturesToIndex(network);
    }
    foreach (NodeTemplate network in _match.NodeTemplates) {
      AddTexturesToIndex(network);
    }
  }

  private void AddTexturesToIndex(NodeTemplate template) {
    // HashSet is tolerant of adding the same key more than once, but _textures
    // is not. So add the new textures to a hashset first, then add the hashset
    // to the dictionary.
    HashSet<string> newTextures = new(template.Textures);
    foreach (var scopeEntry in template.ReplacementTextures) {
      foreach (var entry in scopeEntry.Value) {
        newTextures.Add(entry.Key);
      }
    }
    foreach (var texture in newTextures) {
      _textures.Add(texture, template);
    }
  }

  public TreeArrayAttribute ToTreeAttributes(Node[] nodes) {
    List<TreeAttribute> info = new(Count);
    foreach (var network in _scope.NodeTemplates) {
      info.Add(nodes[network.Id].ToTreeAttributes());
    }
    foreach (var network in _match.NodeTemplates) {
      info.Add(nodes[network.Id].ToTreeAttributes());
    }
    if (info.Count == 0) {
      return null;
    }

    return new TreeArrayAttribute(info.ToArray());
  }

  public int Count {
    get { return _scope.NodeTemplates.Length + _match.NodeTemplates.Length; }
  }

  public bool FromTreeAttributes(BlockPos pos, TreeArrayAttribute info,
                                 ref Node[] nodes) {
    bool needRefresh = false;
    if ((nodes?.Length ?? 0) != Count) {
      needRefresh = true;
      nodes = new Node[Count];
    }
    if (info == null) {
      SetSourceScope(pos, nodes);
      return true;
    }
    int index = 0;
    foreach (var network in _scope.NodeTemplates) {
      if (index < info.value.Length) {
        needRefresh |=
            nodes[network.Id].FromTreeAttributes(info.value[index++]);
      }
    }
    foreach (var network in _match.NodeTemplates) {
      if (index < info.value.Length) {
        needRefresh |=
            nodes[network.Id].FromTreeAttributes(info.value[index++]);
      }
    }
    return needRefresh;
  }

  private static TextureAtlasPosition BakeTexture(ICoreClientAPI capi,
                                                  CompositeTexture texture) {
    if (!texture.Base.HasDomain()) {
      texture.Base.Domain = LambdaFactoryModSystem.Domain;
    }
    if (texture.BlendedOverlays != null) {
      foreach (var overlay in texture.BlendedOverlays) {
        if (!overlay.Base.HasDomain()) {
          overlay.Base.Domain = LambdaFactoryModSystem.Domain;
        }
      }
    }

    texture.Bake(capi.Assets);
    ITextureAtlasAPI atlas = capi.BlockTextureAtlas;
    atlas.GetOrInsertTexture(
        texture.Baked.BakedName, out int id, out TextureAtlasPosition tex,
        () => atlas.LoadCompositeBitmap(texture.Baked.BakedName));
    return tex;
  }

  public TextureAtlasPosition GetTexture(string name, ICoreClientAPI capi,
                                         Block block, Node[] nodes) {
    if (!_textures.TryGetValue(name, out NodeTemplate template)) {
      return null;
    }
    Scope scope = template.GetScope(nodes);
    CompositeTexture composite;
    // Check for a complete replacement texture.
    if (template.ReplacementTextures.TryGetValue(
            scope,
            out Dictionary<string, CompositeTexture> replacementTextures)) {
      if (replacementTextures.TryGetValue(name, out composite)) {
        return BakeTexture(capi, composite);
      }
    }
    if (!template.Textures.Contains(name)) {
      return null;
    }
    // The texture is in the list to add an overlay to. So read the block's base
    // texture, then add the overlay based on the scope.
    if (!block.Textures.TryGetValue(name, out composite)) {
      return null;
    }
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
    return BakeTexture(capi, composite);
  }

  private void SetSourceScope(BlockPos pos, Node[] nodes) {
    foreach (var network in _scope.NodeTemplates) {
      if (network.Source) {
        nodes[network.Id].Scope = network.SourceScope;
        nodes[network.Id].Source.Block = pos;
        nodes[network.Id].Source.NodeId = network.Id;
        nodes[network.Id].PropagationDistance = 0;
      }
    }
    foreach (var network in _match.NodeTemplates) {
      if (network.Source) {
        nodes[network.Id].Scope = network.SourceScope;
        nodes[network.Id].Source.Block = pos;
        nodes[network.Id].Source.NodeId = network.Id;
        nodes[network.Id].PropagationDistance = 0;
      }
    }
  }

  // `pos` should be treated as immutable.
  public Node[] CreateNodes(BlockPos pos) {
    Node[] nodes = new Node[Count];
    Node.ArrayInitialize(nodes);
    SetSourceScope(pos, nodes);
    return nodes;
  }

  public NodeTemplate GetNodeTemplate(bool scopeNetwork, Edge edge) {
    BlockNodeCategory network = scopeNetwork ? _scope : _match;
    return !network.Index.TryGetValue(edge, out NodeTemplate n) ? null : n;
  }

  private BlockNodeTemplate[] GetNeighbors(BlockPos pos,
                                           out Node[][] neighbors) {
    BlockNodeTemplate[] neighborTemplates =
        new BlockNodeTemplate[BlockFacing.NumberOfFaces];
    neighbors = new Node[6][];
    BlockPos neighborPos = new(pos.dimension);
    for (int i = 0; i < BlockFacing.NumberOfFaces; ++i) {
      neighborPos.Set(pos);
      neighborPos.Offset(BlockFacing.ALLFACES[i]);
      neighborTemplates[i] = _accessor.GetBlock(neighborPos, out neighbors[i]);
    }
    return neighborTemplates;
  }

  public bool CanPlace(BlockPos pos, ref string failureCode) {
    BlockNodeTemplate[] neighborTemplates =
        GetNeighbors(pos, out Node[][] neighbors);

    return _scope.CanPlace(_manager, pos, true, neighborTemplates, neighbors,
                           ref failureCode) &&
           _match.CanPlace(_manager, pos, false, neighborTemplates, neighbors,
                           ref failureCode);
  }

  public NodeTemplate GetNodeTemplate(int nodeId) {
    if (nodeId < _scope.NodeTemplates.Length) {
      return _scope.NodeTemplates[nodeId];
    }
    nodeId -= _scope.NodeTemplates.Length;
    return _match.NodeTemplates[nodeId];
  }

  public void OnPlaced(BlockPos pos, Node[] nodes) {
    BlockNodeTemplate[] neighborTemplates =
        GetNeighbors(pos, out Node[][] neighbors);

    _scope.OnPlaced(_manager, pos, true, neighborTemplates, neighbors, nodes);
    _match.OnPlaced(_manager, pos, false, neighborTemplates, neighbors, nodes);
  }

  public void OnRemoved(BlockPos pos, Node[] nodes) {
    BlockNodeTemplate[] neighborTemplates =
        GetNeighbors(pos, out Node[][] neighbors);

    _scope.OnRemoved(_accessor, _manager, pos, true, neighborTemplates,
                     neighbors, nodes);
    _match.OnRemoved(_accessor, _manager, pos, false, neighborTemplates,
                     neighbors, nodes);
  }

  public bool IsScopeNetwork(int nodeId) {
    return nodeId < _scope.NodeTemplates.Length;
  }

  public ulong GetTextureKey(Node[] nodes) {
    if (Scope.Min < 0 || (int)Scope.Max > 7) {
      throw new Exception("Scope range is too large for 3 bits.");
    }
    ulong key = 0;
    int texturedNodes = 0;
    foreach (NodeTemplate template in _scope.NodeTemplates) {
      if (template.Textures.Count == 0 &&
          template.ReplacementTextures.Count == 0) {
        continue;
      }
      ++texturedNodes;
      key <<= 3;
      key |= (ulong)nodes[template.Id].Scope;
    }
    foreach (NodeTemplate template in _match.NodeTemplates) {
      if (template.Textures.Count == 0 &&
          template.ReplacementTextures.Count == 0) {
        continue;
      }
      ++texturedNodes;
      key <<= 3;
      key |= (ulong)nodes[template.Id].Scope;
    }
    if (texturedNodes > 64 / 3) {
      throw new Exception(
          $"Block has more than the max supported networks with texture overrides.");
    }

    return key;
  }
}