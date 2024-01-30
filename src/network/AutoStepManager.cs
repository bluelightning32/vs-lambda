using System;
using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.Network;

public abstract class AutoStepManager : Manager {
  public bool SingleStep = false;

  protected readonly IWorldAccessor _world;
  private bool _stepEnqueued = false;

  public AutoStepManager(IWorldAccessor world, NodeAccessor accessor)
      : base(world.Api.Side, world.Logger, accessor) {
    _world = world;
  }

  private void MaybeEnqueueStep() {
    if (!_stepEnqueued && !SingleStep) {
      _world.Api.Event.EnqueueMainThreadTask(() => {
        _stepEnqueued = false;
        if (!SingleStep) {
          Step();
          if (HasPendingWork) {
            MaybeEnqueueStep();
          }
        }
      }, "lambdanetwork");
      _stepEnqueued = true;
    }
  }

  public void ToggleSingleStep() {
    SingleStep = !SingleStep;
    // `MaybeEnqueueStep` checks that SingleStep is false.
    if (HasPendingWork) {
      MaybeEnqueueStep();
    }
  }

  public override void EnqueueNode(Node node, BlockPos pos, int nodeId) {
    base.EnqueueNode(node, pos, nodeId);
    MaybeEnqueueStep();
  }

  // Parse the block template. `connectFaces` describes center edges to add to
  // node[0]. node[0] is not changed if `connectFaces` is 0.
  public override BlockNodeTemplate ParseBlockNodeTemplate(
      JsonObject properties, int occupiedPorts, int connectFaces) {
    Dictionary<Tuple<JsonObject, int, int>, BlockNodeTemplate> cache =
        ObjectCacheUtil.GetOrCreate(
            _world.Api, $"lambda-properties",
            () => new Dictionary<Tuple<JsonObject, int, int>,
                                 BlockNodeTemplate>());
    Tuple<JsonObject, int, int> key =
        Tuple.Create(properties, occupiedPorts, connectFaces);
    if (cache.TryGetValue(key, out BlockNodeTemplate block)) {
      return block;
    }
    block =
        base.ParseBlockNodeTemplate(properties, occupiedPorts, connectFaces);

    cache.Add(key, block);
    return block;
  }
}