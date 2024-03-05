using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Lambda.Network;

using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Lambda.Token;

using RegexMatch = System.Text.RegularExpressions.Match;

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
  private static partial Regex ParseErrorLocationGenerator();

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
}
