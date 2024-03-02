using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Lambda.Token;

public class CoqSanitizer {
  enum SanitizeState {
    BeforeCommand,
    Command,
    Import,
    BodyTokenStart,
    InNumber,
    RealDecimal,
    RealFraction,
    InToken,
  }

  // Throws an exception if the Coq code is potentially unsafe.
  static public void Sanitize(TextReader reader) {
    char[] buffer = new char[1000];
    SanitizeState state = SanitizeState.BeforeCommand;
    StringBuilder command = new();
    while (true) {
      int read = reader.Read(buffer);
      if (read == 0) {
        break;
      }
      for (int i = 0; i < read; ++i) {
        char c = buffer[i];
        switch (state) {
        case SanitizeState.BeforeCommand:
          if (!Char.IsWhiteSpace(c)) {
            command.Append(c);
            state = SanitizeState.Command;
          }
          break;
        case SanitizeState.Command:
          if (Char.IsWhiteSpace(c)) {
            string cmd = command.ToString();
            if (cmd == "Require") {
              state = SanitizeState.Import;
              command.Clear();
            } else {
              SanitizeCommand(cmd);
              state = SanitizeState.BodyTokenStart;
              command.Clear();
            }
          } else {
            command.Append(c);
            if (command.Length > 1000) {
              throw new ArgumentException(
                  $"Command '{command.ToString()}' is too long.");
            }
          }
          break;
        case SanitizeState.Import:
          if (Char.IsWhiteSpace(c) && command.Length > 1 &&
              command[^1] == '.') {
            SanitizeImport(command.ToString());
            state = SanitizeState.BodyTokenStart;
            command.Clear();
          } else {
            command.Append(c);
            if (command.Length > 1000) {
              throw new ArgumentException(
                  $"Import '{command.ToString()}' is too long.");
            }
          }
          break;
        case SanitizeState.BodyTokenStart:
          if (Char.IsWhiteSpace(c) || IsOperator(c)) {
          } else if (Char.IsAsciiDigit(c)) {
            state = SanitizeState.InNumber;
          } else if (Char.IsAsciiLetter(c) || c == '_') {
            state = SanitizeState.InToken;
          } else if (c == '.') {
            state = SanitizeState.BeforeCommand;
          } else {
            throw new ArgumentException(
                $"Unexpected character '{c}' in command body.");
          }
          break;
        case SanitizeState.InNumber:
          if (Char.IsWhiteSpace(c) || IsOperator(c)) {
            state = SanitizeState.BodyTokenStart;
          } else if (Char.IsAsciiDigit(c)) {

          } else if (c == '.') {
            state = SanitizeState.RealDecimal;
          } else {
            throw new ArgumentException(
                $"Unexpected character '{c}' in number.");
          }
          break;
        case SanitizeState.RealDecimal:
          if (Char.IsWhiteSpace(c)) {
            // Treat the previous period as the end of a command.
            state = SanitizeState.BeforeCommand;
          } else if (Char.IsAsciiDigit(c)) {
            state = SanitizeState.RealFraction;
          } else {
            throw new ArgumentException(
                $"Unexpected character '{c}' in real number.");
          }
          break;
        case SanitizeState.RealFraction:
          if (Char.IsWhiteSpace(c) || IsOperator(c)) {
            state = SanitizeState.BodyTokenStart;
          } else if (Char.IsAsciiDigit(c)) {

          } else if (c == '.') {
            state = SanitizeState.BeforeCommand;
          } else {
            throw new ArgumentException(
                $"Unexpected character '{c}' in number.");
          }
          break;
        case SanitizeState.InToken:
          if (Char.IsWhiteSpace(c) || IsOperator(c)) {
            state = SanitizeState.BodyTokenStart;
          } else if (Char.IsAsciiDigit(c) || Char.IsAsciiLetter(c) ||
                     c == '\'' || c == '_') {
          } else if (c == '.') {
            state = SanitizeState.BeforeCommand;
          } else {
            throw new ArgumentException(
                $"Unexpected character '{c}' in command body.");
          }
          break;
        }
      }
    }
    if (state != SanitizeState.BeforeCommand) {
      throw new ArgumentException(
          $"Unexpected end of program in state {state}.");
    }
  }

  private static readonly HashSet<char> _allowedOperators =
      new() { ':', '(', ')', ',', '*', '+', '-', '>', '<', '=', '|' };

  private static bool IsOperator(char c) {
    return _allowedOperators.Contains(c);
  }

  private static readonly HashSet<string> _allowedImports =
      new() { "List", "Reals" };

  private static void SanitizeImport(string import) {
    if (import.StartsWith("Import ")) {
      import = import.Substring("Import ".Length);
    }
    if (!_allowedImports.Contains(import)) {
      throw new ArgumentException(
          $"Import '{import}' is not in the allow list.");
    }
  }

  private static void SanitizeCommand(string c) {
    switch (c) {
    case "Definition":
      break;
    case "Check":
      break;
    case "Goal":
      break;
    default:
      throw new ArgumentException($"Unexpected command {c}.");
    }
  }
}