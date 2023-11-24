using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace LambdaFactory;

public class LambdaFactoryModSystem : ModSystem {
  public override void Start(ICoreAPI api) {
    api.RegisterBlockClass("Scope", typeof(BlockScope));
    api.RegisterBlockEntityClass("Scope", typeof(BlockEntityScope));
  }

  public override void StartClientSide(ICoreClientAPI api) {}

  public override void StartServerSide(ICoreServerAPI api) {}
}
