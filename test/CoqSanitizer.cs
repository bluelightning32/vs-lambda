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
}
