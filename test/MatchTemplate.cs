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

    using TokenEmitter state = new(_accessor);
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

    using TokenEmitter state = new(_accessor);
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

    using TokenEmitter state = new(_accessor);
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

    using TokenEmitter state = new(_accessor);
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

    using TokenEmitter state = new(_accessor);
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

  [TestMethod]
  public void DisconnectedMatch() {
    Legend legend = _templates.CreateLegend();
    legend.AddPuzzle('@', "forall A B, A*B -> A*B");
    legend.AddCase('a', "pair");
    legend.AddConstant('b', "pair");
    legend.AddConstant('O', "O");
    // clang-format off
    const string schematic = (
"""
/* A B ab */
#@#i#i#i##########
#      +         #
#    ++++++++++++o
#    +           #
#    +  M+       #
#    +  a#i#i##  #
#    +  # +   #  #
#  b+A++++A+++o  #
#       #######  #
#                #
##################
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmitter state = new(_accessor);
    Random r = new(0);
    BlockPos puzzleBlock = new(1, 0, 1, 0);
    Token puzzle = state.Process(
        new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")),
        r, TestContext.FullyQualifiedTestClassName,
                     TestContext.TestName);
    if (puzzle is Function f) {
      CollectionAssert.AreEqual(new Token[] { f },
                                state.UnreferencedRoots.ToList());
    } else {
      Assert.Fail();
    }
  }

  [TestMethod]
  public void DisconnectedMatchNoOutput() {
    Legend legend = _templates.CreateLegend();
    legend.AddPuzzle('@', "forall A B, A*B -> A*B");
    legend.AddCase('a', "pair");
    legend.AddConstant('b', "pair");
    legend.AddConstant('O', "O");
    // clang-format off
    const string schematic = (
"""
/* A B ab */
#@#i#i#i##########
#      +         #
#    ++++++++++++o
#    +           #
#    +  M+       #
#    +  a#i#i##  #
#    +  # +   #  #
#  b+A++++A+  #  #
#       #######  #
#                #
##################
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmitter state = new(_accessor);
    Random r = new(0);
    BlockPos puzzleBlock = new(1, 0, 1, 0);
    Token puzzle = state.Process(
        new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")),
        r, TestContext.FullyQualifiedTestClassName,
                     TestContext.TestName);
    if (puzzle is Function f) {
      CollectionAssert.AreEqual(new Token[] { f },
                                state.UnreferencedRoots.ToList());
    } else {
      Assert.Fail();
    }
  }

  [TestMethod]
  public void OrAndOne() {
    Legend legend = _templates.CreateLegend();
    legend.AddPuzzle('@', "forall A B C D, A+B -> C*D -> (A*C) + (B*D)");
    legend.AddCase('a', "inl");
    legend.AddCase('b', "inr");
    legend.AddCase('c', "pair");
    legend.AddConstant('d', "pair");
    legend.AddConstant('e', "inl");
    legend.AddConstant('g', "inr");
    // clang-format off
    const string schematic = (
"""
/* A B C D ab cd */
#@#i#i#i#i#i##i#########
#          +  +        #
#+++++++++++  +++M+++++o
#+               c#i#i #
#+               # + + #
#+M++++++++++++++o + + #
# a#i##          # + + #
# # +++++++ ++++++++ + #
# #       + +        + #
# #     d+A+A+       + #
# #          +       + #
# #        e+A+      + #
# #           +      + #
# #  ++++++++++      + #
# #  +o              + #
# #####              + #
# .....              + #
# b#i##              + #
# # +++++++ ++++++++++ #
# #       + +          #
# #     d+A+A+         #
# #          +         #
# #        g+A+        #
# #           +        #
# #  ++++++++++        #
# #  +o                #
# #####                #
#                      #
########################
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmitter state = new(_accessor);
    Random r = new(0);
    BlockPos puzzleBlock = new(1, 0, 1, 0);
    Token puzzle = state.Process(
        new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")),
        r, TestContext.FullyQualifiedTestClassName,
                     TestContext.TestName);
    if (puzzle is Function f) {
      CollectionAssert.AreEqual(new Token[] { puzzle },
                                state.UnreferencedRoots.ToList());

      Assert.AreEqual(
"""
Definition d: (forall A B C D, A+B -> C*D -> (A*C) + (B*D)):=
fun parameter_3_0_1_0_2 parameter_5_0_1_0_2 parameter_7_0_1_0_2 parameter_9_0_1_0_2 parameter_11_0_1_0_2 parameter_14_0_1_0_2 =>
  match parameter_14_0_1_0_2 with
  | pair parameter_19_0_4_0_2 parameter_21_0_4_0_2 =>
    match parameter_11_0_1_0_2 with
    | inl parameter_4_0_7_0_2 =>
      inl (pair parameter_4_0_7_0_2 parameter_19_0_4_0_2)
    | inr parameter_4_0_18_0_2 =>
      inr (pair parameter_4_0_18_0_2 parameter_21_0_4_0_2)
    end
  end.

""", state.EmitDefinition("d"));
    } else {
      Assert.Fail();
    }
  }

  [TestMethod]
  public void DoubleDanglingMatch() {
    Legend legend = _templates.CreateLegend();
    legend.AddPuzzle('@', "forall A B, A*B -> A*B -> nat");
    legend.AddCase('a', "pair");
    legend.AddConstant('b', "pair");
    legend.AddConstant('O', "O");
    // clang-format off
    const string schematic = (
"""
/* A B ab ab2 */
#@#i#i#i##i###########
#      +  +          #
# ++++++  ++         #
# +        +         #
# +        +         #
# +        +         #
# +M+      +M+       #
#  a#i#i##  a#i#i##  #
#    +   o    +   o  #
#    +        +      #
#  b+A++++++++A+     #
#                 O++o
######################
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmitter state = new(_accessor);
    Random r = new(0);
    BlockPos puzzleBlock = new(1, 0, 1, 0);
    Token puzzle = state.Process(
        new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")),
        r, TestContext.FullyQualifiedTestClassName,
                     TestContext.TestName);
    Function f = (Function)puzzle;
    CollectionAssert.AreEqual(new Token[] { puzzle },
                              state.UnreferencedRoots.ToList());
    TermInput t1 = (TermInput)f.Children[1].Children[0].Children[0].Children[0].Children[0];
    Assert.AreEqual(1, t1.Anchored.Count);
    TermInput t2 = (TermInput)t1.Anchored[0].Children[1].Children[0].Children[0].Children[0];
    Assert.AreEqual(1, t2.Anchored.Count);
    TermInput t3 = (TermInput)t2.Anchored[0].Children[1].Children[0].Children[0].Children[0];
    Assert.AreEqual(1, t3.Anchored.Count);

    Assert.AreEqual(
"""
Definition d: (forall A B, A*B -> A*B -> nat):=
fun parameter_3_0_1_0_2 parameter_5_0_1_0_2 parameter_7_0_1_0_2 parameter_10_0_1_0_2 =>
  let match_3_0_7_0_0 :=
    match parameter_7_0_1_0_2 with
    | pair parameter_5_0_8_0_2 _ =>
      let match_12_0_7_0_0 :=
        match parameter_10_0_1_0_2 with
        | pair parameter_14_0_8_0_2 _ =>
          let app_14_0_11_0_2 :=
            pair parameter_5_0_8_0_2 parameter_14_0_8_0_2 in
          _
        end in
      _
    end in
  O.

""", state.EmitDefinition("d"));
  }

  [TestMethod]
  public void DoubleMultiuse() {
    Legend legend = _templates.CreateLegend();
    legend.AddPuzzle('@', "forall A B, A*B -> A*B*A*B");
    legend.AddCase('a', "pair");
    legend.AddConstant('b', "pair");
    // clang-format off
    const string schematic = (
"""
/* A B ab */
#@#i#i#i#################
#      +                #
# ++++++++++++          #
# +          +          #
# +M++++++++ +M++++++++ #
#  a#i#i## +  a#i#i## + #
#  # +   # +  #   + # + #
#  # ++++o +  #   ++o + #
#  ####### +  ####### + #
#          +          + #
#    +++++++          + #
#    +                + #
#    + ++++++++++++++++ #
#    + +   +            #
#    ++%++ +            #
#    + + + +            #
#  b+A+A+A+A++++++++++++o
#########################
""");
    // clang-format on

    _accessor.SetSchematic(new BlockPos(0, 0, 0, 0), legend, schematic);

    using TokenEmitter state = new(_accessor);
    Random r = new(0);
    BlockPos puzzleBlock = new(1, 0, 1, 0);
    Token puzzle = state.Process(
        new NodePos(puzzleBlock, _accessor.FindNodeId(puzzleBlock, "scope")),
        r, TestContext.FullyQualifiedTestClassName,
                     TestContext.TestName);
    Function f = (Function)puzzle;
    CollectionAssert.AreEqual(new Token[] { puzzle },
                              state.UnreferencedRoots.ToList());
    Assert.AreEqual(
"""
Definition d: (forall A B, A*B -> A*B*A*B):=
fun parameter_3_0_1_0_2 parameter_5_0_1_0_2 parameter_7_0_1_0_2 =>
  let match_14_0_5_0_0 :=
    match parameter_7_0_1_0_2 with
    | pair _ parameter_18_0_6_0_2 =>
      parameter_18_0_6_0_2
    end in
  let match_3_0_5_0_0 :=
    match parameter_7_0_1_0_2 with
    | pair parameter_5_0_6_0_2 _ =>
      parameter_5_0_6_0_2
    end in
  pair match_3_0_5_0_0 match_14_0_5_0_0 match_3_0_5_0_0 match_14_0_5_0_0.

""", state.EmitDefinition("d"));
  }
}
