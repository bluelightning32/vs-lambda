using System;
using System.Collections.Generic;
using System.Linq;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Lambda.BlockEntityBehaviors;

public class SortBlockPosByY : IComparer<BlockPos> {
  public int Compare(BlockPos a, BlockPos b) {
    if (a.Y != b.Y) {
      return a.Y - b.Y;
    }
    if (a.X != b.X) {
      return a.X - b.X;
    }
    return a.Z - b.Z;
  }

  public static readonly SortBlockPosByY Instance = new();
}

// Monitors for block replacement.
public class SealedCharcoalPit : BlockEntityBehavior, ICharcoalConverter {
  private float _minCharcoalPerLog = 0.125f;
  private float _maxCharcoalPerLog = 0.25f;
  private AssetLocation[] _sealedDecors = Array.Empty<AssetLocation>();

  public SealedCharcoalPit(BlockEntity blockentity) : base(blockentity) {}

  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);
    _minCharcoalPerLog =
        properties["minCharcoalPerLog"].AsFloat(_minCharcoalPerLog);
    _maxCharcoalPerLog =
        properties["maxCharcoalPerLog"].AsFloat(_maxCharcoalPerLog);
    _sealedDecors = properties["sealedDecors"].AsArray<AssetLocation>(
        Array.Empty<AssetLocation>());
  }

  public bool ConvertPit() {
    Dictionary<BlockPos, int> logsPerLocation = new();
    Queue<BlockPos> queue = new();
    queue.Enqueue(Blockentity.Pos);
    int borderFaces = 0;
    int sealedBorderFaces = 0;
    while (queue.Count > 0) {
      BlockPos pos = queue.Dequeue();
      logsPerLocation[pos] =
          BlockFirepit.GetFireWoodQuanity(Blockentity.Api.World, pos);
      foreach (BlockFacing facing in BlockFacing.ALLFACES) {
        BlockPos consider = pos.AddCopy(facing);
        if (logsPerLocation.ContainsKey(consider)) {
          continue;
        }
        if (BlockFirepit.IsFirewoodPile(Api.World, consider) &&
            consider.InRangeHorizontally(Pos.X, Pos.Z, 6) &&
            Math.Abs(consider.Y - Pos.Y) <= 6) {
          logsPerLocation[consider] = 0;
          queue.Enqueue(consider);
          continue;
        }
        ++borderFaces;
        Block decor = Api.World.BlockAccessor.GetDecor(
            consider, BlockSelection.GetDecorIndex(facing.Opposite));
        if (decor != null) {
          foreach (AssetLocation sealedDecor in _sealedDecors) {
            // The `needle` parameter to Match is the one that can contain a
            // wildcard.
            if (WildcardUtil.Match(sealedDecor, decor.Code)) {
              ++sealedBorderFaces;
              break;
            }
          }
        }
      }
    }
    Api.Logger.Debug(
        "Converting charcoal block at {0}. {1} out of {2} faces were sealed.",
        Pos, sealedBorderFaces, borderFaces);
    float sealedRatio = (float)sealedBorderFaces / borderFaces;
    float charcoalPerLogBase =
        _minCharcoalPerLog +
        (_maxCharcoalPerLog - _minCharcoalPerLog) * sealedRatio;
    float charcoalPerLogVariable = _maxCharcoalPerLog - charcoalPerLogBase;
    foreach (BlockPos pos in logsPerLocation.Keys.Order(
                 SortBlockPosByY.Instance)) {
      int logs = logsPerLocation[pos];
      if (logs == -1) {
        // This block was already processed.
        continue;
      }
      ConvertColumn(pos, logsPerLocation, charcoalPerLogBase,
                    charcoalPerLogVariable);
    }
    return true;
  }

  private void ConvertColumn(BlockPos basePos,
                             Dictionary<BlockPos, int> logsPerLocation,
                             float charcoalPerLogBase,
                             float charcoalPerLogVariable) {
    float charcoalFloat =
        logsPerLocation[basePos] *
        (charcoalPerLogBase +
         charcoalPerLogVariable * Api.World.Rand.NextSingle());
    int height = 1;
    BlockPos copy = basePos.Copy();
    while (true) {
      copy.Y = basePos.Y + height;
      if (!logsPerLocation.TryGetValue(copy, out int aboveLogs)) {
        break;
      }
      ++height;
      charcoalFloat +=
          aboveLogs * (charcoalPerLogBase +
                       charcoalPerLogVariable * Api.World.Rand.NextSingle());
    }
    int charcoal = (int)charcoalFloat;
    // Place charcoal blocks for the column. The bottom locations of the column
    // get full charcoal blocks. The middle of the column gets a partial
    // charcoal block. The top of the column gets air.
    for (int i = 0; i < height; ++i) {
      copy.Y = basePos.Y + i;
      // Mark this location as processed.
      logsPerLocation[copy] = -1;
      if (charcoal > 0) {
        Block charcoalBlock = Api.World.GetBlock(
            new AssetLocation("charcoalpile-" + Math.Min(charcoal, 8)));
        Api.World.BlockAccessor.SetBlock(charcoalBlock.BlockId, copy);
        charcoal -= 8;
      } else {
        // Set to air
        Api.World.BlockAccessor.SetBlock(0, copy);
      }
    }
  }
}
