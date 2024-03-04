using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Lambda.Token;

public class CoqSession : IDisposable {
  private readonly ServerConfig _config;
  public CoqSession(ServerConfig config) { _config = config; }

  public void Dispose() { GC.SuppressFinalize(this); }

  // Returns the error string if the program failed, or null if it succeeded.
  public string ValidateCoq(TokenEmitter emitter) {
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
        return stderrBuilder.ToString();
      }
      return null;
    } catch (Exception e) {
      return e.ToString();
    }
  }
}
