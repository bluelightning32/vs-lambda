using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Lambda.Network;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public class TokenEmissionState : IDisposable {
  private readonly NodeAccessor _accessor;
  // Nodes that have already been created but whose references have not been
  // fully visited yet.
  private readonly Dictionary<NodePos, Token> _prepared = new();
  public IReadOnlyDictionary<NodePos, Token> Prepared { get => _prepared; }
  private readonly List<NodePos> _pending = new();
  private readonly HashSet<NodePos> _pendingSources = new();
  private readonly List<ConstructRoot> _unreferencedRoots = new();
  public IReadOnlyList<ConstructRoot> UnreferencedRoots {
    get => _unreferencedRoots;
  }

  public TokenEmissionState(NodeAccessor accessor) { _accessor = accessor; }

  private Token EmitPos(NodePos pos) {
    BlockNodeTemplate template = _accessor.GetBlock(pos.Block, out Node[] nodes,
                                                    out string inventoryTerm);
    return template.Emit(this, pos, nodes, inventoryTerm);
  }

  public Token Process(NodePos start) {
    Token result = EmitPos(start);
    while (_pending.Count != 0) {
      NodePos popped = _pending[_pending.Count - 1];
      _pending.RemoveAt(_pending.Count - 1);
      EmitPos(popped);
    }
    Debug.Assert(_pending.Count == 0);
    Debug.Assert(
        _prepared.Count == 0,
        $"Prepared nodes were not cleared, remaining={_prepared.Count} first={_prepared.First()}");
    return result;
  }

  // Emit all of the nodes starting at `start`, while processing the pending
  // queue in a randomized order.
  //
  // Graphviz files are written along the way if the GRAPHVIZ environmental
  // variable is set and `testClass` is non-null.
  public Token Process(NodePos start, Random random, string testClass,
                       string testName) {
    Token result = EmitPos(start);
    while (_pending.Count != 0) {
      ShufflePending(random);
      VerifyInvariants();
      NodePos popped = _pending[_pending.Count - 1];
      _pending.RemoveAt(_pending.Count - 1);
      _pendingSources.Remove(popped);
      EmitPos(popped);
      VerifyInvariants();
    }
    SaveGraphviz(testClass, testName, "", null);
    Debug.Assert(_pending.Count == 0);
    Debug.Assert(
        _prepared.Count == 0,
        $"Prepared nodes were not cleared, remaining={_prepared.Count} first={_prepared.First()}");
    Dictionary<Parameter, List<ConstructRoot>> extraEdges =
        ReverseEdges(CollectConstructParameterParents());
    SaveGraphviz(testClass, testName, ".parameters", extraEdges);
    ScopeUnused(extraEdges);
    SaveGraphviz(testClass, testName, ".scoped", null);
    return result;
  }

  public void
  ScopeUnused(Dictionary<Parameter, List<ConstructRoot>> extraEdges) {
    Dictionary<Token, bool> visited = new();
    foreach (ConstructRoot c in _unreferencedRoots.ToArray()) {
      ScopeUnusedVisit(extraEdges, c, visited);
    }
  }

  private void
  ScopeUnusedVisit(Dictionary<Parameter, List<ConstructRoot>> extraEdges,
                   Token t, Dictionary<Token, bool> visited) {
    // Mark t with a temporary mark
    if (!visited.TryAdd(t, false)) {
      if (!visited[t]) {
        throw new InvalidOperationException("The graph has a cycle.");
      }
      return;
    }
    if (t is TermInput) {
      foreach (Token c in t.Children) {
        // Ignore TermInput to Parameter edges to prevent cycles.
        if (c is not Parameter) {
          ScopeUnusedVisit(extraEdges, c, visited);
        }
      }
    } else {
      foreach (Token c in t.Children) {
        ScopeUnusedVisit(extraEdges, c, visited);
      }
      if (t is Parameter p) {
        if (extraEdges.TryGetValue(p, out List<ConstructRoot> addEdges)) {
          foreach (ConstructRoot c in addEdges) {
            if (c.IncomingEdgeCount != 0) {
              // This construct was already linked in.
              continue;
            }
            c.AddAnchor(p);
            ScopeUnusedVisit(extraEdges, c, visited);
            if (!_unreferencedRoots.Remove(c)) {
              throw new InvalidOperationException(
                  "Unreferenced construct was not in the unreferenced constructs list.");
            }
          }
        }
      }
    }
    visited[t] = true;
  }

  private static Dictionary<Parameter, List<ConstructRoot>>
  ReverseEdges(Dictionary<ConstructRoot, List<Parameter>> rev) {
    Dictionary<Parameter, List<ConstructRoot>> result = new();
    foreach (KeyValuePair<ConstructRoot, List<Parameter>> pair in rev) {
      foreach (Parameter p in pair.Value) {
        if (!result.TryGetValue(p, out List<ConstructRoot> list)) {
          list = new();
          result.Add(p, list);
        }
        list.Add(pair.Key);
      }
    }
    return result;
  }

  public void ShufflePending(Random random) {
    for (int i = 0; i < _pending.Count; ++i) {
      int swapWith = i + random.Next(_pending.Count - i);
      (_pending[swapWith], _pending[i]) = (_pending[i], _pending[swapWith]);
    }
  }

  public void FinishPrepared(Token token) {
    bool removed = false;
    foreach (NodePos pos in token.Blocks) {
      if (_prepared.Remove(pos, out Token removedToken)) {
        Debug.Assert(removedToken == token);
        removed = true;
      }
    }
    if (!removed) {
      throw new KeyNotFoundException("Token not found in prepared list.");
    }
    if (token is ConstructRoot root) {
      if (root.IncomingEdgeCount == 0) {
        AddUnreferencedRoot(root);
      }
    }
  }

  public void AddUnreferencedRoot(ConstructRoot root) {
    _unreferencedRoots.Add(root);
  }

  public bool PreparedContains(NodePos pos, Token token) {
    return _prepared.TryGetValue(pos, out Token contained) &&
           token == contained;
  }
  public bool PreparedContains(Token token) {
    foreach (NodePos pos in token.Blocks) {
      if (_prepared.ContainsKey(pos)) {
        return true;
      }
    }
    return false;
  }

  public void AddPending(NodePos pos) {
    if (_pending.Contains(pos)) {
      Debug.Assert(false);
    }
    _pending.Add(pos);
  }

  public void AddPrepared(NodePos tokenPos, Token token) {
    _prepared.Add(tokenPos, token);
  }

  public void AddPrepared(NodePos tokenPos, Token token, NodePos refFor) {
    AddPrepared(tokenPos, token);
    token.AddRef(this, refFor);
  }

  public Token TryGetSource(NodePos source) {
    Debug.Assert(
        _accessor.GetNode(source.Block, source.NodeId, out Node node).IsSource);
    if (Prepared.TryGetValue(source, out Token sourceToken)) {
      return sourceToken;
    }
    return null;
  }

  public void Dispose() {
    foreach (KeyValuePair<NodePos, Token> p in _prepared) {
      p.Value.Dispose();
    }
    _prepared.Clear();
    foreach (ConstructRoot r in _unreferencedRoots) {
      r.Dispose();
    }
    _unreferencedRoots.Clear();
  }

  public void
  SaveGraphviz(string testClass, string testName, string stage,
               Dictionary<Parameter, List<ConstructRoot>> extraEdges) {
    if (testClass == null) {
      return;
    }
    // To save graphviz files, run the tests with:
    // dotnet test -c Debug --logger:"console;verbosity=detailed" -e GRAPHVIZ=1
    //
    // clang-format off
    //
    // To render the file, use a command like the following:
    // dot -Tsvg test/bin/Debug/net7.0/Lambda.Tests.FunctionTemplateTest.NestedPassthrough.dot -o NestedPassthrough.svg
    //
    // clang-format on
    if (Environment.GetEnvironmentVariable("GRAPHVIZ") == null) {
      return;
    }
    using StreamWriter writer = new($"{testClass}.{testName}{stage}.dot");
    SaveGraphviz(testName, writer, extraEdges);
  }

  public void
  SaveGraphviz(string name, TextWriter writer,
               Dictionary<Parameter, List<ConstructRoot>> extraEdges) {
    GraphvizState graphviz = new(writer);
    graphviz.WriteHeader(name);
    foreach (ConstructRoot root in _unreferencedRoots) {
      graphviz.Add(root);
    }
    if (extraEdges != null) {
      graphviz.StartSubgraph("extraedges", "blue");
      foreach (KeyValuePair<Parameter, List<ConstructRoot>> pair in
                   extraEdges) {
        foreach (ConstructRoot target in pair.Value) {
          graphviz.WriteSubgraphEdge(pair.Key, target);
        }
      }
      graphviz.EndSubgraph();
    }
    graphviz.WriteFooter();
  }

  public Token AddPort(NodePos source, BlockPos childPos, NodeTemplate child) {
    BlockNodeTemplate template = _accessor.GetBlock(
        source.Block, out Node[] nodes, out string inventoryTerm);
    return ((IAcceptScopePort) template)
        .AddPort(this, source, nodes, inventoryTerm, childPos, child);
  }

  public Case AddCase(NodePos source, NodePos childMatchPos, int childScopeId,
                      BlockFacing face, string inventoryTerm) {
    BlockNodeTemplate template = _accessor.GetBlock(
        source.Block, out Node[] nodes, out string matchInventory);
    return ((MatchTemplate) template)
        .AddCase(this, source, nodes, childMatchPos, childScopeId, face,
                 inventoryTerm);
  }

  public MatchIn AddMatchIn(NodePos source, NodePos childMatchPos,
                            int childScopeId, BlockFacing face,
                            string inventoryTerm) {
    BlockNodeTemplate template = _accessor.GetBlock(
        source.Block, out Node[] nodes, out string matchInventory);
    return ((MatchTemplate) template)
        .AddMatchIn(this, source, nodes, childMatchPos, childScopeId, face,
                    inventoryTerm);
  }

  public void VerifyInvariants() {
    HashSet<NodePos> sourceHasPending = new();
    HashSet<NodePos> pendingSet = new();
    foreach (NodePos pos in _pending) {
      if (!pendingSet.Add(pos)) {
        throw new Exception(
            $"The pending list contains multiple copies of {pos}.");
      }
      BlockNodeTemplate template = _accessor.GetBlock(
          pos.Block, out Node[] nodes, out string inventoryTerm);
      NodePos source = nodes[pos.NodeId].Source;
      if (!nodes[pos.NodeId].IsConnected()) {
        source = pos;
      }
      if (!_prepared.ContainsKey(source) && !_pendingSources.Contains(source)) {
        throw new Exception(
            $"Pending node {pos} does not have a prepared source at {source}.");
      }
      sourceHasPending.Add(source);
    }
    foreach (NodePos pos in _pendingSources) {
      if (!pendingSet.Contains(pos)) {
        throw new Exception($"{pos} is in _pendingSources but not _pending.");
      }
    }
    foreach (KeyValuePair<NodePos, Token> entry in _prepared) {
      if (entry.Value.PendingRef == 0) {
        throw new Exception($"Prepared node {entry.Key} has 0 references.");
      }
      foreach (NodePos refHolder in entry.Value.PendingRefLocations) {
        BlockNodeTemplate template = _accessor.GetBlock(
            refHolder.Block, out Node[] nodes, out string inventoryTerm);
        NodePos source = nodes[refHolder.NodeId].Source;
        if (!nodes[refHolder.NodeId].IsConnected()) {
          source = refHolder;
        }
        if (!sourceHasPending.Contains(source)) {
          throw new Exception(
              $"Prepared node {entry.Key} is referenced by {refHolder}, which has source {source}, but the source has no pending children.");
        }
      }
    }
  }

  public void MaybeAddPendingSource(NodePos sourcePos) {
    if (Prepared.TryGetValue(sourcePos, out Token portSource)) {
      // If the port source is already in the prepared dict, then the source
      // or its connectors should already be pending.
      Debug.Assert(portSource.PendingRef > 0);
      return;
    }
    if (_pendingSources.Add(sourcePos)) {
      AddPending(sourcePos);
    }
  }

  public void MaybeAddPendingSource(NodePos child, Node[] nodes) {
    NodePos childSource = nodes[child.NodeId].Source;
    if (!nodes[child.NodeId].IsConnected()) {
      childSource = child;
    }
    MaybeAddPendingSource(childSource);
  }

  public Dictionary<ConstructRoot, List<Parameter>>
  CollectConstructParameterParents() {
    Dictionary<ConstructRoot, List<Parameter>> result = new();
    Dictionary<ConstructRoot, HashSet<Parameter>> cache = new();
    HashSet<Token> ancestors = new();
    foreach (ConstructRoot c in _unreferencedRoots) {
      HashSet<Parameter> hashset = new();
      CollectConstructParameterParents(c, ancestors, cache, hashset);
      result[c] = hashset.ToList();
    }
    return result;
  }

  private void CollectConstructParameterParents(
      Token t, HashSet<Token> ancestors,
      Dictionary<ConstructRoot, HashSet<Parameter>> cache,
      HashSet<Parameter> result) {
    HashSet<Parameter> localResult = result;
    if (t is ConstructRoot c && c.IncomingEdgeCount > 1) {
      if (cache.TryGetValue(c, out HashSet<Parameter> cachedResult)) {
        result.UnionWith(cachedResult);
        return;
      }
      localResult = new();
    }
    if (!ancestors.Add(t)) {
      throw new InvalidOperationException("The graph has a cycle.");
    }
    if (t is TermInput) {
      foreach (Token child in t.Children) {
        if (child is Parameter p) {
          localResult.Add(p);
        } else {
          CollectConstructParameterParents(child, ancestors, cache,
                                           localResult);
        }
      }
    } else {
      foreach (Token child in t.Children) {
        CollectConstructParameterParents(child, ancestors, cache, localResult);
      }
      if (t is Parameter p) {
        localResult.Remove(p);
        if (!ReferenceEquals(localResult, result)) {
          result.Remove(p);
        }
      }
    }
    if (!ancestors.Remove(t)) {
      throw new InvalidOperationException(
          "Invariant violation: ancestor already removed.");
    }
    if (!ReferenceEquals(localResult, result)) {
      if (!cache.TryAdd((ConstructRoot)t, localResult)) {
        throw new InvalidOperationException(
            "Invariant violation: cache is already populated.");
      }
      result.UnionWith(localResult);
    }
  }
}
