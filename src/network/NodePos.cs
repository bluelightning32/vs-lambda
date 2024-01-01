using System;

using Vintagestory.API.MathTools;

namespace LambdaFactory.Network;

public struct NodePos : IEquatable<NodePos> {
  public BlockPos Block;
  public int NodeId;

  public NodePos(BlockPos block, int nodeId) {
    Block = block;
    NodeId = nodeId;
  }

  public bool IsSet() { return Block != null; }

  public readonly bool Equals(NodePos other) {
    if (Block == null) {
      if (other.Block != null) {
        return false;
      }
    } else {
      if (Block != other.Block) {
        return false;
      }
    }
    return NodeId == other.NodeId;
  }

  public static bool operator ==(NodePos left, NodePos right) {
    return left.Equals(right);
  }

  public static bool operator !=(NodePos left, NodePos right) {
    return !left.Equals(right);
  }

  public override readonly bool Equals(object obj) {
    return obj is NodePos && Equals((NodePos)obj);
  }

  public override readonly int GetHashCode() {
    return (Block?.GetHashCode() ?? 0) ^ (NodeId << 6);
  }

  public override readonly string ToString() { return $"<{Block}>:{NodeId}"; }

  public readonly string ToEscapedString() {
    return $"&lt;{Block}&gt;:{NodeId}";
  }
}
