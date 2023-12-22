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

  public void Propagate(ICoreAPI api, BlockPos pos, bool scopeNetwork,
                        Node[] nodes) {
    foreach (var network in NodeTemplates) {
      network.Propagate(api, pos, scopeNetwork, ref nodes[network.Id]);
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
    _match = new BlockNodeCategory(_scope.NodeTemplates.Length,
                                   match ?? Array.Empty<NodeTemplate>());
    foreach (NodeTemplate network in _scope.NodeTemplates) {
      foreach (string texture in network.Textures) {
        _textures.Add(texture, network);
      }
    }
    foreach (NodeTemplate network in _match.NodeTemplates) {
      foreach (string texture in network.Textures) {
        _textures.Add(texture, network);
      }
    }
  }

  public TreeArrayAttribute ToTreeAttributes(Node[] nodes) {
    List<TreeAttribute> info = new List<TreeAttribute>(Count);
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

  public Node[] FromTreeAttributes(BlockPos pos, TreeArrayAttribute info) {
    Node[] nodes = new Node[Count];
    if (info == null) {
      SetSourceScope(pos, nodes);
      return nodes;
    }
    int index = 0;
    foreach (var network in _scope.NodeTemplates) {
      nodes[network.Id] = Node.FromTreeAttributes(info.value[index++]);
    }
    foreach (var network in _match.NodeTemplates) {
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

  public Node[] CreateNodes(BlockPos pos) {
    Node[] nodes = new Node[Count];
    SetSourceScope(pos, nodes);
    return nodes;
  }

  public Node GetNode(bool scopeNetwork, Edge edge, Node[] nodes) {
    BlockNodeCategory network = scopeNetwork ? _scope : _match;
    if (!network.Index.TryGetValue(edge, out NodeTemplate n)) {
      return new Node();
    }
    return nodes[n.Id];
  }

  public NodeTemplate GetNodeTemplate(bool scopeNetwork, Edge edge) {
    BlockNodeCategory network = scopeNetwork ? _scope : _match;
    return !network.Index.TryGetValue(edge, out NodeTemplate n) ? null : n;
  }

  public void Propagate(ICoreAPI api, BlockPos pos, Node[] nodes) {
    _scope.Propagate(api, pos, true, nodes);
    _match.Propagate(api, pos, false, nodes);
  }

  public bool CanPlace(NetworkManager manager, BlockPos pos,
                       ref string failureCode) {
    BlockNodeTemplate[] neighborTemplates =
        new BlockNodeTemplate[BlockFacing.NumberOfFaces];
    Node[][] neighbors = new Node[6][];
    BlockPos neighborPos = new(pos.dimension);
    for (int i = 0; i < BlockFacing.NumberOfFaces; ++i) {
      neighborPos.Set(pos);
      neighborPos.Offset(BlockFacing.ALLFACES[i]);
      neighborTemplates[i] =
          manager.Accessor.GetBlock(neighborPos, out neighbors[i]);
    }

    return _scope.CanPlace(manager, pos, true, neighborTemplates, neighbors,
                           ref failureCode) &&
           _match.CanPlace(manager, pos, false, neighborTemplates, neighbors,
                           ref failureCode);
  }

  public NodeTemplate GetNodeTemplate(int nodeId) {
    if (nodeId < _scope.NodeTemplates.Length) {
      return _scope.NodeTemplates[nodeId];
    }
    nodeId -= _scope.NodeTemplates.Length;
    return _match.NodeTemplates[nodeId];
  }
}
