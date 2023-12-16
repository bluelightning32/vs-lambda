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

public class BEBehaviorScope : BlockEntityBehavior,
                               IMeshGenerator,
                               ITexPositionSource {
  protected Scope _face = Scope.None;

  public BEBehaviorScope(BlockEntity blockentity) : base(blockentity) {}

  public bool UpdatedPickedStack(ItemStack stack) {
    stack.Attributes.SetInt("face", (int)_face);
    return true;
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetInt("face", (int)_face);
  }

  public override void OnBlockPlaced(ItemStack byItemStack) {
    base.OnBlockPlaced(byItemStack);
    if (byItemStack == null) {
      // The OmniRotatable behavior does not pass the item stack through when it
      // places the oriented block.
      _face = Scope.None;
    } else {
      _face = (Scope)byItemStack.Attributes.GetAsInt("face", (int)Scope.None);
    }
  }

  public override void
  FromTreeAttributes(ITreeAttribute tree,
                     IWorldAccessor worldAccessForResolve) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    _face = (Scope)tree.GetAsInt("face", (int)Scope.None);
    // No need to update the mesh here. Initialize will be called before the
    // block is rendered.
  }

  public void GenerateMesh(ref MeshData mesh) {
    ((ICoreClientAPI)Api)
        .Tesselator.TesselateShape("scope", Block.Code, Block.Shape, out mesh,
                                   this);
  }

  public object GetKey() { return _face; }

  public object GetClonedKey() { return _face; }

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
        "up" => GetBlendedTexture("scope/up", _face),
        "eastwest" => GetBlendedTexture("scope/eastwest", _face),
        "northsouth" => GetBlendedTexture("scope/northsouth", _face),
        _ => ((ICoreClientAPI)Api)
                 .Tesselator.GetTextureSource(Block)[textureCode],
      };
    }
  }
}
