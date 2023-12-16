using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace LambdaFactory;

public class LambdaFactoryModSystem : ModSystem {
  public static string Domain;
  public override void Start(ICoreAPI api) {
    Domain = Mod.Info.ModID;
    api.RegisterBlockBehaviorClass("BlockEntityForward",
                                   typeof(BlockBehaviorBlockEntityForward));
    api.RegisterBlockEntityClass("CacheMesh", typeof(BlockEntityCacheMesh));
    api.RegisterBlockEntityClass("Scope",
                                 typeof(BlockEntityScope<ScopeCacheKey>));
    api.RegisterBlockEntityClass("Wire", typeof(BlockEntityWire));
    api.RegisterBlockEntityBehaviorClass("Corner", typeof(BEBehaviorCorner));
    api.RegisterBlockEntityBehaviorClass("DoubleScope",
                                         typeof(BEBehaviorDoubleScope));
    api.RegisterBlockEntityBehaviorClass("PortHole",
                                         typeof(BEBehaviorPortHole));
    BlockEntityWire.OnModLoaded();
  }

  public override void StartClientSide(ICoreClientAPI api) {}

  public override void StartServerSide(ICoreServerAPI api) {}
}
