using System.Collections.Generic;

using LambdaFactory.Network;

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
  public AutoStepManager ScopeNetworkManager { get; private set; }
  public AutoStepManager MatchNetworkManager { get; private set; }
  public BlockEntityBehavior.TermNetwork.Manager TermNetworkManager {
    get; private set;
  }

  // Indexed by behavior name
  private readonly Dictionary<string, AutoStepManager> _networkManagers =
      new Dictionary<string, AutoStepManager>();
  // Indexed by behavior name
  public IReadOnlyDictionary<string, AutoStepManager> NetworkManagers {
    get { return _networkManagers; }
  }

  public static LambdaFactoryModSystem GetInstance(ICoreAPI api) {
    return ObjectCacheUtil.GetOrCreate(
        api, $"lambdafactory",
        () => api.ModLoader.GetModSystem<LambdaFactoryModSystem>());
  }

  public override void Start(ICoreAPI api) {
    Domain = Mod.Info.ModID;
    api.RegisterCollectibleBehaviorClass("Term",
                                         typeof(CollectibleBehavior.Term));
    api.RegisterBlockBehaviorClass("BlockEntityForward",
                                   typeof(BlockBehavior.BlockEntityForward));
    api.RegisterBlockBehaviorClass("Connect", typeof(BlockBehavior.Connect));
    api.RegisterBlockBehaviorClass("Inventory",
                                   typeof(BlockBehavior.Inventory));
    api.RegisterBlockBehaviorClass("Network", typeof(BlockBehavior.Network));
    api.RegisterBlockBehaviorClass("Orient", typeof(BlockBehavior.Orient));
    api.RegisterBlockBehaviorClass("Port", typeof(BlockBehavior.Port));
    api.RegisterBlockEntityClass("TermContainer",
                                 typeof(BlockEntity.TermContainer));
    api.RegisterBlockEntityBehaviorClass(
        "AcceptPort", typeof(BlockEntityBehavior.AcceptPort));
    api.RegisterBlockEntityBehaviorClass("CacheMesh",
                                         typeof(BlockEntityBehavior.CacheMesh));
    api.RegisterBlockEntityBehaviorClass(
        BlockEntityBehavior.ScopeNetwork.Name,
        typeof(BlockEntityBehavior.ScopeNetwork));
    api.RegisterBlockEntityBehaviorClass(
        BlockEntityBehavior.MatchNetwork.Name,
        typeof(BlockEntityBehavior.MatchNetwork));
    api.RegisterBlockEntityBehaviorClass(
        BlockEntityBehavior.TermNetwork.Name,
        typeof(BlockEntityBehavior.TermNetwork));
    api.RegisterBlockEntityBehaviorClass(
        "Wire", typeof(BlockEntityBehavior.BEBehaviorWire));
    _networkManagers[BlockEntityBehavior.ScopeNetwork.Name] =
        ScopeNetworkManager =
            new BlockEntityBehavior.ScopeNetwork.Manager(api.World);
    _networkManagers[BlockEntityBehavior.MatchNetwork.Name] =
        MatchNetworkManager =
            new BlockEntityBehavior.MatchNetwork.Manager(api.World);
    _networkManagers[BlockEntityBehavior.TermNetwork.Name] =
        TermNetworkManager =
            new BlockEntityBehavior.TermNetwork.Manager(api.World);
  }

  public void RegisterNetworkDebugCommands(IChatCommandApi api, string name,
                                           AutoStepManager manager) {
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
