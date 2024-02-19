using Lambda.Network;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Lambda.Tests;

using Lambda.Token;

[TestClass]
public class MatchTemplateTest {
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
  public void NoCases() {
    Legend legend = _templates.CreateLegend();
    legend.AddConstant('f', "false_axiom");
    // clang-format off
    const string schematic = (
"""
f+M+
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmissionState state = new(_accessor);
    Random r = new();
    BlockPos startBlock = new(2, 0, 0, 0);
    Token result = state.Process(
        new NodePos(startBlock, _accessor.FindNodeId(startBlock, "output")),
        r);
    SaveGraphviz(state);
    if (result is Match m) {
      CollectionAssert.AreEqual(new Token[] { m },
                                state.UnreferencedRoots.ToList());

      Assert.IsTrue(m.TermConnectors.Contains(new NodePos(3, 0, 0, 0, 0)));
      Assert.AreEqual(1, m.Children.Count);
      Assert.AreEqual("input", m.Children[0].Name);
      Assert.AreEqual("false_axiom", m.Children[0].Children[0].Name);
    } else {
      Assert.Fail();
    }
  }

  [TestMethod]
  public void MatchPair() {
    Legend legend = _templates.CreateLegend();
    legend.AddCase('a', "pair");
    legend.AddConstant('b', "pair_const");
    // clang-format off
    const string schematic = (
"""
b+M+
  a#i#i#
  # +  #
  # +++o
  ######
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmissionState state = new(_accessor);
    Random r = new();
    BlockPos startBlock = new(2, 0, 0, 0);
    Token result = state.Process(
        new NodePos(startBlock, _accessor.FindNodeId(startBlock, "output")),
        r);
    SaveGraphviz(state);
    if (result is Match m) {
      CollectionAssert.AreEqual(new Token[] { m },
                                state.UnreferencedRoots.ToList());

      Assert.IsTrue(m.TermConnectors.Contains(new NodePos(3, 0, 0, 0, 0)));
      Assert.AreEqual(2, m.Children.Count);
      Assert.AreEqual("input", m.Children[0].Name);
      Assert.AreEqual("pair_const", m.Children[0].Children[0].Name);

      Assert.AreEqual("pair", m.Children[1].Name);
      Assert.IsTrue(m.Children[1].ScopeMatchConnectors.Contains(
          new NodePos(3, 0, 1, 0, 0)));
      Assert.AreEqual(1, m.Children[1].Children.Count);
      Assert.AreEqual("parameter", m.Children[1].Children[0].Name);
      Assert.AreEqual(1, m.Children[1].Children[0].Children.Count);
      Assert.AreEqual("parameter", m.Children[1].Children[0].Children[0].Name);
      Assert.AreEqual(1, m.Children[1].Children[0].Children[0].Children.Count);
      Assert.AreEqual("result",
                      m.Children[1].Children[0].Children[0].Children[0].Name);
      Assert.AreEqual(
          1, m.Children[1].Children[0].Children[0].Children[0].Children.Count);
      Assert.AreEqual(
          m.Children[1].Children[0],
          m.Children[1].Children[0].Children[0].Children[0].Children[0]);
    } else {
      Assert.Fail();
    }
  }
}