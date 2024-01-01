using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace LambdaFactory.Gui;

public static partial class GuiComposerHelpers {
  public static GuiComposer AddDialogTitleBar(this GuiComposer composer,
                                              string text, string key) {
    composer.AddInteractiveElement(
        new GuiElementDialogTitleBar(composer.Api, text, composer), key);
    return composer;
  }
}

public class DialogTermInventory : GuiDialogBlockEntity {
  private static int _instance = 0;
  public DialogTermInventory(string title, string description,
                             InventoryBase inventory, BlockPos blockPos,
                             ICoreClientAPI capi)
      : base(title, inventory, blockPos, capi) {
    int instance = _instance++;
    ElementBounds dialogBounds =
        ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.RightMiddle)
            .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

    ElementBounds bgBounds = ElementBounds.Fill;
    bgBounds.BothSizing = ElementSizing.FitToChildren;

    ElementBounds textBounds =
        ElementBounds
            .Fixed(2.5 * GuiStyle.HalfPadding,
                   GuiStyle.TitleBarHeight + GuiStyle.HalfPadding, 300, 80)
            .WithFixedPadding(GuiStyle.HalfPadding);
    ElementBounds gridBounds =
        ElementStdBounds.SlotGrid(EnumDialogArea.CenterTop, 0, 0, 1, 1)
            .FixedUnder(textBounds)
            .WithFixedPadding(2 * GuiStyle.HalfPadding);

    ClearComposers();
    SingleComposer =
        capi.Gui
            .CreateCompo("terminventory" + BlockEntityPosition, dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get(title), "title")
            .BeginChildElements(bgBounds)
            .AddRichtext(Lang.Get(description), CairoFont.WhiteSmallText(),
                         textBounds, "description")
            .AddItemSlotGrid(Inventory, DoSendPacket, 1, gridBounds)
            .EndChildElements();
    bgBounds.CalcWorldBounds();
    SingleComposer.Compose();
  }
}
