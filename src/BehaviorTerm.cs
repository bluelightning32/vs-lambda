using System.Text;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace LambdaFactory;

// Forwards more methods from the Block to the BlockEntity.
public class BehaviorTerm : CollectibleBehavior {
  private string _term;
  private string _type;
  private string _constructs;
  private bool? _isType;

  public BehaviorTerm(CollectibleObject collObj) : base(collObj) {}

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