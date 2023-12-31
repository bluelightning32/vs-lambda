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
  public AutoStepNetworkManager ScopeNetworkManager { get; private set; }
  public AutoStepNetworkManager MatchNetworkManager { get; private set; }
  public BEBehaviorTermNetwork.Manager TermNetworkManager { get; private set; }

  // Indexed by behavior name
  private readonly Dictionary<string, AutoStepNetworkManager> _networkManagers =
      new Dictionary<string, AutoStepNetworkManager>();
  // Indexed by behavior name
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
    api.RegisterCollectibleBehaviorClass("Term", typeof(BehaviorTerm));
    api.RegisterBlockBehaviorClass("BlockEntityForward",
                                   typeof(BlockBehaviorBlockEntityForward));
    api.RegisterBlockBehaviorClass("Connect", typeof(BlockBehaviorConnect));
    api.RegisterBlockBehaviorClass("Network", typeof(BlockBehaviorNetwork));
    api.RegisterBlockBehaviorClass("Orient", typeof(BlockBehaviorOrient));
    api.RegisterBlockBehaviorClass("Port", typeof(BlockBehaviorPort));
    api.RegisterBlockEntityClass("TermContainer",
                                 typeof(BlockEntityTermContainer));
    api.RegisterBlockEntityBehaviorClass("AcceptPorts",
                                         typeof(BEBehaviorAcceptPorts));
    api.RegisterBlockEntityBehaviorClass("CacheMesh",
                                         typeof(BEBehaviorCacheMesh));
    api.RegisterBlockEntityBehaviorClass(BEBehaviorScopeNetwork.Name,
                                         typeof(BEBehaviorScopeNetwork));
    api.RegisterBlockEntityBehaviorClass(BEBehaviorMatchNetwork.Name,
                                         typeof(BEBehaviorMatchNetwork));
    api.RegisterBlockEntityBehaviorClass(BEBehaviorTermNetwork.Name,
                                         typeof(BEBehaviorTermNetwork));
    api.RegisterBlockEntityBehaviorClass("Wire", typeof(BEBehaviorWire));
    _networkManagers[BEBehaviorScopeNetwork.Name] = ScopeNetworkManager =
        new BEBehaviorScopeNetwork.Manager(api.World);
    _networkManagers[BEBehaviorMatchNetwork.Name] = MatchNetworkManager =
        new BEBehaviorMatchNetwork.Manager(api.World);
    _networkManagers[BEBehaviorTermNetwork.Name] = TermNetworkManager =
        new BEBehaviorTermNetwork.Manager(api.World);
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
    foreach (var manager in _networkManagers) {
      RegisterNetworkDebugCommands(
          api.ChatCommands, manager.Value.GetNetworkName(), manager.Value);
    }
  }
}
