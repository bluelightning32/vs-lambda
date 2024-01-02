using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Lambda.Gui;

public class DialogFunctionInventory : GuiDialogBlockEntity {
  private static int _instance = 0;
  public DialogFunctionInventory(string title, string description,
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
            .Fixed(GuiStyle.ElementToDialogPadding,
                   GuiStyle.TitleBarHeight + GuiStyle.ElementToDialogPadding,
                   300, 100)
            .WithFixedPadding(GuiStyle.ElementToDialogPadding / 2, 0);
    ElementBounds gridBounds =
        ElementStdBounds
            .SlotGrid(EnumDialogArea.LeftTop, GuiStyle.ElementToDialogPadding,
                      0, 1, 1)
            .FixedUnder(textBounds, -GuiStyle.ElementToDialogPadding)
            .WithFixedPadding(0, GuiStyle.ElementToDialogPadding);

    ElementBounds buttonBounds =
        ElementStdBounds.MenuButton(0, EnumDialogArea.LeftTop)
            .WithFixedSize(150, 25)
            .FixedUnder(textBounds)
            .FixedRightOf(gridBounds, GuiStyle.ElementToDialogPadding);

    ElementBounds progressBounds =
        ElementStdBounds
            .Statbar(EnumDialogArea.LeftTop,
                     buttonBounds.fixedWidth + 2 * buttonBounds.fixedPaddingX)
            .FixedUnder(buttonBounds,
                        GuiStyle.HalfPadding + buttonBounds.fixedPaddingY)
            .FixedRightOf(gridBounds, GuiStyle.ElementToDialogPadding)
            .WithFixedPadding(GuiStyle.ElementToDialogPadding / 2);

    ClearComposers();
    description = Lang.Get(description);
    SingleComposer =
        capi.Gui
            .CreateCompo("terminventory" + BlockEntityPosition, dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get(title), "title")
            .BeginChildElements(bgBounds)
            .AddRichtext(description, CairoFont.WhiteSmallText(), textBounds,
                         "description")
            .AddItemSlotGrid(Inventory, DoSendPacket, 1, gridBounds)
            .AddSmallButton(Lang.Get("lambda-inscribe"), OnInscribe,
                            buttonBounds)
            .AddStatbar(progressBounds, GuiStyle.SuccessTextColor, true,
                        "progress")
            .EndChildElements();
    bgBounds.CalcWorldBounds();
    SingleComposer.Compose();
  }

  private bool OnInscribe() { return true; }
}
