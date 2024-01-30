using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Lambda.Network.BlockBehavior;

using VSBlockEntity = Vintagestory.API.Common.BlockEntity;

public interface IAcceptPort {
  bool SetPort(Block port, PortDirection direction, BlockFacing face,
               out string failureCode);
}

// Places the block as a decor. The decision of whether or not to accept the
// port is forwarded to block entity behaviors that implement `IAcceptPort`.
public class Port : Vintagestory.GameContent.BlockBehaviorDecor {
  public PortDirection Direction { get; private set; }

  public Port(Block block) : base(block) {}

  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);
    // `AsObject` converts the token into a string without the quotes, and
    // Newtonsoft fails to parse that back as an enum. So instead use the Token
    // directly.
    Direction = properties["direction"].Token?.ToObject<PortDirection>() ??
                PortDirection.None;
  }

  public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer,
                                     ItemStack itemstack,
                                     BlockSelection blockSel,
                                     ref EnumHandling handling,
                                     ref string failureCode) {
    handling = EnumHandling.PreventDefault;

    BlockPos pos = blockSel.Position.AddCopy(blockSel.Face.Opposite);

    VSBlockEntity parentBlock = world.BlockAccessor.GetBlockEntity(pos);
    if (parentBlock == null) {
      failureCode = "doesnotacceptports";
      return false;
    }

    bool foundAcceptor = false;
    foreach (var behavior in parentBlock.Behaviors) {
      if (behavior is not IAcceptPort acceptor) {
        continue;
      }
      foundAcceptor = true;
      if (acceptor.SetPort(block, Direction, blockSel.Face, out failureCode)) {
        return true;
      }
    }
    if (!foundAcceptor) {
      failureCode = "doesnotacceptports";
    }
    return false;
  }
}
