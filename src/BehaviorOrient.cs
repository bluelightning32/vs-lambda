using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json.Serialization;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace LambdaFactory;

enum OrientationMode { Slab, AllFaces, Horizontals }

public class BlockBehaviorOrient : BlockBehavior {
  private string _facingCode;
  private OrientationMode _mode;
  private bool _flip;
  private int _rotateY;
  private bool _pillar;
  private string[] _networks;

  // If true, rotate the block so that it connects to any of the neighbors.
  private bool _pairToAny;
  public BlockBehaviorOrient(Block block) : base(block) {}

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
    _networks = properties["networks"].AsArray<string>(Array.Empty<string>());
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
    if (!byPlayer.Entity.Controls.ShiftKey && _networks.Length != 0) {
      List<Block> matching = GetConnectedOrientations(world, blockSel.Position,
                                                      blockSel.Face.Opposite);
      if (!matching.Contains(oriented)) {
        oriented = matching.First(block => block != null);
      }
    }
    if (!oriented.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) {
      handling = EnumHandling.PreventDefault;
      return false;
    }
    handling = EnumHandling.PreventSubsequent;
    return oriented.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
  }

  private List<Block> GetConnectedOrientations(IWorldAccessor world,
                                               BlockPos pos,
                                               BlockFacing preferredNeighbor) {
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
    List<Block> filtered =
        orientations
            .Select(orientation => world.BlockAccessor.GetBlock(
                        block.CodeWithVariant(_facingCode, orientation)))
            .ToList();

    IReadOnlyDictionary<string, AutoStepNetworkManager> networkManagers =
        LambdaFactoryModSystem.GetInstance(world.Api).NetworkManagers;
    foreach (string network in _networks) {
      if (!networkManagers.TryGetValue(network,
                                       out AutoStepNetworkManager manager)) {
        world.Api.Logger.Error($"network {network} not registered.");
        continue;
      }
      List<BlockNodeTemplate> blockTemplates = new();
      foreach (Block block in filtered) {
        if (block == null) {
          blockTemplates.Add(null);
        } else {
          BlockEntityBehaviorType found = null;
          foreach (var beb in block.BlockEntityBehaviors) {
            if (beb.Name == network) {
              found = beb;
              break;
            }
          }
          if (found == null) {
            world.Api.Logger.Error(
                $"Block entity behavior {network} not found.");
            blockTemplates.Add(null);
          }
          blockTemplates.Add(manager.ParseBlockNodeTemplate(found.properties));
        }
      }
      List<BlockNodeTemplate> matched = new(blockTemplates);
      manager.RemoveUnpaired(matched, pos, preferredNeighbor);
      if (!matched.Any(template => template != null)) {
        if (_pairToAny) {
          // No matches were found on the preferred neighbor. See if any of the
          // neighbors match.
          foreach (BlockFacing facing in BlockFacing.ALLFACES) {
            if (facing == preferredNeighbor) {
              continue;
            }
            List<BlockNodeTemplate> addMatched = new(blockTemplates);
            manager.RemoveUnpaired(addMatched, pos, facing);
            for (int i = 0; i < addMatched.Count; ++i) {
              if (addMatched[i] != null) {
                matched[i] = addMatched[i];
              }
            }
          }
        }
        if (!matched.Any(template => template != null)) {
          // None of the neighbors are connectable. Skip filtering on this
          // network.
          continue;
        }
      }
      // Filter `filtered` based on which templates could connect.
      for (int i = 0; i < matched.Count; ++i) {
        if (matched[i] == null) {
          filtered[i] = null;
        }
      }
    }
    return filtered;
  }
}
