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
    return scope switch {
      Scope.None => "none",
      Scope.Function => "function",
      Scope.Case => "case",
      Scope.Forall => "forall",
      Scope.Matchin => "matchin",
      _ => "unknown",
    };
  }
  public static Scope FromCode(string code, Scope def = Scope.Function) {
    return code switch {
      "none" => Scope.None,
      "function" => Scope.Function,
      "case" => Scope.Case,
      "forall" => Scope.Forall,
      "matchin" => Scope.Matchin,
      _ => def,
    };
  }
}