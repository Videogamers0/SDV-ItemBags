using ItemBags.Bags;
using ItemBags.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Object = StardewValley.Object;

namespace ItemBags.Menus
{
    public class BagInventoryMenu
    {
        public ItemBag Bag { get; }
        public IList<Item> Source { get; }
        public int ActualCapacity { get; }

        public const int DefaultInventoryIconSize = 64;
        /// <summary>The number of columns to use when rendering the user's inventory at the bottom-half of the menu. Recommended = 12 to mimic the default inventory of the main GameMenu</summary>
        public int InventoryColumns { get; }
        /// <summary>The size, in pixels, to use when rendering each slot of the user's inventory at the bottom-half of the menu. Recommended = <see cref="DefaultInventoryIconSize"/></summary>
        public int InventorySlotSize { get; }

        /// <summary>The bounds of each inventory slot, relative to <see cref="TopLeftScreenPosition"/>. Use <see cref="InventorySlotBounds"/> when rendering to screen space.</summary>
        public ReadOnlyCollection<Rectangle> RelativeInventorySlotBounds { get; private set; }
        /// <summary>The bounds of each inventory slot</summary>
        public ReadOnlyCollection<Rectangle> InventorySlotBounds { get; private set; }

        private int TotalInventorySlots { get; set; }
        private int UnlockedInventorySlots { get; set; }
        private int LockedInventorySlots { get { return TotalInventorySlots - UnlockedInventorySlots; } }
        private bool IsLockedInventorySlot(int Index) { return Index >= UnlockedInventorySlots; }

        private Point _TopLeftScreenPosition;
        public Point TopLeftScreenPosition {
            get { return _TopLeftScreenPosition; }
            private set { SetTopLeft(value, true); }
        }

        public void SetTopLeft(Point NewValue, bool CheckIfChanged = true)
        {
            if (!CheckIfChanged || TopLeftScreenPosition != NewValue)
            {
                _TopLeftScreenPosition = NewValue;

                if (RelativeInventorySlotBounds != null)
                {
                    List<Rectangle> TranslatedSlots = new List<Rectangle>();
                    foreach (Rectangle Relative in RelativeInventorySlotBounds)
                    {
                        Rectangle Translated = Relative.GetOffseted(TopLeftScreenPosition);
                        TranslatedSlots.Add(Translated);
                    }
                    this.InventorySlotBounds = new ReadOnlyCollection<Rectangle>(TranslatedSlots);
                }
                else
                {
                    this.InventorySlotBounds = null;
                }

                this.Bounds = RelativeBounds.GetOffseted(TopLeftScreenPosition);
            }
        }

        /// <summary>The bounds of this menu's content, relative to <see cref="TopLeftScreenPosition"/></summary>
        public Rectangle RelativeBounds { get; private set; }
        public Rectangle Bounds { get; private set; }

        /// <param name="Source">Typically this is <see cref="Game1.player.Items"/> if this menu should display the player's inventory.</param>
        /// <param name="ActualCapacity">If non-null, allows you to override the maximum # of items that can be stored in the Source list</param>
        /// <param name="InventoryColumns">The number of columns to use when rendering the user's inventory at the bottom-half of the menu. Recommended = 12 to mimic the default inventory of the main GameMenu</param>
        /// <param name="InventorySlotSize">The size, in pixels, to use when rendering each slot of the user's inventory at the bottom-half of the menu. Recommended = <see cref="DefaultInventoryIconSize"/></param>
        public BagInventoryMenu(ItemBag Bag, IList<Item> Source, int? ActualCapacity, int InventoryColumns, int InventorySlotSize = DefaultInventoryIconSize)
        {
            this.Bag = Bag;
            this.Source = Source;
            this.ActualCapacity = ActualCapacity.HasValue ? Math.Max(ActualCapacity.Value, Source.Count) : Source.Count;
            this.InventoryColumns = InventoryColumns;
            this.InventorySlotSize = InventorySlotSize;
            SetTopLeft(Point.Zero, false);
            InitializeLayout();
        }

        #region Input Handling
        private Rectangle? HoveredSlot = null;

        internal void OnMouseMoved(CursorMovedEventArgs e)
        {
            Rectangle? PreviouslyHovered = HoveredSlot;

            this.HoveredSlot = null;
            if (InventorySlotBounds != null)
            {
                foreach (Rectangle Rect in InventorySlotBounds)
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

        private DateTime? RightButtonPressedTime = null;
        private bool IsRightButtonHeld { get { return RightButtonPressedTime.HasValue; } }
        private Rectangle? RightButtonPressedLocation = null;

        internal void OnMouseButtonPressed(ButtonPressedEventArgs e)
        {
            if (e.Button == SButton.MouseRight)
            {
                RightButtonPressedLocation = HoveredSlot;
                RightButtonPressedTime = DateTime.Now;
            }

            if (e.Button == SButton.MouseLeft || e.Button == SButton.MouseRight)
            {
                Item PressedItem = GetHoveredItem();
                if (PressedItem != null)
                {
                    if (PressedItem is ItemBag IB)
                    {
                        //  Click current bag to close it
                        if (IB == this.Bag &&
                            (e.Button == SButton.MouseRight || Constants.TargetPlatform == GamePlatform.Android)) // Also allow left-clicking on Android since the interface might be too small to display the Close button in top-right))
                        {
                            //IB.CloseContents();
                            //  Rather than immediately closing the menu, queue it up to be closed on the next game update.
                            //  When a bag menu is closed, the previous menu is restored. If we don't queue up the bag close,
                            //  then the game will process this current right-click event on the previous menu after it's restored.
                            //  By handling the right-click on 2 different menus, it can cause an unintended action on the restored menu.
                            //  For example, if the restored menu is a chest interface, the mouse cursor could coincidentally be hovering an item
                            //  in the chest so this current right-click action would close the bag, then transfer the hovered chest item, rather than just closing the bag.
                            QueueCloseBag = true;
                        }
                        else if (IB != this.Bag && this.Bag is OmniBag OB)
                        {
                            if (e.Button == SButton.MouseRight)
                            {
                                IClickableMenu PreviousMenu = this.Bag.PreviousMenu;
                                this.Bag.CloseContents(false, false);
                                IB.OpenContents(Source, ActualCapacity, PreviousMenu);
                            }
                            else
                            {
                                OB.MoveToBag(IB, true, Source, true);
                            }
                        }
                        else if (IB != this.Bag && !(this.Bag is OmniBag) && (e.Button == SButton.MouseRight || Constants.TargetPlatform == GamePlatform.Android))
                        {
                            IClickableMenu PreviousMenu = this.Bag.PreviousMenu;
                            if (PreviousMenu is OmniBagMenu OBM)
                                PreviousMenu = OBM.OmniBag.PreviousMenu;
                            this.Bag.CloseContents(false, false);
                            IB.OpenContents(Source, ActualCapacity, PreviousMenu);
                        }
                    }
                    //  Clicking an Object will attempt to transfer it to the bag
                    else if (PressedItem is Object PressedObject && Bag.IsValidBagObject(PressedObject))
                    {
                        int Qty = ItemBag.GetQuantityToTransfer(e, PressedObject);
                        Bag.MoveToBag(PressedObject, Qty, out int MovedQty, true, Source);
                    }
                }
            }
        }

        internal void OnMouseButtonReleased(ButtonReleasedEventArgs e)
        {
            if (e.Button == SButton.MouseRight)
            {
                RightButtonPressedTime = null;
                RightButtonPressedLocation = null;
            }
        }
        #endregion Input Handling

        private bool QueueCloseBag = false;

        internal void Update(UpdateTickedEventArgs e)
        {
            if (QueueCloseBag)
            {
                QueueCloseBag = false;
                this.Bag.CloseContents();
                return;
            }

            if (e.IsMultipleOf(ItemBagMenu.TransferRepeatFrequency))
            {
                if (IsRightButtonHeld && HoveredSlot.HasValue && RightButtonPressedLocation.HasValue && HoveredSlot.Value == RightButtonPressedLocation.Value
                    && RightButtonPressedTime.HasValue && DateTime.Now.Subtract(RightButtonPressedTime.Value).TotalMilliseconds >= 500)
                {
                    Item PressedItem = GetHoveredItem();
                    if (PressedItem != null)
                    {
                        if (PressedItem is Object PressedObject && Bag.IsValidBagObject(PressedObject))
                        {
                            KeyboardState KeyState = Game1.GetKeyboardState();
                            bool IsShiftHeld = KeyState.IsKeyDown(Keys.LeftShift) || KeyState.IsKeyDown(Keys.RightShift);
                            bool IsControlHeld = KeyState.IsKeyDown(Keys.LeftControl) || KeyState.IsKeyDown(Keys.RightControl);
                            int Qty = ItemBag.GetQuantityToTransfer(ItemBag.InputTransferAction.RightButtonHeld, PressedObject, IsShiftHeld, IsControlHeld);

                            Bag.MoveToBag(PressedObject, Qty, out int MovedQty, false, Source);
                            if (MovedQty > 0)
                                Game1.playSound(ItemBag.MoveContentsSuccessSound);
                        }
                    }
                }
            }
        }

        public void InitializeLayout()
        {
            HoveredSlot = null;
            RightButtonPressedTime = null;
            RightButtonPressedLocation = null;

            //  Compute size of inventory
            int InventoryMargin = 16; // Empty space around the inventory slots

            this.TotalInventorySlots = ActualCapacity;
            this.UnlockedInventorySlots = ActualCapacity;
            int InventoryRows = (TotalInventorySlots - 1) / InventoryColumns + 1;

            int LockedSlotsSeparatorHeight = 16;
            bool HasLockedSlots = LockedInventorySlots > 0;
            bool ShowLockedSlotsSeparator = HasLockedSlots && LockedInventorySlots % InventoryColumns == 0;

            int RequiredInventoryWidth = InventoryColumns * InventorySlotSize + InventoryMargin * 2;
            int RequiredInventoryHeight = InventoryRows * InventorySlotSize + InventoryMargin * 2;
            if (ShowLockedSlotsSeparator)
                RequiredInventoryHeight += LockedSlotsSeparatorHeight;

            this.RelativeBounds = new Rectangle(0, 0, RequiredInventoryWidth, RequiredInventoryHeight);

            //  Set bounds of inventory
            List<Rectangle> InvSlotBounds = new List<Rectangle>();
            for (int i = 0; i < TotalInventorySlots; i++)
            {
                int Row = i / InventoryColumns;
                int Column = i - Row * InventoryColumns;

                int X = InventoryMargin + Column * InventorySlotSize;
                int Y = InventoryMargin + Row * InventorySlotSize;

                bool IsBelowLockedSlotsSeparator = ShowLockedSlotsSeparator && i >= UnlockedInventorySlots;
                if (IsBelowLockedSlotsSeparator)
                    Y += LockedSlotsSeparatorHeight;

                InvSlotBounds.Add(new Rectangle(X, Y, InventorySlotSize, InventorySlotSize));
            }
            RelativeInventorySlotBounds = new ReadOnlyCollection<Rectangle>(InvSlotBounds);
        }

        public void Draw(SpriteBatch b)
        {
            //  Draw the background textures of each inventory slot
            for (int i = 0; i < InventorySlotBounds.Count; i++)
            {
                Rectangle Destination = InventorySlotBounds[i];
                if (IsLockedInventorySlot(i))
                {
                    b.Draw(Game1.menuTexture, Destination, new Rectangle(64, 896, 64, 64), Color.White);
                }
                else
                {
                    b.Draw(Game1.menuTexture, Destination, new Rectangle(128, 128, 64, 64), Color.White);
                }
            }

            for (int i = 0; i < InventorySlotBounds.Count; i++)
            {
                Rectangle Destination = InventorySlotBounds[i];

                Item CurrentItem = null;
                if (!IsLockedInventorySlot(i) && i < Source.Count)
                    CurrentItem = Source[i];
                bool IsValidBagItem = (Bag is OmniBag OB && CurrentItem is ItemBag IB && OB.IsValidBag(IB)) || Bag.IsValidBagItem(CurrentItem);

                //  Draw a transparent black or white overlay if the item is valid for the bag or not
                Color Overlay = IsValidBagItem || CurrentItem == this.Bag ? Color.White : Color.Black;
                float OverlayTransparency = IsValidBagItem || CurrentItem == this.Bag ? 0.15f : 0.35f;
                b.Draw(TextureHelpers.GetSolidColorTexture(Game1.graphics.GraphicsDevice, Overlay), Destination, Color.White * OverlayTransparency);

                if (CurrentItem != null)
                {
                    //  Draw a red outline on the bag that's currently open
                    if (CurrentItem == this.Bag)
                    {
                        b.Draw(Game1.menuTexture, Destination, new Rectangle(0, 896, 64, 64), Color.White);
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

                    //  Draw the item
                    float Scale = IsHovered && IsValidBagItem ? 1.25f : 1.0f;
                    float Transparency = IsValidBagItem || CurrentItem == this.Bag || CurrentItem is ItemBag ? 1.0f : 0.35f;
                    if (InventorySlotSize == DefaultInventoryIconSize)
                    {
                        CurrentItem.drawInMenu(b, new Vector2(Destination.X, Destination.Y), Scale, Transparency, 1f, StackDrawType.Draw_OneInclusive, Color.White, true);
                    }
                    else
                    {
                        DrawHelpers.DrawItem(b, Destination, CurrentItem, true, true, Scale, Transparency, Color.White, Color.White);
                    }
                }
            }
        }

        public void DrawToolTips(SpriteBatch b)
        {
            if (HoveredSlot.HasValue)
            {
                Item HoveredItem = GetHoveredItem();
                if (HoveredItem != null && Bag.IsValidBagItem(HoveredItem))
                {
                    //Rectangle Location = HoveredSlot.Value;
                    Rectangle Location = new Rectangle(Game1.getMouseX() - 8, Game1.getMouseY() + 36, 8 + 36, 1);
                    DrawHelpers.DrawToolTipInfo(b, Location, HoveredItem, true, true, true, true, true, null);
                }
            }
        }

        internal Item GetHoveredItem()
        {
            if (HoveredSlot.HasValue)
            {
                int Index = InventorySlotBounds.IndexOf(HoveredSlot.Value);
                if (!IsLockedInventorySlot(Index) && Index < Source.Count)
                {
                    Item HoveredItem = Source[Index];
                    return HoveredItem;
                }
                else
                    return null;
            }
            else
                return null;
        }
    }
}
