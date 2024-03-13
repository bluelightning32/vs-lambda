using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Lambda.Network.BlockBehaviors;

public interface IConnectable {
  Manager GetManager(ICoreAPI api);
  bool CanAddEdge(NetworkType network, Edge edge, out NodePos source);
  void AddEdge(NetworkType network, Edge edge);
  void RemoveEdge(NetworkType network, Edge edge);
}

// Asks the neighbor blocks to modify their BlockNodeTemplates to add edges to
// the newly placed block. Also adds edges on the newly placed block to the
// neighboring blocks.
//
// This is used by the wire blocks, where they actually add edges when placed.
// For other blocks, the possible edges are static, and the edges are either
// paired or unpaired depending on whether the neighboring block reciprocates.
public class AutoConnect : BlockBehavior {
  private bool _singleConnect = false;
  // Disconnect the edge on this block when a neighbor loses its corresponding
  // edge, even if the neighbor does not have the connect behavior.
  private bool _disconnectOnNeighborChange = false;
  // When this block is broken, disconnect any auto connectible neighbors.
  private bool _disconnectOnBreak;
  private NetworkType[] _networks;
  public AutoConnect(Block block) : base(block) {}

  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);
    _singleConnect = properties["singleConnect"].AsBool();
    _disconnectOnNeighborChange =
        properties["disconnectOnNeighborChange"].AsBool();
    _disconnectOnBreak = properties["disconnectOnBreak"].AsBool(true);
    _networks =
        properties["networks"].AsArray<NetworkType>(new[] { NetworkType.Term });
  }

  public override void OnNeighbourBlockChange(IWorldAccessor world,
                                              BlockPos pos, BlockPos neibpos,
                                              ref EnumHandling handling) {
    if (_disconnectOnNeighborChange) {
      IConnectable connectable = block.GetInterface<IConnectable>(world, pos);
      if (connectable != null) {
        foreach (BlockFacing face in BlockFacing.ALLFACES) {
          BlockPos neighborPos = pos.AddCopy(face);
          foreach (NetworkType network in _networks) {
            if (!connectable.GetManager(world.Api).GetSource(
                    neighborPos, network,
                    EdgeExtension.GetFaceCenter(face.Opposite),
                    out NodePos source)) {
              connectable.RemoveEdge(network,
                                     EdgeExtension.GetFaceCenter(face));
            }
          }
        }
      }
    }

    base.OnNeighbourBlockChange(world, pos, neibpos, ref handling);
  }

  public override void OnBlockBroken(IWorldAccessor world, BlockPos pos,
                                     IPlayer byPlayer,
                                     ref EnumHandling handling) {
    base.OnBlockBroken(world, pos, byPlayer, ref handling);
    foreach (BlockFacing face in BlockFacing.ALLFACES) {
      BlockPos neighborPos = pos.AddCopy(face);
      IConnectable connectable =
          block.GetInterface<IConnectable>(world, neighborPos);
      if (connectable == null) {
        continue;
      }
      foreach (NetworkType network in _networks) {
        connectable.RemoveEdge(network,
                               EdgeExtension.GetFaceCenter(face.Opposite));
      }
    }
  }

  public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer,
                                    BlockSelection blockSel,
                                    ItemStack byItemStack,
                                    ref EnumHandling handling) {
    world.BlockAccessor.SetBlock(block.BlockId, blockSel.Position, byItemStack);

    IConnectable connectable =
        block.GetInterface<IConnectable>(world, blockSel.Position);
    if (connectable != null) {
      BlockFacing preferredFace = blockSel.Face.Opposite;
      bool found = false;
      foreach (NetworkType network in _networks) {
        if (connectable.GetManager(world.Api).IsBlockInNetwork(
                blockSel.Position.AddCopy(preferredFace), network)) {
          found = true;
          break;
        }
      }

      if (!found) {
        preferredFace =
            BlockFacing.HorizontalFromAngle(byPlayer.Entity.Pos.Yaw);
      }
      TryAddEdge(world, connectable, blockSel.Position,
                 EdgeExtension.GetFaceCenter(preferredFace));
      if (!_singleConnect &&
          !byItemStack.Attributes.GetAsBool("singleconnect", false)) {
        foreach (BlockFacing face in BlockFacing.ALLFACES) {
          if (face != preferredFace) {
            TryAddEdge(world, connectable, blockSel.Position,
                       EdgeExtension.GetFaceCenter(face));
          }
        }
      }
    }
    handling = EnumHandling.PreventSubsequent;
    return true;
  }

  private void TryAddEdge(IWorldAccessor world, IConnectable connectable,
                          BlockPos pos, Edge edge) {
    foreach (NetworkType network in _networks) {
      TryAddEdge(world, connectable, pos, network, edge);
    }
  }

  private static void TryAddEdge(IWorldAccessor world, IConnectable connectable,
                                 BlockPos pos, NetworkType network, Edge edge) {
    if (!connectable.CanAddEdge(network, edge, out NodePos source1)) {
      return;
    }
    BlockPos neighborPos = pos.AddCopy(edge.GetFace());
    if (!connectable.GetManager(world.Api).GetSource(
            neighborPos, network, edge.GetOpposite(), out NodePos source2)) {
      IConnectable neighbor =
          world.BlockAccessor.GetBlock(neighborPos)
              .GetInterface<IConnectable>(world, neighborPos);
      if (neighbor == null) {
        return;
      }
      if (!neighbor.CanAddEdge(network, edge.GetOpposite(), out source2)) {
        return;
      }
      if (source1.IsSet() && source2.IsSet() && source1 != source2) {
        return;
      }
      neighbor.AddEdge(network, edge.GetOpposite());
      connectable.AddEdge(network, edge);
      return;
    }
    if (source1.IsSet() && source2.IsSet() && source1 != source2) {
      return;
    }
    connectable.AddEdge(network, edge);
  }
}
