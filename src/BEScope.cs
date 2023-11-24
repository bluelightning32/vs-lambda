using System.Xml.Schema;

using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace LambdaFactory;

public class BlockEntityScope : BlockEntity, IBlockEntityForward {
  public override void Initialize(ICoreAPI api) {
    api.Logger.Notification($"lambda: Initialize {GetHashCode()}");
    base.Initialize(api);
  }

  public override bool OnTesselation(ITerrainMeshPool mesher,
                                     ITesselatorAPI tessThreadTesselator) {
    Api.Logger.Notification($"lambda: OnTesselation {GetHashCode()}");
    return base.OnTesselation(mesher, tessThreadTesselator);
  }

  public override void OnBlockPlaced(ItemStack byItemStack = null) {
    Api.Logger.Notification($"lambda: OnBlockPlaced {GetHashCode()}");
    base.OnBlockPlaced(byItemStack);
  }

  public override void OnExchanged(Block block) {
    Api.Logger.Notification($"lambda: OnExchanged {GetHashCode()}");
    base.OnExchanged(block);
  }

  void IBlockEntityForward.OnNeighbourBlockChange(
      Vintagestory.API.MathTools.BlockPos neibpos,
      ref Vintagestory.API.Common.EnumHandling handling) {
    Block[] decors = Api.World.BlockAccessor.GetDecors(Pos);
    int nonnull = 0;
    foreach (Block decor in decors) {
      if (decor != null) {
        ++nonnull;
      }
    }

    Api.Logger.Notification(
        $"lambda: IBlockEntityForward.OnNeighbourBlockChange {GetHashCode()} mypos {Pos} neighpos {neibpos} nonnull decors {nonnull}");
  }
}
