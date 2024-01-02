using System;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Lambda.Gui;

public static partial class GuiComposerHelpers {
  public static GuiComposer AddDialogTitleBar(this GuiComposer composer,
                                              string text, Action onClose,
                                              string key) {
    composer.AddInteractiveElement(
        new GuiElementDialogTitleBar(composer.Api, text, composer, onClose),
        key);
    return composer;
  }
}

public class DialogTermInventory : GuiDialogBlockEntity {
  private static int _instance = 0;
  public DialogTermInventory(string title, string description,
                             InventoryBase inventory, BlockPos blockPos,
                             ICoreClientAPI capi)
      : base(Lang.Get(title), inventory, blockPos, capi) {
    int instance = _instance++;
    ElementBounds dialogBounds =
        ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.RightMiddle)
            .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

    ElementBounds bgBounds = ElementBounds.Fill;
    bgBounds.BothSizing = ElementSizing.FitToChildren;

    ElementBounds textBounds =
        ElementBounds
            .Fixed(GuiStyle.ElementToDialogPadding,
                   GuiStyle.TitleBarHeight + GuiStyle.ElementToDialogPadding,
                   300, 100)
            .WithFixedPadding(GuiStyle.ElementToDialogPadding / 2, 0);
    ElementBounds gridBounds =
        ElementStdBounds.SlotGrid(EnumDialogArea.CenterTop, 0, 0, 1, 1)
            .FixedUnder(textBounds, -GuiStyle.ElementToDialogPadding)
            .WithFixedPadding(0, GuiStyle.ElementToDialogPadding);

    ClearComposers();
    SingleComposer =
        capi.Gui
            .CreateCompo("terminventory" + BlockEntityPosition, dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(DialogTitle, () => TryClose(), "title")
            .BeginChildElements(bgBounds)
            .AddRichtext(Lang.Get(description), CairoFont.WhiteSmallText(),
                         textBounds, "description")
            .AddItemSlotGrid(Inventory, DoSendPacket, 1, gridBounds)
            .EndChildElements();
    bgBounds.CalcWorldBounds();
    SingleComposer.Compose();
  }
}
