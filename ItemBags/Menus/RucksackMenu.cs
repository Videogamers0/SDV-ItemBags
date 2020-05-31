using ItemBags.Bags;
using ItemBags.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Object = StardewValley.Object;

namespace ItemBags.Menus
{
    public class RucksackMenu : IBagMenuContent
    {
        #region Lookup Anything Compatibility
        /// <summary>
        /// Warning - do not remove/rename this field. It is used via reflection by Lookup Anything mod.<para/>
        /// See also: <see cref="https://github.com/Pathoschild/StardewMods/tree/develop/LookupAnything#extensibility-for-modders"/>
        /// </summary>
        public Item HoveredItem { get; private set; }
        public void UpdateHoveredItem(CursorMovedEventArgs e)
        {
            if (Bounds.Contains(e.NewPosition.ScreenPixels.AsPoint()))
            {
                HoveredItem = GetHoveredItem();
            }
            else
            {
                HoveredItem = null;
            }
        }
        #endregion Lookup Anything Compatibility

        public ItemBagMenu IBM { get; }
        public ItemBag Bag { get { return Rucksack; } }
        public Rucksack Rucksack { get; }
        private void Bag_ContentsChanged(object sender, EventArgs e) { InitializePlaceholders(); }

        public int Padding { get; }

        private int OriginalSlotSize { get; }
        /// <summary>The size, in pixels, to use when rendering an item slot. Recommended = <see cref="BagInventoryMenu.DefaultInventoryIconSize"/></summary>
        public int SlotSize { get; private set; }
        /// <summary>The number of columns to display in each row</summary>
        public int ColumnCount { get; }
        /// <summary>If true, then the grid will always be displayed as a perfect square, even if some of the slots in the bottom-right cannot store anything.<para/>
        /// For example, if the bag can only store 8 items and is set to 5 columns, then the grid will display as 2 rows, 5 columns = 10 slots. The bottom-right 2 slots will be rendered as an empty slot.</summary>
        public bool ShowLockedSlots { get; }

        /// <summary>The bounds of this menu's content, relative to <see cref="TopLeftScreenPosition"/></summary>
        public Rectangle RelativeBounds { get; private set; }
        public Rectangle Bounds { get { return RelativeBounds.GetOffseted(TopLeftScreenPosition); } }

        private Point _TopLeftScreenPosition;
        public Point TopLeftScreenPosition {
            get { return _TopLeftScreenPosition; }
            private set { SetTopLeft(value, true); }
        }

        public void SetTopLeft(Point Point) { SetTopLeft(Point, true); }
        private void SetTopLeft(Point NewValue, bool CheckIfChanged = true)
        {
            if (!CheckIfChanged || TopLeftScreenPosition != NewValue)
            {
                Point Previous = TopLeftScreenPosition;
                _TopLeftScreenPosition = NewValue;

                if (RelativeSlotBounds != null)
                {
                    this.SlotBounds = new ReadOnlyCollection<Rectangle>(RelativeSlotBounds.Select(x => x.GetOffseted(TopLeftScreenPosition)).ToList());

                    if (IsRightSidebarVisible)
                    {
                        this.SortingPropertyBounds = new Rectangle(IBM.xPositionOnScreen + IBM.width - ItemBagMenu.ContentsMargin - ItemBagMenu.ButtonLeftTopMargin - ItemBagMenu.ButtonSize, 
                            IBM.yPositionOnScreen + ItemBagMenu.ContentsMargin + ItemBagMenu.ButtonLeftTopMargin + SidebarTopMargin, ItemBagMenu.ButtonSize, ItemBagMenu.ButtonSize);
                        this.SortingOrderBounds = new Rectangle(IBM.xPositionOnScreen + IBM.width - ItemBagMenu.ContentsMargin - ItemBagMenu.ButtonLeftTopMargin - ItemBagMenu.ButtonSize,
                            IBM.yPositionOnScreen + ItemBagMenu.ContentsMargin + ItemBagMenu.ButtonLeftTopMargin + SidebarTopMargin + ItemBagMenu.ButtonSize + ItemBagMenu.ButtonBottomMargin, 
                            ItemBagMenu.ButtonSize, ItemBagMenu.ButtonSize);
                    }
                }
                else
                {
                    this.SlotBounds = null;
                }
            }
        }

        /// <summary>The bounds of each item slot, relative to <see cref="TopLeftScreenPosition"/>. Use <see cref="SlotBounds"/> when rendering to screen space.</summary>
        public ReadOnlyCollection<Rectangle> RelativeSlotBounds { get; private set; }
        /// <summary>The bounds of each item slot</summary>
        public ReadOnlyCollection<Rectangle> SlotBounds { get; private set; }

        #region Sidebar
        public bool IsRightSidebarVisible { get; }

        private enum ContentsSidebarButton
        {
            SortingProperty,
            SortingOrder
        }
        private ContentsSidebarButton? HoveredContentsButton { get; set; } = null;

        public Rectangle SortingPropertyBounds { get; private set; }
        public Rectangle SortingOrderBounds { get; private set; }

        private IEnumerable<Rectangle> ContentsRightSidebarButtonBounds { get { return new List<Rectangle>() { SortingPropertyBounds, SortingOrderBounds }; } }

        private Rectangle? HoveredContentsButtonBounds
        {
            get
            {
                if (HoveredContentsButton.HasValue)
                {
                    if (HoveredContentsButton.Value == ContentsSidebarButton.SortingProperty)
                        return SortingPropertyBounds;
                    else if (HoveredContentsButton.Value == ContentsSidebarButton.SortingOrder)
                        return SortingOrderBounds;
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

        public ReadOnlyCollection<Object> PlaceholderItems { get; private set; }

        public RucksackMenu(ItemBagMenu IBM, Rucksack Bag, int Columns, int SlotSize, bool ShowLockedSlots, int Padding)
        {
            this.IBM = IBM;
            this.Rucksack = Bag;
            Bag.OnContentsChanged += Bag_ContentsChanged;
            this.Padding = Padding;

            this.ColumnCount = Math.Min(Rucksack.NumSlots, Columns);
            this.OriginalSlotSize = SlotSize;
            this.SlotSize = SlotSize;
            this.ShowLockedSlots = ShowLockedSlots;

            this.IsRightSidebarVisible = true;

            InitializePlaceholders();
            SetTopLeft(Point.Zero, false);
            InitializeLayout(1);
        }

        private Dictionary<Object, DateTime> TempVisualFeedback = new Dictionary<Object, DateTime>();

        public void InitializePlaceholders()
        {
            List<Object> Temp = new List<Object>();

            IEnumerable<Object> SortedContents;
            switch (Rucksack.SortProperty)
            {
                case SortingProperty.Time:
                    SortedContents = Rucksack.Contents;
                    break;
                case SortingProperty.Name:
                    SortedContents = Rucksack.Contents.OrderBy(x => x.DisplayName);
                    break;
                case SortingProperty.Id:
                    SortedContents = Rucksack.Contents.OrderBy(x => x.bigCraftable.Value).ThenBy(x => x.ParentSheetIndex);
                    break;
                case SortingProperty.Category:
                    //SortedContents = Rucksack.Contents.OrderBy(x => x.getCategorySortValue());
                    SortedContents = Rucksack.Contents.OrderBy(x => x.getCategoryName());
                    break;
                case SortingProperty.Quantity:
                    SortedContents = Rucksack.Contents.OrderBy(x => x.Stack);
                    break;
                case SortingProperty.SingleValue:
                    SortedContents = Rucksack.Contents.OrderBy(x => ItemBag.GetSingleItemPrice(x));
                    break;
                case SortingProperty.StackValue:
                    SortedContents = Rucksack.Contents.OrderBy(x => ItemBag.GetSingleItemPrice(x) * x.Stack);
                    break;
                case SortingProperty.Similarity:
                    //Possible TODO: Maybe SortingProperty.Similarity shouldn't exist - maybe it should just be a "bool GroupBeforeSorting" 
                    //that only groups by ItemId (and maybe also sorts by Quality after). Then the grouping can be applied to any of the other sorting properties.
                    //So it could just be a togglebutton to turn on/off like the Autofill Toggle

                    SortedContents = Rucksack.Contents
                        .OrderBy(Item => Item.getCategoryName()).GroupBy(Item => Item.getCategoryName()) // First sort and group by CategoryName
                        .SelectMany(
                            CategoryGroup => 
                                CategoryGroup.GroupBy(Item => Item.ParentSheetIndex) // Then Group by item Id
                                .SelectMany(IdGroup => IdGroup.OrderBy(y => y.Quality)) // Then sort by Quality
                        );
                    break;
                default: throw new NotImplementedException(string.Format("Unexpected SortingProperty: {0}", Rucksack.SortProperty.ToString()));
            }
            if (Rucksack.SortOrder == SortingOrder.Descending)
                SortedContents = SortedContents.Reverse();

            //Possible TODO Add filtering?
            //For EX: if you only wanted to show Fish,
            //SortedContents = SortedContents.Where(x => x.Category == <WhateverTheIdIsForFishCategory>);

            TempVisualFeedback = new Dictionary<Object, DateTime>();
            foreach (Object Item in SortedContents)
            {
                bool WasRecentlyModified = Bag.RecentlyModified.TryGetValue(Item, out DateTime ModifiedTime);

                int NumSlots = (Item.Stack - 1) / Rucksack.MaxStackSize + 1;
                int RemainingQty = Item.Stack;
                for (int i = 0; i < NumSlots; i++)
                {
                    Object Copy = ItemBag.CreateCopy(Item);
                    ItemBag.ForceSetQuantity(Copy, Math.Min(RemainingQty, Rucksack.MaxStackSize));
                    Temp.Add(Copy);
                    RemainingQty -= Copy.Stack;

                    if (WasRecentlyModified)
                    {
                        TempVisualFeedback.Add(Copy, ModifiedTime);
                    }
                }
            }

            this.PlaceholderItems = Temp.AsReadOnly();
        }

        #region Input Handling
        private Rectangle? HoveredSlot = null;

        public void OnMouseMoved(CursorMovedEventArgs e)
        {
            if (Bounds.Contains(e.OldPosition.ScreenPixels.AsPoint()) || Bounds.Contains(e.NewPosition.ScreenPixels.AsPoint()))
            {
                Rectangle? PreviouslyHovered = HoveredSlot;

                this.HoveredSlot = null;
                if (SlotBounds != null)
                {
                    foreach (Rectangle Rect in SlotBounds)
                    {
                        if (Rect.Contains(e.NewPosition.ScreenPixels.AsPoint()))
                        {
                            if (PreviouslyHovered.HasValue && Rect != PreviouslyHovered.Value)
                                RightButtonPressedLocation = null;
                            this.HoveredSlot = Rect;
                            break;
                        }
                    }
                }
            }

            if (IsRightSidebarVisible)
            {
                Point OldPos = e.OldPosition.ScreenPixels.AsPoint();
                Point NewPos = e.NewPosition.ScreenPixels.AsPoint();

                if (ContentsRightSidebarButtonBounds.Any(x => x.Contains(OldPos) || x.Contains(NewPos)))
                {
                    if (SortingPropertyBounds.Contains(NewPos))
                        this.HoveredContentsButton = ContentsSidebarButton.SortingProperty;
                    else if (SortingOrderBounds.Contains(NewPos))
                        this.HoveredContentsButton = ContentsSidebarButton.SortingOrder;
                    else
                        this.HoveredContentsButton = null;
                }
                else
                    this.HoveredContentsButton = null;
            }

            UpdateHoveredItem(e);
        }

        private DateTime? RightButtonPressedTime = null;
        private bool IsRightButtonHeld { get { return RightButtonPressedTime.HasValue; } }
        private Rectangle? RightButtonPressedLocation = null;

        public void OnMouseButtonPressed(ButtonPressedEventArgs e)
        {
            if (e.Button == SButton.MouseRight)
            {
                RightButtonPressedLocation = HoveredSlot;
                RightButtonPressedTime = DateTime.Now;
            }

            if (e.Button == SButton.MouseLeft || e.Button == SButton.MouseRight)
            {
                Object PressedObject = GetHoveredItem();
                if (PressedObject != null)
                {
                    int Qty = ItemBag.GetQuantityToTransfer(e, PressedObject);
                    Bag.MoveFromBag(PressedObject, Qty, out int MovedQty, true, IBM.InventorySource, IBM.ActualInventoryCapacity);
                }
            }

            if (e.Button == SButton.MouseLeft && IsRightSidebarVisible && HoveredContentsButton.HasValue)
            {
                if (HoveredContentsButton.Value == ContentsSidebarButton.SortingProperty)
                {
                    int NextValue = ((int)Rucksack.SortProperty + 1) % Enum.GetValues(typeof(SortingProperty)).Length;
                    Rucksack.SortProperty = (SortingProperty)NextValue;
                    InitializePlaceholders();
                }
                else if (HoveredContentsButton.Value == ContentsSidebarButton.SortingOrder)
                {
                    int NextValue = ((int)Rucksack.SortOrder + 1) % Enum.GetValues(typeof(SortingOrder)).Length;
                    Rucksack.SortOrder = (SortingOrder)NextValue;
                    InitializePlaceholders();
                }
            }
        }

        public void OnMouseButtonReleased(ButtonReleasedEventArgs e)
        {
            if (e.Button == SButton.MouseRight)
            {
                RightButtonPressedTime = null;
                RightButtonPressedLocation = null;
            }
        }
        #endregion Input Handling

        public void Update(UpdateTickedEventArgs e)
        {
            if (e.IsMultipleOf(ItemBagMenu.TransferRepeatFrequency))
            {
                if (IsRightButtonHeld && HoveredSlot.HasValue && RightButtonPressedLocation.HasValue && HoveredSlot.Value == RightButtonPressedLocation.Value
                    && RightButtonPressedTime.HasValue && DateTime.Now.Subtract(RightButtonPressedTime.Value).TotalMilliseconds >= 500
                    // Disallow Hold-to-repeat if sorting is set to a property that will cause the items to be dynamically re-ordered as items are removed
                    && Rucksack.SortProperty != SortingProperty.StackValue && Rucksack.SortProperty != SortingProperty.Quantity)
                {
                    Object PressedObject = GetHoveredItem();
                    if (PressedObject != null)
                    {
                        KeyboardState KeyState = Game1.GetKeyboardState();
                        bool IsShiftHeld = KeyState.IsKeyDown(Keys.LeftShift) || KeyState.IsKeyDown(Keys.RightShift);
                        bool IsControlHeld = KeyState.IsKeyDown(Keys.LeftControl) || KeyState.IsKeyDown(Keys.RightControl);
                        int Qty = ItemBag.GetQuantityToTransfer(ItemBag.InputTransferAction.RightButtonHeld, PressedObject, IsShiftHeld, IsControlHeld);

                        Bag.MoveFromBag(PressedObject, Qty, out int MovedQty, false, IBM.InventorySource, IBM.ActualInventoryCapacity);
                        if (MovedQty > 0)
                            Game1.playSound(ItemBag.MoveContentsSuccessSound);
                    }
                }
            }
        }

        public void OnClose()
        {
            Bag.OnContentsChanged -= Bag_ContentsChanged;
        }

        public bool CanResize { get; } = true;

        private int SidebarTopMargin = Constants.TargetPlatform == GamePlatform.Android ? 32 : 16;

        public void InitializeLayout(int ResizeIteration)
        {
            if (Rucksack == null)
                return;

            if (ResizeIteration > 1)
                this.SlotSize = Math.Min(OriginalSlotSize, Math.Max(24, OriginalSlotSize - (ResizeIteration - 1) * 8));

            int SidebarWidth = 0;
            int SidebarHeight = 0;
            if (IsRightSidebarVisible)
            {
                SidebarWidth = ItemBagMenu.ButtonSize + ItemBagMenu.ButtonLeftTopMargin * 2;
                int RightButtons = ContentsRightSidebarButtonBounds.Count();
                int RightHeight = ItemBagMenu.ContentsMargin + ItemBagMenu.ButtonLeftTopMargin + RightButtons * ItemBagMenu.ButtonSize + (RightButtons - 1) * ItemBagMenu.ButtonBottomMargin
                    + ItemBagMenu.ButtonLeftTopMargin + ItemBagMenu.ContentsMargin;
                SidebarHeight = RightHeight;
            }

            HoveredSlot = null;
            HoveredContentsButton = null;
            RightButtonPressedTime = null;
            RightButtonPressedLocation = null;

            List<Rectangle> SlotBounds = new List<Rectangle>();

            int CurrentRow = 0;
            int CurrentColumn = 0;

            int TotalSlots = (((Rucksack.NumSlots - 1) / ColumnCount) + 1) * ColumnCount; // make it a perfect square. EX: if 12 columns, and 18 total slots, increase to next multiple of 12... 24
            for (int i = 0; i < TotalSlots; i++)
            {
                if (CurrentColumn == ColumnCount)
                {
                    CurrentRow++;
                    CurrentColumn = 0;
                }

                int X = CurrentColumn * SlotSize;
                int Y = CurrentRow * SlotSize;
                SlotBounds.Add(new Rectangle(Padding + SidebarWidth + X, Padding + Y, SlotSize, SlotSize));

                CurrentColumn++;
            }

            RelativeSlotBounds = new ReadOnlyCollection<Rectangle>(SlotBounds);

            int TotalWidth = ColumnCount * SlotSize + Padding * 2 + SidebarWidth * 2;
            int TotalHeight = Math.Max((CurrentRow + 1) * SlotSize + Padding * 2, SidebarHeight);

            //  Set bounds of sidebar buttons
            if (IsRightSidebarVisible)
            {
                SortingPropertyBounds = new Rectangle(TotalWidth - ItemBagMenu.ContentsMargin - ItemBagMenu.ButtonLeftTopMargin - ItemBagMenu.ButtonSize,
                    ItemBagMenu.ButtonLeftTopMargin, ItemBagMenu.ButtonSize, ItemBagMenu.ButtonSize);
                SortingOrderBounds = new Rectangle(TotalWidth - ItemBagMenu.ContentsMargin - ItemBagMenu.ButtonLeftTopMargin - ItemBagMenu.ButtonSize,
                    ItemBagMenu.ButtonLeftTopMargin + ItemBagMenu.ButtonSize + ItemBagMenu.ButtonBottomMargin, ItemBagMenu.ButtonSize, ItemBagMenu.ButtonSize);
            }

            this.RelativeBounds = new Rectangle(0, 0, TotalWidth, TotalHeight);
        }

        public void Draw(SpriteBatch b)
        {
            //b.Draw(TextureHelpers.GetSolidColorTexture(Game1.graphics.GraphicsDevice, Color.Cyan), Bounds, Color.White);

            //  Draw the backgrounds of each slot
            for (int i = 0; i < SlotBounds.Count; i++)
            {
                if (i < Rucksack.NumSlots)
                    b.Draw(Game1.menuTexture, SlotBounds[i], new Rectangle(128, 128, 64, 64), Color.White);
                else if (ShowLockedSlots)
                    b.Draw(Game1.menuTexture, SlotBounds[i], new Rectangle(64, 896, 64, 64), Color.White);
            }

            //  Draw the items of each slot
            for (int i = 0; i < SlotBounds.Count; i++)
            {
                if (i < PlaceholderItems.Count)
                {
                    Rectangle Destination = SlotBounds[i];
                    Object CurrentItem = PlaceholderItems[i];

                    //  Apply some visual feedback if the bag has a large grid of items:
                    //  If this item was recently added to the bag or had its quantity changed, Draw a thin green border around it for a couple seconds
                    if (Rucksack.NumSlots >= 24 && TempVisualFeedback.TryGetValue(CurrentItem, out DateTime ModifiedTime))
                    {
                        TimeSpan TotalDuration = TimeSpan.FromSeconds(2.0);
                        TimeSpan Elapsed = DateTime.Now.Subtract(ModifiedTime);
                        if (Elapsed < TotalDuration)
                        {
                            TimeSpan RemainingDuration = TotalDuration.Subtract(Elapsed);
                            float Transparency = Math.Min(1.0f, (float)(RemainingDuration.TotalSeconds / 1.0));
                            Color HighlightColor = Color.DarkGreen * Transparency;
                            Texture2D Highlight = TextureHelpers.GetSolidColorTexture(Game1.graphics.GraphicsDevice, HighlightColor);
                            b.Draw(Highlight, Destination, Color.White * 0.40f);

                            int BorderThickness = Math.Max(Destination.Width / 8, 6);
                            DrawHelpers.DrawBorder(b, Destination, BorderThickness, HighlightColor);
                        }
                    }

                    //  Draw a thin yellow border if mouse is hovering this slot
                    bool IsHovered = Destination == HoveredSlot;
                    if (IsHovered)
                    {
                        Color HighlightColor = Color.Yellow;
                        Texture2D Highlight = TextureHelpers.GetSolidColorTexture(Game1.graphics.GraphicsDevice, HighlightColor);
                        b.Draw(Highlight, Destination, Color.White * 0.25f);

                        int BorderThickness = Destination.Width / 16;
                        DrawHelpers.DrawBorder(b, Destination, BorderThickness, HighlightColor);
                    }

                    float IconScale = IsHovered ? 1.25f : 1.0f;
                    Color Overlay = CurrentItem.Stack == 0 ? Color.White * 0.30f : Color.White;
                    DrawHelpers.DrawItem(b, Destination, CurrentItem, CurrentItem.Stack > 0, true, IconScale, 1.0f, Overlay, CurrentItem.Stack >= Bag.MaxStackSize ? Color.Red : Color.White);
                }
            }

            //  Draw the sidebar buttons
            if (IsRightSidebarVisible)
            {
                //  Draw Sort Property icons
                b.Draw(Game1.menuTexture, SortingPropertyBounds, new Rectangle(128, 128, 64, 64), Color.White);
                if (Rucksack.SortProperty == SortingProperty.Time)
                {
                    //  Clock icon
                    Texture2D SourceTexture = Game1.mouseCursors;
                    Rectangle SourceRect = new Rectangle(410, 501, 9, 9);
                    int IconSize = (int)(SourceRect.Width * 2.0 / 32.0 * SortingPropertyBounds.Width);
                    b.Draw(Game1.mouseCursors, new Rectangle(SortingPropertyBounds.X + (SortingPropertyBounds.Width - IconSize) / 2,
                        SortingPropertyBounds.Y + (SortingPropertyBounds.Height - IconSize) / 2, IconSize, IconSize), SourceRect, Color.White);
                }
                else if (Rucksack.SortProperty == SortingProperty.Name)
                {
                    //  Alphabetical letter icon
                    Texture2D SourceTexture = Game1.mouseCursors;
                    Rectangle SourceRect = new Rectangle(279, 25, 9, 9);
                    int IconSize = (int)(SourceRect.Width * 2.0 / 32.0 * SortingPropertyBounds.Width);
                    b.Draw(Game1.mouseCursors, new Rectangle(SortingPropertyBounds.X + (SortingPropertyBounds.Width - IconSize) / 2,
                        SortingPropertyBounds.Y + (SortingPropertyBounds.Height - IconSize) / 2, IconSize, IconSize), SourceRect, Color.White);
                }
                else if (Rucksack.SortProperty == SortingProperty.Id)
                {
                    //  Open book icon
                    Texture2D SourceTexture = Game1.mouseCursors;
                    Rectangle SourceRect = new Rectangle(146, 447, 11, 10);
                    int IconSize = (int)(SourceRect.Width * 1.5 / 32.0 * SortingPropertyBounds.Width);
                    b.Draw(Game1.mouseCursors, new Rectangle(SortingPropertyBounds.X + (SortingPropertyBounds.Width - IconSize) / 2,
                        SortingPropertyBounds.Y + (SortingPropertyBounds.Height - IconSize) / 2, IconSize, IconSize), SourceRect, Color.White);
                }
                else if (Rucksack.SortProperty == SortingProperty.Category)
                {
                    //  Magnifying glass icon
                    Texture2D SourceTexture = Game1.mouseCursors;
                    Rectangle SourceRect = new Rectangle(80, 0, 13, 13);
                    int IconSize = (int)(SourceRect.Width * 1.5 / 32.0 * SortingPropertyBounds.Width);
                    b.Draw(Game1.mouseCursors, new Rectangle(SortingPropertyBounds.X + (SortingPropertyBounds.Width - IconSize) / 2,
                        SortingPropertyBounds.Y + (SortingPropertyBounds.Height - IconSize) / 2, IconSize, IconSize), SourceRect, Color.White);
                }
                else if (Rucksack.SortProperty == SortingProperty.Quantity)
                {
                    //  Large # '9' icon
                    Texture2D SourceTexture = Game1.mouseCursors;
                    Rectangle SourceRect = new Rectangle(537, 136, 7, 8);
                    int IconSize = (int)(SourceRect.Width * 2.5 / 32.0 * SortingPropertyBounds.Width);
                    b.Draw(Game1.mouseCursors, new Rectangle(SortingPropertyBounds.X + (SortingPropertyBounds.Width - IconSize) / 2,
                        SortingPropertyBounds.Y + (SortingPropertyBounds.Height - IconSize) / 2, IconSize, IconSize), SourceRect, Color.White);
                }
                else if (Rucksack.SortProperty == SortingProperty.SingleValue)
                {
                    //  'G' with golden background icon
                    Texture2D SourceTexture = Game1.mouseCursors;
                    Rectangle SourceRect = new Rectangle(408, 476, 9, 11);
                    int IconSize = (int)(SourceRect.Width * 2.0 / 32.0 * SortingPropertyBounds.Width);
                    b.Draw(Game1.mouseCursors, new Rectangle(SortingPropertyBounds.X + (SortingPropertyBounds.Width - IconSize) / 2,
                        SortingPropertyBounds.Y + (SortingPropertyBounds.Height - IconSize) / 2, IconSize, IconSize), SourceRect, Color.White);
                }
                else if (Rucksack.SortProperty == SortingProperty.StackValue)
                {
                    //  Big bag of gold icon
                    Texture2D SourceTexture = Game1.mouseCursors;
                    Rectangle SourceRect = new Rectangle(397, 1941, 19, 20);
                    int IconSize = (int)(SourceRect.Width * 1.0 / 32.0 * SortingPropertyBounds.Width);
                    b.Draw(Game1.mouseCursors, new Rectangle(SortingPropertyBounds.X + (SortingPropertyBounds.Width - IconSize) / 2,
                        SortingPropertyBounds.Y + (SortingPropertyBounds.Height - IconSize) / 2, IconSize, IconSize), SourceRect, Color.White);
                }
                else if (Rucksack.SortProperty == SortingProperty.Similarity)
                {
                    //  Star icon
                    Texture2D SourceTexture = Game1.mouseCursors;
                    Rectangle SourceRect = new Rectangle(310, 392, 16, 16);
                    int IconSize = (int)(SourceRect.Width * 1.0 / 32.0 * SortingPropertyBounds.Width);
                    b.Draw(Game1.mouseCursors, new Rectangle(SortingPropertyBounds.X + (SortingPropertyBounds.Width - IconSize) / 2,
                        SortingPropertyBounds.Y + (SortingPropertyBounds.Height - IconSize) / 2, IconSize, IconSize), SourceRect, Color.White);
                }

                //  Draw Sort Order icons
                b.Draw(Game1.menuTexture, SortingOrderBounds, new Rectangle(128, 128, 64, 64), Color.White);
                if (Rucksack.SortOrder == SortingOrder.Ascending)
                {
                    Rectangle ArrowUpIconSourceRect = new Rectangle(421, 459, 12, 12);
                    int ArrowSize = (int)(ArrowUpIconSourceRect.Width * 1.5 / 32.0 * SortingOrderBounds.Width);
                    b.Draw(Game1.mouseCursors, new Rectangle(SortingOrderBounds.X + (SortingOrderBounds.Width - ArrowSize) / 2, 
                        SortingOrderBounds.Y + (SortingOrderBounds.Height - ArrowSize) / 2, ArrowSize, ArrowSize), ArrowUpIconSourceRect, Color.White);
                }
                else if (Rucksack.SortOrder == SortingOrder.Descending)
                {
                    Rectangle ArrowDownIconSourceRect = new Rectangle(421, 472, 12, 12);
                    int ArrowSize = (int)(ArrowDownIconSourceRect.Width * 1.5 / 32.0 * SortingOrderBounds.Width);
                    b.Draw(Game1.mouseCursors, new Rectangle(SortingOrderBounds.X + (SortingOrderBounds.Width - ArrowSize) / 2,
                        SortingOrderBounds.Y + (SortingOrderBounds.Height - ArrowSize) / 2, ArrowSize, ArrowSize), ArrowDownIconSourceRect, Color.White);
                }

                //  Draw a yellow border around the hovered sidebar button
                if (HoveredContentsButton.HasValue)
                {
                    Rectangle HoveredBounds = HoveredContentsButtonBounds.Value;
                    Color HighlightColor = Color.Yellow;
                    Texture2D Highlight = TextureHelpers.GetSolidColorTexture(Game1.graphics.GraphicsDevice, HighlightColor);
                    b.Draw(Highlight, HoveredBounds, Color.White * 0.25f);
                    int BorderThickness = HoveredBounds.Width / 16;
                    DrawHelpers.DrawBorder(b, HoveredBounds, BorderThickness, HighlightColor);
                }
            }
        }

        public void DrawToolTips(SpriteBatch b)
        {
            //  Draw tooltips on the hovered item inside the bag
            if (HoveredSlot.HasValue)
            {
                Object HoveredItem = GetHoveredItem();
                if (HoveredItem != null)
                {
                    //Rectangle Location = HoveredSlot.Value;
                    Rectangle Location = new Rectangle(Game1.getMouseX() - 8, Game1.getMouseY() + 36, 8 + 36, 1);
                    DrawHelpers.DrawToolTipInfo(b, Location, HoveredItem, true, true, true, true, true, true, Bag.MaxStackSize);
                }
            }

            //  Draw tooltips on the sidebar buttons
            if (IsRightSidebarVisible && HoveredContentsButton.HasValue)
            {
                string ButtonToolTip = "";
                if (HoveredContentsButton.Value == ContentsSidebarButton.SortingProperty)
                    ButtonToolTip = ItemBagsMod.Translate(string.Format("RucksackSortProperty{0}ToolTip", Rucksack.SortProperty.ToString()));
                else if (HoveredContentsButton.Value == ContentsSidebarButton.SortingOrder)
                {
                    if (Rucksack.SortOrder == SortingOrder.Ascending)
                        ButtonToolTip = ItemBagsMod.Translate("RucksackSortOrderAscendingToolTip");
                    else if (Rucksack.SortOrder == SortingOrder.Descending)
                        ButtonToolTip = ItemBagsMod.Translate("RucksackSortOrderDescendingToolTip");
                }

                if (!string.IsNullOrEmpty(ButtonToolTip))
                {
                    int Margin = 16;
                    Vector2 ToolTipSize = Game1.smallFont.MeasureString(ButtonToolTip);
                    DrawHelpers.DrawBox(b, HoveredContentsButtonBounds.Value.Left - (int)(ToolTipSize.X + Margin * 2), HoveredContentsButtonBounds.Value.Top, (int)(ToolTipSize.X + Margin * 2), (int)(ToolTipSize.Y + Margin * 2));
                    b.DrawString(Game1.smallFont, ButtonToolTip, new Vector2(HoveredContentsButtonBounds.Value.Left - Margin - ToolTipSize.X, HoveredContentsButtonBounds.Value.Top + Margin), Color.Black);
                }
            }
        }

        internal Object GetHoveredItem()
        {
            if (HoveredSlot.HasValue)
            {
                int Index = SlotBounds.IndexOf(HoveredSlot.Value);
                if (Index >= 0 && Index < PlaceholderItems.Count)
                    return PlaceholderItems[Index];
                else
                    return null;
            }
            else
                return null;
        }
    }
}
