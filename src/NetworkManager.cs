using System;
using System.Collections.Generic;
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
public class PendingUpdates {
  [ProtoMember(1)]
  public SortedSet<NodeQueueItem> Queue = new SortedSet<NodeQueueItem>();
  [ProtoMember(2)]
  public List<NodePos> Ejections = new List<NodePos>();

  public PendingUpdates() {}

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
  PendingUpdates _pendingUpdates = new PendingUpdates();

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
      _pendingUpdates = new PendingUpdates();
      return;
    }
    _pendingUpdates = SerializerUtil.Deserialize<PendingUpdates>(serialized);
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
  public virtual void EnqueueNode(int distance, BlockPos pos, int nodeId) {
    if (Side == EnumAppSide.Client) {
      return;
    }
    System.Diagnostics.Debug.Assert(_accessor.GetDistance(pos, nodeId) ==
                                    distance);
    System.Diagnostics.Debug.Assert(
        !_pendingUpdates.QueueContains(pos, nodeId));
    _pendingUpdates.Queue.Add(new NodeQueueItem(distance, pos, nodeId));
    Debug("Added node to queue. pos=<{0}>:{1} dist={2}", pos, nodeId, distance);
  }

  public int QueueSize {
    get { return _pendingUpdates.Queue.Count; }
  }

  public bool HasPendingWork {
    get { return _pendingUpdates.HasWork; }
  }
  public string QueueDebugString() {
    StringBuilder builder = new StringBuilder();
    builder.AppendLine(
        $"Queue.Count={_pendingUpdates.Queue.Count} Ejections.Count={_pendingUpdates.Ejections.Count}");
    builder.AppendLine("First 5 queue updates:");
    NodeQueueItem[] firstN =
        new NodeQueueItem[Math.Min(_pendingUpdates.Queue.Count, 5)];
    _pendingUpdates.Queue.CopyTo(firstN, 0, firstN.Length);
    for (int i = 0; i < firstN.Length; ++i) {
      builder.AppendLine(firstN[i].ToEscapedString());
    }
    builder.AppendLine("First 5 ejection updates:");
    for (int i = 0; i < Math.Min(5, _pendingUpdates.Ejections.Count); ++i) {
      builder.AppendLine(_pendingUpdates.Ejections[i].ToEscapedString());
    }
    return builder.ToString();
  }

  public bool AreQueueDistancesConsistent() {
    foreach (NodeQueueItem item in _pendingUpdates.Queue) {
      if (item.PropagationDistance == Node.InfDistance) {
        Debug("Node <{0}>:{1} in the queue has infinite distance.", item.Pos,
              item.NodeId);
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
    return true;
  }

  public void Step() {
    if (_pendingUpdates.Queue.Count > 0) {
      System.Diagnostics.Debug.Assert(AreQueueDistancesConsistent());
      NodeQueueItem min = _pendingUpdates.Queue.Min;
      _pendingUpdates.Queue.Remove(min);
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
