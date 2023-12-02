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
    api.RegisterBlockEntityClass("Scope",
                                 typeof(BlockEntityScope<ScopeCacheKey>));
    api.RegisterBlockEntityClass("Wire", typeof(BlockEntityWire));
  }

  public override void StartClientSide(ICoreClientAPI api) {}

  public override void StartServerSide(ICoreServerAPI api) {}
}
