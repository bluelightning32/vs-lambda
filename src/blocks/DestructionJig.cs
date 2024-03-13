using System.Text;

using Lambda.CollectibleBehavior;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Lambda.Blocks;

using VSBlockBehavior = Vintagestory.API.Common.BlockBehavior;
using VSBlockEntity = Vintagestory.API.Common.BlockEntity;

public class DestructionJig : BlockLiquidContainerBase {
  // This is the slot for liquids
  public override int ContainerSlotId => 0;

  private void DropContainerWithoutContents(IWorldAccessor world, BlockPos pos,
                                            IPlayer player) {
    ItemStack container = new(this);
    world.SpawnItemEntity(
        container, new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);

    world.PlaySoundAt(Sounds.GetBreakSound(player), pos.X, pos.Y, pos.Z,
                      player);
  }

  // BlockLiquidContainerBase inheris from BlockContainer, and
  // BlockContainer.OnBlockBroken drops the block with its contents intact, then
  // it calls the block entity. The destruction jig's block entity inherits from
  // BlockEntityContainer, and its OnBlockBroken drops the inventory. So by
  // default these two classes would duplicate the inventory.
  //
  // So instead this class overrides the function to drop the block not holding
  // its contents, then calls the block entity to drop the contents.
  //
  // One side effect is that the liquid contents are lost, but the barrel has
  // the same behavior.
  public override void OnBlockBroken(IWorldAccessor world, BlockPos pos,
                                     IPlayer player,
                                     float dropQuantityMultiplier) {
    EnumHandling handled = EnumHandling.PassThrough;
    ForwardToBlock.WalkBlockBehaviors(
        world.BlockAccessor, this, pos,
        (VSBlockBehavior behavior, ref EnumHandling handled) =>
            behavior.OnBlockBroken(world, pos, player, ref handled),
        (Block block, ref EnumHandling handled) => {
          if (world.Side == EnumAppSide.Server &&
              (player?.WorldData.CurrentGameMode != EnumGameMode.Creative)) {
            DropContainerWithoutContents(world, pos, player);
          }
        },
        (VSBlockEntity entity, ref EnumHandling handled) =>
            entity.OnBlockBroken(),
        ref handled);
    if (handled != EnumHandling.PreventDefault &&
        handled != EnumHandling.PreventSubsequent) {
      world.BlockAccessor.SetBlock(0, pos);
    }
  }

  // Override BlockLiquidContainerBase to restore the grand base's behavior of calling the BlockEntity to get the placed info.
  public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer) {
    VSBlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
    StringBuilder sb = new();
    be?.GetBlockInfo(forPlayer, sb);
    return sb.ToString();
  }
}
