using System;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace LambdaFactory;

public class GuiDialogTermInventory : GuiDialogBlockEntity {
  private static int _instance = 0;
  public GuiDialogTermInventory(string title, string description,
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
        ElementBounds.Fixed(0, GuiStyle.TitleBarHeight, 300, 80)
            .WithFixedPadding(2 * GuiStyle.HalfPadding);
    ElementBounds gridBounds =
        ElementStdBounds.SlotGrid(EnumDialogArea.CenterTop, 0, 0, 1, 1)
            .FixedUnder(textBounds)
            .WithFixedPadding(2 * GuiStyle.HalfPadding);

    ClearComposers();
    SingleComposer =
        capi.Gui
            .CreateCompo("terminventory" + BlockEntityPosition, dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get(title))
            .BeginChildElements(bgBounds)
            .AddStaticText(Lang.Get(description), CairoFont.WhiteSmallText(),
                           textBounds)
            .AddItemSlotGrid(Inventory, DoSendPacket, 1, gridBounds)
            .EndChildElements();
    bgBounds.CalcWorldBounds();
    SingleComposer.Compose();
  }
}
