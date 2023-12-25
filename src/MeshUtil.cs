using System;

using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace LambdaFactory;

public static class MeshUtil {
  static public void GetFaceAxisBounds(EnumAxis axis, float[] xyz,
                                       int beginVertex, int endVertex,
                                       out float min, out float max) {
    if (beginVertex >= endVertex) {
      min = 0;
      max = 0;
      return;
    }
    int xyzStart = beginVertex * 3;
    min = max = xyz[xyzStart + (int)axis];
    xyzStart += 3;
    for (; xyzStart < endVertex * 3; xyzStart += 3) {
      float v = xyz[xyzStart + (int)axis];
      if (v < min) {
        min = v;
      } else if (v > max) {
        max = v;
      }
    }
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

  static public bool IsEmptyFace(float[] xyz, int beginVertex, int endVertex) {
    Cuboidf bounds = new Cuboidf();
    GetFaceBounds(bounds, xyz, beginVertex, endVertex);
    int zerodims = 0;
    for (int i = 0; i < 3; ++i) {
      if (bounds[i] == bounds[i + 3])
        ++zerodims;
    }
    return zerodims >= 2;
  }

  static public bool IsEmptyFace(MeshData mesh, int face) {
    return IsEmptyFace(mesh.xyz, face * mesh.VerticesPerFace,
                       (face + 1) * mesh.VerticesPerFace);
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

  static public bool ClampVertex(MeshData mesh, EnumAxis project, Cuboidf clamp,
                                 int origVertex, int neighbor1, int neighbor2,
                                 int modifyVertex, bool skipOutOfBounds) {
    bool clamped;
    FastVec3f oldVertex =
        new FastVec3f(mesh.xyz[origVertex * 3], mesh.xyz[origVertex * 3 + 1],
                      mesh.xyz[origVertex * 3 + 2]);
    FastVec3f newVertex = oldVertex;
    clamped = Clamp(ref newVertex.X, clamp.X1, clamp.X2);
    clamped |= Clamp(ref newVertex.Y, clamp.Y1, clamp.Y2);
    clamped |= Clamp(ref newVertex.Z, clamp.Z1, clamp.Z2);
    if (!clamped)
      return true;
    FastVec3f n1V =
        new FastVec3f(mesh.xyz[neighbor1 * 3], mesh.xyz[neighbor1 * 3 + 1],
                      mesh.xyz[neighbor1 * 3 + 2]);
    FastVec3f n2V =
        new FastVec3f(mesh.xyz[neighbor2 * 3], mesh.xyz[neighbor2 * 3 + 1],
                      mesh.xyz[neighbor2 * 3 + 2]);
    GetTriangleProjection(project, newVertex, oldVertex, n1V, n2V, out float t,
                          out float u);
    if (skipOutOfBounds &&
        (t < -0.001 || t > 1.001 || u < -0.001 || u > 1.001)) {
      // The vertex is outside of the range of this triangle. Skip it and hope
      // that one of the other triangles for the face can take care of it.
      return false;
    }

    mesh.xyz[modifyVertex * 3] = newVertex.X;
    mesh.xyz[modifyVertex * 3 + 1] = newVertex.Y;
    mesh.xyz[modifyVertex * 3 + 2] = newVertex.Z;
    for (int i = 0; i < 2; i++) {
      mesh.Uv[modifyVertex * 2 + i] =
          Lerp2(t, u, mesh.Uv[origVertex * 2 + i], mesh.Uv[neighbor1 * 2 + i],
                mesh.Uv[neighbor2 * 2 + i]);
    }
    for (int i = 0; i < 4; i++) {
      mesh.Rgba[modifyVertex * 4 + i] =
          Lerp2(t, u, mesh.Rgba[origVertex * 4 + i], mesh.Rgba[neighbor1 * 4],
                mesh.Rgba[neighbor2 * 4 + i]);
    }
    return true;
  }

  static public void ClampTriangle(MeshData mesh, EnumAxis project,
                                   Cuboidf clamp, int originalBeginIndex,
                                   int modifyBeginIndex) {
    bool allInBounds = true;
    for (int i = 0; i < 3; ++i) {
      allInBounds &= ClampVertex(mesh, project, clamp,
                                 mesh.Indices[originalBeginIndex + i],
                                 mesh.Indices[originalBeginIndex + (i + 1) % 3],
                                 mesh.Indices[originalBeginIndex + (i + 2) % 3],
                                 mesh.Indices[modifyBeginIndex + i], true);
    }
    if (!allInBounds) {
      for (int i = 0; i < 3; ++i) {
        ClampVertex(mesh, project, clamp, mesh.Indices[originalBeginIndex + i],
                    mesh.Indices[originalBeginIndex + (i + 1) % 3],
                    mesh.Indices[originalBeginIndex + (i + 2) % 3],
                    mesh.Indices[modifyBeginIndex + i], false);
      }
    }
  }

  static public int AddFaceCopy(MeshData mesh, int sourceIndex) {
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
    if (mesh.TextureIndicesCount > 0) {
      mesh.AddTextureId(mesh.TextureIds[mesh.TextureIndices[sourceIndex]]);
    }
    if (mesh.XyzFacesCount > 0) {
      mesh.AddXyzFace(mesh.XyzFaces[sourceIndex]);
    }
    if (mesh.RenderPassCount > 0) {
      mesh.AddRenderPass(mesh.RenderPassesAndExtraBits[sourceIndex]);
    }
    if (mesh.ColorMapIdsCount > 0) {
      mesh.AddColorMapIndex(mesh.ClimateColorMapIds[sourceIndex],
                            mesh.SeasonColorMapIds[sourceIndex]);
    }
    return mesh.VerticesCount / mesh.VerticesPerFace - 1;
  }

  static public void CopyFaceTo(MeshData mesh, int sourceFace, int targetFace) {
    int firstSourceVertex = sourceFace * mesh.VerticesPerFace;
    int firstTargetVertex = targetFace * mesh.VerticesPerFace;
    for (int i = 0; i < mesh.VerticesPerFace * 3; ++i) {
      mesh.xyz[firstTargetVertex * 3 + i] = mesh.xyz[firstSourceVertex * 3 + i];
    }
    for (int i = 0; i < mesh.VerticesPerFace * 2; ++i) {
      mesh.Uv[firstTargetVertex * 2 + i] = mesh.Uv[firstSourceVertex * 2 + i];
    }
    for (int i = 0; i < mesh.VerticesPerFace; ++i) {
      mesh.Flags[firstTargetVertex + i] = mesh.Flags[firstSourceVertex + i];
    }
    for (int i = 0; i < mesh.VerticesPerFace * 4; ++i) {
      mesh.Rgba[firstTargetVertex * 4 + i] =
          mesh.Rgba[firstSourceVertex * 4 + i];
    }

    int firstSourceIndex = sourceFace * mesh.IndicesPerFace;
    int firstTargetIndex = targetFace * mesh.IndicesPerFace;
    for (int i = 0; i < mesh.IndicesPerFace; ++i) {
      mesh.Indices[firstTargetIndex + i] = mesh.Indices[firstSourceIndex + i] -
                                           firstSourceVertex +
                                           firstTargetVertex;
    }
    if (mesh.TextureIndicesCount > 0) {
      mesh.TextureIndices[targetFace] = mesh.TextureIndices[sourceFace];
    }
    if (mesh.XyzFacesCount > 0) {
      mesh.XyzFaces[targetFace] = mesh.XyzFaces[sourceFace];
    }
    if (mesh.RenderPassCount > 0) {
      mesh.RenderPassesAndExtraBits[targetFace] =
          mesh.RenderPassesAndExtraBits[sourceFace];
    }
    if (mesh.ColorMapIdsCount > 0) {
      mesh.ClimateColorMapIds[targetFace] = mesh.ClimateColorMapIds[sourceFace];
      mesh.SeasonColorMapIds[targetFace] = mesh.SeasonColorMapIds[sourceFace];
    }
  }

  static public void RemoveLastFace(MeshData mesh) {
    mesh.VerticesCount -= mesh.VerticesPerFace;
    mesh.IndicesCount -= mesh.IndicesPerFace;

    mesh.TextureIndicesCount--;
    mesh.XyzFacesCount--;
    mesh.RenderPassCount--;
    mesh.ColorMapIdsCount--;
  }

  static public void ClampFace(MeshData mesh, EnumAxis project, Cuboidf clamp,
                               int originalFace, int modifyFace) {
    for (int i = 0; i < mesh.IndicesPerFace; i += 3) {
      ClampTriangle(mesh, project, clamp,
                    originalFace * mesh.IndicesPerFace + i,
                    modifyFace * mesh.IndicesPerFace + i);
    }
  }

  static public int CopyFaceWithHole(MeshData mesh, EnumAxis project,
                                     int faceIndex, BlockFacing face,
                                     Cuboidf hole) {
    int beginVertex = faceIndex * mesh.VerticesPerFace;

    int extraCopy = AddFaceCopy(mesh, faceIndex);

    // bottom rectangle
    Cuboidf clamp = face.Plane.Clone();
    clamp[3 + ((int)face.Axis + 2) % 3] = hole[((int)face.Axis + 2) % 3];
    ClampFace(mesh, project, clamp, extraCopy, faceIndex);

    if (IsEmptyFace(mesh, faceIndex)) {
      CopyFaceTo(mesh, extraCopy, faceIndex);
    } else {
      faceIndex = extraCopy;
      extraCopy = AddFaceCopy(mesh, extraCopy);
    }

    // top rectangle
    clamp.Set(face.Plane);
    clamp[0 + ((int)face.Axis + 2) % 3] = hole[3 + ((int)face.Axis + 2) % 3];
    ClampFace(mesh, project, clamp, extraCopy, faceIndex);

    if (IsEmptyFace(mesh, faceIndex)) {
      CopyFaceTo(mesh, extraCopy, faceIndex);
    } else {
      faceIndex = extraCopy;
      extraCopy = AddFaceCopy(mesh, extraCopy);
    }

    // left rectangle
    clamp.Set(face.Plane);
    clamp[0 + ((int)face.Axis + 2) % 3] = hole[((int)face.Axis + 2) % 3];
    clamp[3 + ((int)face.Axis + 1) % 3] = hole[((int)face.Axis + 1) % 3];
    clamp[3 + ((int)face.Axis + 2) % 3] = hole[3 + ((int)face.Axis + 2) % 3];
    ClampFace(mesh, project, clamp, extraCopy, faceIndex);

    if (IsEmptyFace(mesh, faceIndex)) {
      CopyFaceTo(mesh, extraCopy, faceIndex);
    } else {
      faceIndex = extraCopy;
      extraCopy = AddFaceCopy(mesh, extraCopy);
    }

    // right rectangle
    clamp.Set(face.Plane);
    clamp[0 + ((int)face.Axis + 2) % 3] = hole[((int)face.Axis + 2) % 3];
    clamp[0 + ((int)face.Axis + 1) % 3] = hole[3 + ((int)face.Axis + 1) % 3];
    clamp[3 + ((int)face.Axis + 2) % 3] = hole[3 + ((int)face.Axis + 2) % 3];
    ClampFace(mesh, project, clamp, extraCopy, faceIndex);

    if (IsEmptyFace(mesh, faceIndex)) {
      CopyFaceTo(mesh, extraCopy, faceIndex);
      RemoveLastFace(mesh);
      return faceIndex;
    } else {
      return extraCopy;
    }
  }

  static public void AddFaceHole(MeshData mesh, EnumAxis project, int faceIndex,
                                 BlockFacing face) {
    Cuboidf hole = face.Plane.Clone();

    hole[0 + ((int)face.Axis + 1) % 3] = (8 - 3) / 16f;
    hole[0 + ((int)face.Axis + 2) % 3] = (8 - 3) / 16f;
    hole[3 + ((int)face.Axis + 1) % 3] = (8 + 3) / 16f;
    hole[3 + ((int)face.Axis + 2) % 3] = (8 + 3) / 16f;
    CopyFaceWithHole(mesh, project, faceIndex, face, hole);
    RemoveLastFace(mesh);
  }

  static public MeshData CutPortHoles(int sides, MeshData mesh) {
    if (mesh.VerticesPerFace != 4 || mesh.IndicesPerFace != 6) {
      throw new Exception("Unexpected VerticesPerFace or IndicesPerFace");
    }
    if (sides == 0) {
      return mesh;
    }
    Cuboidf faceBounds = new Cuboidf();
    int origFaceCount = mesh.VerticesCount / mesh.VerticesPerFace;
    MeshData copy = mesh.Clone();
    for (int face = 0; face < origFaceCount; face++) {
      GetFaceBounds(faceBounds, mesh.xyz, face * mesh.VerticesPerFace,
                    (face + 1) * mesh.VerticesPerFace);
      faceBounds.OmniGrowBy(0.001f);
      for (int i = 0; i < 6; ++i) {
        if ((sides & (1 << i)) == 0) {
          continue;
        }
        BlockFacing facing = BlockFacing.ALLFACES[i];
        if (faceBounds[(int)facing.Axis + 3] - faceBounds[(int)facing.Axis] <
                0.1f &&
            faceBounds.Contains(facing.PlaneCenter.X, facing.PlaneCenter.Y,
                                facing.PlaneCenter.Z)) {
          AddFaceHole(copy, facing.Axis, face, facing);
        }
      }
    }
    return copy;
  }

  static public void ReplaceTexture(MeshData mesh, BlockFacing face,
                                    float faceAxisRange,
                                    TextureAtlasPosition original,
                                    TextureAtlasPosition replacement) {
    if (mesh.TextureIndices == null || mesh.TextureIndices.Length == 0) {
      return;
    }
    int faceCount = mesh.VerticesCount / mesh.VerticesPerFace;
    const float errorX = 0.1f / 4096;
    const float errorY = 0.1f / 4096;
    for (int f = 0; f < faceCount; f++) {
      if (mesh.XyzFaces[f] != 0 && mesh.XyzFaces[f] != face.MeshDataIndex) {
        continue;
      }
      // Verify the mesh face is close to the block face on the block face's
      // normal axis. The face normal was effectively checked already through
      // XyzFaces. So checking only the first vertex is sufficient.
      float axisLocation =
          mesh.xyz[f * mesh.VerticesPerFace * 3 + (int)face.Axis];
      if (axisLocation + faceAxisRange < face.PlaneCenter[(int)face.Axis] ||
          axisLocation - faceAxisRange > face.PlaneCenter[(int)face.Axis]) {
        continue;
      }

      bool allMatched = true;
      for (int i = 0; i < mesh.VerticesPerFace; ++i) {
        int uvoffset = (f * mesh.VerticesPerFace + i) * 2;
        float u = mesh.Uv[uvoffset];
        float v = mesh.Uv[uvoffset + 1];
        int textureId = mesh.TextureIds[mesh.TextureIndices[f]];
        if (textureId != original.atlasTextureId || u < original.x1 - errorX ||
            u > original.x2 + errorX || v < original.y1 - errorY ||
            v > original.y2 + errorY) {
          allMatched = false;
          break;
        }
      }
      if (!allMatched) {
        continue;
      }
      mesh.TextureIndices[f] = mesh.getTextureIndex(replacement.atlasTextureId);
      for (int i = 0; i < mesh.VerticesPerFace; ++i) {
        int uvoffset = (f * mesh.VerticesPerFace + i) * 2;
        float u = mesh.Uv[uvoffset];
        float v = mesh.Uv[uvoffset + 1];
        // The original and replacment meshes are the same size. So reoffsetting
        // the uv coordinates is enough. Rescaling them is not necessary.
        mesh.Uv[uvoffset] = replacement.x1 + (u - original.x1);
        mesh.Uv[uvoffset + 1] = replacement.y1 + (v - original.y1);
      }
    }
  }

  static public void ReplaceTextureInBounds(MeshData mesh, BlockFacing face,
                                            Cuboidf bounds,
                                            TextureAtlasPosition original,
                                            TextureAtlasPosition replacement) {
    if (mesh.TextureIndices == null || mesh.TextureIndices.Length == 0) {
      return;
    }
    int faceCount = mesh.VerticesCount / mesh.VerticesPerFace;
    const float errorX = 0.1f / 4096;
    const float errorY = 0.1f / 4096;
    for (int f = 0; f < faceCount; f++) {
      if (mesh.XyzFaces[f] != 0 && mesh.XyzFaces[f] != face.MeshDataIndex) {
        continue;
      }
      // Verify the mesh face is close to the block face on the block face's
      // normal axis. The face normal was effectively checked already through
      // XyzFaces. So checking only the first vertex is sufficient.
      float axisLocation =
          mesh.xyz[f * mesh.VerticesPerFace * 3 + (int)face.Axis];
      if (axisLocation < bounds[(int)face.Axis] ||
          axisLocation > bounds[(int)face.Axis + 3]) {
        continue;
      }

      // Verify the face has the original texture.
      bool allMatched = true;
      for (int i = 0; i < mesh.VerticesPerFace; ++i) {
        int uvoffset = (f * mesh.VerticesPerFace + i) * 2;
        float u = mesh.Uv[uvoffset];
        float v = mesh.Uv[uvoffset + 1];
        int textureId = mesh.TextureIds[mesh.TextureIndices[f]];
        if (textureId != original.atlasTextureId || u < original.x1 - errorX ||
            u > original.x2 + errorX || v < original.y1 - errorY ||
            v > original.y2 + errorY) {
          allMatched = false;
          break;
        }
      }
      if (!allMatched) {
        continue;
      }

      int hole = CopyFaceWithHole(mesh, face.Axis, f, face, bounds);
      int extraCopy = AddFaceCopy(mesh, hole);
      ClampFace(mesh, face.Axis, bounds, extraCopy, hole);
      RemoveLastFace(mesh);
      if (IsEmptyFace(mesh, hole)) {
        RemoveLastFace(mesh);
        continue;
      }

      mesh.TextureIndices[hole] =
          mesh.getTextureIndex(replacement.atlasTextureId);
      for (int i = 0; i < mesh.VerticesPerFace; ++i) {
        int uvoffset = (hole * mesh.VerticesPerFace + i) * 2;
        float u = mesh.Uv[uvoffset];
        float v = mesh.Uv[uvoffset + 1];
        // The original and replacment meshes are the same size. So reoffsetting
        // the uv coordinates is enough. Rescaling them is not necessary.
        mesh.Uv[uvoffset] = replacement.x1 + (u - original.x1);
        mesh.Uv[uvoffset + 1] = replacement.y1 + (v - original.y1);
      }
    }
  }
}
