using System;
using System.Diagnostics;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Lambda.Gui;

public class DialogFunctionInventory : GuiDialogBlockEntity {
  private int _tab = 0;
  private string _errorMessage;
  private string _description;
  public string Description {
    get { return _description; }
    set {
      _description = value;
      if (_tab == 0) {
        _richText?.SetNewText(_description, CairoFont.WhiteSmallText());
      }
    }
  }
  public string ErrorMessage {
    get { return _errorMessage; }
    set {
      _errorMessage = value;
      if (_tab == 1) {
        _richText?.SetNewText(_errorMessage ?? "", CairoFont.WhiteSmallText());
      }
    }
  }
  private readonly float[] _scrollPos = new float[2] { 0, 0 };
  // This is set to non-null when the scroll bar and rich text elements are
  // fully created. When this is null, any events from the scroll bar should be
  // ignored, because the scroll bar is still being initialized.
  GuiElementRichtext _richText = null;
  float _progress = 0;
  readonly Action _onInscribe;

  public float Progress {
    get { return _progress; }
    set {
      _progress = value;
      GuiElementStatbar bar = SingleComposer.GetStatbar("progress");
      bar?.SetValue(_progress);
    }
  }

  private static readonly int _insetDepth =
      GuiElementScrollbar.DeafultScrollbarPadding;

  public DialogFunctionInventory(string title, string description,
                                 float progress, string errorMessage,
                                 InventoryBase inventory, BlockPos blockPos,
                                 ICoreClientAPI capi, Action onInscribe)
      : base(Lang.Get(title), inventory, blockPos, capi) {
    _onInscribe = onInscribe;
    _description = description;
    _progress = progress;
    _errorMessage = errorMessage;
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
    bgBounds.WithFixedPosition(0, 0);
    bgBounds.WithFixedPadding(GuiStyle.ElementToDialogPadding);

    // clip bounds and inset bounds for the first tab.
    ElementBounds clipBounds =
        ElementBounds.Fixed(0, GuiStyle.TitleBarHeight, 300, 130);
    ElementBounds insetBounds = clipBounds.ForkBoundingParent(
        _insetDepth, _insetDepth, _insetDepth, _insetDepth);

    // Calculate the gridBounds using the inset bounds from the first tab.
    ElementBounds gridBounds =
        ElementStdBounds.SlotGrid(EnumDialogArea.LeftTop, 0, 0, 1, 1)
            .FixedUnder(insetBounds, GuiStyle.ElementToDialogPadding);
    if (_tab == 1) {
      // Now update clipBounds and insetBounds for the current tab.
      clipBounds = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight, 300, 200);
      insetBounds = clipBounds.ForkBoundingParent(_insetDepth, _insetDepth,
                                                  _insetDepth, _insetDepth);
    }

    ElementBounds textBounds =
        clipBounds.ForkContainingChild(0, -_scrollPos[_tab]);
    ElementBounds scrollbarBounds =
        ElementStdBounds.VerticalScrollbar(insetBounds);

    string message = _tab == 0 ? _description : (_errorMessage ?? "");
    _richText = null;
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
            .BeginChildElements(bgBounds)
            .AddInset(insetBounds, _insetDepth)
            .BeginClip(clipBounds)
            .AddRichtext(message, CairoFont.WhiteSmallText(), textBounds,
                         "richtext")
            .EndClip()
            .AddVerticalScrollbar(OnScrollText, scrollbarBounds, "scrollbar");

    SingleComposer.GetHorizontalTabs("tabs").SetValue(_tab, false);

    if (_tab == 0) {
      ElementBounds buttonBounds =
          ElementStdBounds.MenuButton(0, EnumDialogArea.LeftTop)
              .WithFixedSize(150, buttonHeight)
              .WithFixedPosition(0, gridBounds.fixedY + 2)
              .FixedRightOf(gridBounds, GuiStyle.ElementToDialogPadding);

      ElementBounds progressBounds =
          ElementStdBounds
              .Statbar(EnumDialogArea.LeftTop,
                       buttonBounds.fixedWidth + 2 * buttonBounds.fixedPaddingX)
              .FixedUnder(buttonBounds,
                          GuiStyle.HalfPadding + buttonBounds.fixedPaddingY)
              .FixedRightOf(gridBounds, GuiStyle.ElementToDialogPadding);

      SingleComposer.AddItemSlotGrid(Inventory, DoSendPacket, 1, gridBounds)
          .AddSmallButton(Lang.Get("lambda:inscribe"), OnInscribe, buttonBounds)
          .AddStatbar(progressBounds, GuiStyle.SuccessTextColor, true,
                      "progress");
      Progress = _progress;
    } else {
      // The gridBounds represents the last vertical element on the other tab.
      // Even though the error tab does not include the grid, explicitly add its
      // bound here so that error tab is the same height as the container tab.
      bgBounds.WithChild(gridBounds);
    }

    SingleComposer.EndChildElements();
    bgBounds.CalcWorldBounds();
    SingleComposer.Compose();
    GuiElementScrollbar scroll = SingleComposer.GetScrollbar("scrollbar");
    // `scroll.SetHeights` will fire some scroll events before the scroll bar is
    // full initialized. `_richText` should be set to null at this point so that
    // those bad events are ignored.
    Debug.Assert(_richText == null);
    GuiElementRichtext richText = SingleComposer.GetRichtext("richtext");
    scroll.SetHeights((float)clipBounds.fixedHeight,
                      (float)richText.Bounds.fixedHeight);
    scroll.CurrentYPosition = _scrollPos[_tab];
    // Now the scroll bar is fully initialized.
    _richText = richText;
  }

  private void OnScrollText(float value) {
    if (_richText == null) {
      // Ignore this event, because the scroll bar is still getting initialized.
      return;
    }
    _scrollPos[_tab] = value;
    _richText.Bounds.fixedY = _insetDepth - value;
    _richText.Bounds.CalcWorldBounds();
  }

  private bool OnInscribe() {
    _onInscribe();
    return true;
  }

  private void OnTabClicked(int tab) {
    _tab = tab;
    Compose();
  }
}
