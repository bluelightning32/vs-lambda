using Lambda.Network;
using Lambda.Token;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Lambda.Tests;

using Lambda.Token;

[TestClass]
public class FunctionTemplateTest {
  private Manager _manager;
  private MemoryNodeAccessor _accessor;
  private TestBlockNodeTemplates _templates;

  [TestInitialize]
  public void Initialize() {
    _accessor = new MemoryNodeAccessor();
    _manager = new Manager(EnumAppSide.Server, null, _accessor);
    _templates = new TestBlockNodeTemplates(_manager);
  }

  [TestMethod]
  public void EmitPassthrough() {
    Legend legend = _templates.CreateLegend();
    legend.AddPuzzle('@', "nat -> nat");
    // clang-format off
    const string schematic = (
"""
#@#i##
#  + #
#  ++o
######
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    for (int i = 0; i < 5; ++i) {
      TokenEmission state = new(_accessor);
      Random r = new(i);
      BlockPos puzzleBlock = new(1, 0, 0, 0);
      Token puzzle = state.Process(
          new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")));
      if (puzzle is Function f) {
        CollectionAssert.AreEqual(new Token[] { puzzle },
                                  state.UnreferencedRoots.ToList());

        Assert.IsTrue(
            f.ScopeMatchConnectors.Contains(new NodePos(0, 0, 3, 0, 0)));
        Assert.AreEqual(new NodePos(puzzleBlock,
                                    _accessor.FindNodeId(puzzleBlock, "scope")),
                        f.Pos);
        Assert.AreEqual("parameter", f.Children[0].Name);
        Assert.IsTrue(
            f.Children[0].TermConnectors.Contains(new NodePos(3, 0, 1, 0, 0)));
        Assert.AreEqual("resultType", f.Children[0].Children[0].Name);
        Assert.AreEqual("nat -> nat",
                        ((Constant)f.Children[0].Children[0].Children[0]).Term);
        Assert.AreEqual("result", f.Children[0].Children[1].Name);
        Assert.AreEqual(f.Children[0], f.Children[0].Children[1].Children[0]);
      } else {
        Assert.Fail();
      }
    }
  }
}