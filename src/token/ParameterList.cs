using System;
using System.Collections.Generic;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public class ParameterList {
  public readonly SortedSet<Parameter> Parameters;
  public Token Result = null;

  public ParameterList(BlockFacing constructFace) {
    Parameters = new SortedSet<Parameter>(new ParameterComparer(constructFace));
  }

  public Parameter GetNext(Parameter after) {
    int index = 0;
    foreach (Parameter p in Parameters.GetViewBetween(after, Parameters.Max)) {
      if (index == 1) {
        return p;
      }
      ++index;
    }
    return null;
  }

  public IReadOnlyList<Token> GetChildrenAtLevel(Parameter p) {
    if (p == null) {
      if (Result == null) {
        return Array.Empty<Token>();
      }
      return new Token[] { Result };
    }
    if (p.Type != null) {
      return new Token[] { p.Type, p };
    }
    return new Token[] { p };
  }
}