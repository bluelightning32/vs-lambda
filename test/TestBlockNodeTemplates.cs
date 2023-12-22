using Vintagestory.API.Common;

namespace LambdaFactory.Tests;

public static class TestBlockNodeTemplates {
  public static BlockNodeTemplate ScopeCenterConnector =
      JsonUtil.FromString<BlockNodeTemplate>(@"
      {
        scope: [
          {
            edges: ['north-center', 'east-center', 'south-center', 'west-center']
          }
        ]
      }");

  public static BlockNodeTemplate ScopeCenterSource =
      JsonUtil.FromString<BlockNodeTemplate>(@"
      {
        scope: [
          {
            edges: ['north-center', 'east-center', 'south-center', 'west-center', 'source'],
            sourceScope: 'function'
          }
        ]
      }");
}