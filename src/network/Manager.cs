using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;

using ProtoBuf;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.Network;

public struct NodeQueueItem : IComparable<NodeQueueItem> {
  public int PropagationDistance = Node.InfDistance;
  public BlockPos Pos;
  public int NodeId;

  public NodeQueueItem(int propagationDistance, BlockPos pos, int nodeId) {
    PropagationDistance = propagationDistance;
    Pos = pos;
    NodeId = nodeId;
  }

  public readonly int CompareTo(NodeQueueItem other) {
    if (PropagationDistance != other.PropagationDistance) {
      return PropagationDistance - other.PropagationDistance;
    }
    if (Pos.X != other.Pos.X) {
      return Pos.X - other.Pos.X;
    }
    if (Pos.Y != other.Pos.Y) {
      return Pos.Y - other.Pos.Y;
    }
    if (Pos.Z != other.Pos.Z) {
      return Pos.Z - other.Pos.Z;
    }
    if (Pos.dimension != other.Pos.dimension) {
      return Pos.dimension - other.Pos.dimension;
    }
    return NodeId - other.NodeId;
  }
  public override readonly string ToString() {
    return $"{PropagationDistance} <{Pos}>:{NodeId}";
  }

  public readonly string ToEscapedString() {
    return $"{PropagationDistance} &lt;{Pos}&gt;:{NodeId}";
  }
}

[ProtoContract]
public class SourcePendingUpdates {
  [ProtoMember(1)]
  public SortedSet<NodeQueueItem> Queue = new SortedSet<NodeQueueItem>();
  [ProtoMember(2)]
  public HashSet<NodePos> Ejections = new HashSet<NodePos>();

  public SourcePendingUpdates() {}

  public bool HasWork {
    get { return Queue.Count > 0 || Ejections.Count > 0; }
  }

  // Returns whether `pos` is in `Queue` at any distance. Note this function is
  // O(n), because it scans the entire queue.
  public bool QueueContains(BlockPos pos, int nodeId) {
    foreach (var item in Queue) {
      if (item.Pos == pos && item.NodeId == nodeId) {
        return true;
      }
    }
    return false;
  }
}

public class Manager {
  readonly Dictionary<string, Type> _blockNodeTemplateClasses = new();
  public const int OccupiedPortsBitsPerFace = 3;
  Dictionary<NodePos, SourcePendingUpdates> _pendingUpdates =
      new Dictionary<NodePos, SourcePendingUpdates>();

  private readonly int _distanceIncrementVariance = 18;

  public int DefaultDistanceIncrement {
    get { return 1 + (_distanceIncrementVariance >> 1); }
  }

  public int MinDistanceIncrement {
    get { return 1; }
  }

  public int MaxDistanceIncrement {
    get { return 1 + _distanceIncrementVariance; }
  }

  protected readonly NodeAccessor _accessor;

  public readonly EnumAppSide Side;

  private readonly ILogger _logger;

  public Manager(EnumAppSide side, ILogger logger, NodeAccessor accessor) {
    Side = side;
    _logger = logger;
    _accessor = accessor;
    _blockNodeTemplateClasses.Add("BlockNodeTemplate",
                                  typeof(BlockNodeTemplate));
    _blockNodeTemplateClasses.Add("ScopeTemplate", typeof(ScopeTemplate));
    _blockNodeTemplateClasses.Add("FunctionTemplate", typeof(FunctionTemplate));
    _blockNodeTemplateClasses.Add("AppTemplate", typeof(AppTemplate));
    _blockNodeTemplateClasses.Add("MatchTemplate", typeof(MatchTemplate));
  }

  public void Load(byte[] serialized) {
    if (serialized == null) {
      _pendingUpdates = new Dictionary<NodePos, SourcePendingUpdates>();
      return;
    }
    _pendingUpdates =
        SerializerUtil.Deserialize<Dictionary<NodePos, SourcePendingUpdates>>(
            serialized);
  }

  public byte[] Save() { return SerializerUtil.Serialize(_pendingUpdates); }

  public void Debug(string format, params object[] args) {
    _logger?.Debug(format, args);
  }

  public bool IsPropagationDistanceTooLow(int parent, int child) {
    return parent >= child;
  }

  public bool IsPropagationDistanceTooHigh(int parent, int child) {
    return parent + _distanceIncrementVariance + 1 < child;
  }

  public bool IsPropagationDistanceInRange(int parent, int child) {
    return !(IsPropagationDistanceTooLow(parent, child) ||
             IsPropagationDistanceTooHigh(parent, child));
  }

  // Puts the node in the pending updates queue. This should only be called by
  // NodeTemplate.
  // The caller treat `pos` as immutable after the function call.
  public virtual void EnqueueNode(Node node, BlockPos pos, int nodeId) {
    if (Side == EnumAppSide.Client) {
      return;
    }
    System.Diagnostics.Debug.Assert(_accessor.GetSource(pos, nodeId) ==
                                    node.Source);
    System.Diagnostics.Debug.Assert(!node.HasInfDistance);
    System.Diagnostics.Debug.Assert(_accessor.GetDistance(pos, nodeId) ==
                                    node.PropagationDistance);
    if (!_pendingUpdates.TryGetValue(node.Source,
                                     out SourcePendingUpdates sourceUpdates)) {
      sourceUpdates = new SourcePendingUpdates();
      _pendingUpdates.Add(node.Source, sourceUpdates);
    }
    bool added = sourceUpdates.Queue.Add(
        new NodeQueueItem(node.PropagationDistance, pos, nodeId));
  }

  // Removes the node from the pending updates queue with an old value, if it
  // exists. Then puts the node in the pending updates queue with the new
  // distance. This should only be called by NodeTemplate. The caller treat
  // `pos` as immutable after the function call.
  public virtual void RequeueNode(int oldDistance, Node node, BlockPos pos,
                                  int nodeId) {
    if (Side == EnumAppSide.Client) {
      return;
    }
    System.Diagnostics.Debug.Assert(_accessor.GetSource(pos, nodeId) ==
                                    node.Source);
    System.Diagnostics.Debug.Assert(!node.HasInfDistance);
    System.Diagnostics.Debug.Assert(_accessor.GetDistance(pos, nodeId) ==
                                    node.PropagationDistance);
    if (!_pendingUpdates.TryGetValue(node.Source,
                                     out SourcePendingUpdates sourceUpdates)) {
      sourceUpdates = new SourcePendingUpdates();
      _pendingUpdates.Add(node.Source, sourceUpdates);
    }
    if (oldDistance != Node.InfDistance &&
        oldDistance != node.PropagationDistance) {
      // Remove the old entry before adding the new entry.
      sourceUpdates.Queue.Remove(new NodeQueueItem(oldDistance, pos, nodeId));
    }
    bool added = sourceUpdates.Queue.Add(
        new NodeQueueItem(node.PropagationDistance, pos, nodeId));
  }

  // Puts the node in the pending ejections queue. This should only be called by
  // NodeTemplate.
  // The caller treat `pos` as immutable after the function call.
  public virtual void EnqueueEjection(Node node, BlockPos pos, int nodeId) {
    if (Side == EnumAppSide.Client) {
      return;
    }
    System.Diagnostics.Debug.Assert(node.Source.IsSet());
    System.Diagnostics.Debug.Assert(_accessor.GetSource(pos, nodeId) ==
                                    node.Source);
    if (!_pendingUpdates.TryGetValue(node.Source,
                                     out SourcePendingUpdates sourceUpdates)) {
      sourceUpdates = new SourcePendingUpdates();
      _pendingUpdates.Add(node.Source, sourceUpdates);
    }
    bool added = sourceUpdates.Ejections.Add(new NodePos(pos, nodeId));
  }

  // Removes the node from the pending updates queue and ejection queue. This
  // should only be called by NodeTemplate when the block is broken.
  public virtual void RemoveNode(Node node, BlockPos pos, int nodeId) {
    if (Side == EnumAppSide.Client) {
      return;
    }
    if (!_pendingUpdates.TryGetValue(node.Source,
                                     out SourcePendingUpdates sourceUpdates)) {
      return;
    }
    sourceUpdates.Queue.Remove(
        new NodeQueueItem(node.PropagationDistance, pos, nodeId));
    sourceUpdates.Ejections.Remove(new NodePos(pos, nodeId));
  }

  public bool HasPendingWork {
    get {
      foreach (var sourceQueue in _pendingUpdates) {
        if (sourceQueue.Value.HasWork) {
          return true;
        }
      }
      return false;
    }
  }

  public string QueueDebugString() {
    StringBuilder builder = new StringBuilder();
    builder.AppendLine(
        $"SourceQueueCount={_pendingUpdates.Count} First 2 source queues:");
    int printed = 0;
    foreach (var sourceQueue in _pendingUpdates) {
      builder.AppendLine(
          $"Source={sourceQueue.Key.ToEscapedString()} Queue.Count={sourceQueue.Value.Queue.Count} Ejections.Count={sourceQueue.Value.Ejections.Count}");
      builder.AppendLine("  First 5 queue updates:");
      NodeQueueItem[] firstN =
          new NodeQueueItem[Math.Min(sourceQueue.Value.Queue.Count, 5)];
      sourceQueue.Value.Queue.CopyTo(firstN, 0, firstN.Length);
      for (int i = 0; i < firstN.Length; ++i) {
        builder.AppendLine($"  {firstN[i].ToEscapedString()}");
      }
      builder.AppendLine("  First 5 ejection updates:");
      int printedEjections = 0;
      foreach (NodePos ejection in sourceQueue.Value.Ejections) {
        builder.AppendLine($"  {ejection.ToEscapedString()}");
        if (++printedEjections >= 5) {
          break;
        }
      }
      if (++printed >= 2) {
        break;
      }
    }
    return builder.ToString();
  }

  public bool AreQueueDistancesConsistent() {
    foreach (var sourceQueue in _pendingUpdates) {
      foreach (NodeQueueItem item in sourceQueue.Value.Queue) {
        if (item.PropagationDistance == Node.InfDistance) {
          Debug("Node <{0}>:{1} in the queue has infinite distance.", item.Pos,
                item.NodeId);
          return false;
        }
        if (sourceQueue.Key != _accessor.GetSource(item.Pos, item.NodeId)) {
          Debug(
              "The queue claims node <{0}>:{1} has source {2}, but it really has source {3}.",
              item.Pos, item.NodeId, sourceQueue.Key,
              _accessor.GetSource(item.Pos, item.NodeId));
          return false;
        }
        if (item.PropagationDistance !=
            _accessor.GetDistance(item.Pos, item.NodeId)) {
          Debug(
              "The queue claims node <{0}>:{1} has distance {2}, but it really has distance {3}.",
              item.Pos, item.NodeId, item.PropagationDistance,
              _accessor.GetDistance(item.Pos, item.NodeId));
          return false;
        }
      }
    }
    return true;
  }

  public void Step() {
    foreach (var sourceQueue in _pendingUpdates) {
      StepSource(sourceQueue.Key, sourceQueue.Value);
      if (!sourceQueue.Value.HasWork) {
        _pendingUpdates.Remove(sourceQueue.Key);
      }
    }
  }

  private void StepSource(NodePos source, SourcePendingUpdates sourceQueue) {
    System.Diagnostics.Debug.Assert(AreQueueDistancesConsistent());
    if (sourceQueue.Queue.Count > 0) {
      NodeQueueItem min = sourceQueue.Queue.Min;
      sourceQueue.Queue.Remove(min);
      System.Diagnostics.Debug.Assert(
          _accessor.GetDistance(min.Pos, min.NodeId) ==
          min.PropagationDistance);
      NodeTemplate template =
          _accessor.GetNode(min.Pos, min.NodeId, out Node node);
      System.Diagnostics.Debug.Assert(template != null);
      if (template == null) {
        return;
      }
      template.Expand(_accessor, this, min.Pos, node);
      return;
    }

    if (sourceQueue.Ejections.Count > 0) {
      NodePos first = sourceQueue.Ejections.PopOne();
      NodeTemplate template =
          _accessor.GetNode(first.Block, first.NodeId, out Node node);
      System.Diagnostics.Debug.Assert(template != null);
      if (template == null) {
        return;
      }
      template.EjectIfDisconnected(_accessor, this, source, first.Block, node);
      return;
    }
  }

  public void FinishPendingWork() {
    while (HasPendingWork) {
      Step();
    }
  }

  public virtual string GetNetworkName() { return "test"; }

  // Returns whether the node exists. If it exists, then the source of the node
  // is stored in `source`.
  public bool GetSource(BlockPos pos, NetworkType network, Edge edge,
                        out NodePos source) {
    if (_accessor.GetNode(pos, network, edge, out Node node) == null) {
      source = new NodePos();
      return false;
    }
    source = node.Source;
    return true;
  }

  public bool IsBlockInNetwork(BlockPos pos, NetworkType network) {
    return _accessor.GetBlock(pos, out Node[] nodes)
               ?.ContainsNetwork(network) ??
           false;
  }

  // Set entries in `templates` to null if they have no edges that pair with the
  // edges at `pos`, or if they pair less than the max networks with the
  // neighbor. Nulls in `templates` are ignored.
  public void RemoveUnpaired(List<BlockNodeTemplate> templates, BlockPos pos,
                             BlockFacing face) {
    BlockNodeTemplate neighbor =
        _accessor.GetBlock(pos.AddCopy(face), out Node[] nodes);
    int max = 1;
    for (int i = 0; i < templates.Count; ++i) {
      if (templates[i] == null) {
        continue;
      }
      if (neighbor == null) {
        templates[i] = null;
        continue;
      }
      int pairable = templates[i].GetPairableNetworkCount(face, neighbor);
      if (pairable < max) {
        templates[i] = null;
      } else if (pairable > max) {
        max = pairable;
        // Null out all previous, non-null entries, because they paired using
        // less than the max pairable networks.
        for (int j = 0; j < i; ++j) {
          templates[j] = null;
        }
      }
    }
  }

  // Parse the block template. `connectFaces` describes center edges to add to
  // node[0]. node[0] is not changed if `connectFaces` is 0.
  public virtual BlockNodeTemplate ParseBlockNodeTemplate(JsonObject properties,
                                                          int occupiedPorts,
                                                          int connectFaces) {
    List<NodeTemplate> nodeTemplates =
        new(properties["nodes"]?.AsObject<NodeTemplate[]>() ??
            Array.Empty<NodeTemplate>());
    PortOption[] ports = properties["ports"]?.AsObject<PortOption[]>() ??
                         Array.Empty<PortOption>();
    foreach (var port in ports) {
      NodeTemplate node = new() { Network = NetworkType.Placeholder,
                                  Name = port.Name, Parent = port.Parent };
      foreach (var portFace in port.Faces) {
        const int mask = (1 << OccupiedPortsBitsPerFace) - 1;
        PortDirection dir =
            (PortDirection)((occupiedPorts >>
                             (portFace.Index * OccupiedPortsBitsPerFace)) &
                            mask);
        if (dir == PortDirection.DirectIn) {
          node.Edges = new Edge[] { EdgeExtension.GetFaceCenter(portFace) };
          node.Network = port.Network;
          break;
        }
        if (dir == PortDirection.DirectOut) {
          node.Edges =
              new Edge[] { EdgeExtension.GetFaceCenter(portFace), Edge.Source };
          node.Network = port.Network;
          break;
        }
      }
      nodeTemplates.Add(node);
    }
    if (connectFaces != 0) {
      if (nodeTemplates.Count < 1) {
        nodeTemplates.Add(new() { Name = "default" });
      }
      HashSet<Edge> connectedEdges = new(nodeTemplates[0].Edges);
      for (int i = 0; i < 6; ++i) {
        BlockFacing connectedFace = BlockFacing.ALLFACES[i];
        if ((connectFaces & (1 << i)) != 0) {
          connectedEdges.Add(EdgeExtension.GetFaceCenter(connectedFace));
        }
      }
      nodeTemplates[0].Edges = connectedEdges.ToArray();
    }
    Type blockNodeType = _blockNodeTemplateClasses[properties["class"].AsString(
        "BlockNodeTemplate")];
    string face = properties["face"].AsString();
    try {
      return (BlockNodeTemplate)Activator.CreateInstance(
          blockNodeType, _accessor, this, face, nodeTemplates.ToArray());
    } catch (TargetInvocationException ex) {
      ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
      // Without this rethrow, the compiler complains that the function returns
      // without returning a value.
      throw;
    }
  }
}
