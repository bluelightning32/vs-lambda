using Vintagestory.API.Common;
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

  public override void SetNode(NodePos pos, in Node node) {
    _nodes.Get(pos.Block).Nodes[pos.NodeId] = node;
  }

  public void SetBlock(BlockPos pos, BlockNodeTemplate block) {
    _nodes[pos] = new BlockNodeInfo(block, block.CreateNodes(pos));
  }

  public void SetBlock(int x, int y, int z, int dimension,
                       BlockNodeTemplate block) {
    BlockPos pos = new(x, y, z, dimension);
    SetBlock(pos, block);
  }
}