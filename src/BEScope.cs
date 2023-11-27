using System;
using System.Xml.Schema;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LambdaFactory;

public class BlockEntityScope : BlockEntity, IBlockEntityForward {

  private MeshData _mesh;

  public override void Initialize(ICoreAPI api) {
    api.Logger.Notification($"lambda: Initialize {GetHashCode()}");
    base.Initialize(api);
    UpdateMesh();
  }

  static public void GetFaceBounds(Cuboidf bounds, float[] xyz, int beginVertex,
                                   int endVertex) {
    if (beginVertex >= endVertex) {
      bounds.Set(0, 0, 0, 0, 0, 0);
      return;
    }
    int xyzStart = beginVertex * 3;
    bounds.Set(xyz[xyzStart + 0], xyz[xyzStart + 1], xyz[xyzStart + 2],
               xyz[xyzStart + 0], xyz[xyzStart + 1], xyz[xyzStart + 2]);
    xyzStart += 3;
    for (; xyzStart < endVertex * 3; xyzStart += 3) {
      float v = xyz[xyzStart + 0];
      if (v < bounds.X1) {
        bounds.X1 = v;
      } else if (v > bounds.X2) {
        bounds.X2 = v;
      }
      v = xyz[xyzStart + 1];
      if (v < bounds.Y1) {
        bounds.Y1 = v;
      } else if (v > bounds.Y2) {
        bounds.Y2 = v;
      }
      v = xyz[xyzStart + 2];
      if (v < bounds.Z1) {
        bounds.Z1 = v;
      } else if (v > bounds.Z2) {
        bounds.Z2 = v;
      }
    }
  }

  static public bool Clamp(ref float val, float min, float max) {
    if (val < min) {
      val = min;
      return true;
    }
    if (val > max) {
      val = max;
      return true;
    }
    return false;
  }

  static public void Subtract(ref FastVec3f minuend, FastVec3f subtrahend) {
    minuend.X -= subtrahend.X;
    minuend.Y -= subtrahend.Y;
    minuend.Z -= subtrahend.Z;
  }

  static public void GetTriangleProjection(EnumAxis project, FastVec3f input,
                                           FastVec3f corner,
                                           FastVec3f neighbor1,
                                           FastVec3f neighbor2, out float t,
                                           out float u) {
    Subtract(ref neighbor1, corner);
    Subtract(ref neighbor2, corner);
    // A 2x2 matrix in column major order. Drop the project axis.
    float[] matrix = {
      neighbor1[((int)project + 1) % 3],
      neighbor1[((int)project + 2) % 3],
      neighbor2[((int)project + 1) % 3],
      neighbor2[((int)project + 2) % 3],
    };
    Mat22.Invert(matrix, matrix);

    Subtract(ref input, corner);
    t = matrix[0] * input[((int)project + 1) % 3] +
        matrix[2] * input[((int)project + 2) % 3];
    u = matrix[1] * input[((int)project + 1) % 3] +
        matrix[3] * input[((int)project + 2) % 3];
  }

  static public float Lerp2(float t, float u, float origin, float tdest,
                            float udest) {
    return origin + t * (tdest - origin) + u * (udest - origin);
  }

  static public byte Lerp2(float t, float u, byte origin, byte tdest,
                           byte udest) {
    return (byte)((int)origin + t * ((int)tdest - origin) +
                  u * ((int)udest - origin));
  }

  static public void ClampVertex(MeshData mesh, MeshData orig, EnumAxis project,
                                 int vertex, int neighbor1, int neighbor2,
                                 Cuboidf clamp) {
    bool clamped;
    FastVec3f newVertex =
        new FastVec3f(mesh.xyz[vertex * 3], mesh.xyz[vertex * 3 + 1],
                      mesh.xyz[vertex * 3 + 2]);
    clamped = Clamp(ref newVertex.X, clamp.X1, clamp.X2);
    clamped |= Clamp(ref newVertex.Y, clamp.Y1, clamp.Y2);
    clamped |= Clamp(ref newVertex.Z, clamp.Z1, clamp.Z2);
    if (!clamped)
      return;
    FastVec3f oldVertex =
        new FastVec3f(orig.xyz[vertex * 3], orig.xyz[vertex * 3 + 1],
                      orig.xyz[vertex * 3 + 2]);
    FastVec3f n1V =
        new FastVec3f(orig.xyz[neighbor1 * 3], orig.xyz[neighbor1 * 3 + 1],
                      orig.xyz[neighbor1 * 3 + 2]);
    FastVec3f n2V =
        new FastVec3f(orig.xyz[neighbor2 * 3], orig.xyz[neighbor2 * 3 + 1],
                      orig.xyz[neighbor2 * 3 + 2]);
    GetTriangleProjection(project, newVertex, oldVertex, n1V, n2V, out float t,
                          out float u);
    if (t < -0.001 || t > 1.001 || u < -0.001 || u > 1.001) {
      // The vertex is outside of the range of this triangle. Skip it and hope
      // that one of the other triangles for the face can take care of it.
      return;
    }

    mesh.xyz[vertex * 3] = newVertex.X;
    mesh.xyz[vertex * 3 + 1] = newVertex.Y;
    mesh.xyz[vertex * 3 + 2] = newVertex.Z;
    for (int i = 0; i < 2; i++) {
      mesh.Uv[vertex * 2 + i] =
          Lerp2(t, u, orig.Uv[vertex * 2 + i], orig.Uv[neighbor1 * 2 + i],
                orig.Uv[neighbor2 * 2 + i]);
    }
    for (int i = 0; i < 4; i++) {
      mesh.Rgba[vertex * 4 + i] =
          Lerp2(t, u, orig.Rgba[vertex * 4 + i], orig.Rgba[neighbor1 * 4],
                orig.Rgba[neighbor2 * 4 + i]);
    }
  }

  static public void ClampTriangle(MeshData mesh, MeshData orig,
                                   EnumAxis project, int beginIndex,
                                   Cuboidf clamp) {
    for (int i = 0; i < 3; ++i) {
      ClampVertex(mesh, orig, project, mesh.Indices[beginIndex + i],
                  mesh.Indices[beginIndex + (i + 1) % 3],
                  mesh.Indices[beginIndex + (i + 2) % 3], clamp);
    }
  }

  static public int AddFaceCopy(MeshData mesh, int sourceIndex) {
    int addFace = mesh.XyzFacesCount;
    int firstAddVertex = mesh.VerticesCount;
    for (int i = sourceIndex * mesh.VerticesPerFace;
         i < (sourceIndex + 1) * mesh.VerticesPerFace; ++i) {
      int addAt = mesh.VerticesCount;
      mesh.AddVertex(mesh.xyz[i * 3 + 0], mesh.xyz[i * 3 + 1],
                     mesh.xyz[i * 3 + 2], mesh.Uv[i * 2 + 0],
                     mesh.Uv[i * 2 + 1]);
      mesh.Flags[addAt] = mesh.Flags[i];
      for (int j = 0; j < 4; j++) {
        mesh.Rgba[addAt * 4 + j] = mesh.Rgba[i * 4 + j];
      }
    }
    for (int i = sourceIndex * mesh.IndicesPerFace;
         i < (sourceIndex + 1) * mesh.IndicesPerFace; ++i) {
      mesh.AddIndex(mesh.Indices[i] - sourceIndex * mesh.VerticesPerFace +
                    firstAddVertex);
    }
    mesh.AddTextureId(mesh.TextureIds[mesh.TextureIndices[sourceIndex]]);
    mesh.AddXyzFace(mesh.XyzFaces[sourceIndex]);
    mesh.AddRenderPass(mesh.RenderPassesAndExtraBits[sourceIndex]);
    mesh.AddColorMapIndex(mesh.ClimateColorMapIds[sourceIndex],
                          mesh.SeasonColorMapIds[sourceIndex]);
    return addFace;
  }

  static public void ClampFace(MeshData mesh, MeshData orig, EnumAxis project,
                               int faceIndex, Cuboidf clamp) {
    for (int i = 0; i < mesh.IndicesPerFace; i += 3) {
      ClampTriangle(mesh, orig, project, faceIndex * mesh.IndicesPerFace + i,
                    clamp);
    }
  }

  static public void AddFaceHole(MeshData mesh, MeshData orig, EnumAxis project,
                                 int faceIndex, BlockFacing face) {
    int beginVertex = faceIndex * mesh.VerticesPerFace;

    int copy1 = faceIndex;
    int copy2 = AddFaceCopy(mesh, faceIndex);
    int copy3 = AddFaceCopy(mesh, faceIndex);
    int copy4 = AddFaceCopy(mesh, faceIndex);
    AddFaceCopy(orig, faceIndex);
    AddFaceCopy(orig, faceIndex);
    AddFaceCopy(orig, faceIndex);

    // bottom rectangle
    Cuboidf clamp = face.Plane.Clone();
    clamp[3 + ((int)face.Axis + 2) % 3] = (8 - 3) / 16f;
    ClampFace(mesh, orig, project, copy1, clamp);

    // top rectangle
    clamp.Set(face.Plane);
    clamp[0 + ((int)face.Axis + 2) % 3] = (8 + 3) / 16f;
    ClampFace(mesh, orig, project, copy2, clamp);

    // left rectangle
    clamp.Set(face.Plane);
    clamp[0 + ((int)face.Axis + 2) % 3] = (8 - 3) / 16f;
    clamp[3 + ((int)face.Axis + 1) % 3] = (8 - 3) / 16f;
    clamp[3 + ((int)face.Axis + 2) % 3] = (8 + 3) / 16f;
    ClampFace(mesh, orig, project, copy3, clamp);

    // right rectangle
    clamp.Set(face.Plane);
    clamp[0 + ((int)face.Axis + 1) % 3] = (8 + 3) / 16f;
    clamp[0 + ((int)face.Axis + 2) % 3] = (8 - 3) / 16f;
    clamp[3 + ((int)face.Axis + 2) % 3] = (8 + 3) / 16f;
    ClampFace(mesh, orig, project, copy4, clamp);
  }

  private void UpdateMesh() {
    if (Api.Side == EnumAppSide.Server)
      return;
    ((ICoreClientAPI)Api).Tesselator.TesselateBlock(Block, out _mesh);
    if (_mesh.VerticesPerFace != 4 || _mesh.IndicesPerFace != 6) {
      throw new Exception("Unexpected VerticesPerFace or IndicesPerFace");
    }
    MeshData orig = _mesh.Clone();
    Cuboidf faceBounds = new Cuboidf();
    int origFaceCount = _mesh.VerticesCount / _mesh.VerticesPerFace;
    for (int face = 0; face < origFaceCount; face++) {
      GetFaceBounds(faceBounds, _mesh.xyz, face * _mesh.VerticesPerFace,
                    (face + 1) * _mesh.VerticesPerFace);
      faceBounds.OmniGrowBy(0.001f);
      foreach (BlockFacing facing in BlockFacing.ALLFACES) {
        if (faceBounds[(int)facing.Axis + 3] - faceBounds[(int)facing.Axis] <
                0.1f &&
            faceBounds.Contains(facing.PlaneCenter.X, facing.PlaneCenter.Y,
                                facing.PlaneCenter.Z)) {
          AddFaceHole(_mesh, orig, facing.Axis, face, facing);
        }
      }
    }
  }

  public override bool OnTesselation(ITerrainMeshPool mesher,
                                     ITesselatorAPI tessThreadTesselator) {
    Api.Logger.Notification($"lambda: OnTesselation {GetHashCode()}");
    mesher.AddMeshData(_mesh);
    return true;
  }

  public override void OnBlockPlaced(ItemStack byItemStack = null) {
    Api.Logger.Notification($"lambda: OnBlockPlaced {GetHashCode()}");
    base.OnBlockPlaced(byItemStack);
  }

  public override void OnExchanged(Block block) {
    Api.Logger.Notification($"lambda: OnExchanged {GetHashCode()}");
    base.OnExchanged(block);
  }

  void IBlockEntityForward.OnNeighbourBlockChange(
      Vintagestory.API.MathTools.BlockPos neibpos,
      ref Vintagestory.API.Common.EnumHandling handling) {
    Block[] decors = Api.World.BlockAccessor.GetDecors(Pos);
    int nonnull = 0;
    foreach (Block decor in decors) {
      if (decor != null) {
        ++nonnull;
      }
    }

    Api.Logger.Notification(
        $"lambda: IBlockEntityForward.OnNeighbourBlockChange {GetHashCode()} mypos {Pos} neighpos {neibpos} nonnull decors {nonnull}");
  }
}
