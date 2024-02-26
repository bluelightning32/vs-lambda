using System;
using System.Diagnostics;

using Vintagestory.API.Datastructures;

namespace Lambda.Network;

public struct Node {
  public NodePos Source;
  public Scope Scope = Scope.None;
  // The index of the edge in this node that points to the parent node. If the
  // network is fully up to date, then the parent edges point to the source
  // block.
  //
  // This default initializes to Edge.Unknown.
  public Edge Parent;
  public static readonly int InfDistance = Int32.MaxValue;
  public int PropagationDistance = InfDistance;
  public bool HasInfDistance {
    get { return PropagationDistance == InfDistance; }
  }

  public Node() {}

  // Array allocation does not call the struct default constructor. See
  // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-10.0/parameterless-struct-constructors#array-allocation.
  //
  // So call this on all members of the array after allocating the array.
  public static void ArrayInitialize(Node[] nodes) {
    for (int i = 0; i < nodes.Length; ++i) {
      nodes[i].PropagationDistance = InfDistance;
    }
  }

  public bool IsConnected() { return Source.IsSet() && !HasInfDistance; }

  public bool IsDisconnected() { return Source.IsSet() && HasInfDistance; }

  public bool IsEjected() { return !Source.IsSet(); }

  public bool IsSource() { return PropagationDistance == 0; }

  public TreeAttribute ToTreeAttributes() {
    TreeAttribute tree = new TreeAttribute();
    if (Source.IsSet()) {
      tree.SetBlockPos("SourceBlock", Source.Block);
      tree.SetInt("SourceNodeId", Source.NodeId);
      tree.SetInt("Scope", (int)Scope);
      tree.SetInt("Parent", (int)Parent);
      tree.SetInt("PropagationDistance", PropagationDistance);
    }
    return tree;
  }

  public bool FromTreeAttributes(TreeAttribute tree) {
    Source.Block = tree.GetBlockPos("SourceBlock", null);
    Source.NodeId = tree.GetAsInt("SourceNodeId", 0);
    Scope oldScope = Scope;
    Scope = (Scope)tree.GetAsInt("Scope", (int)Scope.None);
    Parent = (Edge)tree.GetAsInt("Parent", (int)Edge.Unknown);
    PropagationDistance = tree.GetAsInt("PropagationDistance", InfDistance);
    return oldScope != Scope;
  }

  public override readonly string ToString() {
    return $"source=<{Source.Block?.ToString() ?? "null"}>:{Source.NodeId}, parent={Parent}, dist={PropagationDistance}";
  }

  public void Connect(Manager networkManager, Node node, Edge parentEdge) {
    Source = node.Source;
    Scope = node.Scope;
    Parent = parentEdge;
    Debug.Assert(!node.HasInfDistance);
    PropagationDistance =
        node.PropagationDistance + networkManager.DefaultDistanceIncrement;
  }

  public void SetDisconnected() {
    Parent = Edge.Unknown;
    PropagationDistance = InfDistance;
  }

  public void SetEjected() {
    SetDisconnected();
    Scope = Scope.None;
    Source.Block = null;
    Source.NodeId = 0;
  }
}
