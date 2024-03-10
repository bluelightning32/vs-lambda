using Vintagestory.API.MathTools;

namespace Lambda.Network;

public abstract class NodeAccessor {
  // Gets the node and its template at `pos`. If the node does not exist, then
  // null is returned.
  public virtual NodeTemplate GetNode(BlockPos pos, int nodeId, out Node node) {
    BlockNodeTemplate block = GetBlock(pos, out Node[] nodes);
    if (block == null) {
      node = new Node();
      return null;
    }
    node = nodes[nodeId];
    return block.GetNodeTemplate(nodeId);
  }

  // Returns the node in `pos` that contains `edge`. Returns
  // null if the block does not exist or does not contain the edge.
  public virtual NodeTemplate GetNode(BlockPos pos, NetworkType network,
                                      Edge edge, out Node node) {
    BlockNodeTemplate block = GetBlock(pos, out Node[] nodes);
    if (block == null) {
      node = new Node();
      return null;
    }
    NodeTemplate template = block.GetNodeTemplate(network, edge);
    if (template == null) {
      node = new Node();
      return null;
    }
    node = nodes[template.Id];
    return template;
  }

  public abstract BlockNodeTemplate GetBlock(BlockPos pos, out Node[] nodes,
                                             out string[] inventoryImports,
                                             out string inventoryTerm);

  public virtual BlockNodeTemplate GetBlock(BlockPos pos, out Node[] nodes) {
    return GetBlock(pos, out nodes, out string[] inventoryImports,
                    out string inventoryTerm);
  }

  // Set the contents of the node at `pos`:`nodeId` to `node`. The caller must
  // ensure that `nodeId` is valid for the NodeTemplate at the location. The
  // caller must treat `pos` as immutable.
  public abstract void SetNode(BlockPos pos, int nodeId, in Node node);

  public virtual int GetDistance(BlockPos pos, int nodeId) {
    NodeTemplate template = GetNode(pos, nodeId, out Node node);
    return node.PropagationDistance;
  }

  public virtual NodePos GetSource(BlockPos pos, int nodeId) {
    NodeTemplate template = GetNode(pos, nodeId, out Node node);
    return node.Source;
  }

  public int FindNodeId(BlockPos pos, string name) {
    BlockNodeTemplate block = GetBlock(pos, out Node[] nodes);
    if (block == null) {
      return -1;
    }
    return block.FindNodeTemplate(name);
  }
}
