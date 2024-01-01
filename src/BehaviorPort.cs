using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace LambdaFactory;

using PortDirection = BlockEntityBehavior.PortDirection;

// Forwards more methods from the Block to the BlockEntity.
public class BlockBehaviorPort : Vintagestory.GameContent.BlockBehaviorDecor {
  public PortDirection Direction { get; private set; }

  public BlockBehaviorPort(Block block) : base(block) {}

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

    BlockEntity parentBlock = world.BlockAccessor.GetBlockEntity(pos);
    if (parentBlock == null) {
      failureCode = "doesnotacceptports";
      return false;
    }

    bool foundAcceptor = false;
    foreach (var behavior in parentBlock.Behaviors) {
      if (behavior is not BlockEntityBehavior.IAcceptPorts acceptor) {
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
