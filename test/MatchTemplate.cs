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

  [TestMethod]
  public void NoCases() {
    Legend legend = _templates.CreateLegend();
    legend.AddConstant('0', "false_axiom");
    // clang-format off
    const string schematic = (
"""
0+M+
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmissionState state = new(_accessor);
    Random r = new();
    BlockPos startBlock = new(2, 0, 0, 0);
    Token result = state.Process(
        new NodePos(startBlock, _accessor.FindNodeId(startBlock, "output")),
        r, TestContext.FullyQualifiedTestClassName,
                       TestContext.TestName);
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
        r, TestContext.FullyQualifiedTestClassName,
                       TestContext.TestName);
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

  [TestMethod]
  public void MatchPairWithConnector() {
    Legend legend = _templates.CreateLegend();
    legend.AddCase('a', "pair");
    legend.AddConstant('b', "pair_const");
    // clang-format off
    const string schematic = (
"""
b+M+
  .
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
        r, TestContext.FullyQualifiedTestClassName,
                       TestContext.TestName);
    if (result is Match m) {
      CollectionAssert.AreEqual(new Token[] { m },
                                state.UnreferencedRoots.ToList());

      Assert.IsTrue(m.TermConnectors.Contains(new NodePos(3, 0, 0, 0, 0)));
      Assert.AreEqual(2, m.Children.Count);
      Assert.AreEqual("input", m.Children[0].Name);
      Assert.AreEqual("pair_const", m.Children[0].Children[0].Name);

      Assert.AreEqual("pair", m.Children[1].Name);
      Assert.IsTrue(m.Children[1].ScopeMatchConnectors.Contains(
          new NodePos(3, 0, 2, 0, 0)));
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

  [TestMethod]
  public void DestructSum() {
    Legend legend = _templates.CreateLegend();
    legend.AddCase('a', "inl");
    legend.AddCase('b', "inr");
    legend.AddConstant('c', "sum_const");
    // clang-format off
    const string schematic = (
"""
c+M+
  a#i##
  # ++o
  #####
  .....
  b#i##
  # ++o
  #####
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmissionState state = new(_accessor);
    Random r = new();
    BlockPos startBlock = new(2, 0, 0, 0);
    Token result = state.Process(
        new NodePos(startBlock, _accessor.FindNodeId(startBlock, "output")),
        r, TestContext.FullyQualifiedTestClassName,
                       TestContext.TestName);
    if (result is Match m) {
      CollectionAssert.AreEqual(new Token[] { m },
                                state.UnreferencedRoots.ToList());

      Assert.IsTrue(m.TermConnectors.Contains(new NodePos(3, 0, 0, 0, 0)));
      Assert.AreEqual(3, m.Children.Count);
      Assert.AreEqual("input", m.Children[0].Name);
      Assert.AreEqual("sum_const", m.Children[0].Children[0].Name);

      Assert.AreEqual("inl", m.Children[1].Name);
      Assert.IsTrue(m.Children[1].ScopeMatchConnectors.Contains(
          new NodePos(3, 0, 1, 0, 0)));
      Assert.AreEqual(1, m.Children[1].Children.Count);
      Assert.AreEqual("parameter", m.Children[1].Children[0].Name);
      Assert.AreEqual(1, m.Children[1].Children[0].Children.Count);
      Assert.AreEqual("result", m.Children[1].Children[0].Children[0].Name);
      Assert.AreEqual(1, m.Children[1].Children[0].Children[0].Children.Count);
      Assert.AreEqual(m.Children[1].Children[0],
                      m.Children[1].Children[0].Children[0].Children[0]);

      Assert.AreEqual("inr", m.Children[2].Name);
      Assert.IsTrue(m.Children[2].ScopeMatchConnectors.Contains(
          new NodePos(3, 0, 5, 0, 0)));
      Assert.AreEqual(1, m.Children[2].Children.Count);
      Assert.AreEqual("parameter", m.Children[2].Children[0].Name);
      Assert.AreEqual(1, m.Children[2].Children[0].Children.Count);
      Assert.AreEqual("result", m.Children[2].Children[0].Children[0].Name);
      Assert.AreEqual(1, m.Children[2].Children[0].Children[0].Children.Count);
      Assert.AreEqual(m.Children[2].Children[0],
                      m.Children[2].Children[0].Children[0].Children[0]);
    } else {
      Assert.Fail();
    }
  }

  [TestMethod]
  public void MatchIn() {
    /*
    Builds the following definition without the surrounding function definition.
    // clang-format off
    Definition MatchIn (P Q: Prop) (b: bool) (d: BoolSpec P Q b)
      : BoolSpec Q P (negb b) :=
    match d in BoolSpec _ _ b return BoolSpec _ _ (negb b) with
    | BoolSpecT _ p => BoolSpecF _ p
    | BoolSpecF _ q => BoolSpecT _ q
    end.
    // clang-format on
    */
    Legend legend = _templates.CreateLegend();
    legend.AddMatchIn('a', "BoolSpec");
    legend.AddCase('b', "BoolSpecT");
    legend.AddCase('c', "BoolSpecF");
    legend.AddConstant('d', "negb");
    legend.AddConstant('e', "BoolSpec");
    legend.AddConstant('1', "BoolSpecT");
    legend.AddConstant('0', "BoolSpecF");
    legend.AddConstant('h', "boolspec_const");
    // clang-format off
    const string schematic = (
"""
h+M+
  a##i#i#i###
  #      +  #
  #    d+A+ #
  #       + #
  # e+A+A+A+o
  ###########
  ...........
  b###i#i####
  #     +   #
  # 0+A+A+++o
  ###########
  ...........
  c###i#i####
  #     +   #
  # 1+A+A+++o
  ###########
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmissionState state = new(_accessor);
    Random r = new(0);
    BlockPos startBlock = new(2, 0, 0, 0);
    Token result = state.Process(
        new NodePos(startBlock, _accessor.FindNodeId(startBlock, "output")),
        r, TestContext.FullyQualifiedTestClassName,
                       TestContext.TestName);
    if (result is Match m) {
      CollectionAssert.AreEqual(new Token[] { m },
                                state.UnreferencedRoots.ToList());

      Assert.AreEqual(4, m.Children.Count);
      Assert.AreEqual("input", m.Children[0].Name);
      Assert.AreEqual("boolspec_const", m.Children[0].Children[0].Name);

      Assert.AreEqual("BoolSpec", m.Children[1].Name);
      Assert.IsTrue(m.Children[1] is MatchIn);

      Assert.AreEqual(1, m.Children[1].Children.Count);

      Assert.AreEqual("BoolSpecT", m.Children[2].Name);
      Assert.IsTrue(m.Children[2] is Case);

      Assert.AreEqual("BoolSpecF", m.Children[3].Name);
      Assert.IsTrue(m.Children[3] is Case);
    } else {
      Assert.Fail();
    }
  }
}
