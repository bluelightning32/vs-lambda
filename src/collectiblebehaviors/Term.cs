using System;
using System.Collections.Generic;
using System.Text;

using Lambda.Token;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Lambda.CollectibleBehaviors;

// Identifies the item as a Coq term and provides introspection of the term.
public class Term : CollectibleBehavior {
  private string _term;
  private string _type;
  private string _constructs;
  private bool? _isType;
  private bool? _isTypeFamily;
  private string[] _imports;
  private int[] _implicitArguments;

  public Term(CollectibleObject collObj) : base(collObj) {}

  public override void Initialize(JsonObject properties) {
    base.Initialize(properties);
    // `AsObject` converts the token into a string without the quotes, and
    // Newtonsoft fails to parse that back as an enum. So instead use the Token
    // directly.
    _imports = properties["imports"].AsArray<string>(Array.Empty<string>());
    _term = properties["term"].AsString();
    _type = properties["type"].AsString();
    _constructs = properties["constructs"].AsString();
    _isType =
        properties["isType"].Exists ? properties["isType"].AsBool() : null;
    _isTypeFamily = properties["isTypeFamily"].Exists
                        ? properties["isTypeFamily"].AsBool()
                        : null;
    _implicitArguments =
        properties["implicitArguments"].AsArray<int>(Array.Empty<int>());
  }

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);
    GetTermDict(api).Add(_term ?? "", collObj);
  }

  public string GetTerm(ItemStack stack) {
    return _term ?? stack.Attributes.GetAsString("term");
  }

  public string GetType(ItemStack stack) {
    return _type ?? stack.Attributes.GetAsString("type");
  }

  public int[] GetImplicitArguments(ItemStack stack) {
    return _implicitArguments;
  }

  public string GetConstructs(ItemStack stack) {
    return _constructs ?? stack.Attributes.GetAsString("constructs");
  }

  public bool IsConstructor(ItemStack stack) {
    return (GetConstructs(stack) ?? "") != "";
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

  public bool GetIsTypeFamily(ItemStack stack) {
    return _isTypeFamily ?? stack.Attributes.GetAsBool("isTypeFamily");
  }

  public string[] GetImports(ItemStack stack) {
    if (_imports != null) {
      return _imports;
    }
    return (stack.Attributes["imports"] as StringArrayAttribute)?.value ??
           Array.Empty<string>();
  }

  public static string Escape(string s) {
    return s == null ? "null" : s.Replace("<", "&lt;").Replace(">", "&gt;");
  }

  public override void GetHeldItemName(StringBuilder sb, ItemStack stack) {
    string term = GetTerm(stack);
    if (term != null) {
      sb.Clear();
      sb.Append(Lang.Get("lambda:term-name-prefix"));
      sb.Append(Escape(GetTerm(stack)));
    }
  }

  public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc,
                                       IWorldAccessor world,
                                       bool withDebugInfo) {
    base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    dsc.AppendLine($"Type: {Escape(GetType(inSlot.Itemstack))}");
    if (IsConstructor(inSlot.Itemstack)) {
      dsc.AppendLine(
          $"Constructor for type family: {Escape(GetConstructs(inSlot.Itemstack))}");
    }
    if (GetIsType(inSlot.Itemstack)) {
      dsc.AppendLine("Is type");
    } else if (GetIsTypeFamily(inSlot.Itemstack)) {
      dsc.AppendLine("Is type family");
    }
  }

  static private Dictionary<string, CollectibleObject>
  GetTermDict(ICoreAPI api) {
    return ObjectCacheUtil.GetOrCreate(
        api, $"lambda-terms",
        () => new Dictionary<string, CollectibleObject>());
  }

  public static ItemStack Find(ICoreAPI api, TermInfo termInfo) {
    Dictionary<string, CollectibleObject> termDict = GetTermDict(api);
    if (termDict.TryGetValue(termInfo.Term, out CollectibleObject term)) {
      return new ItemStack(term, 1);
    }
    CollectibleObject generic = termDict[""];
    TreeAttribute tree = new();
    termInfo.ToTreeAttributes(tree);
    ItemStack stack = new(generic.Id, generic.ItemClass, 1, tree, api.World);
    return stack;
  }

  public static ItemStack FindStandard(ICoreAPI api, string term) {
    Dictionary<string, CollectibleObject> termDict = GetTermDict(api);
    if (termDict.TryGetValue(term, out CollectibleObject termObj)) {
      return new ItemStack(termObj, 1);
    }
    return null;
  }
}
