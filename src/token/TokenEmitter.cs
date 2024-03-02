using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Lambda.Network;

using Vintagestory.API.MathTools;

namespace Lambda.Token;

public class TokenEmitter : IDisposable {
  private readonly NodeAccessor _accessor;
  // Nodes that have already been created but whose references have not been
  // fully visited yet.
  private readonly Dictionary<NodePos, Token> _prepared = new();
  public IReadOnlyDictionary<NodePos, Token> Prepared { get => _prepared; }
  private readonly List<NodePos> _pending = new();
  private readonly HashSet<NodePos> _pendingSources = new();
  private readonly List<ConstructRoot> _unreferencedRoots = new();
  private readonly List<ConstructRoot> _topLevelMultiuse = new();
  public IReadOnlyList<ConstructRoot> UnreferencedRoots {
    get => _unreferencedRoots;
  }

  private Token _main = null;

  public TokenEmitter(NodeAccessor accessor) { _accessor = accessor; }

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
    _main = EmitPos(start);
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
    Dictionary<ConstructRoot, HashSet<Parameter>> newEdgesByTarget =
        CollectConstructParameterParents();
    Dictionary<Parameter, HashSet<ConstructRoot>> newEdgesBySource =
        ReverseEdges(newEdgesByTarget);
    SaveGraphviz(testClass, testName, ".parameters", newEdgesBySource);
    ScopeUnused(newEdgesBySource, newEdgesByTarget);
    SaveGraphviz(testClass, testName, ".unusedscoped", null);
    ScopeMultiuse();
    SaveGraphviz(testClass, testName, ".scoped", null);
    return _main;
  }

  private static bool AreEdgesPaired(
      Dictionary<Parameter, HashSet<ConstructRoot>> newEdgesBySource,
      Dictionary<ConstructRoot, HashSet<Parameter>> newEdgesByTarget) {
    foreach (KeyValuePair<Parameter, HashSet<ConstructRoot>> pair in
                 newEdgesBySource) {
      foreach (ConstructRoot target in pair.Value) {
        if (!newEdgesByTarget[target].Contains(pair.Key)) {
          return false;
        }
      }
    }
    foreach (KeyValuePair<ConstructRoot, HashSet<Parameter>> pair in
                 newEdgesByTarget) {
      foreach (Parameter source in pair.Value) {
        if (!newEdgesBySource[source].Contains(pair.Key)) {
          return false;
        }
      }
    }
    return true;
  }

  private void
  ScopeUnused(Dictionary<Parameter, HashSet<ConstructRoot>> newEdgesBySource,
              Dictionary<ConstructRoot, HashSet<Parameter>> newEdgesByTarget) {
    Debug.Assert(AreEdgesPaired(newEdgesBySource, newEdgesByTarget));
    HashSet<Token> ancestors = new();
    while (newEdgesBySource.Count != 0) {
      bool added = false;
      // Try the adding the new edges as deep as possible in all unreferenced
      // roots that would be referenced if the new edges were added.
      if (_main is ConstructRoot mainc) {
        HashSet<Parameter> remaining = new();
        Dictionary<ConstructRoot, HashSet<Parameter>> cache = new();
        added = ScopeUnusedVisit(newEdgesBySource, newEdgesByTarget, mainc,
                                 ancestors, false, cache, remaining);
        Debug.Assert(remaining.Count == 0);
        if (added) {
          continue;
        }
      }
      // If that did not add any edges, then try adding the new edges as deep as
      // possible in all of the unreferenced roots except for the main one. The
      // unreferenced roots are prioritized, because they can be changed into
      // child, whereas the main root should remain parentless.
      foreach (ConstructRoot c in _unreferencedRoots.ToArray()) {
        if (c == _main) {
          continue;
        }
        HashSet<Parameter> remaining = new();
        Dictionary<ConstructRoot, HashSet<Parameter>> cache = new();
        added |= ScopeUnusedVisit(newEdgesBySource, newEdgesByTarget, c,
                                  ancestors, true, cache, remaining);
        InsertEdgesByTarget(c, remaining, newEdgesBySource, newEdgesByTarget);
      }
      if (added) {
        continue;
      }
      // If none of the above worked, try adding the new edges somewhere inside
      // the main tree.
      if (_main is ConstructRoot mainc2) {
        HashSet<Parameter> remaining = new();
        Dictionary<ConstructRoot, HashSet<Parameter>> cache = new();
        added = ScopeUnusedVisit(newEdgesBySource, newEdgesByTarget, mainc2,
                                 ancestors, true, cache, remaining);
        InsertEdgesByTarget(mainc2, remaining, newEdgesBySource,
                            newEdgesByTarget);
      }
    }
  }

  private static void InsertEdgesByTarget(
      ConstructRoot target, HashSet<Parameter> sources,
      Dictionary<Parameter, HashSet<ConstructRoot>> edgesBySource,
      Dictionary<ConstructRoot, HashSet<Parameter>> edgesByTarget) {
    if (sources.Count != 0) {
      if (!edgesByTarget.TryAdd(target, sources)) {
        edgesByTarget[target].UnionWith(sources);
      }
      foreach (Parameter source in sources) {
        if (!edgesBySource.TryGetValue(source,
                                       out HashSet<ConstructRoot> targets)) {
          targets = new();
          edgesBySource.Add(source, targets);
        }
        targets.Add(target);
      }
    }
  }

  private bool ScopeUnusedVisit(
      Dictionary<Parameter, HashSet<ConstructRoot>> newEdgesBySource,
      Dictionary<ConstructRoot, HashSet<Parameter>> newEdgesByTarget, Token t,
      HashSet<Token> ancestors, bool allowAdd,
      Dictionary<ConstructRoot, HashSet<Parameter>> cache,
      HashSet<Parameter> remaining) {
    bool result = false;
    HashSet<Parameter> localRemaining = remaining;
    if (t is ConstructRoot c && c.IncomingEdgeCount > 1) {
      if (cache.TryGetValue(c, out HashSet<Parameter> cachedResult)) {
        remaining.UnionWith(cachedResult);
        return result;
      }
      localRemaining = new();
    }
    if (!ancestors.Add(t)) {
      throw new InvalidOperationException("The graph has a cycle.");
    }
    if (t is TermInput) {
      foreach (Token child in t.Children) {
        // Ignore TermInput to Parameter edges to prevent cycles.
        if (child is not Parameter) {
          result |=
              ScopeUnusedVisit(newEdgesBySource, newEdgesByTarget, child,
                               ancestors, allowAdd, cache, localRemaining);
        }
      }
    } else {
      foreach (Token child in t.Children) {
        result |= ScopeUnusedVisit(newEdgesBySource, newEdgesByTarget, child,
                                   ancestors, allowAdd, cache, localRemaining);
      }
      if (t is Parameter p) {
        if (newEdgesBySource.TryGetValue(p,
                                         out HashSet<ConstructRoot> targets)) {
          foreach (ConstructRoot child in targets) {
            if (allowAdd) {
              result |=
                  ScopeUnusedVisit(newEdgesBySource, newEdgesByTarget, child,
                                   ancestors, true, cache, localRemaining);
            } else {
              HashSet<Parameter> localRemaining2 = new();
              result |=
                  ScopeUnusedVisit(newEdgesBySource, newEdgesByTarget, child,
                                   ancestors, true, cache, localRemaining2);
              InsertEdgesByTarget(child, localRemaining2, newEdgesBySource,
                                  newEdgesByTarget);
            }
          }
        }
        if (allowAdd && newEdgesBySource.Remove(p, out targets)) {
          foreach (ConstructRoot target in targets) {
            if (target.IncomingEdgeCount != 0) {
              throw new InvalidOperationException(
                  "construct already linked in.");
            }
            // This adds the edge from `p` to `target` and updates the
            // IncomingEdgeCount of `target`.
            target.AddAnchor(p);
            result = true;
            if (!_unreferencedRoots.Remove(target)) {
              throw new InvalidOperationException(
                  "Unreferenced construct was not in the unreferenced constructs list.");
            }
            if (!newEdgesByTarget.Remove(target,
                                         out HashSet<Parameter> sources)) {
              throw new InvalidOperationException(
                  "Edge missing in by-target dictionary.");
            }
            // `t` takes over all other new edges pointing to `target`.
            foreach (Parameter source in sources) {
              if (source != p) {
                newEdgesBySource[source].Remove(target);
                localRemaining.Add(source);
              }
            }
          }
        }
        localRemaining.Remove(p);
      }
    }
    if (!ancestors.Remove(t)) {
      throw new InvalidOperationException(
          "Invariant violation: ancestor already removed.");
    }
    if (!ReferenceEquals(localRemaining, remaining)) {
      if (!cache.TryAdd((ConstructRoot)t, localRemaining)) {
        throw new InvalidOperationException(
            "Invariant violation: cache is already populated.");
      }
      remaining.UnionWith(localRemaining);
    }
    return result;
  }

  public void ScopeMultiuse() {
    AnchorPoint tracker = new();
    int i = 0;
    foreach (ConstructRoot c in _unreferencedRoots.ToArray()) {
      c.ScopeMultiuse(tracker, false);
      for (; i < tracker.ReadyCount; ++i) {
        ConstructRoot r = tracker.Ready[i];
        r.ScopeMultiuseReady(tracker);
      }
    }
    if (tracker.ReadyCount != 0) {
      _topLevelMultiuse.AddRange(tracker.Ready);
      _topLevelMultiuse.Reverse(_topLevelMultiuse.Count - tracker.Ready.Count,
                                tracker.Ready.Count);
    }
    tracker.Done();
  }

  private static Dictionary<Parameter, HashSet<ConstructRoot>>
  ReverseEdges(Dictionary<ConstructRoot, HashSet<Parameter>> rev) {
    Dictionary<Parameter, HashSet<ConstructRoot>> result = new();
    foreach (KeyValuePair<ConstructRoot, HashSet<Parameter>> pair in rev) {
      foreach (Parameter p in pair.Value) {
        if (!result.TryGetValue(p, out HashSet<ConstructRoot> list)) {
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
               Dictionary<Parameter, HashSet<ConstructRoot>> extraEdges) {
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
               Dictionary<Parameter, HashSet<ConstructRoot>> extraEdges) {
    GraphvizEmitter graphviz = new(writer);
    graphviz.WriteHeader(name);
    foreach (ConstructRoot root in _unreferencedRoots) {
      graphviz.Add(root);
    }
    if (extraEdges != null) {
      graphviz.StartSubgraph("extraedges", "blue");
      foreach (KeyValuePair<Parameter, HashSet<ConstructRoot>> pair in
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

  public Dictionary<ConstructRoot, HashSet<Parameter>>
  CollectConstructParameterParents() {
    Dictionary<ConstructRoot, HashSet<Parameter>> result = new();
    Dictionary<ConstructRoot, HashSet<Parameter>> cache = new();
    HashSet<Token> ancestors = new();
    foreach (ConstructRoot c in _unreferencedRoots) {
      HashSet<Parameter> hashset = new();
      CollectConstructParameterParents(c, ancestors, cache, hashset);
      result[c] = hashset;
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

  public string EmitDefinition(string name) {
    StringWriter writer = new();
    CoqEmitter emitter = new(writer);
    ((ConstructRoot)_main).EmitDefinition(name, emitter);
    string result = writer.ToString();
    CoqSanitizer.Sanitize(new StringReader(result));
    return result;
  }
}
