using System.Collections.Generic;

using Lambda.Network;

namespace Lambda.Token;

public abstract class Token {
  public abstract IReadOnlyList<NodePos> Blocks { get; }

  // The location of all scope and match connector nodes that point to this
  // block as a source.
  public abstract IReadOnlyList<NodePos> ScopeMatchConnectors { get; }

  // The location of all term connector nodes that point to this block as a
  // source.
  public abstract IReadOnlyList<NodePos> TermConnectors { get; }

  public abstract ConstructRoot Construct { get; }

  public abstract IReadOnlyList<Token> Children { get; }

  public int Depth { get; private set; }
}

public abstract class TermSource : Token {
  private readonly List<NodePos> _termConnectors = new();
  public override IReadOnlyList<NodePos> TermConnectors => _termConnectors;

  public TermSource() {}

  public virtual void AddTermConnector(NodePos pos) {
    _termConnectors.Add(pos);
  }
}

public interface IScopeSource {
  public void AddScopeConnector(NodePos pos);
}

public abstract class ConstructRoot : TermSource {}