using System.Collections.Generic;
using System.Dynamic;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace LambdaFactory;

public class LambdaFactoryModSystem : ModSystem {
  public static string Domain { get; private set; }

  // Storing this field in the mod ensures that there is once instance per API
  // instance. These fields cannot be stored in the object cache, because the
  // object cache can be cleared with a command.
  public BEBehaviorNetwork.Manager NetworkManager { get; private set; }

  private readonly Dictionary<string, AutoStepNetworkManager> _networkManagers =
      new Dictionary<string, AutoStepNetworkManager>();
  public IReadOnlyDictionary<string, AutoStepNetworkManager> NetworkManagers {
    get { return _networkManagers; }
  }

  public static LambdaFactoryModSystem GetInstance(ICoreAPI api) {
    return ObjectCacheUtil.GetOrCreate(
        api, $"lambdafactory",
        () => api.ModLoader.GetModSystem<LambdaFactoryModSystem>());
  }

  public override void Start(ICoreAPI api) {
    Domain = Mod.Info.ModID;
    api.RegisterBlockBehaviorClass("BlockEntityForward",
                                   typeof(BlockBehaviorBlockEntityForward));
    api.RegisterBlockBehaviorClass("Network", typeof(BlockBehaviorNetwork));
    api.RegisterBlockEntityClass("CacheMesh", typeof(BlockEntityCacheMesh));
    api.RegisterBlockEntityClass("Wire", typeof(BlockEntityWire));
    api.RegisterBlockEntityBehaviorClass(BEBehaviorNetwork.Name,
                                         typeof(BEBehaviorNetwork));
    api.RegisterBlockEntityBehaviorClass("AcceptPorts",
                                         typeof(BEBehaviorAcceptPorts));
    BlockEntityWire.OnModLoaded();
    _networkManagers[BEBehaviorNetwork.Name] = NetworkManager =
        new BEBehaviorNetwork.Manager(api.World);
  }

  public void RegisterNetworkDebugCommands(IChatCommandApi api, string name,
                                           AutoStepNetworkManager manager) {
    IChatCommand network =
        api.GetOrCreate("debug").BeginSubCommand(name).WithDescription(
            "Debug commands for network propagation in the Lambda mod.");

    network.BeginSubCommand("togglesinglestep")
        .WithDescription(
            "Toggles whether the network propagation should run automatically, or only one step each time singlestep is run.")
        .HandleWith((args) => {
          manager.ToggleSingleStep();
          return TextCommandResult.Success(
              $"SingleStep set to {manager.SingleStep}.");
        })
        .EndSubCommand();

    network.BeginSubCommand("pending")
        .WithDescription("Prints out the next items in the queue.")
        .HandleWith((args) => {
          return TextCommandResult.Success(manager.QueueDebugString());
        })
        .EndSubCommand();

    network.BeginSubCommand("step")
        .WithDescription("Advances one step in single step mode.")
        .HandleWith((args) => {
          if (manager.SingleStep) {
            manager.Step();
            return TextCommandResult.Success(
                $"Step complete. HasPendingWork={manager.HasPendingWork}.");
          } else {
            return TextCommandResult.Error(
                "The step command is only valid in single step mode.");
          }
        })
        .EndSubCommand();

    network.EndSubCommand();
  }

  public override void StartClientSide(ICoreClientAPI api) {}

  public override void StartServerSide(ICoreServerAPI api) {
    RegisterNetworkDebugCommands(api.ChatCommands, "netowrk", NetworkManager);
  }
}
