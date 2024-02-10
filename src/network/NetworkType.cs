using System;

namespace Lambda.Network;

public enum NetworkType {
  Placeholder = 0,
  Match = 1,
  Scope = 2,
  Term = 3,
  Max = Term
}

public static class NetworkTypeExtension {
  public static string GetCode(this NetworkType network) {
    return network.ToString().ToLower();
  }
  public static NetworkType FromCode(string code,
                                     NetworkType def = NetworkType.Match) {
    if (!Enum.TryParse<NetworkType>(code, true, out NetworkType result)) {
      result = def;
    }
    return result;
  }
}