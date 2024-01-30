using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Lambda.Network.BlockEntityBehavior;
using VSBlockEntity = Vintagestory.API.Common.BlockEntity;

public class TokenEmitter : AbstractNetwork {

  public static string Name {
    get { return "TokenEmitter"; }
  }

  public class Manager : AutoStepManager {
    public Manager(IWorldAccessor world)
        : base(world, new NetworkNodeAccessor(
                          (pos) => world.BlockAccessor.GetBlockEntity(pos)
                                       ?.GetBehavior<TokenEmitter>())) {}

    public override string GetNetworkName() { return "token"; }
  }

  public TokenEmitter(VSBlockEntity blockentity) : base(blockentity) {}

  protected override AutoStepManager GetManager(ICoreAPI api) {
    return NetworkSystem.GetInstance(api).TokenEmitterManager;
  }
}
