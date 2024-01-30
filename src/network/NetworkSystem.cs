using System;
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

  // Indexed by behavior name
  private readonly Dictionary<string, Type> _networkBlockEntityBehaviors =
      new();
  // Indexed by behavior name
  public IReadOnlyDictionary<string, Type> NetworkBlockEntityBehaviors {
    get { return _networkBlockEntityBehaviors; }
  }

  public static NetworkSystem GetInstance(ICoreAPI api) {
    return api.GetCachedModSystem<NetworkSystem>();
  }

  public void RegisterNetworkBlockEntityBehavior(ICoreAPI api, string name,
                                                 Type blockEntityBehaviorType) {
    _networkBlockEntityBehaviors.Add(name, blockEntityBehaviorType);
    api.RegisterBlockEntityBehaviorClass(name, blockEntityBehaviorType);
  }
  // Change the value of the setting with ".clientconfig lambdaShowMaxNodes
  // value".
  public static readonly string ShowMaxNodesName = "lambdaShowMaxNodes";

  public override void Start(ICoreAPI api) {
    if (api is ICoreClientAPI capi) {
      if (!capi.Settings.Int.Exists(ShowMaxNodesName)) {
        capi.Settings.Int[ShowMaxNodesName] = 10;
      }
    }
    api.RegisterBlockBehaviorClass("AutoConnect",
                                   typeof(BlockBehavior.AutoConnect));
    api.RegisterBlockBehaviorClass("Network", typeof(BlockBehavior.Network));
    api.RegisterBlockBehaviorClass("Port", typeof(BlockBehavior.Port));
    RegisterNetworkBlockEntityBehavior(
        api, "TokenEmitter", typeof(BlockEntityBehavior.TokenEmitter));
    RegisterNetworkBlockEntityBehavior(api, "AcceptPort",
                                       typeof(BlockEntityBehavior.AcceptPort));
    RegisterNetworkBlockEntityBehavior(api, "Wire",
                                       typeof(BlockEntityBehavior.Wire));
    TokenEmitterManager =
        new BlockEntityBehavior.TokenEmitter.Manager(api.World);
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
    RegisterNetworkDebugCommands(api.ChatCommands,
                                 TokenEmitterManager.GetNetworkName(),
                                 TokenEmitterManager);
  }

  public override void Dispose() { base.Dispose(); }
}
