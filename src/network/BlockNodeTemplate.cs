using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.Network;

using Lambda.Token;

public class BlockNodeTemplate {
  protected readonly NodeTemplate[] _nodeTemplates;
  private readonly Dictionary<(NetworkType, Edge), NodeTemplate> _index = new();

  private readonly Dictionary<string, NodeTemplate> _textures = new();

  private readonly NodeAccessor _accessor;
  private readonly Manager _manager;

  public BlockNodeTemplate(NodeAccessor accessor, Manager manager, string face,
                           NodeTemplate[] nodeTemplates) {
    _accessor = accessor;
    _manager = manager;
    _nodeTemplates = nodeTemplates;
    Dictionary<string, int> nodesByname = new();
    for (int i = 0; i < nodeTemplates.Length; ++i) {
      var nodeTemplate = nodeTemplates[i];
      nodeTemplate.Id = i;
      if (nodeTemplate.Name != null && nodeTemplate.Name.Length != 0) {
        nodesByname.Add(nodeTemplate.Name, i);
      }
      foreach (var edge in nodeTemplate.Edges) {
        if (edge == Edge.Source) {
          nodeTemplate.IsSource = true;
        }
        if (edge != Edge.Unknown && edge != Edge.Source) {
          _index.Add((nodeTemplate.Network, edge), nodeTemplate);
        }
      }
      AddTexturesToIndex(nodeTemplate);
    }
    for (int i = 0; i < nodeTemplates.Length; ++i) {
      var nodeTemplate = nodeTemplates[i];
      if (nodeTemplate.Parent != null) {
        nodeTemplate.ParentId = nodesByname[nodeTemplate.Parent];
        nodeTemplates[nodeTemplate.ParentId].ChildIds =
            nodeTemplates[nodeTemplate.ParentId].ChildIds.Append(i);
      } else {
        nodeTemplate.ParentId = -1;
      }
    }
  }

  public void FinishPendingWork() { _manager.FinishPendingWork(); }

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
    foreach (var nodeTemplate in _nodeTemplates) {
      info.Add(nodes[nodeTemplate.Id].ToTreeAttributes());
    }
    if (info.Count == 0) {
      return null;
    }

    return new TreeArrayAttribute(info.ToArray());
  }

  public int Count {
    get { return _nodeTemplates.Length; }
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
    foreach (var _nodeTemplate in _nodeTemplates) {
      if (index < info.value.Length) {
        needRefresh |=
            nodes[_nodeTemplate.Id].FromTreeAttributes(info.value[index++]);
      }
    }
    return needRefresh;
  }

  private static TextureAtlasPosition BakeTexture(ICoreClientAPI capi,
                                                  CompositeTexture texture) {
    if (!texture.Base.HasDomain()) {
      texture.Base.Domain = CoreSystem.Domain;
    }
    if (texture.BlendedOverlays != null) {
      foreach (var overlay in texture.BlendedOverlays) {
        if (!overlay.Base.HasDomain()) {
          overlay.Base.Domain = CoreSystem.Domain;
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
      scopeBlend.Base = new AssetLocation(
          CoreSystem.Domain, $"scope/{ScopeExtension.GetCode(scope)}");
      scopeBlend.BlendMode = EnumColorBlendMode.ColorBurn;
      composite.BlendedOverlays =
          composite.BlendedOverlays?.Append(scopeBlend) ??
          new BlendedOverlayTexture[] { scopeBlend };
    }
    return BakeTexture(capi, composite);
  }

  private static void SetSourceScope(BlockPos pos, NodeTemplate nodeTemplate,
                                     ref Node node) {
    if (nodeTemplate.IsSource) {
      node.Scope = nodeTemplate.SourceScope;
      node.Source.Block = pos;
      node.Source.NodeId = nodeTemplate.Id;
      node.PropagationDistance = 0;
    }
  }

  public void SetSourceScope(BlockPos pos, Node[] nodes) {
    foreach (var nodeTemplate in _nodeTemplates) {
      SetSourceScope(pos, nodeTemplate, ref nodes[nodeTemplate.Id]);
    }
  }

  // `pos` should be treated as immutable.
  public Node[] CreateNodes(BlockPos pos) {
    Node[] nodes = new Node[Count];
    Node.ArrayInitialize(nodes);
    SetSourceScope(pos, nodes);
    return nodes;
  }

  public NodeTemplate GetNodeTemplate(NetworkType network, Edge edge) {
    return !_index.TryGetValue((network, edge), out NodeTemplate n) ? null : n;
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

  public bool CanPlace(BlockPos pos, out string failureCode) {
    BlockNodeTemplate[] neighborTemplates =
        GetNeighbors(pos, out Node[][] neighbors);

    foreach (var template in _nodeTemplates) {
      if (!template.CanPlace(_manager, pos, neighborTemplates, neighbors,
                             out failureCode)) {
        return false;
      }
    }
    failureCode = string.Empty;
    return true;
  }

  public NodeTemplate GetNodeTemplate(int nodeId) {
    return _nodeTemplates[nodeId];
  }

  public bool OnPlaced(BlockPos pos, Node[] nodes) {
    BlockNodeTemplate[] neighborTemplates =
        GetNeighbors(pos, out Node[][] neighbors);
    bool sourceModified = false;
    foreach (var template in _nodeTemplates) {
      bool hadSource = nodes[template.Id].Source.IsSet();
      template.OnPlaced(_manager, pos, neighborTemplates, neighbors,
                        ref nodes[template.Id]);
      sourceModified |= hadSource != nodes[template.Id].Source.IsSet();
    }
    return sourceModified;
  }

  public void OnNodePlaced(BlockPos pos, int id, ref Node node) {
    BlockNodeTemplate[] neighborTemplates =
        GetNeighbors(pos, out Node[][] neighbors);
    _nodeTemplates[id].OnPlaced(_manager, pos, neighborTemplates, neighbors,
                                ref node);
  }

  public void OnNodeChanged(BlockPos pos, int id, ref Node node) {
    BlockNodeTemplate[] neighborTemplates =
        GetNeighbors(pos, out Node[][] neighbors);
    _nodeTemplates[id].OnRemoved(_accessor, _manager, pos, neighborTemplates,
                                 neighbors, in node);
    node = new Node();
    SetSourceScope(pos, _nodeTemplates[id], ref node);

    _nodeTemplates[id].OnPlaced(_manager, pos, neighborTemplates, neighbors,
                                ref node);
  }

  public void OnRemoved(BlockPos pos, Node[] nodes) {
    BlockNodeTemplate[] neighborTemplates =
        GetNeighbors(pos, out Node[][] neighbors);
    foreach (var template in _nodeTemplates) {
      template.OnRemoved(_accessor, _manager, pos, neighborTemplates, neighbors,
                         in nodes[template.Id]);
    }
  }

  public ulong GetTextureKey(Node[] nodes, out int bits) {
    if (Scope.Min < 0 || (int)Scope.Max > 7) {
      throw new Exception("Scope range is too large for 3 bits.");
    }
    ulong key = 0;
    bits = 0;
    foreach (NodeTemplate template in _nodeTemplates) {
      if (template.Textures.Count == 0 &&
          template.ReplacementTextures.Count == 0) {
        continue;
      }
      bits += 3;
      key <<= 3;
      key |= (ulong)nodes[template.Id].Scope;
    }
    if (bits > 64) {
      throw new Exception(
          $"Block has more than the max supported networks with texture overrides.");
    }

    return key;
  }

  public string GetNetworkName() => _manager.GetNetworkName();

  // Returns the number of networks that this template can match edges with the
  // neighbor on `face`. Each network is only counted once, even if it matches
  // multiple edges.
  public int GetPairableNetworkCount(BlockFacing face,
                                     BlockNodeTemplate neighbor) {
    Debug.Assert((int)NetworkType.Max < sizeof(int) * 8 - 1);
    int usedCount = 0;
    int used = 0;
    foreach (var edge in _index) {
      if (edge.Key.Item2.GetFace() != face) {
        continue;
      }
      if (neighbor._index.ContainsKey(
              (edge.Key.Item1, edge.Key.Item2.GetOpposite()))) {
        if ((used & (1 << (int)edge.Key.Item1)) == 0) {
          used |= 1 << (int)edge.Key.Item1;
          ++usedCount;
        }
      }
    }
    return usedCount;
  }

  public bool ContainsNetwork(NetworkType network) {
    foreach (NodeTemplate template in _nodeTemplates) {
      if (template.Network == network) {
        return true;
      }
    }
    return false;
  }

  public int FindNodeTemplate(string name) {
    for (int i = 0; i < _nodeTemplates.Length; ++i) {
      if (_nodeTemplates[i].Name == name) {
        return i;
      }
    }
    return -1;
  }

  public List<NodePos> GetDownstream(NodePos pos) {
    List<NodePos> result = new();
    NodeTemplate nodeTemplate = _nodeTemplates[pos.NodeId];
    foreach (Edge edge in nodeTemplate.Edges) {
      if (edge == Edge.Source) {
        continue;
      }
      BlockPos neighborPos = pos.Block.AddCopy(edge.GetFace());
      NodeTemplate neighborTemplate =
          _accessor.GetNode(neighborPos, nodeTemplate.Network,
                            edge.GetOpposite(), out Node neighborNode);
      if (neighborTemplate != null &&
          neighborNode.Parent == edge.GetOpposite()) {
        result.Add(new NodePos(neighborPos, neighborTemplate.Id));
      }
    }
    return result;
  }

  public virtual Token Emit(TokenEmissionState state, NodePos pos, Node[] nodes,
                            string inventoryTerm) {
    return null;
  }
}
