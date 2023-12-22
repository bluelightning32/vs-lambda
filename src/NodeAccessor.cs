using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LambdaFactory;

public abstract class NodeAccessor {
  // Gets the node and its template at `pos`. If the node does not exist, then
  // null is returned.
  public NodeTemplate GetNode(NodePos pos, out Node node) {
    Node[] nodes;
    BlockNodeTemplate block = GetBlock(pos.Block, out nodes);
    if (block == null) {
      node = new Node();
      return null;
    }
    node = nodes[pos.NodeId];
    return block.GetNodeTemplate(pos.NodeId);
  }

  public abstract BlockNodeTemplate GetBlock(BlockPos pos, out Node[] nodes);

  public abstract void SetNode(NodePos pos, in Node node);
}
