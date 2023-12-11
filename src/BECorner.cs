using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Xml.Schema;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LambdaFactory;

public class CornerScopes : IEquatable<CornerScopes>, IEnumerable<Scope>, ICloneable {
  public class Enumerator : IEnumerator<Scope> {
    private int _index = 0;
    private readonly CornerScopes _scopes;

    public Enumerator(CornerScopes scopes) {
      _scopes = scopes;
    }

    public Scope Current {
      get => _scopes[_index];
      set => _scopes[_index] = value;
    }

    object IEnumerator.Current => Current;

    public void Dispose() {
    }

    public bool MoveNext() {
      if (_index < 12) {
        ++_index;
        return true;
      }
      return false;
    }

    public void Reset() {
      _index = 0;
    }
  }

  private static Scope _badIndex;

  public Scope NorthEast = Scope.None;
  public Scope SouthEast = Scope.None;
  public Scope SouthWest = Scope.None;
  public Scope NorthWest = Scope.None;
  public Scope UpNorth = Scope.None;
  public Scope UpEast = Scope.None;
  public Scope UpSouth = Scope.None;
  public Scope UpWest = Scope.None;
  public Scope DownNorth = Scope.None;
  public Scope DownEast = Scope.None;
  public Scope DownSouth = Scope.None;
  public Scope DownWest = Scope.None;

  public ref Scope GetScope(int key) {
    switch (key) {
      case 0:
        return ref NorthWest;
      case 1:
        return ref NorthEast;
      case 2:
        return ref SouthEast;
      case 3:
        return ref SouthWest;
      case 4:
        return ref UpNorth;
      case 5:
        return ref UpEast;
      case 6:
        return ref UpSouth;
      case 7:
        return ref UpWest;
      case 8:
        return ref DownNorth;
      case 9:
        return ref DownEast;
      case 10:
        return ref DownSouth;
      case 11:
        return ref DownWest;
      default:
        return ref _badIndex;
    }
  }

  public Scope this[int index] {
    get => GetScope(index);
    set => GetScope(index) = value;
  }

  public bool Equals(CornerScopes other) {
    if (other == null) {
      return false;
    }
    for (int i = 0; i < 12; ++i) {
      if (this[i] != other[i]) return false;
    }
    return true;
  }

  public override int GetHashCode() {
    HashCode hash = new HashCode();
    for (int i = 0; i < 12; ++i) {
      hash.Add(this[i]);
    }
    return hash.ToHashCode();
  }

  public override bool Equals(object obj) {
    return Equals(obj as CornerScopes);
  }

  public object Clone() { return MemberwiseClone(); }

  public IEnumerator<Scope> GetEnumerator() {
    return new Enumerator(this);
  }

  IEnumerator IEnumerable.GetEnumerator() {
    // Call the generic version
    return GetEnumerator();
  }

  public int[] GetIntArray() {
    int[] result = new int[12];
    for (int i = 0; i < 12; ++i) {
      result[i] = (int)this[i];
    }
    return result;
  }

  public void SetArray(int[] scopes) {
    for (int i = 0; i < Math.Min(scopes?.Length ?? 0, 12); i++) {
      if (scopes[i] >= (int)Scope.Min && scopes[i] <= (int)Scope.Max) {
        this[i] = (Scope)scopes[i];
      } else {
        this[i] = Scope.None;
      }
    }
    for (int i = scopes?.Length ?? 0; i < 12; ++i) {
      this[i] = Scope.None;
    }
  }

  public void SetArray(long[] scopes) {
    SetArray(scopes?.Select(scope => (int)scope).ToArray());
  }

  public TextureAtlasPosition TryGetTexture(ICoreClientAPI capi, string textureCode) {
    Scope scope;
    switch (textureCode) {
      case "southwest":
        scope = SouthWest;
        break;
      case "upwest":
        scope = UpWest;
        break;
      case "northwest":
        scope = NorthWest;
        break;
      case "downeast":
        scope = DownEast;
        break;
      case "northeast":
        scope = NorthEast;
        break;
      case "downsouth":
        scope = DownSouth;
        break;
      case "downwest":
        scope = DownWest;
        break;
      case "upnorth":
        scope = UpNorth;
        break;
      case "upsouth":
        scope = UpSouth;
        break;
      case "upeast":
        scope = UpEast;
        break;
      case "downnorth":
        scope = DownNorth;
        break;
      case "southeast":
        scope = SouthEast;
        break;
      default:
        return null;
    }

    CompositeTexture composite = new CompositeTexture(new AssetLocation(LambdaFactoryModSystem.Domain, "scope/cornercrystal"));
    if (scope != Scope.None) {
      BlendedOverlayTexture scopeBlend = new BlendedOverlayTexture();
      scopeBlend.Base =
          new AssetLocation(LambdaFactoryModSystem.Domain,
                            $"scope/{ScopeHelper.GetCode(scope)}");
      scopeBlend.BlendMode = EnumColorBlendMode.ColorBurn;
      composite.BlendedOverlays = new BlendedOverlayTexture[]{
        scopeBlend};
    }
    composite.Bake(capi.Assets);
    ITextureAtlasAPI atlas = capi.BlockTextureAtlas;
    atlas.GetOrInsertTexture(
        composite.Baked.BakedName, out int id,
        out TextureAtlasPosition tex,
        () => atlas.LoadCompositeBitmap(composite.Baked.BakedName));
    return tex;
  }
}

public class BlockEntityCorner : BlockEntity, IBlockEntityForward, ITexPositionSource {
  private MeshData _mesh;
  protected CornerScopes _corners = new CornerScopes();

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);

    UpdateMesh();
  }

  public ItemStack OnPickBlock(ref EnumHandling handling) {
    ItemStack stack = new ItemStack(Block, 1);
    stack.Attributes["Corners"] = new IntArrayAttribute(_corners.GetIntArray());

    handling = EnumHandling.PreventDefault;
    return stack;
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree["Corners"] = new IntArrayAttribute(_corners.GetIntArray());
  }

  public override void OnBlockPlaced(ItemStack byItemStack) {
    base.OnBlockPlaced(byItemStack);
    IAttribute cornersAttr = byItemStack.Attributes["Corners"];
    IntArrayAttribute cornersInts = cornersAttr as IntArrayAttribute;
    if (cornersInts != null) {
      _corners.SetArray(cornersInts.value);
    } else {
      _corners.SetArray((cornersAttr as LongArrayAttribute)?.value);
    }
    UpdateMesh();
  }

  public override void
  FromTreeAttributes(ITreeAttribute tree,
                     IWorldAccessor worldAccessForResolve) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    _corners.SetArray((tree["Corners"] as IntArrayAttribute)?.value);
    // No need to update the mesh here. Initialize will be called before the
    // block is rendered.
  }

  static private Dictionary<CornerScopes, MeshData> GetMeshCache(ICoreAPI api,
                                                        Block block) {
    return ObjectCacheUtil.GetOrCreate(api,
                                       $"lambdafactory-bescope-{block.Code}",
                                       () => new Dictionary<CornerScopes, MeshData>());
  }

  private void UpdateMesh() {
    if (Api.Side == EnumAppSide.Server)
      return;
    Dictionary<CornerScopes, MeshData> cache = GetMeshCache(Api, Block);
    if (cache.TryGetValue(_corners, out _mesh)) {
      return;
    }
    Api.Logger.Notification(
        "lambda: Cache miss for corner {0} {1}. Dict has {2} entries.", Block.Code,
        _corners, cache.Count);

    _mesh = cache[(CornerScopes)_corners.Clone()] = GenerateMesh(_corners);
  }

  protected virtual MeshData GenerateMesh(CornerScopes key) {
    MeshData mesh;
    ((ICoreClientAPI)Api).Tesselator.TesselateShape("corner", Block.Code, Block.Shape, out mesh, this);
    return mesh;
  }

  public override bool OnTesselation(ITerrainMeshPool mesher,
                                     ITesselatorAPI tessThreadTesselator) {
    if (_mesh == null) {
      return false;
    }
    mesher.AddMeshData(_mesh);
    return true;
  }

  public override void OnExchanged(Block block) {
    Api.Logger.Notification($"lambda: OnExchanged {GetHashCode()}");
    base.OnExchanged(block);
  }


  public Size2i AtlasSize {
    get {
      ITexPositionSource def = ((ICoreClientAPI)Api).Tesselator.GetTextureSource(Block);
      return def.AtlasSize;
    }
  }

  public TextureAtlasPosition this[string textureCode] {
    get {
      TextureAtlasPosition tex = _corners.TryGetTexture(((ICoreClientAPI)Api), textureCode);
      if (tex != null) {
        return tex;
      }
      ITexPositionSource def = ((ICoreClientAPI)Api).Tesselator.GetTextureSource(Block);
      return def[textureCode];
    }
  }
}
