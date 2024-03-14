using System;
using System.Collections.Generic;
using System.Text;

using Lambda.BlockBehaviors;
using Lambda.CollectibleBehaviors;
using Lambda.Token;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Lambda.BlockEntities;

public class DestructionFluidProps {
  public float UseLitres;
  public float OutputLitres = -1;
  public JsonItemStack Output;
}

public class DestructionJig : Jig {
  // Pass in null for the API and inventory class name for now. The correct
  // values will be passed by `BlockEntityContainer.Initialize` when it calls
  // `Inventory.LateInitialize`.
  private readonly InventoryGeneric _inventory =
      new(2, null, null, (int slot, InventoryGeneric inv) => {
        if (slot == 0)
          return new ItemSlotLiquidOnly(inv, 9999);
        else
          return new ItemSlotSurvival(inv);
      });
  public override InventoryBase Inventory => _inventory;
  DestructInfo _destructInfo = null;
  private ItemSlot LiquidSlot => _inventory[0];

  public override void Initialize(ICoreAPI api) {
    // Set CapacityLitres before calling the base, because the base calls back
    // into the DestructionJig to initialize the meshes, and the DestructionJig
    // mesh generator expects the capacity to be initialized.
    if (Block.Attributes?["capacityLitres"].Exists == true) {
      ((ItemSlotLiquidOnly)_inventory[0]).CapacityLitres =
          Block.Attributes["capacityLitres"].AsFloat();
    }
    base.Initialize(api);
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    if (_termState == TermState.CompilationDone) {
      _destructInfo?.ToTreeAttributes(tree);
    }
  }

  protected override void LoadTermInfo(ITreeAttribute tree,
                                       IWorldAccessor worldForResolving) {
    if (_termState != TermState.CompilationDone) {
      _destructInfo = null;
    } else {
      _destructInfo ??= new();
      _destructInfo.FromTreeAttributes(tree);
    }
  }

  protected override MeshData GetOrCreateErrorMesh() {
    if (MeshCache.TryGetValue("error", out MeshData result)) {
      return result;
    }

    ICoreClientAPI capi = Api as ICoreClientAPI;
    ITexPositionSource contentSource = new ContainerTextureSource(
        capi, null,
        Block.Attributes["errorTexture"].AsObject<CompositeTexture>(
            null, Block.Code.Domain));
    MeshData contentMesh = CreateLiquidMesh(capi, contentSource);
    MeshCache["error"] = contentMesh;
    return contentMesh;
  }
  private Matrixf GetTransformationMatrix(float startY, ItemSlot slot) {
    return GetTransformationMatrix(startY, 8f / 16f, 6f / 16f, 8f / 16f, slot);
  }

  protected override float[][] genTransformationMatrices() {
    float[][] matrices = new float [_inventory.Count][];
    float fill = 0;
    float capacity = 1;
    if (!LiquidSlot.Empty) {
      WaterTightContainableProps props =
          BlockLiquidContainerBase.GetContainableProps(LiquidSlot.Itemstack);
      fill = LiquidSlot.StackSize / props.ItemsPerLitre;
      capacity = ((ItemSlotLiquidOnly)LiquidSlot).CapacityLitres;
    }
    const float liquidContainerHeight = 2f / 16f;
    const float liquidContainerBase = 0.5f + 6 / 16f;
    Matrixf liquidMat = GetTransformationMatrix(
        liquidContainerBase + liquidContainerHeight * fill / capacity,
        LiquidSlot);
    matrices[0] = liquidMat.Values;
    for (int i = 1; i < _inventory.Count; ++i) {
      ItemSlot slot = Inventory[i];
      Matrixf mat = GetTransformationMatrix(0.5f + i * 6f / 16f, slot);
      matrices[i] = mat.Values;
    }
    return matrices;
  }

  public static DestructionFluidProps GetDestructionProps(ItemStack stack) {
    return stack?.ItemAttributes["destructionFluidProps"]
        .AsObject<DestructionFluidProps>();
  }

  protected override void ResetTermState() {
    base.ResetTermState();
    _destructInfo = null;
  }

  protected override bool GetCompilationTerms(out string[] terms,
                                              out string[][] imports) {
    Term termBhv = _inventory[1].Itemstack?.Collectible.GetBehavior<Term>();
    terms = new string[1] { termBhv?.GetTerm(_inventory[1].Itemstack) };
    if (terms[0] == null) {
      imports = null;
      return false;
    }
    imports = new string[1][] { termBhv?.GetImports(_inventory[1].Itemstack) };

    // Unless the state is in the done phase, check the liquid.
    //
    // If the liquid isn't a valid destruction fluid, then reset the state and
    // return.
    //
    // This check is skipped in the done phase, because the player is allowed to
    // remove the liquid in that phase.
    if (_termState != TermState.CompilationDone) {
      ItemStack liquid = LiquidSlot.Itemstack;
      if (liquid == null) {
        return false;
      }
      WaterTightContainableProps props =
          BlockLiquidContainerBase.GetContainableProps(liquid);
      DestructionFluidProps destructionProps = GetDestructionProps(liquid);
      float fill = liquid.StackSize / props.ItemsPerLitre;
      if (destructionProps == null || fill < destructionProps.UseLitres) {
        return false;
      }
    }
    return true;
  }

  protected override void Compile(string[][] imports, string[] terms) {
    ServerConfig config = CoreSystem.GetInstance(Api).ServerConfig;
    using CoqSession session = new(config);

    DestructInfo info = session.GetDestructInfo(Pos, imports[0], terms[0]);

    Api.Event.EnqueueMainThreadTask(() => CompilationDone(info), "lambda");
  }

  private void CompilationDone(DestructInfo info) {
    _compilationRunning = false;
    if (_termState != TermState.CompilationRunning) {
      if (_termState == TermState.CompilationWaiting) {
        StartCompilation();
      }
      return;
    }
    _destructInfo = info;
    _termState = TermState.CompilationDone;
    if (info.ErrorMessage != null) {
      _dropWaiting = false;
      MarkDirty();
      return;
    }

    ItemStack liquid = LiquidSlot.Itemstack;
    DestructionFluidProps destructionProps = GetDestructionProps(liquid);
    destructionProps.Output.Resolve(Api.World, Block.Code.Path);
    if (destructionProps.OutputLitres > 0) {
      WaterTightContainableProps outputProps =
          BlockLiquidContainerBase.GetContainableProps(
              destructionProps.Output.ResolvedItemstack);
      destructionProps.Output.ResolvedItemstack.StackSize =
          (int)(destructionProps.OutputLitres * outputProps.ItemsPerLitre);
    }
    LiquidSlot.Itemstack = destructionProps.Output.ResolvedItemstack;
    LiquidSlot.MarkDirty();
    MarkDirty();

    if (_dropWaiting) {
      _dropWaiting = false;
      if (CanCraftItems() && Api.Side == EnumAppSide.Server) {
        CraftItems();
      }
    }
  }

  protected override Cuboidf GetItemBounds(int begin, int end) {
    return new(4f / 16f, 0.5f + begin * 6f / 16f, 4f / 16f, 12f / 16f,
               0.5f + end * 6f / 16f, 12f / 16f);
  }

  protected override void UpdateInventoryBounds() {
    if (!_inventory[1].Empty) {
      _inventoryBounds = new Cuboidf[1] { GetItemBounds(1, 2) };
    } else {
      _inventoryBounds = null;
    }
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    string error = _destructInfo?.ErrorMessage;
    if (error != null) {
      dsc.AppendLine(Term.Escape(error));
    }
  }

  protected override MeshData GetMesh(int slot, ItemStack stack) {
    if (slot == 0) {
      // This is the liquid slot
      if (stack != null) {
        string key;
        if (_termState == TermState.CompilationDone &&
            _destructInfo.ErrorMessage != null) {
          key = "error";
        } else {
          key = getMeshCacheKey(stack);
        }
        MeshCache.TryGetValue(key, out MeshData mesh);
        return mesh;
      }
    }
    return base.GetMesh(slot, stack);
  }

  protected override bool CanCraftItems() {
    if (!base.CanCraftItems()) {
      return false;
    }
    if (_destructInfo.ErrorMessage != null) {
      return false;
    }
    return true;
  }

  protected override void CraftItems() {
    DestructInfo destructInfo = _destructInfo;
    _termState = TermState.Invalid;
    _destructInfo = null;
    // Take out the water as a convenience to the player, and because it
    // "splashed" when the term was hit.
    for (int i = 0; i < _inventory.Count; ++i) {
      _inventory[i].TakeOutWhole();
    }
    HashSet<int> skipTerms = new();
    if (destructInfo.Terms[0].Term[0] == '@') {
      ItemStack constructor =
          Term.FindStandard(Api, destructInfo.Terms[0].Term[1..]);
      if (constructor != null) {
        Term term = constructor.Collectible.GetBehavior<Term>();
        skipTerms.Add(0);
        foreach (int i in term.GetImplicitArguments(constructor)) {
          skipTerms.Add(i + 1);
        }
        Api.World.SpawnItemEntity(constructor, Pos.ToVec3d().Add(0.5, 0.5, 0.5),
                                  null);
      }
    }

    for (int i = 0; i < destructInfo.Terms.Count; ++i) {
      if (skipTerms.Contains(i)) {
        continue;
      }
      TermInfo termInfo = destructInfo.Terms[i];
      ItemStack drop = Term.Find(Api, termInfo);
      Api.World.SpawnItemEntity(drop, Pos.ToVec3d().Add(0.5, 0.5, 0.5), null);
    }
    MarkDirty();
  }
}
