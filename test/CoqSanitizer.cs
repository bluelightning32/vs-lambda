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
}