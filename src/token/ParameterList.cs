using System;
using System.Collections.Generic;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public class ParameterList : IDisposable {
  public SortedSet<Parameter> Parameters;
  public TermInput Result = null;

  public ParameterList(BlockFacing constructFace) {
    Parameters = new SortedSet<Parameter>(new TokenComparer(constructFace));
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

  public Token[] GetChildrenAtLevel(Parameter p) {
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

  public void Dispose() {
    if (Parameters == null) {
      return;
    }
    SortedSet<Parameter> parameters = Parameters;
    Parameters = null;
    foreach (Parameter p in parameters) {
      p.Dispose();
    }
    Result = null;
    parameters.Clear();
    GC.SuppressFinalize(this);
  }
}