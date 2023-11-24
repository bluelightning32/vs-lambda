using System.Linq.Expressions;

using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace LambdaFactory {
  public class BlockEntityScope : BlockEntity {
    public BlockEntityScope() {
    }

    public override void Initialize(ICoreAPI api) {
      api.Logger.Notification($"lambda: Initialize {GetHashCode()}");
      base.Initialize(api);
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator) {
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
  }
}
