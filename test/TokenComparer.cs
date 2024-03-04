using Lambda.Network;
using Lambda.Token;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.Tests;

[TestClass]
public class TokenComparerTest {

  public static Parameter MakeParameter(int x, int y, int z, int node) {
    return new Parameter(new NodePos(new BlockPos(x, y, z, 0), node), null,
                         null);
  }

  public static void AssertSorted(Parameter[] parameters,
                                  TokenComparer comparer) {
    for (int i = 0; i < parameters.Length; ++i) {
      Assert.IsTrue(comparer.Compare(parameters[i], parameters[i]) == 0);
      for (int j = i + 1; j < parameters.Length; ++j) {
        Assert.IsTrue(comparer.Compare(parameters[i], parameters[j]) < 0,
                      $"Index {i} is not less than index {j}.");
        Assert.IsTrue(comparer.Compare(parameters[j], parameters[i]) > 0,
                      $"Index {i} is not greater than index {j}.");
      }
    }
  }

  [TestMethod]
  public void CompareNorth() {

    /*
    North: sort by -x, then -z, then -y
    #######
    6    _4
    #2#0#B#
    */
    Parameter[] parameters = new Parameter[] {
      MakeParameter(3, 1, 2, 0), MakeParameter(3, 0, 2, 0),
      MakeParameter(1, 1, 2, 0), MakeParameter(1, 0, 2, 0),
      MakeParameter(6, 1, 1, 0), MakeParameter(6, 0, 1, 0),
      MakeParameter(0, 1, 1, 0), MakeParameter(0, 0, 1, 0),
    };
    TokenComparer comparer = new TokenComparer(BlockFacing.NORTH);
    AssertSorted(parameters, comparer);
  }

  [TestMethod]
  public void CompareEast() {

    /*
    #6###
    2   #
    0   #
    B|  #
    #4###
    */
    Parameter[] parameters = new Parameter[] {
      MakeParameter(0, 1, 2, 0), MakeParameter(0, 0, 2, 0),
      MakeParameter(0, 1, 1, 0), MakeParameter(0, 0, 1, 0),
      MakeParameter(1, 1, 4, 0), MakeParameter(1, 0, 4, 0),
      MakeParameter(1, 1, 0, 0), MakeParameter(1, 0, 0, 0),
    };
    TokenComparer comparer = new TokenComparer(BlockFacing.EAST);
    AssertSorted(parameters, comparer);
  }

  [TestMethod]
  public void CompareSouth() {

    /*
    #B#0#2#
    4-    6
    #######
    */
    Parameter[] parameters = new Parameter[] {
      MakeParameter(3, 1, 0, 0), MakeParameter(3, 0, 0, 0),
      MakeParameter(5, 1, 0, 0), MakeParameter(5, 0, 0, 0),
      MakeParameter(0, 1, 1, 0), MakeParameter(0, 0, 1, 0),
      MakeParameter(6, 1, 1, 0), MakeParameter(6, 0, 1, 0),
    };
    TokenComparer comparer = new TokenComparer(BlockFacing.SOUTH);
    AssertSorted(parameters, comparer);
  }

  [TestMethod]
  public void CompareWest() {
    /*
    ###4#
    #  |B
    #   0
    #   2
    ###6#
    */
    Parameter[] parameters = new Parameter[] {
      MakeParameter(4, 1, 2, 0), MakeParameter(4, 0, 2, 0),
      MakeParameter(4, 1, 3, 0), MakeParameter(4, 0, 3, 0),
      MakeParameter(3, 1, 0, 0), MakeParameter(3, 0, 0, 0),
      MakeParameter(3, 1, 4, 0), MakeParameter(3, 0, 4, 0),
    };
    TokenComparer comparer = new TokenComparer(BlockFacing.WEST);
    AssertSorted(parameters, comparer);
  }
}
