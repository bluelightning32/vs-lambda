using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LambdaFactory;

// The BlockEntity should implement this interface.
public interface IBlockEntityForward {
  public void OnNeighbourBlockChange(BlockPos neibpos,
                                     ref EnumHandling handling) {}

  public ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos,
                               ref EnumHandling handling) {
    return null;
  }
}

// Forwards more methods from the Block to the BlockEntity.
public class BlockBehaviorBlockEntityForward : BlockBehavior {
  public BlockBehaviorBlockEntityForward(Block block) : base(block) {}

  private IBlockEntityForward GetForward(IWorldAccessor world, BlockPos pos) {
    if (block.EntityClass == null)
      return null;
    return world.BlockAccessor.GetBlockEntity(pos) as IBlockEntityForward;
  }

  public override void OnNeighbourBlockChange(IWorldAccessor world,
                                              BlockPos pos, BlockPos neibpos,
                                              ref EnumHandling handling) {
    base.OnNeighbourBlockChange(world, pos, neibpos, ref handling);
    // The base sets handling to EnumHandling.PassThrough. So call the block
    // entity afterwards so that it has a chance to override it.
    GetForward(world, pos)?.OnNeighbourBlockChange(neibpos, ref handling);
  }

  public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos,
                                        ref EnumHandling handling) {
    handling = EnumHandling.PassThrough;
    return GetForward(world, pos)?.OnPickBlock(world, pos, ref handling);
  }
}
