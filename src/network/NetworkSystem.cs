using System.Collections.Generic;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Lambda.Network;

public class NetworkSystem : ModSystem {
  // Storing this field in the mod ensures that there is once instance per API
  // instance. These fields cannot be stored in the object cache, because the
  // object cache can be cleared with a command.
  public AutoStepManager TokenEmitterManager { get; private set; }
  public BlockEntityBehavior.TermNetwork.Manager TermNetworkManager {
    get; private set;
  }

  // Indexed by behavior name
  private readonly Dictionary<string, AutoStepManager> _networkManagers = new();
  // Indexed by behavior name
  public IReadOnlyDictionary<string, AutoStepManager> NetworkManagers {
    get { return _networkManagers; }
  }

  public static NetworkSystem GetInstance(ICoreAPI api) {
    return api.GetCachedModSystem<NetworkSystem>();
  }

  public override void Start(ICoreAPI api) {
    api.RegisterBlockBehaviorClass("AutoConnect",
                                   typeof(BlockBehavior.AutoConnect));
    api.RegisterBlockBehaviorClass("Network", typeof(BlockBehavior.Network));
    api.RegisterBlockBehaviorClass("Port", typeof(BlockBehavior.Port));
    api.RegisterBlockEntityBehaviorClass(
        BlockEntityBehavior.TokenEmitter.Name,
        typeof(BlockEntityBehavior.TokenEmitter));
    api.RegisterBlockEntityBehaviorClass(
        BlockEntityBehavior.TermNetwork.Name,
        typeof(BlockEntityBehavior.TermNetwork));
    api.RegisterBlockEntityBehaviorClass(
        "AcceptPort", typeof(BlockEntityBehavior.AcceptPort));
    api.RegisterBlockEntityBehaviorClass("Wire",
                                         typeof(BlockEntityBehavior.Wire));
    _networkManagers[BlockEntityBehavior.TokenEmitter.Name] =
        TokenEmitterManager =
            new BlockEntityBehavior.TokenEmitter.Manager(api.World);
    _networkManagers[BlockEntityBehavior.TermNetwork.Name] =
        TermNetworkManager =
            new BlockEntityBehavior.TermNetwork.Manager(api.World);
  }

  public static void RegisterNetworkDebugCommands(IChatCommandApi api,
                                                  string name,
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

  public override void Dispose() { base.Dispose(); }
}
