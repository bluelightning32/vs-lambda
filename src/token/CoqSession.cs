using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Lambda.Network;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Lambda.Token;

using RegexMatch = System.Text.RegularExpressions.Match;

public partial class TermInfo {
  public string ErrorMessage;
  public string[] Imports;
  public string Term;
  public string Type;
  public string Constructs;
  public bool IsType;
  public bool IsTypeFamily;

  public static TermInfo Error(string message, string filename) {
    MatchCollection locations =
        CoqResult.ParseErrorLocationGenerator().Matches(message);
    StringBuilder sb = new();
    int start = 0;
    foreach (RegexMatch m in locations) {
      if (!m.Groups[1].ValueSpan.SequenceEqual(filename)) {
        continue;
      }
      sb.Append(message.AsSpan(start, m.Index - start));
      start = m.Index + m.Length;
    }
    sb.Append(message.AsSpan(start, message.Length - start));
    return new() { ErrorMessage = sb.ToString() };
  }

  private static string RemoveOuterTypeScope(string term) {
    if (term.StartsWith('(') && term.EndsWith(")%type")) {
      return term[1.. ^ 6];
    }
    return term;
  }

  public static TermInfo Success(string[] imports, string message) {
    RegexMatch match = ParseInfo().Match(message);
    if (!match.Success) {
      throw new ArgumentException("Coq output does not match regex.");
    }
    TermInfo result = new() { Imports = imports,
                              Term = match.Groups[2].Value,
                              Type = RemoveOuterTypeScope(match.Groups[1].Value) };
    if (match.Groups[3].Value.StartsWith("constructor: ")) {
      result.Constructs =
          match.Groups[3].Value.Substring("constructor: ".Length);
    } else {
      result.IsType = match.Groups[3].Value switch {
        "prod" => true,
        "sort" => true,
        "inductive" => true,
        _ => false,
      };
      if (match.Groups[4].Value == "true" && result.IsType) {
        result.IsType = false;
        result.IsTypeFamily = true;
      }
    }
    return result;
  }
  [GeneratedRegex(
      @"^- : constr \* constr \* message \* bool =\s+\(constr:\((.+)\),\s+constr:\((.+)\),\s+message:\((.+)\),\s+(true|false)\)\s+$",
      RegexOptions.Compiled | RegexOptions.Singleline)]
  private static partial Regex ParseInfo();

  public void ToTreeAttributes(ITreeAttribute tree) {
    if (ErrorMessage != null) {
      tree.SetString("errorMessage", ErrorMessage);
    }
    if (Imports != null) {
      tree["imports"] = new StringArrayAttribute(Imports);
    }
    if (Term != null) {
      tree.SetString("term", Term);
    }
    if (Type != null) {
      tree.SetString("type", Type);
    }
    if (Constructs != null) {
      tree.SetString("constructs", Constructs);
    }
    tree.SetBool("isType", IsType);
    tree.SetBool("isTypeFamily", IsTypeFamily);
  }

  public void FromTreeAttributes(ITreeAttribute tree) {
    ErrorMessage = tree.GetAsString("ErrorMessage");
    Imports = (tree["imports"] as StringArrayAttribute)?.value;
    Term = tree.GetAsString("Term");
    Type = tree.GetAsString("Type");
    Constructs = tree.GetAsString("Constructs");
    IsType = tree.GetAsBool("IsType");
    IsTypeFamily = tree.GetAsBool("IsTypeFamily");
  }
}

public partial class CoqResult {
  public bool Successful;
  public string ErrorMessage = null;
  public HashSet<NodePos> ErrorLocations = new();

  public static CoqResult Success() {
    return new CoqResult() { Successful = true };
  }

  public static CoqResult Error(string message, string filename,
                                CoqEmitter emitter) {
    CoqResult result = new() { Successful = false };
    MatchCollection locations = ParseErrorLocationGenerator().Matches(message);
    StringBuilder sb = new();
    RegexMatch prev = null;
    foreach (RegexMatch m in locations) {
      if (!m.Groups[1].ValueSpan.SequenceEqual(filename)) {
        continue;
      }
      if (prev == null) {
        sb.Append(message.AsSpan(0, m.Index));
      } else {
        result.AddError(message, prev, m.Index, emitter, sb);
      }
      prev = m;
    }
    if (prev == null) {
      sb.Append(message);
    } else {
      result.AddError(message, prev, message.Length, emitter, sb);
    }
    result.ErrorMessage = sb.ToString();

    return result;
  }

  public static CoqResult Error(InvalidFormatException e) {
    return new() {
      Successful = false,
      ErrorMessage =
          AssetLocation.Create(e.Message, CoreSystem.Domain).ToShortString(),
      ErrorLocations = e.Nodes.ToHashSet()
    };
  }

  private void AddError(string message, RegexMatch lineInfo, int end,
                        CoqEmitter emitter, StringBuilder sb) {
    int start = lineInfo.Index + lineInfo.Length;
    RegexMatch match =
        ParseUnresolvedParameterType().Match(message, start, end - start);
    if (match.Success) {
      // The original error will point to the entire expression as being bad.
      // Instead, point to just the location of the parameter with the
      // unresolved type.
      Token t = emitter.GetTokenByName(match.Groups[1].Value);
      if (t != null) {
        ErrorLocations.AddRange(t.Blocks);
        sb.Append(
            "Cannot infer the type of a parameter. Either use the parameter, or specify its type with a typed scope block.");
        return;
      }
    }

    // Subtract one from the line to make it 0-indexed.
    int line = int.Parse(lineInfo.Groups[2].ValueSpan) - 1;
    // Coqc already returns the columns as 0-indexed.
    int startCol = int.Parse(lineInfo.Groups[3].ValueSpan);
    int endCol = int.Parse(lineInfo.Groups[4].ValueSpan);

    foreach (Token t in emitter.FindOverlapping(line, startCol, line, endCol)) {
      ErrorLocations.AddRange(t.Blocks);
    }

    sb.Append(message.AsSpan(start, end - start));
  }

  public static CoqResult Error(string message) {
    return new CoqResult() { Successful = false, ErrorMessage = message };
  }

  private CoqResult() {}

  [GeneratedRegex("^File \"(.+)\", line (\\d+), characters (\\d+)-(\\d+):\\n",
                  RegexOptions.Multiline | RegexOptions.Compiled)]
  public static partial Regex ParseErrorLocationGenerator();

  [GeneratedRegex(
      @"^Error:\nThe following term contains unresolved implicit arguments:\n.*More precisely:.*Cannot infer the type of (\S+) in.+environment:",
      RegexOptions.Compiled | RegexOptions.Singleline)]
  private static partial Regex ParseUnresolvedParameterType();
}

public class CoqSession : IDisposable {
  private readonly ServerConfig _config;
  public CoqSession(ServerConfig config) { _config = config; }

  public void Dispose() { GC.SuppressFinalize(this); }

  // Returns the error string if the program failed, or null if it succeeded.
  public CoqResult ValidateCoq(TokenEmitter emitter) {
    try {
      string filename = Path.Combine(
          _config.CoqTmpDir,
          $"Puzzle_{emitter.MainName}_{Environment.CurrentManagedThreadId}.v");
      using FileStream stream = new(filename, FileMode.Create);
      CoqEmitter coqEmitter = new(stream);
      emitter.EmitImports(coqEmitter);
      emitter.EmitDefinition("puzzle", coqEmitter);
      stream.Close();
      using StreamReader reader = new(filename);
      CoqSanitizer.Sanitize(reader);

      StringBuilder stdoutBuilder = new();
      StringBuilder stderrBuilder = new();

      using Process coqc = new() {
        StartInfo = new ProcessStartInfo { FileName = _config.CoqcPath,
                                           ArgumentList = { filename },
                                           UseShellExecute = false,
                                           RedirectStandardOutput = true,
                                           RedirectStandardError = true,
                                           CreateNoWindow = true },
      };
      coqc.OutputDataReceived += (sender, e) =>
          stdoutBuilder.AppendLine(e.Data);
      coqc.ErrorDataReceived += (sender, e) => stderrBuilder.AppendLine(e.Data);
      coqc.Start();
      coqc.BeginOutputReadLine();
      coqc.BeginErrorReadLine();
      coqc.WaitForExit(600000);
      if (coqc.ExitCode != 0) {
        return CoqResult.Error(stderrBuilder.ToString(), filename, coqEmitter);
      }
      return CoqResult.Success();
    } catch (Exception e) {
      return CoqResult.Error(e.ToString());
    }
  }

  public TermInfo GetTermInfo(BlockPos pos, string[] imports, string term) {
    string filename = Path.Combine(
        _config.CoqTmpDir,
        $"CheckTerm_{pos.X}_{pos.Y}_{pos.Z}_{Environment.CurrentManagedThreadId}.v");
    try {
      using FileStream stream = new(filename, FileMode.Create);
      using StreamWriter writer = new(stream);
      foreach (string import in imports) {
        writer.WriteLine($"Require Import {import}.");
      }
      writer.Write(TermInfoHeader);
      writer.WriteLine(
          $"Ltac2 Eval get_info open_constr:({term}).");
      writer.Close();

      using StreamReader reader = new(filename);
      CoqSanitizer.Sanitize(reader);

      StringBuilder stdoutBuilder = new();
      StringBuilder stderrBuilder = new();

      using Process coqc = new() {
        StartInfo = new ProcessStartInfo { FileName = _config.CoqcPath,
                                           ArgumentList = { filename },
                                           UseShellExecute = false,
                                           RedirectStandardOutput = true,
                                           RedirectStandardError = true,
                                           CreateNoWindow = true },
      };
      coqc.OutputDataReceived += (sender, e) =>
          stdoutBuilder.AppendLine(e.Data);
      coqc.ErrorDataReceived += (sender, e) => stderrBuilder.AppendLine(e.Data);
      coqc.Start();
      coqc.BeginOutputReadLine();
      coqc.BeginErrorReadLine();
      coqc.WaitForExit(600000);
      TermInfo result = new();
      if (coqc.ExitCode != 0) {
        return TermInfo.Error(stderrBuilder.ToString(), filename);
      }
      return TermInfo.Success(imports, stdoutBuilder.ToString());
    } catch (Exception e) {
      return TermInfo.Error(e.ToString(), filename);
    }
  }

  // clang-format off
  private static readonly string TermInfoHeader = """
From Ltac2 Require Import Ltac2.
From Ltac2 Require Import Message.
From Ltac2 Require Import Constr.
From Ltac2 Require Import Constructor.
From Ltac2 Require Import Printf.

Ltac2 get_kind (c: constr) :=
  match Unsafe.kind c with
  | Unsafe.Constructor a b =>
    fprintf "constructor: %t"
    (Constr.Unsafe.make (Constr.Unsafe.Ind (inductive a) b))
  | Unsafe.Ind inductive instance =>
    fprintf "inductive"
  | Unsafe.Sort sort =>
    fprintf "sort"
  | Unsafe.Prod binder constr =>
    fprintf "prod"
  | _ =>
    fprintf "unknown"
  end.

Ltac2 get_is_function (c: constr) :=
    (match! type c with
    | forall x : _, ?y => true
    | _ => false
    end).

Ltac2 get_type (c: constr) :=
  (type c).

Ltac2 get_reduced (c: constr) :=
  (eval cbn in $c).

Ltac2 get_info (c: constr) :=
  (get_type c, get_reduced c, get_kind c,
      get_is_function c).

""";
  // clang-format on
}
