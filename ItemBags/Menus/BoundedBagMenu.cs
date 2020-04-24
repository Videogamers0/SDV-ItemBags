using ItemBags.Bags;
using ItemBags.Helpers;
using ItemBags.Persistence;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ItemBags.Bags.BoundedBag;
using Object = StardewValley.Object;

namespace ItemBags.Menus
{
    public class ItemSlotRenderedEventArgs : EventArgs
    {
        public SpriteBatch SB { get; }
        public Rectangle Slot { get; }
        public Object Item { get; }
        public bool IsHovered { get; }

        public ItemSlotRenderedEventArgs(SpriteBatch SB, Rectangle Slot, Object Item, bool IsHovered)
        {
            this.SB = SB;
            this.Slot = Slot;
            this.Item = Item;
            this.IsHovered = IsHovered;
        }
    }

    public class BoundedBagMenu : ItemBagMenu
    {
        public BoundedBag BoundedBag { get; }

        /// <summary>If true, each distinct Item Id that can have multiple qualities will be displayed adjacent to each other in a group of 4 columns, where the columns in are the different qualities (Regular, Silver, Gold, Iridium)</summary>
        public bool GroupByQuality { get; }
        /// <summary>Only relevant if <see cref="GroupByQuality"/> = true. Layout options to use for Items that are grouped by quality.</summary>
        public GroupedLayoutOptions GroupedOptions { get; }

        /// <summary>Layout options to use for Items that are not grouped by quality. See also <see cref="GroupByQuality"/></summary>
        public UngroupedLayoutOptions UngroupedOptions { get; }

        /// <summary>The bounds of this menu's content, relative to <see cref="TopLeftScreenPosition"/></summary>
        public Rectangle RelativeContentBounds { get; private set; }
        public override Rectangle GetRelativeContentBounds() { return this.RelativeContentBounds; }
        public Rectangle ContentBounds { get; private set; }
        public override Rectangle GetContentBounds() { return this.ContentBounds; }

        private Point _TopLeftScreenPosition;
        public Point TopLeftScreenPosition {
            get { return _TopLeftScreenPosition; }
            private set { SetTopLeft(value, true); }
        }

        public override void SetTopLeft(Point Point) { SetTopLeft(Point, true); }
        public void SetTopLeft(Point NewValue, bool CheckIfChanged = true)
        {
            if (!CheckIfChanged || TopLeftScreenPosition != NewValue)
            {
                Point Previous = TopLeftScreenPosition;
                _TopLeftScreenPosition = NewValue;

                if (GroupedOptions != null && UngroupedOptions != null)
                {
                    Point Offset = new Point(TopLeftScreenPosition.X - Previous.X, TopLeftScreenPosition.Y - Previous.Y);

                    Point GroupedTopLeft = new Point(GroupedOptions.TopLeftScreenPosition.X + Offset.X, GroupedOptions.TopLeftScreenPosition.Y + Offset.Y);
                    this.GroupedOptions.SetTopLeft(GroupedTopLeft, CheckIfChanged);
                    Point UngroupedTopLeft = new Point(UngroupedOptions.TopLeftScreenPosition.X + Offset.X, UngroupedOptions.TopLeftScreenPosition.Y + Offset.Y);
                    this.UngroupedOptions.SetTopLeft(UngroupedTopLeft, CheckIfChanged);

                    if (HorizontalSeparatorPosition.HasValue)
                        HorizontalSeparatorPosition = HorizontalSeparatorPosition.Value.GetOffseted(Offset);

                    this.ContentBounds = new Rectangle(TopLeftScreenPosition.X, TopLeftScreenPosition.Y, RelativeContentBounds.Width, RelativeContentBounds.Height);
                }
            }
        }

        /// <param name="InventorySource">Typically this is <see cref="Game1.player.Items"/> if this menu should display the player's inventory.</param>
        /// <param name="ActualCapacity">The maximum # of items that can be stored in the InventorySource list. Use <see cref="Game1.player.MaxItems"/> if moving to/from the inventory.</param>
        /// <param name="InventoryColumns">The number of columns to use when rendering the user's inventory at the bottom-half of the menu. Recommended = 12 to mimic the default inventory of the main GameMenu</param>
        /// <param name="InventorySlotSize">The size, in pixels, to use when rendering each slot of the user's inventory at the bottom-half of the menu. Recommended = <see cref="BagInventoryMenu.DefaultInventoryIconSize"/></param>
        public BoundedBagMenu(BoundedBag Bag, IList<Item> InventorySource, int ActualCapacity, int InventoryColumns, int InventorySlotSize, bool GroupContentsByQuality, GroupedLayoutOptions GroupedLayout, UngroupedLayoutOptions UngroupedLayout)
            : base(Bag, InventorySource, ActualCapacity, InventoryColumns, InventorySlotSize)
        {
            this.BoundedBag = Bag;

            this.GroupByQuality = GroupContentsByQuality;
            this.GroupedOptions = GroupedLayout;
            this.GroupedOptions.SetParent(this);
            this.GroupedOptions.OnItemSlotRendered += OnItemSlotRendered;
            this.UngroupedOptions = UngroupedLayout;
            this.UngroupedOptions.SetParent(this);
            this.UngroupedOptions.OnItemSlotRendered += OnItemSlotRendered;

            SetTopLeft(Point.Zero, false);
            InitializeLayout();
        }

        /// <param name="InventorySource">Typically this is <see cref="Game1.player.Items"/> if this menu should display the player's inventory.</param>
        /// <param name="ActualCapacity">The maximum # of items that can be stored in the InventorySource list. Use <see cref="Game1.player.MaxItems"/> if moving to/from the inventory.</param>
        public BoundedBagMenu(BoundedBag Bag, IList<Item> InventorySource, int ActualCapacity, BagMenuOptions Opts)
            : this(Bag, InventorySource, ActualCapacity, Opts.InventoryColumns, Opts.InventorySlotSize, Opts.GroupByQuality, new GroupedLayoutOptions(Opts.GroupedLayoutOptions), new UngroupedLayoutOptions(Opts.UngroupedLayoutOptions)) { }

        #region Input Handling
        private Point CurrentMousePosition = new Point(0, 0);

        protected override void OverridableOnMouseMoved(CursorMovedEventArgs e)
        {
            base.OverridableOnMouseMoved(e);

            CurrentMousePosition = e.NewPosition.ScreenPixels.AsPoint();

            if (ContentBounds.Contains(e.OldPosition.ScreenPixels.AsPoint()) || ContentBounds.Contains(e.NewPosition.ScreenPixels.AsPoint()))
            {
                if (!GroupedOptions.IsEmptyMenu && (GroupedOptions.Bounds.Contains(e.OldPosition.ScreenPixels.AsPoint()) || GroupedOptions.Bounds.Contains(e.NewPosition.ScreenPixels.AsPoint())))
                {
                    GroupedOptions.OnMouseMoved(e);
                }

                if (!UngroupedOptions.IsEmptyMenu && (UngroupedOptions.Bounds.Contains(e.OldPosition.ScreenPixels.AsPoint()) || UngroupedOptions.Bounds.Contains(e.NewPosition.ScreenPixels.AsPoint())))
                {
                    UngroupedOptions.OnMouseMoved(e);
                }
            }
        }

        protected override void OverridableOnMouseButtonPressed(ButtonPressedEventArgs e)
        {
            base.OverridableOnMouseButtonPressed(e);

            bool Handled = false;
            if (e.Button == StardewModdingAPI.SButton.MouseLeft && BoundedBag.Autofill)
            {
                if (!Handled && !GroupedOptions.IsEmptyMenu && GroupedOptions.HoveredSlot.HasValue)
                {
                    if (GetAutofillToggleClickableRegion(GroupedOptions.HoveredSlot.Value).Contains(CurrentMousePosition))
                    {
                        BoundedBag.ToggleItemAutofill(GroupedOptions.GetHoveredItem());
                        Handled = true;
                    }
                }

                if (!Handled && !UngroupedOptions.IsEmptyMenu && UngroupedOptions.HoveredSlot.HasValue)
                {
                    if (GetAutofillToggleClickableRegion(UngroupedOptions.HoveredSlot.Value).Contains(CurrentMousePosition))
                    {
                        BoundedBag.ToggleItemAutofill(UngroupedOptions.GetHoveredItem());
                        Handled = true;
                    }
                }
            }

            if (!Handled)
            {
                if (!GroupedOptions.IsEmptyMenu)
                    GroupedOptions.OnMouseButtonPressed(e);
                if (!UngroupedOptions.IsEmptyMenu)
                    UngroupedOptions.OnMouseButtonPressed(e);
            }
        }

        protected override void OverridableOnMouseButtonReleased(ButtonReleasedEventArgs e)
        {
            base.OverridableOnMouseButtonReleased(e);
            if (!GroupedOptions.IsEmptyMenu)
                GroupedOptions.OnMouseButtonReleased(e);
            if (!UngroupedOptions.IsEmptyMenu)
                UngroupedOptions.OnMouseButtonReleased(e);
        }
        #endregion Input Handling

        protected override void OverridableUpdate(UpdateTickedEventArgs e)
        {
            base.OverridableUpdate(e);
            if (!GroupedOptions.IsEmptyMenu)
                GroupedOptions.Update(e);
            if (!UngroupedOptions.IsEmptyMenu)
                UngroupedOptions.Update(e);
        }

        public override void OnClose()
        {
            GroupedOptions.OnItemSlotRendered -= OnItemSlotRendered;
            GroupedOptions?.SetParent(null);
            UngroupedOptions.OnItemSlotRendered -= OnItemSlotRendered;
            UngroupedOptions?.SetParent(null);
        }

        public Rectangle? HorizontalSeparatorPosition { get; private set; }

        protected override bool CanResize() { return true; }

        protected override void InitializeContentsLayout()
        {
            if (GroupedOptions == null || UngroupedOptions == null)
                return;

            GroupedOptions.InitializeLayout(ResizeIteration);
            UngroupedOptions.InitializeLayout(ResizeIteration);

            int RequiredWidth;
            int RequiredHeight;
            if (UngroupedOptions.IsEmptyMenu)
            {
                RequiredWidth = GroupedOptions.RelativeBounds.Width + ContentsMargin * 2;
                RequiredHeight = GroupedOptions.RelativeBounds.Height + ContentsMargin * 2;

                Point GroupedOptionsPos = new Point(TopLeftScreenPosition.X + ContentsMargin, TopLeftScreenPosition.Y + ContentsMargin);
                GroupedOptions.SetTopLeft(GroupedOptionsPos);
                Point UngroupedOptionsPos = new Point(0, 0);
                UngroupedOptions.SetTopLeft(UngroupedOptionsPos);
                HorizontalSeparatorPosition = null;
            }
            else if (GroupedOptions.IsEmptyMenu)
            {
                RequiredWidth = UngroupedOptions.RelativeBounds.Width + ContentsMargin * 2;
                RequiredHeight = UngroupedOptions.RelativeBounds.Height + ContentsMargin * 2;

                Point GroupedOptionsPos = new Point(0, 0);
                GroupedOptions.SetTopLeft(GroupedOptionsPos);
                Point UngroupedOptionsPos = new Point(TopLeftScreenPosition.X + ContentsMargin, TopLeftScreenPosition.Y + ContentsMargin);
                UngroupedOptions.SetTopLeft(UngroupedOptionsPos);
                HorizontalSeparatorPosition = null;
            }
            else
            {
                int SeparatorHeight = 12;
                RequiredWidth = Math.Max(GroupedOptions.RelativeBounds.Width, UngroupedOptions.RelativeBounds.Width) + ContentsMargin * 2;
                RequiredHeight = ContentsMargin + GroupedOptions.RelativeBounds.Height + SeparatorHeight + UngroupedOptions.RelativeBounds.Height + ContentsMargin;

                Point GroupedOptionsPos = new Point(TopLeftScreenPosition.X + (RequiredWidth - GroupedOptions.RelativeBounds.Width) / 2, TopLeftScreenPosition.Y + ContentsMargin);
                GroupedOptions.SetTopLeft(GroupedOptionsPos);
                Point UngroupedOptionsPos = new Point(TopLeftScreenPosition.X + (RequiredWidth - UngroupedOptions.RelativeBounds.Width) / 2, TopLeftScreenPosition.Y + ContentsMargin + GroupedOptions.RelativeBounds.Height + SeparatorHeight);
                UngroupedOptions.SetTopLeft(UngroupedOptionsPos);

                //  Add a horizontal separator
                int SeparatorXPosition = TopLeftScreenPosition.X + ContentsMargin;
                int SeparatorYPosition = TopLeftScreenPosition.Y + ContentsMargin + GroupedOptions.RelativeBounds.Height;
                int SeparatorWidth = Math.Max(GroupedOptions.RelativeBounds.Width, UngroupedOptions.RelativeBounds.Width);
                HorizontalSeparatorPosition = new Rectangle(SeparatorXPosition, SeparatorYPosition, SeparatorWidth, SeparatorHeight);
            }

            this.RelativeContentBounds = new Rectangle(0, 0, RequiredWidth, RequiredHeight);
        }

        protected override void DrawContents(SpriteBatch b)
        {
            //b.Draw(TextureHelpers.GetSolidColorTexture(Game1.graphics.GraphicsDevice, Color.Red), ContentsBounds, Color.White);

            if (HorizontalSeparatorPosition.HasValue)
                DrawHelpers.DrawHorizontalSeparator(b, HorizontalSeparatorPosition.Value);
            GroupedOptions.Draw(b);
            UngroupedOptions.Draw(b);
        }

        protected override void DrawContentsToolTips(SpriteBatch b)
        {
            GroupedOptions.DrawToolTips(b);
            UngroupedOptions.DrawToolTips(b);
        }

        private void OnItemSlotRendered(object sender, ItemSlotRenderedEventArgs e)
        {
            //  Draw a toggle to enable/disable autofilling this item
            if (BoundedBag.Autofill && (e.IsHovered || IsHoveringAutofillButton))
            {
                Rectangle AutofillDestination;
                float Transparency;
                if (IsHoveringAutofillButton)
                {
                    double PercentSize = 0.75;
                    int Width = (int)(e.Slot.Width * PercentSize);
                    int Height = (int)(e.Slot.Height * PercentSize);

                    AutofillDestination = new Rectangle(e.Slot.Center.X - Width / 2, e.Slot.Center.Y - Height / 2, Width, Height);
                    Transparency = 1.0f;
                }
                else
                {
                    AutofillDestination = GetAutofillToggleDrawPosition(e.Slot);
                    Transparency = GetAutofillToggleClickableRegion(e.Slot).Contains(CurrentMousePosition) ? 1.0f : 0.75f;
                }

                Rectangle HandIconSourceRect = new Rectangle(32, 0, 10, 10);
                int HandIconSize = (int)(HandIconSourceRect.Width * 2.0 / 32.0 * AutofillDestination.Width);
                //b.Draw(Game1.menuTexture, AutofillDestination, new Rectangle(128, 128, 64, 64), Color.White);
                e.SB.Draw(Game1.mouseCursors, new Rectangle(AutofillDestination.X + (AutofillDestination.Width - HandIconSize) / 2, AutofillDestination.Y + (AutofillDestination.Height - HandIconSize) / 2, HandIconSize, HandIconSize), HandIconSourceRect, Color.White * Transparency);

                if (!BoundedBag.CanAutofillWithItem(e.Item))
                {
                    Rectangle DisabledIconSourceRect = new Rectangle(322, 498, 12, 12);
                    int DisabledIconSize = (int)(DisabledIconSourceRect.Width * 1.5 / 32.0 * AutofillDestination.Width);
                    Rectangle DisabledIconDestination = new Rectangle(AutofillDestination.Right - DisabledIconSize - 2, AutofillDestination.Bottom - DisabledIconSize - 2, DisabledIconSize, DisabledIconSize);
                    e.SB.Draw(Game1.mouseCursors, DisabledIconDestination, DisabledIconSourceRect, Color.White * Transparency);
                }
            }
        }

        private Rectangle GetAutofillToggleDrawPosition(Rectangle ItemSlot)
        {
            return new Rectangle(ItemSlot.X, ItemSlot.Y, ItemSlot.Width / 2, ItemSlot.Height / 2);
        }

        private Rectangle GetAutofillToggleClickableRegion(Rectangle ItemSlot)
        {
            Rectangle DrawPosition = GetAutofillToggleDrawPosition(ItemSlot);
            double PaddingPercent = 0.2;
            int ClickableWidth = (int)(DrawPosition.Width * (1.0 - PaddingPercent * 2));
            int ClickableHeight = (int)(DrawPosition.Height * (1.0 - PaddingPercent * 2));
            return new Rectangle(DrawPosition.Center.X - ClickableWidth / 2, DrawPosition.Center.Y - ClickableHeight / 2, ClickableWidth, ClickableHeight);
        }
    }
}
