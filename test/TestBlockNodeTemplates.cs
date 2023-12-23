using Vintagestory.API.Common;

namespace LambdaFactory.Tests;

public class TestBlockNodeTemplates {
  public BlockNodeTemplate ScopeCenterConnector;

  public BlockNodeTemplate ScopeCenterSource;

  public TestBlockNodeTemplates(NetworkManager manager) {
    ScopeCenterConnector =
        new BlockNodeTemplate(JsonUtil.FromString<BlockNodeTemplateLoading>(@"
      {
        scope: [
          {
            edges: ['north-center', 'east-center', 'south-center', 'west-center']
          }
        ]
      }"),
                              manager);

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
                              manager);
  }
}