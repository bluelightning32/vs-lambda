using Lambda.Network;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.Tests;

public class BlockNodeInfo {
  public BlockNodeTemplate Template;
  public Node[] Nodes;
  public string[] InventoryImports = Array.Empty<string>();
  public string InventoryTerm;

  public BlockNodeInfo() {}

  public BlockNodeInfo(BlockNodeTemplate template, Node[] nodes,
                       string inventoryTerm = null) {
    Template = template;
    Nodes = nodes;
    InventoryTerm = inventoryTerm;
  }
}

public class MemoryNodeAccessor : NodeAccessor {
  private readonly Dictionary<BlockPos, BlockNodeInfo> _nodes =
      new Dictionary<BlockPos, BlockNodeInfo>();

  public IReadOnlyCollection<BlockPos> GetNonemptyBlocks() {
    return _nodes.Keys;
  }

  public override BlockNodeTemplate GetBlock(BlockPos pos, out Node[] nodes,
                                             out string[] inventoryImports,
                                             out string inventoryTerm) {
    BlockNodeInfo value;
    if (!_nodes.TryGetValue(pos, out value)) {
      nodes = null;
      inventoryImports = Array.Empty<string>();
      inventoryTerm = null;
      return null;
    }
    nodes = value.Nodes;
    inventoryImports = value.InventoryImports;
    inventoryTerm = value.InventoryTerm;
    return value.Template;
  }

  public override void SetNode(BlockPos pos, int nodeId, in Node node) {
    _nodes.Get(pos).Nodes[nodeId] = node;
  }

  public void SetInventory(BlockPos pos, string term) {
    _nodes.Get(pos).InventoryTerm = term;
  }

  public void RemoveBlock(BlockPos pos) {
    if (_nodes.Remove(pos, out BlockNodeInfo block)) {
      block.Template.OnRemoved(pos, block.Nodes);
    }
  }

  // `pos` should be treated as immutable.
  public void SetBlock(BlockPos pos, BlockNodeTemplate block,
                       string inventoryTerm) {
    RemoveBlock(pos);
    _nodes[pos] =
        new BlockNodeInfo(block, block.CreateNodes(pos), inventoryTerm);
    _nodes[pos].Template.OnPlaced(pos, _nodes[pos].Nodes);
  }

  // `pos` should be treated as immutable.
  public void SetBlock(BlockPos pos, BlockNodeTemplate block) {
    SetBlock(pos, block, null);
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

  public string GetTermInventory(int x, int y, int z, int dimension) {
    BlockPos pos = new(x, y, z, dimension);
    GetBlock(pos, out Node[] nodes, out string[] termImports,
             out string termInventory);
    return termInventory;
  }

  // Sets blocks based on the schematic. Blocks outside of the schematic range
  // are not modified.
  //
  // The schematic is a top-down view. The first character of
  // the schematic corresponds to the block at `topleft`. Moving right in the
  // schematic corresponds to moving east (bigger x) in the world. Moving down
  // in the schematic corresponds to moving south (bigger z) in the world.
  //
  // Characters are mapped to blocks according to `legend`, with 3 exceptions:
  // 1. '\n' is used to advance the z direction.
  // 2. '\r' is ignored.
  // 3. The sequence '/*' starts a comment. The commend ends with '*/'. All
  // characters within the comment (including the starting '/*' and ending '*/)
  // are treated as spaces, except for newlines.
  //
  // The values of the legend are tuples for
  // the block template and its inventory term. If the character maps to null in
  // the legend, then the block is removed from the world. If the character is
  // missing from the legend, then an KeyNotFoundException is thrown.
  public void SetSchematic(BlockPos topleft, Legend legend, string schematic) {
    BlockPos pos = topleft.Copy();
    bool comment = false;
    int commentStart = 0;
    for (int i = 0; i < schematic.Length; ++i) {
      char c = schematic[i];
      if (c == '\r') {
        continue;
      }
      if (c == '\n') {
        ++pos.Z;
        pos.X = topleft.X;
        continue;
      }
      if (comment) {
        if (c == '/' && i > 0 && schematic[i - 1] == '*' &&
            i - commentStart >= 3) {
          comment = false;
        }
        c = ' ';
      } else {
        if (c == '/' && i + 1 < schematic.Length && schematic[i + 1] == '*') {
          comment = true;
          commentStart = i;
          c = ' ';
        }
      }
      Tuple<BlockNodeTemplate, string> block = legend.Dict[c];
      if (block == null) {
        RemoveBlock(pos);
      } else {
        SetBlock(pos.Copy(), block.Item1, block.Item2);
        block.Item1.FinishPendingWork();
      }
      ++pos.X;
    }
  }
}
