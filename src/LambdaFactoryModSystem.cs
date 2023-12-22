using System.Dynamic;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace LambdaFactory;

public class LambdaFactoryModSystem : ModSystem {
  public static string Domain { get; private set; }
  public BEBehaviorNetwork.Manager NetworkManager { get; private set; }

  public override void Start(ICoreAPI api) {
    Domain = Mod.Info.ModID;
    api.RegisterBlockBehaviorClass("BlockEntityForward",
                                   typeof(BlockBehaviorBlockEntityForward));
    api.RegisterBlockBehaviorClass("Network", typeof(BlockBehaviorNetwork));
    api.RegisterBlockEntityClass("CacheMesh", typeof(BlockEntityCacheMesh));
    api.RegisterBlockEntityClass("Scope",
                                 typeof(BlockEntityScope<ScopeCacheKey>));
    api.RegisterBlockEntityClass("Wire", typeof(BlockEntityWire));
    api.RegisterBlockEntityBehaviorClass("Corner", typeof(BEBehaviorCorner));
    api.RegisterBlockEntityBehaviorClass("DoubleScope",
                                         typeof(BEBehaviorDoubleScope));
    api.RegisterBlockEntityBehaviorClass(BEBehaviorNetwork.Name,
                                         typeof(BEBehaviorNetwork));
    api.RegisterBlockEntityBehaviorClass("PortHole",
                                         typeof(BEBehaviorPortHole));
    api.RegisterBlockEntityBehaviorClass("Scope", typeof(BEBehaviorScope));
    BlockEntityWire.OnModLoaded();
    NetworkManager = new BEBehaviorNetwork.Manager(api.World);
  }

  public override void StartClientSide(ICoreClientAPI api) {}

  public override void StartServerSide(ICoreServerAPI api) {}
}
