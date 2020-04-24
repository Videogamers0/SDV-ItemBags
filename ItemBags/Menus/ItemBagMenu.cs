using ItemBags.Bags;
using ItemBags.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Object = StardewValley.Object;
using static ItemBags.Helpers.DrawHelpers;

namespace ItemBags.Menus
{
    //  This really shouldn't have been an abstract class. 
    //  BoundedBagMenu, BundleBagMenu, and RucksackMenu should have just been child objects of ItemBagMenu, not subclasses of it.
    //  Then they could have implemented some kind of ItemBagContentsRenderer interface, for drawing the upper half portion of the menu.
    //  But whatever, too lazy to change it now so enjoy the spaghetti

    public abstract class ItemBagMenu : IClickableMenu
    {
        /// <summary>The number of frames to wait before repeatedly transferring items while the mouse right button is held</summary>
        public const int TransferRepeatFrequency = 4;

        public ItemBag Bag { get; }
        public IList<Item> InventorySource { get; }
        public int ActualInventoryCapacity { get; }

        public Color BorderColor { get; }
        public Color BackgroundColor { get; }

        protected Texture2D White { get { return TextureHelpers.GetSolidColorTexture(Game1.graphics.GraphicsDevice, Color.White); } }

        private BagInventoryMenu InventoryMenu { get; }
        //private BagInfo BagInfo { get; }

        #region Sidebar
        private bool IsLeftSidebarVisible { get; }
        private bool IsRightSidebarVisible { get; }

        private enum SidebarButton
        {
            DepositAll,
            WithdrawAll,
            Autoloot,
            HelpInfo,
            CustomizeIcon
        }
        private SidebarButton? HoveredButton { get; set; } = null;
        public bool IsHoveringAutofillButton { get { return HoveredButton.HasValue && HoveredButton.Value == SidebarButton.Autoloot; } }

        private Rectangle DepositAllBounds { get; set; }
        private Rectangle WithdrawAllBounds { get; set; }
        private Rectangle AutolootBounds { get; set; }
        private Rectangle HelpInfoBounds { get; set; }
        private Rectangle CustomizeIconBounds { get; set; }
        
        private IEnumerable<Rectangle> LeftSidebarButtonBounds { get { return new List<Rectangle>() { DepositAllBounds, WithdrawAllBounds, AutolootBounds }; } }
        private IEnumerable<Rectangle> RightSidebarButtonBounds { get { return new List<Rectangle>() { HelpInfoBounds, CustomizeIconBounds }; } }

        private Rectangle? HoveredButtonBounds
        {
            get
            {
                if (HoveredButton.HasValue)
                {
                    if (HoveredButton.Value == SidebarButton.DepositAll)
                        return DepositAllBounds;
                    else if (HoveredButton.Value == SidebarButton.WithdrawAll)
                        return WithdrawAllBounds;
                    else if (HoveredButton.Value == SidebarButton.Autoloot)
                        return AutolootBounds;
                    else if (HoveredButton.Value == SidebarButton.HelpInfo)
                        return HelpInfoBounds;
                    else if (HoveredButton.Value == SidebarButton.CustomizeIcon)
                        return CustomizeIconBounds;
                    else
                        throw new NotImplementedException();
                }
                else
                {
                    return null;
                }
            }
        }
        #endregion Sidebar

        public CustomizeIconMenu CustomizeIconMenu { get; private set; }
        /// <summary>True if this <see cref="ItemBagMenu"/> is displaying a blocking child menu on top of it. Subclasses of <see cref="ItemBagMenu"/> 
        /// will not be able to handle user input when <see cref="IsShowingModalMenu"/> is true. All input will be directed to the Modal dialog.</summary>
        public bool IsShowingModalMenu { get { return CustomizeIconMenu != null; } }

        public void CloseModalMenu()
        {
            if (IsShowingModalMenu && CustomizeIconMenu != null)
            {
                HoveredButton = null;
                CustomizeIconMenu = null;
            }
        }

        /// <param name="InventorySource">Typically this is <see cref="Game1.player.Items"/> if this menu should display the player's inventory.</param>
        /// <param name="ActualCapacity">The maximum # of items that can be stored in the Source list. Use <see cref="Game1.player.MaxItems"/> if moving to/from the inventory.</param>
        /// <param name="InventoryColumns">The number of columns to use when rendering the user's inventory at the bottom-half of the menu. Recommended = 12 to mimic the default inventory of the main GameMenu</param>
        /// <param name="InventorySlotSize">The size, in pixels, to use when rendering each slot of the user's inventory at the bottom-half of the menu. Recommended = <see cref="BagInventoryMenu.DefaultInventoryIconSize"/></param>
        protected ItemBagMenu(ItemBag Bag, IList<Item> InventorySource, int ActualCapacity, int InventoryColumns, int InventorySlotSize = BagInventoryMenu.DefaultInventoryIconSize)
            : base(1, 1, 1, 1, true)
        {
            this.BorderColor = new Color(220, 123, 5, 255);
            this.BackgroundColor = new Color(255, 201, 121);

            this.Bag = Bag;
            this.IsLeftSidebarVisible = Bag is BoundedBag || Bag is Rucksack;
            this.IsRightSidebarVisible = true;

            this.InventorySource = InventorySource;
            this.ActualInventoryCapacity = Math.Max(ActualCapacity, InventorySource.Count);
            this.InventoryMenu = new BagInventoryMenu(Bag, InventorySource, ActualCapacity, InventoryColumns, InventorySlotSize);
            //this.BagInfo = new BagInfo(Bag);

            this.exitFunction += () => { Bag.CloseContents(); };

            InitializeLayout();
        }

        internal void OnWindowSizeChanged()
        {
            InitializeLayout();
            if (IsShowingModalMenu && CustomizeIconMenu != null)
                CustomizeIconMenu.InitializeLayout();
        }

        private const int InventoryMargin = 12;
        protected const int ContentsMargin = 12;

        protected int ButtonLeftTopMargin = 4;
        protected int ButtonBottomMargin = 6;
        protected int ButtonSize { get { return 32; } } //{ get { return Constants.TargetPlatform == GamePlatform.Android ? 48 : 32; } }

        public int ResizeIteration { get; private set; } = 0;

        protected void InitializeLayout()
        {
            ResizeIteration = 0;
            int PreviousWidth = -1;
            int PreviousHeight = -1;

            bool AttemptResize;
            do
            {
                ResizeIteration++;

                int SidebarWidth = 0;
                int SidebarHeight = 0;
                if (IsLeftSidebarVisible || IsRightSidebarVisible)
                {
                    SidebarWidth = ButtonSize + ButtonLeftTopMargin * 2;

                    int LeftButtons = LeftSidebarButtonBounds.Count();
                    int LeftHeight = InventoryMargin + ButtonLeftTopMargin + LeftButtons * ButtonSize + (LeftButtons - 1) * ButtonBottomMargin + ButtonLeftTopMargin + InventoryMargin;
                    int RightButtons = RightSidebarButtonBounds.Count();
                    int RightHeight = InventoryMargin + ButtonLeftTopMargin + RightButtons * ButtonSize + (RightButtons - 1) * ButtonBottomMargin + ButtonLeftTopMargin + InventoryMargin;
                    SidebarHeight = Math.Max(IsLeftSidebarVisible ? LeftHeight : 0, IsRightSidebarVisible ? RightHeight : 0);
                }

                InventoryMenu.InitializeLayout();
                //BagInfo.InitializeLayout();
                InitializeContentsLayout();

                //  Compute size of menu
                int InventoryWidth = InventoryMenu.RelativeBounds.Width + InventoryMargin * 2 + SidebarWidth * 2;
                int ContentsWidth = GetRelativeContentBounds().Width + ContentsMargin * 2 /*+ BagInfo.RelativeBounds.Width*/;
                width = Math.Max(InventoryWidth, ContentsWidth);
                bool IsWidthBoundToContents = ContentsWidth > InventoryWidth;
                height = Math.Max(InventoryMenu.RelativeBounds.Height + InventoryMargin * 2, SidebarHeight) + Math.Max(GetRelativeContentBounds().Height + ContentsMargin * 2, /*BagInfo.RelativeBounds.Height*/0);
                xPositionOnScreen = (Game1.viewport.Size.Width - width) / 2;
                yPositionOnScreen = (Game1.viewport.Size.Height - height) / 2;

                //  Check if menu fits on screen
                bool IsMenuTooWide = width > Game1.viewport.Size.Width;
                bool IsMenuTooTall = height > Game1.viewport.Size.Height;
                bool FitsOnScreen = !IsMenuTooWide && !IsMenuTooTall;
                bool DidSizeChange = width != PreviousWidth || height != PreviousHeight;
                PreviousWidth = width;
                PreviousHeight = height;

                AttemptResize = !FitsOnScreen && ResizeIteration < 5 && CanResize() && DidSizeChange && (IsWidthBoundToContents || IsMenuTooTall);
            } while (AttemptResize);

            //  Set position of inventory and contents
            InventoryMenu.SetTopLeft(new Point(
                xPositionOnScreen + (width - InventoryMenu.RelativeBounds.Width) / 2,
                yPositionOnScreen + GetRelativeContentBounds().Height + ContentsMargin * 2 + InventoryMargin)
            );
            //BagInfo.SetTopLeft(new Point(
            //    xPositionOnScreen,
            //    yPositionOnScreen)
            //);
            SetTopLeft(new Point(
                xPositionOnScreen + (width - GetRelativeContentBounds().Width) / 2,
                //BagInfo.Bounds.Right - ContentsMargin + (width - BagInfo.Bounds.Width - ContentsMargin - GetRelativeContentBounds().Width) / 2,
                yPositionOnScreen + ContentsMargin));

            //  Set bounds of sidebar buttons
            if (IsLeftSidebarVisible)
            {
                DepositAllBounds = new Rectangle(xPositionOnScreen + ContentsMargin + ButtonLeftTopMargin, InventoryMenu.Bounds.Top + ButtonLeftTopMargin, ButtonSize, ButtonSize);
                WithdrawAllBounds = new Rectangle(xPositionOnScreen + ContentsMargin + ButtonLeftTopMargin, InventoryMenu.Bounds.Top + ButtonLeftTopMargin + ButtonSize + ButtonBottomMargin, ButtonSize, ButtonSize);
                AutolootBounds = new Rectangle(xPositionOnScreen + ContentsMargin + ButtonLeftTopMargin, InventoryMenu.Bounds.Top + ButtonLeftTopMargin + ButtonSize * 2 + ButtonBottomMargin * 2, ButtonSize, ButtonSize);
            }
            if (IsRightSidebarVisible)
            {
                HelpInfoBounds = new Rectangle(xPositionOnScreen + width - ContentsMargin - ButtonLeftTopMargin - ButtonSize, InventoryMenu.Bounds.Top + ButtonLeftTopMargin, ButtonSize, ButtonSize);
                CustomizeIconBounds = new Rectangle(xPositionOnScreen + width - ContentsMargin - ButtonLeftTopMargin - ButtonSize, InventoryMenu.Bounds.Top + ButtonLeftTopMargin + ButtonSize + ButtonBottomMargin, ButtonSize, ButtonSize);
            }

            //  Set bounds of close button
            Point CloseButtonOffset = Constants.TargetPlatform == GamePlatform.Android ? new Point(24, -24) : new Point(16, -16);
            upperRightCloseButton.bounds.X = xPositionOnScreen + width - upperRightCloseButton.bounds.Width + CloseButtonOffset.X;
            upperRightCloseButton.bounds.Y = yPositionOnScreen + CloseButtonOffset.Y;
        }

        protected abstract void InitializeContentsLayout();
        protected abstract void DrawContents(SpriteBatch b);
        protected abstract void DrawContentsToolTips(SpriteBatch b);
        public abstract Rectangle GetRelativeContentBounds();
        public abstract Rectangle GetContentBounds();
        protected abstract bool CanResize();
        public abstract void SetTopLeft(Point Point);
        public abstract void OnClose();

        public void OnMouseMoved(CursorMovedEventArgs e)
        {
            if (IsShowingModalMenu && CustomizeIconMenu != null)
            {
                CustomizeIconMenu.OnMouseMoved(e);
            }
            else
            {
                OverridableOnMouseMoved(e);
            }
        }

        protected virtual void OverridableOnMouseMoved(CursorMovedEventArgs e)
        {
            if (InventoryMenu.Bounds.Contains(e.OldPosition.ScreenPixels.AsPoint()) || InventoryMenu.Bounds.Contains(e.NewPosition.ScreenPixels.AsPoint()))
            {
                InventoryMenu.OnMouseMoved(e);
            }

            if (IsLeftSidebarVisible || IsRightSidebarVisible)
            {
                Point OldPos = e.OldPosition.ScreenPixels.AsPoint();
                Point NewPos = e.NewPosition.ScreenPixels.AsPoint();

                if (LeftSidebarButtonBounds.Any(x => x.Contains(OldPos) || x.Contains(NewPos)) ||
                    RightSidebarButtonBounds.Any(x => x.Contains(OldPos) || x.Contains(NewPos)))
                {
                    if (IsLeftSidebarVisible && DepositAllBounds.Contains(NewPos))
                        this.HoveredButton = SidebarButton.DepositAll;
                    else if (IsLeftSidebarVisible && WithdrawAllBounds.Contains(NewPos))
                        this.HoveredButton = SidebarButton.WithdrawAll;
                    else if (IsLeftSidebarVisible && AutolootBounds.Contains(NewPos))
                        this.HoveredButton = SidebarButton.Autoloot;
                    else if (IsRightSidebarVisible && HelpInfoBounds.Contains(NewPos))
                        this.HoveredButton = SidebarButton.HelpInfo;
                    else if (IsRightSidebarVisible && !(Bag is BundleBag) && CustomizeIconBounds.Contains(NewPos))
                        this.HoveredButton = SidebarButton.CustomizeIcon;
                    else
                        this.HoveredButton = null;
                }
            }
        }

        public void OnMouseButtonPressed(ButtonPressedEventArgs e)
        {
            if (IsShowingModalMenu && CustomizeIconMenu != null)
            {
                CustomizeIconMenu.OnMouseButtonPressed(e);
            }
            else
            {
                OverridableOnMouseButtonPressed(e);
            }
        }

        protected virtual void OverridableOnMouseButtonPressed(ButtonPressedEventArgs e)
        {
            InventoryMenu.OnMouseButtonPressed(e);

            if ((IsLeftSidebarVisible || IsRightSidebarVisible) && HoveredButton.HasValue && e.Button == SButton.MouseLeft)
            {
                if (IsLeftSidebarVisible && HoveredButton.Value == SidebarButton.DepositAll)
                {
                    List<Object> ToDeposit = InventorySource.Where(x => x != null && x is Object Obj && Bag.IsValidBagObject(Obj)).Cast<Object>().ToList();
                    Bag.MoveToBag(ToDeposit, ToDeposit.Select(x => x.Stack).ToList(), out int TotalMovedQty, true, InventorySource);
                }
                else if (IsLeftSidebarVisible && HoveredButton.Value == SidebarButton.WithdrawAll)
                {
                    List<Object> ToWithdraw = Bag.Contents.Where(x => x != null).ToList();
                    Bag.MoveFromBag(ToWithdraw, ToWithdraw.Select(x => x.Stack).ToList(), out int TotalMovedQty, true, InventorySource, ActualInventoryCapacity);
                }
                else if (IsLeftSidebarVisible && HoveredButton.Value == SidebarButton.Autoloot)
                {
                    if (Bag is BoundedBag BB)
                        BB.Autofill = !BB.Autofill;
                    else if (Bag is Rucksack RS)
                        RS.CycleAutofill();
                }
                else if (IsRightSidebarVisible && HoveredButton.Value == SidebarButton.HelpInfo)
                {

                }
                else if (IsRightSidebarVisible && HoveredButton.Value == SidebarButton.CustomizeIcon && Bag.CanCustomizeIcon())
                {
                    ItemBag Copy;
                    if (Bag is BoundedBag BB)
                    {
                        if (BB is BundleBag)
                            Copy = new BundleBag(Bag.Size, false);
                        else
                            Copy = new BoundedBag(BB.TypeInfo, Bag.Size, false);
                    }
                    else if (Bag is Rucksack RS)
                    {
                        Copy = new Rucksack(Bag.Size, false);
                    }
                    else if (Bag is OmniBag OB)
                    {
                        Copy = new OmniBag(Bag.Size);
                    }
                    else
                        throw new NotImplementedException(string.Format("Unexpected Bag Type while creating CustomizeIconMenu: {0}", Bag.GetType().ToString()));

                    CustomizeIconMenu = new CustomizeIconMenu(this, this.Bag, Copy);
                }
            }
        }

        public void OnMouseButtonReleased(ButtonReleasedEventArgs e)
        {
            if (IsShowingModalMenu && CustomizeIconMenu != null)
            {
                CustomizeIconMenu.OnMouseButtonReleased(e);
            }
            else
            {
                OverridableOnMouseButtonReleased(e);
            }
        }

        protected virtual void OverridableOnMouseButtonReleased(ButtonReleasedEventArgs e)
        {
            InventoryMenu.OnMouseButtonReleased(e);
        }

        public void Update(UpdateTickedEventArgs e)
        {
            if (IsShowingModalMenu && CustomizeIconMenu != null)
            {
                CustomizeIconMenu.Update(e);
            }
            else
            {
                OverridableUpdate(e);
            }
        }

        protected virtual void OverridableUpdate(UpdateTickedEventArgs e)
        {
            InventoryMenu.Update(e);
        }

        public sealed override void draw(SpriteBatch b)
        {
            try
            {
                DrawBox(b, xPositionOnScreen, yPositionOnScreen, width, height);
                int SeparatorHeight = 24;
                DrawHorizontalSeparator(b, xPositionOnScreen, InventoryMenu.TopLeftScreenPosition.Y - InventoryMargin - SeparatorHeight / 2, width, SeparatorHeight);
                InventoryMenu.Draw(b);

                if (IsLeftSidebarVisible || IsRightSidebarVisible)
                {
                    if (IsLeftSidebarVisible)
                    {
                        //  Draw the deposit/withdraw-all buttons
                        Rectangle ArrowUpIconSourceRect = new Rectangle(421, 459, 12, 12);
                        Rectangle ArrowDownIconSourceRect = new Rectangle(421, 472, 12, 12);
                        int ArrowSize = (int)(ArrowUpIconSourceRect.Width * 1.5 / 32.0 * DepositAllBounds.Width);
                        b.Draw(Game1.menuTexture, DepositAllBounds, new Rectangle(128, 128, 64, 64), Color.White);
                        b.Draw(Game1.mouseCursors, new Rectangle(DepositAllBounds.X + (DepositAllBounds.Width - ArrowSize) / 2, DepositAllBounds.Y + (DepositAllBounds.Height - ArrowSize) / 2, ArrowSize, ArrowSize), ArrowUpIconSourceRect, Color.White);
                        b.Draw(Game1.menuTexture, WithdrawAllBounds, new Rectangle(128, 128, 64, 64), Color.White);
                        b.Draw(Game1.mouseCursors, new Rectangle(WithdrawAllBounds.X + (WithdrawAllBounds.Width - ArrowSize) / 2, WithdrawAllBounds.Y + (WithdrawAllBounds.Height - ArrowSize) / 2, ArrowSize, ArrowSize), ArrowDownIconSourceRect, Color.White);

                        //  Draw the autofill togglebutton
                        Rectangle HandIconSourceRect = new Rectangle(32, 0, 10, 10);
                        int HandIconSize = (int)(HandIconSourceRect.Width * 2.0 / 32.0 * AutolootBounds.Width);
                        b.Draw(Game1.menuTexture, AutolootBounds, new Rectangle(128, 128, 64, 64), Color.White);
                        b.Draw(Game1.mouseCursors, new Rectangle(AutolootBounds.X + (AutolootBounds.Width - HandIconSize) / 2, AutolootBounds.Y + (AutolootBounds.Height - HandIconSize) / 2, HandIconSize, HandIconSize), HandIconSourceRect, Color.White);

                        if (Bag is BoundedBag BB)
                        {
                            if (!BB.Autofill)
                            {
                                Rectangle DisabledIconSourceRect = new Rectangle(322, 498, 12, 12);
                                int DisabledIconSize = (int)(DisabledIconSourceRect.Width * 1.5 / 32.0 * AutolootBounds.Width);
                                Rectangle Destination = new Rectangle(AutolootBounds.Right - DisabledIconSize - 2, AutolootBounds.Bottom - DisabledIconSize - 2, DisabledIconSize, DisabledIconSize);
                                b.Draw(Game1.mouseCursors, Destination, DisabledIconSourceRect, Color.White);
                            }
                        }
                        else if (Bag is Rucksack RS)
                        {
                            if (!RS.Autofill)
                            {
                                Rectangle DisabledIconSourceRect = new Rectangle(322, 498, 12, 12);
                                int DisabledIconSize = (int)(DisabledIconSourceRect.Width * 1.5 / 32.0 * AutolootBounds.Width);
                                Rectangle Destination = new Rectangle(AutolootBounds.Right - DisabledIconSize - 2, AutolootBounds.Bottom - DisabledIconSize - 2, DisabledIconSize, DisabledIconSize);
                                b.Draw(Game1.mouseCursors, Destination, DisabledIconSourceRect, Color.White);
                            }
                            else
                            {
                                if (RS.AutofillPriority == AutofillPriority.Low)
                                {
                                    Rectangle LowPriorityIconSourceRect = new Rectangle(421, 472, 12, 12);
                                    int LowPriorityIconSize = (int)(LowPriorityIconSourceRect.Width * 1.0 / 32.0 * AutolootBounds.Width);
                                    Rectangle Destination = new Rectangle(AutolootBounds.Right - LowPriorityIconSize - 2, AutolootBounds.Bottom - LowPriorityIconSize - 2, LowPriorityIconSize, LowPriorityIconSize);
                                    b.Draw(Game1.mouseCursors, Destination, LowPriorityIconSourceRect, Color.White);
                                }
                                else if (RS.AutofillPriority == AutofillPriority.High)
                                {
                                    Rectangle HighPriorityIconSourceRect = new Rectangle(421, 459, 12, 12);
                                    int HighPriorityIconSize = (int)(HighPriorityIconSourceRect.Width * 1.0 / 32.0 * AutolootBounds.Width);
                                    Rectangle Destination = new Rectangle(AutolootBounds.Right - HighPriorityIconSize - 2, AutolootBounds.Bottom - HighPriorityIconSize - 2, HighPriorityIconSize, HighPriorityIconSize);
                                    b.Draw(Game1.mouseCursors, Destination, HighPriorityIconSourceRect, Color.White);
                                }
                            }
                        }
                    }

                    if (IsRightSidebarVisible)
                    {
                        //  Draw the help button
                        Rectangle HelpIconSourceRect = new Rectangle(176, 425, 9, 12);
                        int HelpIconWidth = (int)(HelpIconSourceRect.Width * 1.5 / 32.0 * HelpInfoBounds.Width);
                        int HelpIconHeight = (int)(HelpIconSourceRect.Height * 1.5 / 32.0 * HelpInfoBounds.Height);
                        b.Draw(Game1.menuTexture, HelpInfoBounds, new Rectangle(128, 128, 64, 64), Color.White);
                        b.Draw(Game1.mouseCursors, new Rectangle(HelpInfoBounds.X + (HelpInfoBounds.Width - HelpIconWidth) / 2, HelpInfoBounds.Y + (HelpInfoBounds.Height - HelpIconHeight) / 2, HelpIconWidth, HelpIconHeight), HelpIconSourceRect, Color.White);

                        if (Bag.CanCustomizeIcon())
                        {
                            //  Draw the customize icon button
                            Rectangle CustomizeSourceRect = new Rectangle(121, 471, 12, 12);
                            int CustomizeIconWidth = CustomizeIconBounds.Width;
                            int CustomizeIconHeight = CustomizeIconBounds.Height;
                            b.Draw(Game1.mouseCursors, new Rectangle(CustomizeIconBounds.X + (CustomizeIconBounds.Width - CustomizeIconWidth) / 2, CustomizeIconBounds.Y + (CustomizeIconBounds.Height - CustomizeIconHeight) / 2,
                                CustomizeIconWidth, CustomizeIconHeight), CustomizeSourceRect, Color.White);
                            b.Draw(Game1.menuTexture, CustomizeIconBounds, new Rectangle(128, 128, 64, 64), Color.White);
                        }
                    }

                    //  Draw a yellow border around the hovered sidebar button
                    if (HoveredButton.HasValue)
                    {
                        Rectangle HoveredBounds = HoveredButtonBounds.Value;
                        Color HighlightColor = Color.Yellow;
                        Texture2D Highlight = TextureHelpers.GetSolidColorTexture(Game1.graphics.GraphicsDevice, HighlightColor);
                        b.Draw(Highlight, HoveredBounds, Color.White * 0.25f);
                        int BorderThickness = HoveredBounds.Width / 16;
                        DrawBorder(b, HoveredBounds, BorderThickness, HighlightColor);
                    }
                }

                //BagInfo.Draw(b);
                DrawContents(b);

                if (IsShowingModalMenu && CustomizeIconMenu != null)
                {
                    CustomizeIconMenu.Draw(b);
                }
                else
                {
                    InventoryMenu.DrawToolTips(b);
                    DrawContentsToolTips(b);

                    //  Draw tooltips on the sidebar buttons
                    if ((IsLeftSidebarVisible || IsRightSidebarVisible) && HoveredButton.HasValue)
                    {
                        if (IsLeftSidebarVisible)
                        {
                            string ButtonToolTip = "";
                            if (HoveredButton.Value == SidebarButton.DepositAll)
                                ButtonToolTip = ItemBagsMod.Translate("DepositAllToolTip");
                            else if (HoveredButton.Value == SidebarButton.WithdrawAll)
                                ButtonToolTip = ItemBagsMod.Translate("WithdrawAllToolTip");
                            else if (HoveredButton.Value == SidebarButton.Autoloot)
                            {
                                if (Bag is BoundedBag BB)
                                    ButtonToolTip = ItemBagsMod.Translate(BB.Autofill ? "AutofillOnToolTip" : "AutofillOffToolTip");
                                else if (Bag is Rucksack RS)
                                {
                                    string TranslationKey;
                                    if (RS.Autofill)
                                    {
                                        if (RS.AutofillPriority == AutofillPriority.Low)
                                            TranslationKey = "RucksackAutofillLowPriorityToolTip";
                                        else if (RS.AutofillPriority == AutofillPriority.High)
                                            TranslationKey = "RucksackAutofillHighPriorityToolTip";
                                        else
                                            throw new NotImplementedException(string.Format("Unrecognized Rucksack AutofillPriority: {0}", RS.AutofillPriority.ToString()));
                                    }
                                    else
                                        TranslationKey = "RucksackAutofillOffToolTip";
                                    ButtonToolTip = ItemBagsMod.Translate(TranslationKey);
                                }
                            }

                            if (!string.IsNullOrEmpty(ButtonToolTip))
                            {
                                int Margin = 16;
                                Vector2 ToolTipSize = Game1.smallFont.MeasureString(ButtonToolTip);
                                DrawBox(b, HoveredButtonBounds.Value.Right, HoveredButtonBounds.Value.Top, (int)(ToolTipSize.X + Margin * 2), (int)(ToolTipSize.Y + Margin * 2));
                                b.DrawString(Game1.smallFont, ButtonToolTip, new Vector2(HoveredButtonBounds.Value.Right + Margin, HoveredButtonBounds.Value.Top + Margin), Color.Black);
                            }
                        }

                        if (IsRightSidebarVisible)
                        {
                            string ButtonToolTip = "";
                            if (HoveredButton.Value == SidebarButton.HelpInfo)
                                ButtonToolTip = ItemBagsMod.Translate("HelpInfoToolTip");
                            else if (HoveredButton.Value == SidebarButton.CustomizeIcon)
                                ButtonToolTip = ItemBagsMod.Translate("CustomizeIconToolTip");

                            if (!string.IsNullOrEmpty(ButtonToolTip))
                            {
                                int Margin = 16;
                                Vector2 ToolTipSize = Game1.smallFont.MeasureString(ButtonToolTip);
                                DrawBox(b, HoveredButtonBounds.Value.Left - (int)(ToolTipSize.X + Margin * 2), HoveredButtonBounds.Value.Top, (int)(ToolTipSize.X + Margin * 2), (int)(ToolTipSize.Y + Margin * 2));
                                b.DrawString(Game1.smallFont, ButtonToolTip, new Vector2(HoveredButtonBounds.Value.Left - Margin - ToolTipSize.X, HoveredButtonBounds.Value.Top + Margin), Color.Black);
                            }
                        }
                    }
                }

                upperRightCloseButton.draw(b);

                if (!Game1.options.hardwareCursor)
                {
                    drawMouse(b);
                }
            }
            catch (Exception ex)
            {
                ItemBagsMod.ModInstance.Monitor.Log(string.Format("Unhandled error in ItemBagMenu.Draw: {0}", ex.Message), LogLevel.Error);
            }
        }
    }
}
