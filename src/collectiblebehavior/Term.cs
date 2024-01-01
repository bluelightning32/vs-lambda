using System.Text;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Lambda.CollectibleBehavior;

using VSCollectibleBehavior = Vintagestory.API.Common.CollectibleBehavior;

// Identifies the item as a Coq term and provides introspection of the term.
public class Term : VSCollectibleBehavior {
  private string _term;
  private string _type;
  private string _constructs;
  private bool? _isType;

  public Term(CollectibleObject collObj) : base(collObj) {}

  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);
    // `AsObject` converts the token into a string without the quotes, and
    // Newtonsoft fails to parse that back as an enum. So instead use the Token
    // directly.
    _term = properties["term"].AsString();
    _type = properties["type"].AsString();
    _constructs = properties["constructs"].AsString();
    _isType =
        properties["isType"].Exists ? properties["isType"].AsBool() : null;
  }

  public string GetTerm(ItemStack stack) {
    return _term ?? stack.Attributes.GetAsString("term");
  }

  public string GetType(ItemStack stack) {
    return _type ?? stack.Attributes.GetAsString("type");
  }

  public string GetConstructs(ItemStack stack) {
    return _constructs ?? stack.Attributes.GetAsString("constructs");
  }

  public bool IsFunction(ItemStack stack) {
    string type = GetType(stack);
    // For now do a very primitive test that the term is a function. Check
    // whether it has a -> or forall in the type.
    return type.Contains("->") || type.Contains("forall");
  }

  public bool GetIsType(ItemStack stack) {
    return _isType ?? stack.Attributes.GetAsBool("isType");
  }

  public static string Escape(string s) {
    return s.Replace("<", "&lt;").Replace(">", "&gt;");
  }

  public override void GetHeldItemName(StringBuilder sb, ItemStack stack) {
    if (_term == null) {
      sb.Append("term ");
      sb.Append(Escape(GetTerm(stack)));
    }
  }

  public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc,
                                       IWorldAccessor world,
                                       bool withDebugInfo) {
    base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    dsc.AppendLine($"Type: {Escape(GetType(inSlot.Itemstack))}");
    string constructs = GetConstructs(inSlot.Itemstack);
    if (constructs != null) {
      dsc.AppendLine($"Constructor for type family: {Escape(constructs)}");
    }
    if (GetIsType(inSlot.Itemstack)) {
      dsc.AppendLine("Type");
    }
  }
}