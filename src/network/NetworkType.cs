using System;

namespace Lambda.Network;

public enum NetworkType { Match = 0, Scope = 1, Term = 2, Max = Term }

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