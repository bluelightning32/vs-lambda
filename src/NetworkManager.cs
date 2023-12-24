using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ProtoBuf;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LambdaFactory;

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
  public List<NodePos> Ejections = new List<NodePos>();

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

public class NetworkManager {
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

  public NetworkManager(EnumAppSide side, ILogger logger,
                        NodeAccessor accessor) {
    Side = side;
    _logger = logger;
    _accessor = accessor;
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
    System.Diagnostics.Debug.Assert(_accessor.GetDistance(pos, nodeId) ==
                                    node.PropagationDistance);
    SourcePendingUpdates sourceUpdates;
    if (!_pendingUpdates.TryGetValue(node.Source, out sourceUpdates)) {
      sourceUpdates = new SourcePendingUpdates();
      _pendingUpdates.Add(node.Source, sourceUpdates);
    }
    System.Diagnostics.Debug.Assert(!sourceUpdates.QueueContains(pos, nodeId));
    sourceUpdates.Queue.Add(
        new NodeQueueItem(node.PropagationDistance, pos, nodeId));
    Debug("Added node to queue. source={0} pos=<{1}>:{2} dist={3}", node.Source,
          pos, nodeId, node.PropagationDistance);
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
      for (int i = 0; i < Math.Min(5, sourceQueue.Value.Ejections.Count); ++i) {
        builder.AppendLine(
            $"  sourceQueue.Value.Ejections[i].ToEscapedString()");
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
      StepSource(sourceQueue.Value);
    }
  }

  private void StepSource(SourcePendingUpdates sourceQueue) {
    if (sourceQueue.Queue.Count > 0) {
      System.Diagnostics.Debug.Assert(AreQueueDistancesConsistent());
      NodeQueueItem min = sourceQueue.Queue.Min;
      sourceQueue.Queue.Remove(min);
      System.Diagnostics.Debug.Assert(
          _accessor.GetDistance(min.Pos, min.NodeId) ==
          min.PropagationDistance);
      NodeTemplate template = _accessor.GetNode(
          min.Pos, min.NodeId, out Node node, out bool scopeNetwork);
      System.Diagnostics.Debug.Assert(template != null);
      if (template == null) {
        return;
      }
      template.Expand(_accessor, this, min.Pos, scopeNetwork, node);
      return;
    }
  }

  public void FinishPendingWork() {
    while (HasPendingWork) {
      Step();
    }
  }
}
