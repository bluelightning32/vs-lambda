using System;

using Cairo;

using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Lambda.BlockEntityRenderer;

// Renders text on top of a block.
public class TopLabelRenderer : BlockEntitySignRenderer {
  private readonly Matrixf _modelMatrix;
  private string _text = "";

  public TopLabelRenderer(BlockFacing yrot, BlockPos pos, Vec2f topLeft,
                          Vec2f bottomRight, ICoreClientAPI api)
      : base(
            pos, api,
            new TextAreaConfig() { VerticalAlign = EnumVerticalAlign.Middle }) {
    _modelMatrix =
        Matrixf
            .Create()
            // 5. Shift the origin of the quad from the center of the block to
            // the northwest bottom corner of the block. Also put the quad on
            // top of the block. The extra 0.01f is to prevent z-fighting.
            .Translate(0.5f, 1.01f, 0.5f)
            // 4. Rotate the text to face the correct direction. A south facing
            // block gets no rotation. A west facing block gets a 90 degree
            // rotation.
            .RotateY((yrot.HorizontalAngleIndex -
                      BlockFacing.SOUTH.HorizontalAngleIndex) *
                     GameMath.PIHALF)
            // 3. Move the center of the model from the origin to the center of
            // (topLeft, bottomRight), but shifted over (0.5, 0, 0.5).
            .Translate((bottomRight.X + topLeft.X) / 32f - 0.5f, 0,
                       (bottomRight.Y + topLeft.Y) / 32f - 0.5f)
            // 2. Shrink the model from 2x2 to (bottomRight - topLeft) size
            .Scale((bottomRight.X - topLeft.X) / 32f, 0f,
                   (bottomRight.Y - topLeft.Y) / 32f)
            // 1. Transform the model from being in the XY plane
            // to the XZ plane.
            .RotateXDeg(-90)
        // 0. quadModelRef is [-1, -1, 0] to [1, 1, 0].
        ;

    // BlockEntitySignRenderer generates a flipped model. It compensates for
    // that by turning off back culling, but that's inefficient. So instead,
    // dispose of the flipped quad and replace it with a correct quad.
    // Specifically the uv mapping is fixed.
    quadModelRef?.Dispose();
    MeshData modeldata = QuadMeshUtil.GetQuad();
    // bottom-left, bottom-right, top-right, top-left
    modeldata.Uv = new float[] { 0, 1, 1, 1, 1, 0, 0, 0 };
    modeldata.Rgba = new byte[4 * 4];
    modeldata.Rgba.Fill((byte)255);
    quadModelRef = api.Render.UploadMesh(modeldata);

    TextWidth = (int)((bottomRight.X - topLeft.X) / 16 * 200);
    TextHeight = (int)((bottomRight.Y - topLeft.Y) / 16 * 200);
  }

  // Calculates output = Matrixf.Create().Translate(x, y, z).Mul(A.Values)
#pragma warning disable IDE1006 // Naming Styles
  public static Matrixf TranslateLeft(Matrixf output, float x, float y, float z,
                                      Matrixf A) {
    for (int i = 0; i < 16;) {
      int last = i + 3;
      output.Values[i] = A.Values[i] + x * A.Values[last];
      ++i;
      output.Values[i] = A.Values[i] + y * A.Values[last];
      ++i;
      output.Values[i] = A.Values[i] + z * A.Values[last];
      ++i;
      output.Values[i] = A.Values[i];
      ++i;
    }
    return output;
  }
#pragma warning restore IDE1006 // Naming Styles

  public override void OnRenderFrame(float deltaTime, EnumRenderStage stage) {
    if (loadedTexture == null) {
      return;
    }

    IRenderAPI rpi = api.Render;
    // Don't render the sign if it is outside of viewing range.
    if (!rpi.DefaultFrustumCuller.SphereInFrustum(pos.X + 0.5, pos.Y + 0.5,
                                                  pos.Z + 0.5, 1)) {
      return;
    }

    // BlockEntitySignRenderer generates textures with a premultiplied alpha, so
    // the OpenGL blend mode must be set to that. A premultiplied alpha means
    // that all of the color channels are already multiplied by the alpha
    // channel. So for example if the color was white, but the alpha was only
    // 50%, then with a premultiplied alpha image, the red channel would only be
    // 50% (as opposed to 100% for a white image before alpha multiplication).
    rpi.GlToggleBlend(true, EnumBlendMode.PremultipliedAlpha);
    Vec3d camPos = api.World.Player.Entity.CameraPos;

    // This does not create a new shader. It only gets a reference to a shader
    // shared by many renderers.
    IStandardShaderProgram prog =
        rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);

    // Use the text texture created by `BlockEntitySignRenderer`.
    prog.Tex2D = loadedTexture.TextureId;

    // Translate origin of the precalculated `_modelMatrix` from (0, 0, 0) to
    // the block position relative to the camera position.
    prog.ModelMatrix = TranslateLeft(ModelMat, (float)(pos.X - camPos.X),
                                     (float)(pos.Y - camPos.Y),
                                     (float)(pos.Z - camPos.Z), _modelMatrix)
                           .Values;

    prog.ViewMatrix = rpi.CameraMatrixOriginf;
    // PreparedStandardShader already set ProjectionMatrix to
    // CurrentProjectionMatrix.
    prog.NormalShaded = 0;
    // PreparedStandardShader already set ExtraGodray = 0.
    prog.SsaoAttn = 0;
    prog.AlphaTest = 0.05f;
    // PreparedStandardShader already OverlayOpacity = 0.

    rpi.RenderMesh(quadModelRef);
    // Stop using the shader. The shader was enabled in
    // `PreparedStandardShader`.
    prog.Stop();

    // Restore the default blend mode.
    rpi.GlToggleBlend(true, EnumBlendMode.Standard);
  }

  public override void SetNewText(string text, int color) {
    if (ColorUtil.FromRGBADoubles(font.Color) == color && _text == text) {
      return;
    }

    // Reduce the font size if it doesn't fit in the bounds. Don't increase the
    // font size.
    font.UnscaledFontsize = fontSize / RuntimeEnv.GUIScale;
    double maxWidth = 0;
    // Find the length of each line individually, because otherwise GetTextExtents treats '\n' as a space.
    string[] lines = text.Split('\n');
    foreach (string line in lines) {
      TextExtents extents = font.GetTextExtents(line);
      if (extents.Width > maxWidth) {
        maxWidth = extents.Width;
      }
    }
    int lineCount = lines.Length;
    if (text.EndsWith('\n')) {
      --lineCount;
    }
    double maxHeight = lineCount * api.Gui.Text.GetLineHeight(font);
    double originalSize = fontSize;
    if (maxWidth > TextWidth) {
      // Somehow this calculation doesn't shrink the font enough without the 0.9
      // multiple.
      fontSize *= (float)(TextWidth * 0.9 / maxWidth);
    }
    if (maxHeight > TextHeight) {
      fontSize = (float)Math.Min(fontSize, originalSize * TextHeight / maxHeight);
    }

    base.SetNewText(text, color);
    _text = text;
  }
}
