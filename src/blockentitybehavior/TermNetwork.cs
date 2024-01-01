using System;
using System.Collections.Generic;
using System.Linq;

using Lambda.Network;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.BlockEntityBehavior;
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

    public BlockNodeTemplate ParseAcceptPortsTemplate(JsonObject properties,
                                                      int occupiedPorts) {
      Dictionary<Tuple<JsonObject, int>, BlockNodeTemplate> cache =
          ObjectCacheUtil.GetOrCreate(
              _world.Api, $"lambda-term-accept-ports-properties",
              () =>
                  new Dictionary<Tuple<JsonObject, int>, BlockNodeTemplate>());
      Tuple<JsonObject, int> key = Tuple.Create(properties, occupiedPorts);
      if (cache.TryGetValue(key, out BlockNodeTemplate block)) {
        return block;
      }
      Debug("lambda: Accept ports properties cache miss. Dict has {0} entries.",
            cache.Count);
      List<NodeTemplate> nodeTemplates =
          new(properties["nodes"]?.AsObject<NodeTemplate[]>() ??
              Array.Empty<NodeTemplate>());
      PortConfiguration ports =
          AcceptPort.ParseConfiguration(_world.Api, properties);
      foreach (var port in ports.Ports) {
        NodeTemplate node = new();
        foreach (var face in port.Faces) {
          PortDirection dir =
              (PortDirection)((occupiedPorts >> (face.Index << 1)) & 3);
          if (dir == PortDirection.In) {
            node.Edges = new Edge[] { EdgeExtension.GetFaceCenter(face) };
            break;
          }
          if (dir == PortDirection.Out) {
            node.Edges =
                new Edge[] { EdgeExtension.GetFaceCenter(face), Edge.Source };
            break;
          }
        }
        nodeTemplates.Add(node);
      }
      block = new BlockNodeTemplate(_accessor, this, nodeTemplates.ToArray());

      cache.Add(key, block);
      return block;
    }

    public override
        BlockNodeTemplate ParseBlockNodeTemplate(JsonObject properties) {
      Dictionary<JsonObject, BlockNodeTemplate> cache =
          ObjectCacheUtil.GetOrCreate(
              _world.Api, $"lambda-term-network-properties",
              () => new Dictionary<JsonObject, BlockNodeTemplate>());
      if (cache.TryGetValue(properties, out BlockNodeTemplate block)) {
        return block;
      }
      Debug("lambda: Term network properties cache miss. Dict has {0} entries.",
            cache.Count);
      NodeTemplate[] nodeTemplates =
          properties["nodes"]?.AsObject<NodeTemplate[]>();
      block = new BlockNodeTemplate(_accessor, this, nodeTemplates);

      cache.Add(properties, block);
      return block;
    }

    public override string GetNetworkName() { return "term"; }

    internal BlockNodeTemplate ParseWireTemplate(JsonObject properties,
                                                 int directions) {
      Dictionary<Tuple<JsonObject, int>, BlockNodeTemplate> cache =
          ObjectCacheUtil.GetOrCreate(
              _world.Api, $"lambda-term-wire-properties",
              () =>
                  new Dictionary<Tuple<JsonObject, int>, BlockNodeTemplate>());
      Tuple<JsonObject, int> key = Tuple.Create(properties, directions);
      if (cache.TryGetValue(key, out BlockNodeTemplate block)) {
        return block;
      }
      Debug("lambda: Accept ports properties cache miss. Dict has {0} entries.",
            cache.Count);
      NodeTemplate[] nodeTemplates =
          properties["nodes"]?.AsObject<NodeTemplate[]>();
      if (nodeTemplates == null || nodeTemplates.Length < 1) {
        nodeTemplates = new NodeTemplate[1] { new() };
      }
      HashSet<Edge> connectedEdges = new(nodeTemplates[0].Edges);
      for (int i = 0; i < 6; ++i) {
        BlockFacing face = BlockFacing.ALLFACES[i];
        if ((directions & (1 << i)) != 0) {
          connectedEdges.Add(EdgeExtension.GetFaceCenter(face));
        }
      }
      nodeTemplates[0].Edges = connectedEdges.ToArray();
      block = new BlockNodeTemplate(_accessor, this, nodeTemplates);

      cache.Add(key, block);
      return block;
    }
  }

  public TermNetwork(VSBlockEntity blockentity) : base(blockentity) {}

  protected override Manager GetManager(ICoreAPI api) {
    return LambdaModSystem.GetInstance(api).TermNetworkManager;
  }
}
