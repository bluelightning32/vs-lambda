using System.Collections.Generic;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public class ParameterComparer : IComparer<Parameter> {
  private readonly int _axis1;
  private readonly int _axis2;
  private readonly int _axis3;
  public ParameterComparer(BlockFacing constructFace) {
    if (constructFace.IsHorizontal) {
      _axis3 = 1 | 4;
    } else {
      _axis3 = constructFace == BlockFacing.DOWN ? 1 : (1 | 4);
      constructFace = BlockFacing.NORTH;
    }
    _axis1 = (int)constructFace.Axis;
    if (constructFace.Normali[_axis1] < 0) {
      _axis1 = (-_axis1) | 4;
    }
    BlockFacing face2 = constructFace.GetCCW();
    _axis2 = (int)face2.Axis;
    if (face2.Normali[_axis2] < 0) {
      _axis2 = (-_axis2) | 4;
    }
  }

  public int Compare(Parameter x, Parameter y) {
    /*

    +-> x
    |
    v
    z

    North: sort by -x then -z
    #10##9#
    8     7
    6     5
    4    _3
    #2#1#B#

    East: sort by -z then x
    #4#6#8#
    2     10
    1     #
    B|    9
    #3#5#7#
    */
    // Sort nulls first
    if (x == null) {
      return y == null ? 0 : -1;
    }
    if (y == null) {
      return 1;
    }
    if (x.Pos.Block.dimension != y.Pos.Block.dimension) {
      return x.Pos.Block.dimension - y.Pos.Block.dimension;
    }
    if (IsAxisDifferent(_axis1, x.Pos.Block, y.Pos.Block, out int result)) {
      return result;
    }
    if (IsAxisDifferent(_axis2, x.Pos.Block, y.Pos.Block, out result)) {
      return result;
    }
    if (IsAxisDifferent(_axis3, x.Pos.Block, y.Pos.Block, out result)) {
      return result;
    }
    return x.Pos.NodeId - y.Pos.NodeId;
  }

  static public bool IsAxisDifferent(int axis, BlockPos x, BlockPos y,
                                     out int result) {
    int mul = 0;
    if ((axis & 4) != 0) {
      mul = int.MinValue;
      axis &= 3;
    }
    if (x[axis] == y[axis]) {
      result = 0;
      return false;
    }
    result = (x[axis] - y[axis]) ^ mul;
    return true;
  }
}