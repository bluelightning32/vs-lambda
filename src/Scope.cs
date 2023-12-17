using System;

namespace LambdaFactory;

public enum Scope {
  None = -1,
  Function = 0,
  Case = 1,
  Forall = 2,
  Matchin = 3,
  Min = None,
  Max = Matchin
}

public static class ScopeExtension {
  public static string GetCode(this Scope scope) {
    return scope.ToString().ToLower();
  }
  public static Scope FromCode(string code, Scope def = Scope.Function) {
    Scope result;
    if (!Enum.TryParse<Scope>(code, true, out result)) {
      result = def;
    }
    return result;
  }
}