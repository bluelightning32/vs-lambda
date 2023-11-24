using System.Linq.Expressions;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LambdaFactory {
  public class BlockScope : Block {
    public BlockScope() {
    }
    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos) {
      Block[] decors = world.BlockAccessor.GetDecors(pos);
      int nonnull=0;
      foreach (Block decor in decors) {
        if (decor != null) ++nonnull;
      }
      api.Logger.Notification($"lambda: OnNeighbourBlockChange {GetHashCode()} mypos {pos} neighpos {neibpos} nonnull decors {nonnull}");
      base.OnNeighbourBlockChange(world, pos, neibpos);
    }
  }
}