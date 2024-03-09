using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lambda.Tests;

using Lambda.Token;

[TestClass]
public class CoqSanitizerTest {
  [TestMethod]
  [ExpectedException(typeof(ArgumentException))]
  public void DisallowDrop() {
    CoqSanitizer.Sanitize(new StringReader("Drop.\n"));
  }

  [TestMethod]
  public void AllowAt() {
    CoqSanitizer.Sanitize(new StringReader("Definition f:= @pair.\n"));
  }

  [TestMethod]
  public void AllowCheck() {
    CoqSanitizer.Sanitize(new StringReader("Check nat.\n"));
  }

  [TestMethod]
  [ExpectedException(typeof(ArgumentException))]
  public void DisallowRedirect() {
    CoqSanitizer.Sanitize(new StringReader("Redirect \"escape\" Check nat.\n"));
  }

  [TestMethod]
  [ExpectedException(typeof(ArgumentException))]
  public void DisallowCD() {
    CoqSanitizer.Sanitize(new StringReader("Cd \"..\".\n"));
  }

  [TestMethod]
  public void AllowLtac2Import() {
    CoqSanitizer.Sanitize(new StringReader("""
From Ltac2 Require Import Ltac2.
"""));
  }

  [TestMethod]
  [ExpectedException(typeof(ArgumentException))]
  public void DisallowLtac2External() {
    CoqSanitizer.Sanitize(new StringReader("""
From Ltac2 Require Import Ltac2.
Ltac2 @ external print : message -> unit := "coq-core.plugins.ltac2" "print".
"""));
  }

  [TestMethod]
  [ExpectedException(typeof(ArgumentException))]
  public void DisallowLocateFile() {
    CoqSanitizer.Sanitize(new StringReader("""
Locate File "Ltac2".
"""));
  }

  [TestMethod]
  [ExpectedException(typeof(ArgumentException))]
  public void DisallowExtraction() {
    CoqSanitizer.Sanitize(new StringReader("""
Require Import Extraction.
"""));
  }

  [TestMethod]
  public void AllowLtac2TermInfo() {
    CoqSanitizer.Sanitize(new StringReader("""
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
  fprintf "function: %s"
    (match! type c with
    | forall x : _, ?y => "true"
    | _ => "false"
    end).

Ltac2 get_type (c: constr) :=
  fprintf "type: %t" (type c).

Ltac2 get_reduced (c: constr) :=
  fprintf "reduced: %t" (eval cbn in $c).

Ltac2 print_info (p: string) (c: constr) :=
  let tmp :=
    List.map (fun x => printf "%s%s" p (to_string x))
      [get_type c; get_reduced c; get_kind c;
      get_is_function c] in ().

Ltac2 Eval print_info "lambda: " constr:(nat -> nat).
"""));
  }
}
