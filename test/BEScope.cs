using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vintagestory.API.MathTools;

namespace LambdaFactory.Tests;

[TestClass]
public class BlockEntityScope {
  // This property is set by the test framework:
  // https://learn.microsoft.com/en-us/visualstudio/test/how-to-create-a-data-driven-unit-test?view=vs-2022#add-a-testcontext-to-the-test-class
  public TestContext TestContext { get; set; } = null!;

  [TestInitialize]
  public void Initialize() {}

  [TestMethod]
  public void GetFaceBoundsSingle() {
    Cuboidf bounds = new Cuboidf();
    float[] xyz = new float[3 * 15];
    xyz[4 * 3 + 0] = 5;
    xyz[4 * 3 + 1] = 6;
    xyz[4 * 3 + 2] = 7;
    LambdaFactory.BlockEntityScope.GetFaceBounds(bounds, xyz, 4, 5);
    Assert.AreEqual(bounds.X1, 5);
    Assert.AreEqual(bounds.Y1, 6);
    Assert.AreEqual(bounds.Z1, 7);
    Assert.AreEqual(bounds.X2, 5);
    Assert.AreEqual(bounds.Y2, 6);
    Assert.AreEqual(bounds.Z2, 7);
  }

  [TestMethod]
  public void GetFaceBounds3() {
    Cuboidf bounds = new Cuboidf();
    float[] xyz = new float[3 * 15];
    xyz[4 * 3 + 0] = 5;
    xyz[4 * 3 + 1] = 6;
    xyz[4 * 3 + 2] = 7;
    xyz[5 * 3 + 0] = 2;
    xyz[5 * 3 + 1] = 3;
    xyz[5 * 3 + 2] = 4;
    xyz[6 * 3 + 0] = 8;
    xyz[6 * 3 + 1] = 9;
    xyz[6 * 3 + 2] = 10;
    LambdaFactory.BlockEntityScope.GetFaceBounds(bounds, xyz, 4, 7);
    Assert.AreEqual(bounds.X1, 2);
    Assert.AreEqual(bounds.Y1, 3);
    Assert.AreEqual(bounds.Z1, 4);
    Assert.AreEqual(bounds.X2, 8);
    Assert.AreEqual(bounds.Y2, 9);
    Assert.AreEqual(bounds.Z2, 10);
  }

  [TestMethod]
  public void GetTriangleProjectionHalfHorizontal() {
    float offsetX = 23;
    float offsetY = 36;
    float offsetZ = 57;
    FastVec3f input = new FastVec3f(5 + offsetX, 0 + offsetY, 0 + offsetZ);
    FastVec3f corner = new FastVec3f(0 + offsetX, 0 + offsetY, 0 + offsetZ);
    FastVec3f neighbor1 = new FastVec3f(10 + offsetX, 0 + offsetY, 0 + offsetZ);
    FastVec3f neighbor2 = new FastVec3f(0 + offsetX, 10 + offsetY, 0 + offsetZ);
    LambdaFactory.BlockEntityScope.GetTriangleProjection(
        EnumAxis.Z, input, corner, neighbor1, neighbor2, out float t,
        out float u);
    Assert.AreEqual(0.5, t, 0.001);
    Assert.AreEqual(0, u, 0.001);
  }

  [TestMethod]
  public void GetTriangleProjectionDiagonal1() {
    float offsetX = 23;
    float offsetY = 36;
    float offsetZ = 57;
    FastVec3f input = new FastVec3f(5 + offsetX, 5 + offsetY, 0 + offsetZ);
    FastVec3f corner = new FastVec3f(0 + offsetX, 0 + offsetY, 0 + offsetZ);
    FastVec3f neighbor1 =
        new FastVec3f(10 + offsetX, 10 + offsetY, 0 + offsetZ);
    FastVec3f neighbor2 = new FastVec3f(0 + offsetX, 10 + offsetY, 0 + offsetZ);
    LambdaFactory.BlockEntityScope.GetTriangleProjection(
        EnumAxis.Z, input, corner, neighbor1, neighbor2, out float t,
        out float u);
    Assert.AreEqual(5 + offsetX,
                    corner.X + t * (neighbor1.X - corner.X) +
                        u * (neighbor2.X - corner.X),
                    0.001);
    Assert.AreEqual(5 + offsetY,
                    corner.Y + t * (neighbor1.Y - corner.Y) +
                        u * (neighbor2.Y - corner.Y),
                    0.001);
  }

  [TestMethod]
  public void GetTriangleProjectionDiagonal2() {
    float offsetX = 23;
    float offsetY = 36;
    float offsetZ = 57;
    FastVec3f input = new FastVec3f(5 + offsetX, 5 + offsetY, 0 + offsetZ);
    FastVec3f corner = new FastVec3f(0 + offsetX, 0 + offsetY, 0 + offsetZ);
    FastVec3f neighbor1 = new FastVec3f(0 + offsetX, 10 + offsetY, 0 + offsetZ);
    FastVec3f neighbor2 =
        new FastVec3f(10 + offsetX, 10 + offsetY, 0 + offsetZ);
    LambdaFactory.BlockEntityScope.GetTriangleProjection(
        EnumAxis.Z, input, corner, neighbor1, neighbor2, out float t,
        out float u);
    Assert.AreEqual(0, t, 0.001);
    Assert.AreEqual(0.5, u, 0.001);
    Assert.AreEqual(5 + offsetX,
                    corner.X + t * (neighbor1.X - corner.X) +
                        u * (neighbor2.X - corner.X),
                    0.001);
    Assert.AreEqual(5 + offsetY,
                    corner.Y + t * (neighbor1.Y - corner.Y) +
                        u * (neighbor2.Y - corner.Y),
                    0.001);
  }
}
