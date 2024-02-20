using Lambda.Network;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Lambda.Tests;

using Lambda.Token;

[TestClass]
public class ForallTemplateTest {
  private Manager _manager;
  private MemoryNodeAccessor _accessor;
  private TestBlockNodeTemplates _templates;

  public TestContext TestContext { get; set; }

  [TestInitialize]
  public void Initialize() {
    _accessor = new MemoryNodeAccessor();
    _manager = new Manager(EnumAppSide.Server, null, _accessor);
    _templates = new TestBlockNodeTemplates(_manager);
  }

  private void SaveGraphviz(TokenEmissionState state) {
    state.SaveGraphviz(TestContext.FullyQualifiedTestClassName,
                       TestContext.TestName);
  }

  [TestMethod]
  public void Passthrough() {
    Legend legend = _templates.CreateLegend();
    // clang-format off
    const string schematic = (
"""
#f#i##
#  + #
#  ++o
######
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmissionState state = new(_accessor);
    Random r = new(0);
    BlockPos startBlock = new(1, 0, 0, 0);
    Token start = state.Process(
        new NodePos(startBlock,
                    _accessor.FindNodeId(startBlock, "scope")),
        r);
    SaveGraphviz(state);
    if (start is Forall f) {
      CollectionAssert.AreEqual(new Token[] { start },
                                state.UnreferencedRoots.ToList());

      Assert.IsTrue(
          f.ScopeMatchConnectors.Contains(new NodePos(0, 0, 3, 0, 0)));
      Assert.AreEqual(
          new NodePos(startBlock, _accessor.FindNodeId(startBlock, "scope")),
          f.ScopePos);
      Assert.AreEqual("parameter", f.Children[0].Name);
      Assert.IsTrue(
          f.Children[0].TermConnectors.Contains(new NodePos(3, 0, 1, 0, 0)));
      Assert.AreEqual("result", f.Children[0].Children[0].Name);
      Assert.AreEqual(f.Children[0], f.Children[0].Children[0].Children[0]);
    } else {
      Assert.Fail();
    }
  }

  [TestMethod]
  public void TwoParameters() {
    Legend legend = _templates.CreateLegend();
    legend.AddConstant('a', "nat");
    // clang-format off
    const string schematic = (
"""
#f#i#i#
#     #
#   a+o
#######
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmissionState state = new(_accessor);
    Random r = new(0);
    BlockPos startBlock = new(1, 0, 0, 0);
    Token start = state.Process(
        new NodePos(startBlock,
                    _accessor.FindNodeId(startBlock, "scope")),
        r);
    SaveGraphviz(state);
    if (start is Forall f) {
      CollectionAssert.AreEqual(new Token[] { start },
                                state.UnreferencedRoots.ToList());

      Assert.IsTrue(
          f.ScopeMatchConnectors.Contains(new NodePos(0, 0, 3, 0, 0)));
      Assert.AreEqual(
          new NodePos(startBlock, _accessor.FindNodeId(startBlock, "scope")),
          f.ScopePos);
      Assert.AreEqual("parameter", f.Children[0].Name);
      Assert.AreEqual("parameter", f.Children[0].Children[0].Name);
      Assert.AreEqual("result", f.Children[0].Children[0].Children[0].Name);
      Assert.AreEqual("nat",
                      f.Children[0].Children[0].Children[0].Children[0].Name);
    } else {
      Assert.Fail();
    }
  }
}