using Lambda.Network;

using Vintagestory.API.Common;

namespace Lambda.Tests;

public class TestBlockNodeTemplates {
  public BlockNodeTemplate FourWay;

  public BlockNodeTemplate NS;

  public BlockNodeTemplate FourWaySource;

  public TestBlockNodeTemplates(NodeAccessor accessor, Manager manager) {
    FourWay = new BlockNodeTemplate(accessor, manager,
                                    JsonUtil.FromString<NodeTemplate[]>(@"
        [
          {
            network: 'scope',
            edges: ['north-center', 'east-center', 'south-center', 'west-center']
          }
        ]"));

    NS = new BlockNodeTemplate(accessor, manager,
                               JsonUtil.FromString<NodeTemplate[]>(@"
        [
          {
            network: 'scope',
            edges: ['north-center', 'south-center']
          }
        ]"));

    FourWaySource = new BlockNodeTemplate(accessor, manager,
                                          JsonUtil.FromString<NodeTemplate[]>(@"
        [
          {
            network: 'scope',
            edges: ['north-center', 'east-center', 'south-center', 'west-center', 'source'],
            sourceScope: 'function'
          }
        ]"));
  }
}