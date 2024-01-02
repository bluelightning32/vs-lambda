using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Lambda.Gui;

public class DialogFunctionInventory : GuiDialogBlockEntity {
  private int _tab = 0;
  private string _errorMessage = "error message not set";
  private string _description;
  public string Description {
    get { return _description; }
    set {
      _description = value;
      SingleComposer.GetRichtext("description")
          .SetNewText(_description, CairoFont.WhiteSmallText());
    }
  }

  public DialogFunctionInventory(string title, string description,
                                 InventoryBase inventory, BlockPos blockPos,
                                 ICoreClientAPI capi)
      : base(Lang.Get(title), inventory, blockPos, capi) {
    _description = description;
    ClearComposers();
    SingleComposer = capi.Gui.CreateCompo("terminventory" + BlockEntityPosition,
                                          ElementBounds.Empty);
    Compose();
  }

  public void Compose() {
    ElementBounds dialogBounds =
        ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.RightMiddle)
            .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

    const int buttonHeight = 25;
    ElementBounds tabBounds =
        ElementBounds.Fixed(0, -buttonHeight, 300, buttonHeight);

    GuiTab[] tabs = new GuiTab[] {
      new() { Name = Lang.Get("lambda:tab-container"), DataInt = 0 },
      new() { Name = Lang.Get("lambda:tab-errors"), DataInt = 1 },
    };

    ElementBounds bgBounds = ElementBounds.Fill;
    bgBounds.BothSizing = ElementSizing.FitToChildren;

    SingleComposer.Clear(dialogBounds);
    SingleComposer =
        capi.Gui
            .CreateCompo("terminventory" + BlockEntityPosition, dialogBounds)
            .AddHorizontalTabs(tabs, tabBounds, OnTabClicked,
                               CairoFont.ButtonText().WithFontSize(
                                   (float)GuiStyle.SmallFontSize),
                               CairoFont.ButtonPressedText().WithFontSize(
                                   (float)GuiStyle.SmallFontSize),
                               "tabs")
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(DialogTitle, () => TryClose(), "title")
            .BeginChildElements(bgBounds);

    SingleComposer.GetHorizontalTabs("tabs").SetValue(_tab, false);

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
    if (_tab == 0) {
      ElementBounds buttonBounds =
          ElementStdBounds.MenuButton(0, EnumDialogArea.LeftTop)
              .WithFixedSize(150, buttonHeight)
              .FixedUnder(textBounds, 2)
              .FixedRightOf(gridBounds, GuiStyle.ElementToDialogPadding);

      ElementBounds progressBounds =
          ElementStdBounds
              .Statbar(EnumDialogArea.LeftTop,
                       buttonBounds.fixedWidth + 2 * buttonBounds.fixedPaddingX)
              .FixedUnder(buttonBounds,
                          GuiStyle.HalfPadding + buttonBounds.fixedPaddingY)
              .FixedRightOf(gridBounds, GuiStyle.ElementToDialogPadding)
              .WithFixedPadding(GuiStyle.ElementToDialogPadding / 2);

      SingleComposer
          .AddRichtext(Lang.Get(Description), CairoFont.WhiteSmallText(),
                       textBounds, "description")
          .AddItemSlotGrid(Inventory, DoSendPacket, 1, gridBounds)
          .AddSmallButton(Lang.Get("lambda:inscribe"), OnInscribe, buttonBounds)
          .AddStatbar(progressBounds, GuiStyle.SuccessTextColor, true,
                      "progress");
    } else {
      textBounds =
          ElementBounds
              .Fixed(GuiStyle.ElementToDialogPadding,
                     GuiStyle.TitleBarHeight + GuiStyle.ElementToDialogPadding,
                     300, 200)
              .WithFixedPadding(GuiStyle.ElementToDialogPadding / 2, 0);
      SingleComposer.AddRichtext(_errorMessage, CairoFont.WhiteSmallText(),
                                 textBounds, "errors");
      // The gridBounds represents the last vertical element on the other tab.
      // Even though the error tab does not include the grid, explicitly add its
      // bound here so that error tab is the same height as the container tab.
      bgBounds.WithChild(gridBounds);
    }

    SingleComposer.EndChildElements();
    bgBounds.CalcWorldBounds();
    SingleComposer.Compose();
  }

  private bool OnInscribe() { return true; }

  private void OnTabClicked(int tab) {
    _tab = tab;
    Compose();
  }
}
