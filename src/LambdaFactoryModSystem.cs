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
    api.RegisterBlockEntityClass("Wire", typeof(BlockEntityWire));
    api.RegisterBlockEntityBehaviorClass(BEBehaviorNetwork.Name,
                                         typeof(BEBehaviorNetwork));
    api.RegisterBlockEntityBehaviorClass("AcceptPorts",
                                         typeof(BEBehaviorAcceptPorts));
    BlockEntityWire.OnModLoaded();
    NetworkManager = new BEBehaviorNetwork.Manager(api.World);
  }

  public void RegisterServerDebugCommands(IChatCommandApi api) {
    IChatCommand network =
        api.GetOrCreate("debug").BeginSubCommand("network").WithDescription(
            "Debug commands for network propagation in the Lambda mod.");

    network.BeginSubCommand("togglesinglestep")
        .WithDescription(
            "Toggles whether the network propagation should run automatically, or only one step each time singlestep is run.")
        .HandleWith((args) => {
          NetworkManager.ToggleSingleStep();
          return TextCommandResult.Success(
              $"SingleStep set to {NetworkManager.SingleStep}.");
        })
        .EndSubCommand();

    network.BeginSubCommand("pending")
        .WithDescription("Prints out the next items in the queue.")
        .HandleWith((args) => {
          return TextCommandResult.Success(NetworkManager.QueueDebugString());
        })
        .EndSubCommand();

    network.BeginSubCommand("step")
        .WithDescription("Advances one step in single step mode.")
        .HandleWith((args) => {
          if (NetworkManager.SingleStep) {
            NetworkManager.Step();
            return TextCommandResult.Success(
                $"Step complete. HasPendingWork={NetworkManager.HasPendingWork}.");
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
    RegisterServerDebugCommands(api.ChatCommands);
  }
}
