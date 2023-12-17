using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LambdaFactory;

public class BEBehaviorDoubleScope : BlockEntityBehavior,
                                     IMeshGenerator,
                                     ITexPositionSource {
  protected (Scope, Scope) _faces = (Scope.None, Scope.None);

  public BEBehaviorDoubleScope(BlockEntity blockentity) : base(blockentity) {}

  public bool UpdatedPickedStack(ItemStack stack) {
    stack.Attributes.SetInt("face1", (int)_faces.Item1);
    stack.Attributes.SetInt("face2", (int)_faces.Item2);
    return true;
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetInt("face1", (int)_faces.Item1);
    tree.SetInt("face2", (int)_faces.Item2);
  }

  public override void OnBlockPlaced(ItemStack byItemStack) {
    base.OnBlockPlaced(byItemStack);
    _faces.Item1 =
        (Scope)byItemStack.Attributes.GetAsInt("face1", (int)Scope.None);
    _faces.Item2 =
        (Scope)byItemStack.Attributes.GetAsInt("face2", (int)Scope.None);
  }

  public override void
  FromTreeAttributes(ITreeAttribute tree,
                     IWorldAccessor worldAccessForResolve) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    _faces.Item1 = (Scope)tree.GetAsInt("face1", (int)Scope.None);
    _faces.Item2 = (Scope)tree.GetAsInt("face2", (int)Scope.None);
    // No need to update the mesh here. Initialize will be called before the
    // block is rendered.
  }

  public void GenerateMesh(ref MeshData mesh) {
    ((ICoreClientAPI)Api)
        .Tesselator.TesselateShape("doublescope", Block.Code, Block.Shape,
                                   out mesh, this);
  }

  public object GetKey() { return _faces; }

  public object GetImmutableKey() { return _faces; }

  public Size2i AtlasSize {
    get {
      ITexPositionSource def =
          ((ICoreClientAPI)Api).Tesselator.GetTextureSource(Block);
      return def.AtlasSize;
    }
  }

  public TextureAtlasPosition GetBlendedTexture(string baseTex, Scope scope) {
    CompositeTexture composite = new CompositeTexture(
        new AssetLocation(LambdaFactoryModSystem.Domain, baseTex));
    if (scope != Scope.None) {
      BlendedOverlayTexture scopeBlend = new BlendedOverlayTexture();
      scopeBlend.Base = new AssetLocation(LambdaFactoryModSystem.Domain,
                                          $"scope/{scope.GetCode()}");
      scopeBlend.BlendMode = EnumColorBlendMode.ColorBurn;
      composite.BlendedOverlays = new BlendedOverlayTexture[] { scopeBlend };
    }
    ICoreClientAPI capi = (ICoreClientAPI)Api;
    composite.Bake(capi.Assets);
    ITextureAtlasAPI atlas = capi.BlockTextureAtlas;
    atlas.GetOrInsertTexture(
        composite.Baked.BakedName, out int id, out TextureAtlasPosition tex,
        () => atlas.LoadCompositeBitmap(composite.Baked.BakedName));
    return tex;
  }

  public TextureAtlasPosition this[string textureCode] {
    get {
      return textureCode switch {
        "active1up" => GetBlendedTexture("scope/up", _faces.Item1),
        "active1ew" => GetBlendedTexture("scope/eastwest", _faces.Item1),
        "active1ns" => GetBlendedTexture("scope/northsouth", _faces.Item1),
        "active2up" => GetBlendedTexture("scope/up", _faces.Item2),
        "active2ew" => GetBlendedTexture("scope/eastwest", _faces.Item2),
        "active2ns" => GetBlendedTexture("scope/northsouth", _faces.Item2),
        _ => ((ICoreClientAPI)Api)
                 .Tesselator.GetTextureSource(Block)[textureCode],
      };
    }
  }
}
