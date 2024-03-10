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
    Ltac2Start,
    BodyTokenStart,
    InNumber,
    RealDecimal,
    RealFraction,
    InToken,
    InTokenDot,
    InQuote,
  }

  // Throws an exception if the Coq code is potentially unsafe.
  static public void Sanitize(string code) { Sanitize(new StringReader(code)); }

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
          if (!char.IsWhiteSpace(c)) {
            command.Append(c);
            state = SanitizeState.Command;
          }
          break;
        case SanitizeState.Command:
          if (char.IsWhiteSpace(c)) {
            string cmd = command.ToString();
            if (cmd == "Require" || cmd == "From") {
              command.Append(c);
              state = SanitizeState.Import;
            } else if (cmd == "Ltac2") {
              command.Clear();
              state = SanitizeState.Ltac2Start;
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
          if (char.IsWhiteSpace(c) && command.Length > 1 &&
              command[^1] == '.') {
            SanitizeImport(command.ToString());
            state = SanitizeState.BeforeCommand;
            command.Clear();
          } else if (char.IsAsciiLetterOrDigit(c) || c == '.' ||
                     char.IsWhiteSpace(c)) {
            command.Append(c);
            if (command.Length > 1000) {
              throw new ArgumentException(
                  $"Import '{command.ToString()}' is too long.");
            }
          } else {
            throw new ArgumentException(
                $"Unexpected character '{c}' in import command.");
          }
          break;
        case SanitizeState.Ltac2Start:
          if (char.IsWhiteSpace(c)) {
          } else if (char.IsAsciiLetter(c) || c == '_') {
            state = SanitizeState.InToken;
          } else {
            throw new ArgumentException(
                $"Unexpected character '{c}' at start of ltac2 command.");
          }
          break;
        case SanitizeState.InTokenDot:
          if (char.IsWhiteSpace(c)) {
            state = SanitizeState.BeforeCommand;
          } else if (char.IsAsciiLetter(c) || c == '_' || c == '@') {
            state = SanitizeState.InToken;
          } else {
            throw new ArgumentException(
                $"Unexpected character '{c}' in command body after dot.");
          }
          break;
        case SanitizeState.BodyTokenStart:
          if (char.IsWhiteSpace(c) || IsOperator(c)) {
          } else if (char.IsAsciiDigit(c)) {
            state = SanitizeState.InNumber;
          } else if (char.IsAsciiLetter(c) || c == '_' || c == '@' ||
                     c == '?' || c == '$') {
            state = SanitizeState.InToken;
          } else if (c == '.') {
            state = SanitizeState.BeforeCommand;
          } else if (c == '\"') {
            state = SanitizeState.InQuote;
          } else {
            throw new ArgumentException(
                $"Unexpected character '{c}' in command body.");
          }
          break;
        case SanitizeState.InNumber:
          if (char.IsWhiteSpace(c) || IsOperator(c)) {
            state = SanitizeState.BodyTokenStart;
          } else if (char.IsAsciiDigit(c)) {

          } else if (c == '.') {
            state = SanitizeState.RealDecimal;
          } else {
            throw new ArgumentException(
                $"Unexpected character '{c}' in number.");
          }
          break;
        case SanitizeState.RealDecimal:
          if (char.IsWhiteSpace(c)) {
            // Treat the previous period as the end of a command.
            state = SanitizeState.BeforeCommand;
          } else if (char.IsAsciiDigit(c)) {
            state = SanitizeState.RealFraction;
          } else {
            throw new ArgumentException(
                $"Unexpected character '{c}' in real number.");
          }
          break;
        case SanitizeState.RealFraction:
          if (char.IsWhiteSpace(c) || IsOperator(c)) {
            state = SanitizeState.BodyTokenStart;
          } else if (char.IsAsciiDigit(c)) {

          } else if (c == '.') {
            state = SanitizeState.BeforeCommand;
          } else {
            throw new ArgumentException(
                $"Unexpected character '{c}' in number.");
          }
          break;
        case SanitizeState.InToken:
          if (char.IsWhiteSpace(c) || IsOperator(c)) {
            state = SanitizeState.BodyTokenStart;
          } else if (char.IsAsciiDigit(c) || char.IsAsciiLetter(c) ||
                     c == '\'' || c == '_' || c == '!') {
          } else if (c == '.') {
            state = SanitizeState.InTokenDot;
          } else {
            throw new ArgumentException(
                $"Unexpected character '{c}' in command body.");
          }
          break;
        case SanitizeState.InQuote:
          if (c == '\"') {
            state = SanitizeState.BodyTokenStart;
          } else if (!char.IsAscii(c)) {
            throw new ArgumentException(
                "Only ASCII characters are allowed in strings.");
          }
          break;
        }
      }
    }
    if (state == SanitizeState.Import) {
      SanitizeImport(command.ToString());
    } else if (state != SanitizeState.BeforeCommand) {
      throw new ArgumentException(
          $"Unexpected end of program in state {state}.");
    }
  }

  private static readonly HashSet<char> AllowedOperators =
      new() { ':', '(', ')', ',', '*', '+', '-',
              '>', '<', '=', '|', '[', ']', ';' };

  private static bool IsOperator(char c) {
    return AllowedOperators.Contains(c);
  }

  private static readonly List<string> AllowedImports = new() {
    "Coq.Lists.List",    "Coq.Reals.Reals", "Coq.ZArith.ZArith",

    "Ltac2.Ltac2",       "Ltac2.Message",   "Ltac2.Constr",
    "Ltac2.Constructor", "Ltac2.Printf",
  };

  private static void SanitizeImport(string import) {
    List<string> tokens = new(import.Split());
    if (tokens[^1][^1] != '.') {
      throw new ArgumentException("Import command does not end in period.");
    }
    string last = tokens[^1];
    last = last[..^ 1];
    if (last.Length == 0) {
      tokens.RemoveAt(tokens.Count - 1);
    } else {
      tokens[^1] = last;
    }

    string prefix = null;
    int i = 0;
    if (tokens[i] == "From") {
      prefix = tokens[i + 1];
      i += 2;
    }

    if (tokens[i] != "Require") {
      throw new ArgumentException("Only require imports are allowed.");
    }
    ++i;

    if (tokens[i] == "Import") {
      ++i;
    }

    if (i != tokens.Count - 1) {
      throw new ArgumentException("Too many tokens.");
    }

    if (prefix != null) {
      foreach (string allowed in AllowedImports) {
        if (allowed.StartsWith($"{prefix}.") &&
            allowed.EndsWith($".{tokens[i]}")) {
          return;
        }
      }
      throw new ArgumentException(
          $"Import '{prefix}' '{tokens[i]}' is not in the allow list.");
    } else if (!AllowedImports.Contains(tokens[i])) {
      foreach (string allowed in AllowedImports) {
        if (allowed.EndsWith($".{tokens[i]}")) {
          return;
        }
      }
      throw new ArgumentException(
          $"Import '{tokens[i]}' is not in the allow list.");
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
