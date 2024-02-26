using Lambda.Network;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Lambda.Tests;

using Lambda.Token;

[TestClass]
public class AppTemplateTest {
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
  public void EmitConstant() {
    Legend legend = _templates.CreateLegend();
    legend.AddConstant('O', "O");
    // clang-format off
    const string schematic = (
"""
O++
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmitter state = new(_accessor);
    Random r = new();
    BlockPos startBlock = new(0, 0, 0, 0);
    Token result = state.Process(
        new NodePos(startBlock, _accessor.FindNodeId(startBlock, "output")),
        r, TestContext.FullyQualifiedTestClassName,
                   TestContext.TestName);
    if (result is Constant c) {
      CollectionAssert.AreEqual(new Token[] { c },
                                state.UnreferencedRoots.ToList());

      Assert.AreEqual("O", c.Term);
      Assert.IsTrue(c.TermConnectors.Contains(new NodePos(1, 0, 0, 0, 0)));
      Assert.AreEqual(0, c.Children.Count);
    } else {
      Assert.Fail();
    }
  }

  [TestMethod]
  public void AppConstants() {
    Legend legend = _templates.CreateLegend();
    legend.AddConstant('O', "O");
    legend.AddConstant('S', "S");
    // clang-format off
    const string schematic = (
"""
  O
  +
S+A+
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmitter state = new(_accessor);
    Random r = new();
    BlockPos startBlock = new(2, 0, 2, 0);
    Token result = state.Process(
        new NodePos(startBlock, _accessor.FindNodeId(startBlock, "output")),
        r, TestContext.FullyQualifiedTestClassName,
                   TestContext.TestName);
    if (result is App a) {
      CollectionAssert.AreEqual(new Token[] { a },
                                state.UnreferencedRoots.ToList());

      Assert.AreEqual(2, a.Children.Count);

      Assert.AreEqual("applicand", a.Children[0].Name);
      Assert.AreEqual(1, a.Children[0].Children.Count);
      Assert.IsTrue(a.Children[0].Children[0] is Constant);

      Assert.AreEqual("argument", a.Children[1].Name);
      Assert.AreEqual(1, a.Children[1].Children.Count);
      Assert.IsTrue(a.Children[1].Children[0] is Constant);
    } else {
      Assert.Fail();
    }
  }

  [TestMethod]
  public void DoublePending() {
    // This attempts to cause the app block to be pending on its output, then
    // creates the app block by following the input port. The app block has to
    // prevent the output port from getting enqueued a second time.
    Legend legend = _templates.CreateLegend();
    legend.AddPuzzle('@', "bool -> bool");
    legend.AddConstant('a', "negb");
    // clang-format off
    const string schematic = (
"""
/* b */
#@#i##
#  + #
#a+A+o
######
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    bool first = true;
    // In the past the test failed with seed 24. So start there.
    for (int i = 24; i <= 50; ++i) {
      using TokenEmitter state = new(_accessor);
      Random r = new(i);
      BlockPos puzzleBlock = new(1, 0, 1, 0);
      Token puzzle = state.Process(
          new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")),
          r, first ? TestContext.FullyQualifiedTestClassName : null,
          TestContext.TestName);
      first = false;
      if (puzzle is Function f) {
        CollectionAssert.AreEqual(new Token[] { puzzle },
                                  state.UnreferencedRoots.ToList());

        Assert.AreEqual("result", f.Children[1].Children[0].Name);
        Assert.AreEqual("app", f.Children[1].Children[0].Children[0].Name);
        Assert.AreEqual("argument",
                        f.Children[1].Children[0].Children[0].Children[1].Name);
        Assert.AreEqual(
            f.Children[1],
            f.Children[1].Children[0].Children[0].Children[1].Children[0]);
      } else {
        Assert.Fail();
      }
    }
  }
}