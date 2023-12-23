using System;
using System.Collections.Generic;

using ProtoBuf;

using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace LambdaFactory;

public struct NodeQueueItem : IComparable<NodeQueueItem> {
  public int PropagationDistance = Int32.MaxValue;
  public NodePos pos;

  public NodeQueueItem() {}

  public int CompareTo(NodeQueueItem other) {
    if (PropagationDistance != other.PropagationDistance) {
      return PropagationDistance - other.PropagationDistance;
    }
    if (pos.Block.X != other.pos.Block.X) {
      return pos.Block.X - other.pos.Block.X;
    }
    if (pos.Block.Y != other.pos.Block.Y) {
      return pos.Block.Y - other.pos.Block.Y;
    }
    if (pos.Block.Z != other.pos.Block.Z) {
      return pos.Block.Z - other.pos.Block.Z;
    }
    if (pos.Block.dimension != other.pos.Block.dimension) {
      return pos.Block.dimension - other.pos.Block.dimension;
    }
    return pos.NodeId - other.pos.NodeId;
  }
}

[ProtoContract]
public class PendingUpdates {
  [ProtoMember(1)]
  public SortedSet<NodeQueueItem> Queue = new SortedSet<NodeQueueItem>();
  [ProtoMember(2)]
  public List<NodePos> Ejections = new List<NodePos>();

  public PendingUpdates() {}
}

public class NetworkManager {
  PendingUpdates _pendingUpdates = new PendingUpdates();

  private readonly int _distanceIncrementVariance = 18;

  public int DefaultDistanceIncrement {
    get { return 1 + (_distanceIncrementVariance >> 1); }
  }

  public readonly NodeAccessor Accessor;

  public readonly EnumAppSide Side;

  private readonly ILogger _logger;

  public NetworkManager(EnumAppSide side, ILogger logger,
                        NodeAccessor accessor) {
    Side = side;
    _logger = logger;
    Accessor = accessor;
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
}
