using System;

using Lambda.Network;

namespace Lambda.Token;

public class InvalidFormatException : Exception {
  public readonly NodePos[] Nodes;
  public InvalidFormatException(NodePos[] nodes, string message)
      : base(message) {
    Nodes = nodes;
  }
}