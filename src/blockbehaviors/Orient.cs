using System;
using System.Collections.Generic;
using System.Linq;

using Lambda.Network;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Lambda.BlockBehaviors;

enum OrientationMode { Slab, AllFaces, Horizontals }

// Orients the block on placement. The orientation depends on which block face
// the player selected for the placement and the direction of the player. If
// `_networks` is non-empty, then the player selected orientation will be
// overridden to connect the new block to the neighboring blocks.
public class Orient : BlockBehavior {
  private string _facingCode;
  private OrientationMode _mode;
  private bool _flip;
  private int _rotateY;
  private bool _pillar;

  // If true, rotate the block so that it connects to any of the neighbors.
  private bool _pairToAny;
  public Orient(Block block) : base(block) {}

  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);
    _facingCode = properties["facingCode"].AsString("rot");
    // `AsObject` converts the token into a string without the quotes, and
    // Newtonsoft fails to parse that back as an enum. So instead use the Token
    // directly.
    _mode = properties["mode"].Token?.ToObject<OrientationMode>() ??
            OrientationMode.Slab;
    _flip = properties["flip"].AsBool(false);
    _rotateY = properties["rotateY"].AsInt(0);
    _pillar = properties["pillar"].AsBool(false);
    _pairToAny = properties["pairToAny"].AsBool(true);
  }

  public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer,
                                     ItemStack itemstack,
                                     BlockSelection blockSel,
                                     ref EnumHandling handling,
                                     ref string failureCode) {
    BlockFacing face = blockSel.Face.Opposite;
    if (_mode == OrientationMode.Slab) {
      double axis1 = blockSel.HitPosition[((int)blockSel.Face.Axis + 1) % 3];
      double axis2 = blockSel.HitPosition[((int)blockSel.Face.Axis + 2) % 3];
      bool axis1Primary = Math.Abs(axis1 - 0.5) > Math.Abs(axis2 - 0.5);
      double primaryAxis = axis1Primary ? axis1 : axis2;
      if (0.3 < primaryAxis && primaryAxis < 0.7) {
        // The hit box is in the center.
        face = blockSel.Face.Opposite;
      } else {
        Vec3i normal = new();
        int primaryAxisIndex =
            ((int)blockSel.Face.Axis + (axis1Primary ? 1 : 2)) % 3;
        normal[primaryAxisIndex] = primaryAxis < 0.5 ? -1 : 1;
        face = BlockFacing.FromNormal(normal);
      }
    } else if (_mode == OrientationMode.Horizontals) {
      if (face.IsVertical) {
        face = BlockFacing.HorizontalFromAngle(byPlayer.Entity.Pos.Yaw);
      }
    }
    if (_flip) {
      face = face.Opposite;
    }
    face = face.GetHorizontalRotated(_rotateY);
    string orientation;
    if (_pillar) {
      orientation = face.Axis switch {
        EnumAxis.X => "we",
        EnumAxis.Y => "ud",
        _ => "ns",
      };
    } else {
      orientation = face.Code;
    }
    Block oriented = world.BlockAccessor.GetBlock(
        block.CodeWithVariant(_facingCode, orientation));
    if (!byPlayer.Entity.Controls.ShiftKey) {
      Dictionary<Block, PairState> matching = GetConnectedOrientations(
          world, blockSel.Position, blockSel.Face.Opposite);
      if (!matching.TryGetValue(oriented, out PairState state) ||
          state == PairState.Unpaired) {
        // `oriented` was chosen purely based on angles, but it doesn't pair
        // with its neighbor. So if any other orientations do pair, use them
        // instead. If multiple orientations pair with the neighbors, then try
        // to choose one that also has a source set.
        PairState bestPairing = PairState.Unpaired;
        foreach (KeyValuePair<Block, PairState> pairing in matching) {
          if ((int)pairing.Value > (int)bestPairing) {
            bestPairing = pairing.Value;
            oriented = pairing.Key;
          }
        }
      }
    }
    if (!oriented.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) {
      handling = EnumHandling.PreventDefault;
      return false;
    }
    handling = EnumHandling.PreventSubsequent;
    return oriented.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
  }

  private List<Block> GetOrientations(IWorldAccessor world) {
    string[] orientations;
    if (_mode == OrientationMode.Horizontals) {
      if (_pillar) {
        orientations = new string[] { "we", "ns" };
      } else {
        orientations =
            BlockFacing.HORIZONTALS.Select(face => face.Code).ToArray();
      }
    } else {
      if (_pillar) {
        orientations = new string[] { "we", "ud", "ns" };
      } else {
        orientations = BlockFacing.ALLFACES.Select(face => face.Code).ToArray();
      }
    }
    return orientations
        .Select(orientation => world.BlockAccessor.GetBlock(
                    block.CodeWithVariant(_facingCode, orientation)))
        .ToList();
  }

  private Dictionary<Block, PairState>
  GetConnectedOrientations(IWorldAccessor world, BlockPos pos,
                           BlockFacing preferredNeighbor) {
    List<Block> filtered = GetOrientations(world);
    NetworkSystem networkSystem = NetworkSystem.GetInstance(world.Api);
    AutoStepManager manager = networkSystem.TokenEmitterManager;
    List<BlockNodeTemplate> blockTemplates = new();
    foreach (Block block in filtered) {
      if (block == null) {
        blockTemplates.Add(null);
      } else {
        BlockEntityBehaviorType found = null;
        foreach (var beb in block.BlockEntityBehaviors) {
          if (networkSystem.NetworkBlockEntityBehaviors.ContainsKey(beb.Name)) {
            found = beb;
            break;
          }
        }
        if (found == null) {
          world.Api.Logger.Error(
              "Could not find network block entity behavior in block " +
              $"{block.Code}.");
          blockTemplates.Add(null);
        }
        blockTemplates.Add(
            manager.ParseBlockNodeTemplate(found.properties, 0, 0));
      }
    }
    List<PairState> matched =
        manager.GetPairState(blockTemplates, pos, preferredNeighbor);
    if (!matched.Any(state => state != PairState.Unpaired)) {
      if (_pairToAny) {
        // No matches were found on the preferred neighbor. See if any of the
        // neighbors match.
        foreach (BlockFacing facing in BlockFacing.ALLFACES) {
          if (facing == preferredNeighbor) {
            continue;
          }
          List<PairState> addMatched =
              manager.GetPairState(blockTemplates, pos, facing);
          for (int i = 0; i < addMatched.Count; ++i) {
            if ((int)addMatched[i] > (int)matched[i]) {
              matched[i] = addMatched[i];
            }
          }
        }
      }
    }
    return filtered.Zip(matched).ToDictionary(p => p.First, p => p.Second);
  }

  public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos,
                                        ref EnumHandling handling) {
    // Search through the block's item drops. If any of the drops is an oriented
    // version of this block, then return it.
    ItemStack[] drops =
        block.GetDrops(world, pos, null) ?? Array.Empty<ItemStack>();
    HashSet<Block> orientations = new(GetOrientations(world));
    foreach (ItemStack drop in drops) {
      if (orientations.Contains(drop.Block)) {
        handling = EnumHandling.PreventDefault;
        return drop;
      }
    }
    // Couldn't find any drop that is an oriented version of this block. So
    // fallback to the default behavior.
    return base.OnPickBlock(world, pos, ref handling);
  }
}
