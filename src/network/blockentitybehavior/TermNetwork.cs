using System;
using System.Collections.Generic;
using System.Linq;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.Network.BlockEntityBehavior;
using VSBlockEntity = Vintagestory.API.Common.BlockEntity;

public class TermNetwork : AbstractNetwork {

  public static string Name {
    get { return "TermNetwork"; }
  }

  public class Manager : AutoStepManager {
    public Manager(IWorldAccessor world)
        : base(world, new NetworkNodeAccessor(
                          (pos) => world.BlockAccessor.GetBlockEntity(pos)
                                       ?.GetBehavior<TermNetwork>())) {}

    public override string GetNetworkName() { return "term"; }
  }

  public TermNetwork(VSBlockEntity blockentity) : base(blockentity) {}

  protected override Manager GetManager(ICoreAPI api) {
    return NetworkSystem.GetInstance(api).TermNetworkManager;
  }
}
