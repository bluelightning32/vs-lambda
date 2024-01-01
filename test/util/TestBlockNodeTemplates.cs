using LambdaFactory.Network;

using Vintagestory.API.Common;

namespace LambdaFactory.Tests;

public class TestBlockNodeTemplates {
  public BlockNodeTemplate ScopeCenterConnector;

  public BlockNodeTemplate ScopeNSCenterConnector;

  public BlockNodeTemplate ScopeCenterSource;

  public TestBlockNodeTemplates(NodeAccessor accessor, Manager manager) {
    ScopeCenterConnector = new BlockNodeTemplate(
        accessor, manager, JsonUtil.FromString<NodeTemplate[]>(@"
        [
          {
            edges: ['north-center', 'east-center', 'south-center', 'west-center']
          }
        ]"));

    ScopeNSCenterConnector = new BlockNodeTemplate(
        accessor, manager, JsonUtil.FromString<NodeTemplate[]>(@"
        [
          {
            edges: ['north-center', 'south-center']
          }
        ]"));

    ScopeCenterSource = new BlockNodeTemplate(
        accessor, manager, JsonUtil.FromString<NodeTemplate[]>(@"
        [
          {
            edges: ['north-center', 'east-center', 'south-center', 'west-center', 'source'],
            sourceScope: 'function'
          }
        ]"));
  }
}