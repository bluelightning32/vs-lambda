using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LambdaFactory.Tests;

public struct BlockNodeInfo {
  public BlockNodeTemplate Template;
  public Node[] Nodes;

  public BlockNodeInfo() {}

  public BlockNodeInfo(BlockNodeTemplate template, Node[] nodes) {
    Template = template;
    Nodes = nodes;
  }
}

public class MemoryNodeAccessor : NodeAccessor {
  private readonly Dictionary<BlockPos, BlockNodeInfo> _nodes =
      new Dictionary<BlockPos, BlockNodeInfo>();

  public override BlockNodeTemplate GetBlock(BlockPos pos, out Node[] nodes) {
    BlockNodeInfo value;
    if (!_nodes.TryGetValue(pos, out value)) {
      nodes = null;
      return null;
    }
    nodes = value.Nodes;
    return value.Template;
  }

  public override void SetNode(BlockPos pos, int nodeId, in Node node) {
    _nodes.Get(pos).Nodes[nodeId] = node;
  }

  public void RemoveBlock(BlockPos pos) {
    if (_nodes.Remove(pos, out BlockNodeInfo block)) {
      block.Template.OnRemoved(pos, block.Nodes);
    }
  }

  // `pos` should be treated as immutable.
  public void SetBlock(BlockPos pos, BlockNodeTemplate block) {
    RemoveBlock(pos);
    _nodes[pos] = new BlockNodeInfo(block, block.CreateNodes(pos));
    _nodes[pos].Template.OnPlaced(pos, _nodes[pos].Nodes);
  }

  public void SetBlock(int x, int y, int z, int dimension,
                       BlockNodeTemplate block) {
    BlockPos pos = new(x, y, z, dimension);
    SetBlock(pos, block);
  }

  public void RemoveBlock(int x, int y, int z, int dimension) {
    BlockPos pos = new(x, y, z, dimension);
    RemoveBlock(pos);
  }

  public BlockNodeTemplate GetBlock(int x, int y, int z, int dimension,
                                    out Node[] nodes) {
    BlockPos pos = new(x, y, z, dimension);
    return GetBlock(pos, out nodes);
  }
}