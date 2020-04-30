using ItemBags.Bags;
using ItemBags.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Object = StardewValley.Object;
using ItemBags.Persistence;

namespace ItemBags.Menus
{
    public class OmniBagMenu : ItemBagMenu
    {
        public OmniBag OmniBag { get; }
        private void Bag_ContentsChanged(object sender, EventArgs e) { UpdateActualContents(); }

        private int OriginalSlotSize { get; }
        /// <summary>The size, in pixels, to use when rendering an item slot. Recommended = <see cref="BagInventoryMenu.DefaultInventoryIconSize"/></summary>
        public int SlotSize { get; private set; }
        /// <summary>The number of columns to display in each row</summary>
        public int ColumnCount { get; }
        /// <summary>If true, then the grid will always be displayed as a perfect square, even if some of the slots in the bottom-right cannot store anything.<para/>
        /// For example, if the bag can only store 8 items and is set to 5 columns, then the grid will display as 2 rows, 5 columns = 10 slots. The bottom-right 2 slots will be rendered as an empty slot.</summary>
        public bool ShowLockedSlots { get; }

        /// <summary>The bounds of this menu's content, relative to <see cref="TopLeftScreenPosition"/></summary>
        public Rectangle RelativeContentBounds { get; private set; }
        public override Rectangle GetRelativeContentBounds() { return this.RelativeContentBounds; }
        public Rectangle ContentBounds { get { return RelativeContentBounds.GetOffseted(TopLeftScreenPosition); } }
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

                if (RelativeSlotBounds != null)
                {
                    this.SlotBounds = new ReadOnlyCollection<Rectangle>(RelativeSlotBounds.Select(x => x.GetOffseted(TopLeftScreenPosition)).ToList());
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

        public ReadOnlyCollection<ItemBag> Placeholders { get; }
        public List<ItemBag> ActualContents { get; }

        /// <param name="InventorySource">Typically this is <see cref="Game1.player.Items"/> if this menu should display the player's inventory.</param>
        /// <param name="ActualCapacity">The maximum # of items that can be stored in the InventorySource list. Use <see cref="Game1.player.MaxItems"/> if moving to/from the inventory.</param>
        /// <param name="InventoryColumns">The number of columns to use when rendering the user's inventory at the bottom-half of the menu. Recommended = 12 to mimic the default inventory of the main GameMenu</param>
        /// <param name="InventorySlotSize">The size, in pixels, to use when rendering each slot of the user's inventory at the bottom-half of the menu. Recommended = <see cref="BagInventoryMenu.DefaultInventoryIconSize"/></param>
        public OmniBagMenu(OmniBag Bag, IList<Item> InventorySource, int ActualCapacity, int InventoryColumns, int InventorySlotSize, int ContentsColumns, int ContentsSlotSize, bool ShowLockedSlots)
            : base(Bag, InventorySource, ActualCapacity, InventoryColumns, InventorySlotSize)
        {
            this.OmniBag = Bag;
            Bag.OnContentsChanged += Bag_ContentsChanged;

            this.ColumnCount = ContentsColumns;
            this.OriginalSlotSize = ContentsSlotSize;
            this.SlotSize = ContentsSlotSize;
            this.ShowLockedSlots = ShowLockedSlots;

            //  Create a placeholder item for every kind of bag the OmniBag can store
            List<ItemBag> Temp = new List<ItemBag>();
            if (BundleBag.ValidSizes.Any(x => x <= this.Bag.Size))
            {
                ContainerSize PlaceholderSize = BundleBag.ValidSizes.OrderByDescending(x => x).First(x => x <= this.Bag.Size);
                Temp.Add(new BundleBag(PlaceholderSize, false));
            }
            Temp.Add(new Rucksack(Bag.Size, false));
            foreach (BagType BagType in ItemBagsMod.BagConfig.BagTypes)
            {
                if (BagType.SizeSettings.Any(x => x.Size <= this.Bag.Size))
                {
                    ContainerSize PlaceholderSize = BagType.SizeSettings.Select(x => x.Size).OrderByDescending(x => x).First(x => x <= this.Bag.Size);
                    Temp.Add(new BoundedBag(BagType, PlaceholderSize, false));
                }
            }
            this.Placeholders = new ReadOnlyCollection<ItemBag>(Temp);

            this.ActualContents = new List<ItemBag>();
            for (int i = 0; i < Temp.Count; i++)
                ActualContents.Add(null);
            UpdateActualContents();

            SetTopLeft(Point.Zero, false);
            InitializeLayout();
        }

        public void UpdateActualContents()
        {
            HashSet<string> ContainedTypeIds = new HashSet<string>(OmniBag.NestedBags.Select(x => x.GetTypeId()));

            for (int i = 0; i < Placeholders.Count; i++)
            {
                string TypeId = Placeholders[i].GetTypeId();
                if (ContainedTypeIds.Contains(TypeId))
                    ActualContents[i] = OmniBag.NestedBags.First(x => x.GetTypeId() == TypeId);
                else
                    ActualContents[i] = null;
            }
        }

        #region Input Handling
        private Rectangle? HoveredSlot = null;

        protected override void OverridableOnMouseMoved(CursorMovedEventArgs e)
        {
            base.OverridableOnMouseMoved(e);

            if (ContentBounds.Contains(e.OldPosition.ScreenPixels.AsPoint()) || ContentBounds.Contains(e.NewPosition.ScreenPixels.AsPoint()))
            {
                Rectangle? PreviouslyHovered = HoveredSlot;

                this.HoveredSlot = null;
                if (SlotBounds != null)
                {
                    foreach (Rectangle Rect in SlotBounds)
                    {
                        if (Rect.Contains(e.NewPosition.ScreenPixels.AsPoint()))
                        {
                            this.HoveredSlot = Rect;
                            break;
                        }
                    }
                }
            }
        }

        protected override void OverridableOnMouseButtonPressed(ButtonPressedEventArgs e)
        {
            base.OverridableOnMouseButtonPressed(e);

            if (e.Button == SButton.MouseLeft || e.Button == SButton.MouseRight)
            {
                ItemBag PressedBag = GetHoveredBag();
                if (PressedBag != null)
                {
                    if (e.Button == SButton.MouseLeft)
                        OmniBag.MoveFromBag(PressedBag, true, InventorySource, ActualInventoryCapacity, true);
                    else if (e.Button == SButton.MouseRight)
                    {
                        //IClickableMenu PreviousMenu = this.Bag.PreviousMenu;
                        //this.Bag.CloseContents(false, false);
                        //PressedBag.OpenContents(InventorySource, ActualInventoryCapacity, PreviousMenu);
                        PressedBag.OpenContents(InventorySource, ActualInventoryCapacity, this.Bag.ContentsMenu);

                        this.HoveredSlot = null;
                    }
                }
            }
        }
        #endregion Input Handling

        public override void OnClose()
        {
            Bag.OnContentsChanged -= Bag_ContentsChanged;
        }

        protected override bool CanResize() { return true; }

        protected override void InitializeContentsLayout()
        {
            if (OmniBag == null)
                return;

            if (ResizeIteration > 1)
                this.SlotSize = Math.Min(OriginalSlotSize, Math.Max(32, OriginalSlotSize - (ResizeIteration - 1) * 8));

            HoveredSlot = null;

            List<Rectangle> SlotBounds = new List<Rectangle>();

            int CurrentRow = 0;
            int CurrentColumn = 0;

            int TotalSlots = (((Placeholders.Count - 1) / ColumnCount) + 1) * ColumnCount; // make it a perfect square. EX: if 12 columns, and 18 total slots, increase to next multiple of 12... 24
            for (int i = 0; i < TotalSlots; i++)
            {
                if (CurrentColumn == ColumnCount)
                {
                    CurrentRow++;
                    CurrentColumn = 0;
                }

                int X = CurrentColumn * SlotSize;
                int Y = CurrentRow * SlotSize;
                SlotBounds.Add(new Rectangle(ContentsMargin + X, ContentsMargin + Y, SlotSize, SlotSize));

                CurrentColumn++;
            }

            RelativeSlotBounds = new ReadOnlyCollection<Rectangle>(SlotBounds);

            int TotalWidth = ColumnCount * SlotSize + ContentsMargin * 2;
            int TotalHeight = (CurrentRow + 1) * SlotSize + ContentsMargin * 2;

            this.RelativeContentBounds = new Rectangle(0, 0, TotalWidth, TotalHeight);
        }

        protected override void DrawContents(SpriteBatch b)
        {
            //b.Draw(TextureHelpers.GetSolidColorTexture(Game1.graphics.GraphicsDevice, Color.Cyan), Bounds, Color.White);

            //  Draw the backgrounds of each slot
            for (int i = 0; i < SlotBounds.Count; i++)
            {
                if (i < Placeholders.Count)
                    b.Draw(Game1.menuTexture, SlotBounds[i], new Rectangle(128, 128, 64, 64), Color.White);
                else if (ShowLockedSlots)
                    b.Draw(Game1.menuTexture, SlotBounds[i], new Rectangle(64, 896, 64, 64), Color.White);
            }

            //  Draw the items of each slot
            for (int i = 0; i < SlotBounds.Count; i++)
            {
                if (i < Placeholders.Count)
                {
                    Rectangle Destination = SlotBounds[i];

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
                    if (ActualContents[i] != null)
                    {
                        ItemBag CurrentItem = ActualContents[i];
                        DrawHelpers.DrawItem(b, Destination, CurrentItem, CurrentItem.Stack > 0, true, IconScale, 1f, Color.White, CurrentItem.Stack >= Bag.MaxStackSize ? Color.Red : Color.White);
                    }
                    else
                    {
                        ItemBag CurrentItem = Placeholders[i];
                        DrawHelpers.DrawItem(b, Destination, CurrentItem, CurrentItem.Stack > 0, true, IconScale, 0.35f, Color.White * 0.3f, CurrentItem.Stack >= Bag.MaxStackSize ? Color.Red : Color.White);
                    }
                }
            }
        }

        protected override void DrawContentsToolTips(SpriteBatch b)
        {
            //  Draw tooltips on the hovered item inside the bag
            if (HoveredSlot.HasValue)
            {
                ItemBag HoveredBag = GetHoveredBag();
                if (HoveredBag == null)
                {
                    int Index = SlotBounds.IndexOf(HoveredSlot.Value);
                    if (Index >= 0 && Index < Placeholders.Count)
                    {
                        HoveredBag = Placeholders[Index];
                    }
                }

                if (HoveredBag != null)
                {
                    //Rectangle Location = HoveredSlot.Value;
                    Rectangle Location = new Rectangle(Game1.getMouseX() - 8, Game1.getMouseY() + 36, 8 + 36, 1);

                    //if (HoveredBag is Rucksack RS)
                    //{
                    //    int XPos = Location.Right;
                    //    int YPos = Location.Bottom;
                    //    RS.drawTooltip(b, ref XPos, ref YPos, Game1.smallFont, 1f, RS.Description);
                    //}
                    //else
                    //{
                    //    DrawHelpers.DrawToolTipInfo(b, Location, HoveredBag, true, true, true, true, true, Bag.MaxStackSize);
                    //}

                    DrawHelpers.DrawToolTipInfo(b, Location, HoveredBag, true, true, true, true, true, true, Bag.MaxStackSize);
                }
            }
        }

        internal ItemBag GetHoveredBag()
        {
            if (HoveredSlot.HasValue)
            {
                int Index = SlotBounds.IndexOf(HoveredSlot.Value);
                if (Index >= 0 && Index < Placeholders.Count)
                {
                    return ActualContents[Index];
                    //string TypeId = Placeholders[Index].GetTypeId();
                    //return OmniBag.NestedBags.FirstOrDefault(x => x.GetTypeId() == TypeId);
                }
                else
                    return null;
            }
            else
                return null;
        }

        protected override void UpdateHoveredItem(CursorMovedEventArgs e, out bool Handled)
        {
            base.UpdateHoveredItem(e, out Handled);
            if (!Handled)
            {
                if (ContentBounds.Contains(e.NewPosition.ScreenPixels.AsPoint()))
                {
                    HoveredItem = GetHoveredBag();
                    Handled = true;
                }
            }
        }
    }
}
