using System;

namespace LambdaFactory;

public enum Scope {
  None = 0,
  Function = 1,
  Case = 2,
  Forall = 3,
  Matchin = 4,
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