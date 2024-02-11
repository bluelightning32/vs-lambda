using System;
using System.Collections.Generic;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public class ParameterList {
  public readonly SortedSet<Parameter> Parameters;
  public TermInput ResultType = null;
  public TermInput Result = null;

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

  public static Token[] NonnullTokens(Token a, Token b) {
    if (a == null) {
      if (b == null) {
        return Array.Empty<Token>();
      }
      return new Token[] { b };
    }
    if (b == null) {
      return new Token[] { a };
    }
    return new Token[] { a, b };
  }

  public IReadOnlyList<Token> GetChildrenAtLevel(Parameter p) {
    if (p == null) {
      return NonnullTokens(ResultType, Result);
    }
    if (p.Type != null) {
      return new Token[] { p.Type, p };
    }
    return new Token[] { p };
  }
}