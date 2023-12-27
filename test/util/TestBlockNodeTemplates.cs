using Vintagestory.API.Common;

namespace LambdaFactory.Tests;

public class TestBlockNodeTemplates {
  public BlockNodeTemplate ScopeCenterConnector;

  public BlockNodeTemplate ScopeNSCenterConnector;

  public BlockNodeTemplate ScopeCenterSource;

  public TestBlockNodeTemplates(NodeAccessor accessor, NetworkManager manager) {
    ScopeCenterConnector =
        new BlockNodeTemplate(JsonUtil.FromString<BlockNodeTemplateLoading>(@"
      {
        scope: [
          {
            edges: ['north-center', 'east-center', 'south-center', 'west-center']
          }
        ]
      }"),
                              accessor, manager);

    ScopeNSCenterConnector =
        new BlockNodeTemplate(JsonUtil.FromString<BlockNodeTemplateLoading>(@"
      {
        scope: [
          {
            edges: ['north-center', 'south-center']
          }
        ]
      }"),
                              accessor, manager);

    ScopeCenterSource =
        new BlockNodeTemplate(JsonUtil.FromString<BlockNodeTemplateLoading>(@"
      {
        scope: [
          {
            edges: ['north-center', 'east-center', 'south-center', 'west-center', 'source'],
            sourceScope: 'function'
          }
        ]
      }"),
                              accessor, manager);
  }
}