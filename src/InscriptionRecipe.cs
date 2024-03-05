using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Lambda;

public class PuzzleCheck : IByteSerializable {
  public string Name;
  public string Description;
  public string Check;

  public void ToBytes(BinaryWriter writer) {
    writer.Write(Name);
    writer.Write(Description);
    writer.Write(Check);
  }

  public void FromBytes(BinaryReader reader, IWorldAccessor resolver) {
    Name = reader.ReadString();
    Description = reader.ReadString();
    Check = reader.ReadString();
  }
}

public class InscriptionRecipe : RecipeBase<InscriptionRecipe>,
                                 IByteSerializable {
  public AssetLocation Description;
  public string PuzzleType;
  public AssetLocation Label;
  // The names of the parameters. If this is empty, then all of the parameters
  // are simply called 'parameter_<location>'.
  public string[] Parameters;
  public PuzzleCheck[] PuzzleChecks;
  public float ProcessTime = 1;

  public InscriptionRecipe() {}

  // Clones the `Ingredients` and `Output` but references the remaining fields.
  // This must clone the output, because `LoadGenericRecipe` calls this, then
  // proceeds to modify the output.
  public override InscriptionRecipe Clone() {
    return new InscriptionRecipe {
      RecipeId = RecipeId,         Ingredient = Ingredient.Clone(),
      Output = Output.Clone(),     Name = Name,
      Enabled = Enabled,           Description = Description,
      PuzzleType = PuzzleType,     Label = Label,
      PuzzleChecks = PuzzleChecks, ProcessTime = ProcessTime,
    };
  }

  private static List<String> GetWildcardValues<T>(AssetLocation wildcard,
                                                   string[] allowedVariants,
                                                   string[] skipVariants,
                                                   IList<T> objects)
      where T : CollectibleObject {
    Debug.Assert(wildcard.IsWildCard);
    List<string> result = new();
    foreach (CollectibleObject o in objects) {
      if (!WildcardUtil.Match(wildcard, o.Code)) {
        continue;
      }
      string codepart = WildcardUtil.GetWildcardValue(wildcard, o.Code);
      if (allowedVariants != null && !allowedVariants.Contains(codepart)) {
        continue;
      }
      if (skipVariants != null && skipVariants.Contains(codepart)) {
        continue;
      }
      result.Add(codepart);
    }
    return result;
  }

  public override Dictionary<string, string[]>
  GetNameToCodeMapping(IWorldAccessor world) {
    Dictionary<string, string[]> result = new();
    if (!Ingredient.IsWildCard) {
      return result;
    }
    List<string> values =
        Ingredient.Type == EnumItemClass.Block
            ? GetWildcardValues(Ingredient.Code, Ingredient.AllowedVariants,
                                Ingredient.SkipVariants, world.Blocks)
            : GetWildcardValues(Ingredient.Code, Ingredient.AllowedVariants,
                                Ingredient.SkipVariants, world.Items);
    result[Ingredient.Name ?? "input"] = values.ToArray();
    return result;
  }

  public override bool Resolve(IWorldAccessor world,
                               string sourceForErrorLogging) {
    if (Ingredients.Length != 1) {
      world.Logger.Warning(
          "Inscription recipe {0} must have exactly 1 ingredient but has {1}.",
          sourceForErrorLogging, Ingredients.Length);
      return false;
    }
    Description ??= new AssetLocation(Name.Domain, $"recipe-{Name.Path}-desc");
    Label ??= new AssetLocation(Name.Domain, $"recipe-{Name.Path}");
    return Ingredient.Resolve(world, sourceForErrorLogging) &&
           Output.Resolve(world, sourceForErrorLogging);
  }

  public void ToBytes(BinaryWriter writer) {
    writer.Write(RecipeId);
    Ingredient.ToBytes(writer);
    Output.ToBytes(writer);
    writer.Write(Name.ToShortString());
    writer.Write(Description.ToShortString());
    writer.Write(PuzzleType);
    writer.Write(Label.ToShortString());
    writer.Write(PuzzleChecks.Length);
    foreach (PuzzleCheck check in PuzzleChecks) {
      check.ToBytes(writer);
    }
  }

  public void FromBytes(BinaryReader reader, IWorldAccessor resolver) {
    RecipeId = reader.ReadInt32();
    Ingredient = new();
    Ingredient.FromBytes(reader, resolver);
    Output = new();
    Output.FromBytes(reader, resolver.ClassRegistry);
    Output.Resolve(resolver, "FromBytes");
    Name = new AssetLocation(reader.ReadString());
    Description = new AssetLocation(reader.ReadString());
    PuzzleType = reader.ReadString();
    Label = new AssetLocation(reader.ReadString());
    PuzzleChecks = new PuzzleCheck[reader.ReadInt32()];
    for (int i = 0; i < PuzzleChecks.Length; ++i) {
      PuzzleChecks[i] = new PuzzleCheck();
      PuzzleChecks[i].FromBytes(reader, resolver);
    }
  }
}
