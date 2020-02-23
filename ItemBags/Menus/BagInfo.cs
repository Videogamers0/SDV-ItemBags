using ItemBags.Bags;
using ItemBags.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI.Events;
using StardewValley;
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
    public class BagInfo
    {
        public ItemBag Bag { get; }

        /// <summary>Relative bounds of the bag's main icon</summary>
        public Rectangle RelativeIconBounds { get; private set; }
        /// <summary>Bounds of the bag's main icon</summary>
        public Rectangle IconBounds { get; private set; }

        /// <summary>Relative bounds of the bag's maximum capacity (includes the icon and number)</summary>
        public Rectangle RelativeCapacityBounds { get; private set; }
        /// <summary>Bounds of the bag's maximum capacity (includes the icon and number)</summary>
        public Rectangle CapacityBounds { get; private set; }
        /// <summary>The Texture that contains the sprite that is drawn next to the bag's capacity</summary>
        private Texture2D CapacityIconSheet { get { return Game1.mouseCursors; } }
        /// <summary>The position of the sprite in <see cref="CapacityIconSheet"/> that is drawn next to the bag's capacity</summary>
        private Rectangle CapacityIconPosition { get { return new Rectangle(127, 412, 10, 11); } }
        private const float CapacityIconScale = 2f;

        /// <summary>Relative bounds of the total value of the bag's contents (includes the icon and number)</summary>
        public Rectangle RelativeValueBounds { get; private set; }
        /// <summary>Bounds of the total value of the bag's contents (includes the icon and number)</summary>
        public Rectangle ValueBounds { get; private set; }
        /// <summary>The Texture that contains the sprite that is drawn next to the bag's total value</summary>
        private Texture2D ValueIconSheet { get { return TextureHelpers.EmojiSpritesheet; } }
        /// <summary>The position of the sprite in <see cref="ValueIconSheet"/> that is drawn next to the bag's total value</summary>
        private Rectangle ValueIconPosition { get { return new Rectangle(117, 18, 9, 9); } }
        private const float ValueIconScale = 2f;

        /// <summary>Scale to use when rendering capacity and value numbers</summary>
        private const float NumberScale = 2f;
        /// <summary>Padding, in pixels, to use between icons and their values when arranging the layout</summary>
        private const int MarginBetweenIconAndNumber = 4;
        /// <summary>Padding, in pixels, to use between row of data when arranging the layout</summary>
        private const int MarginBetweenRows = 6;

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

                IconBounds = RelativeIconBounds.GetOffseted(TopLeftScreenPosition);
                CapacityBounds = RelativeCapacityBounds.GetOffseted(TopLeftScreenPosition);
                ValueBounds = RelativeValueBounds.GetOffseted(TopLeftScreenPosition);
                Bounds = RelativeBounds.GetOffseted(TopLeftScreenPosition);
            }
        }

        /// <summary>The bounds of this menu's content, relative to <see cref="TopLeftScreenPosition"/></summary>
        public Rectangle RelativeBounds { get; private set; }
        public Rectangle Bounds { get; private set; }

        public BagInfo(ItemBag Bag)
        {
            this.Bag = Bag;
            SetTopLeft(Point.Zero, false);
            InitializeLayout();
        }

        private static int Min(params int[] values) { return Enumerable.Min(values); }
        private static int Max(params int[] values) { return Enumerable.Max(values); }

        public void InitializeLayout()
        {
            int LeftMargin = 20;
            int TopMargin = 12;

            int BaseIconSize = 64;
            int ActualIconSize = (int)(BaseIconSize * 0.8f);

            int CapacityWidth = (int)(CapacityIconPosition.Width * CapacityIconScale + MarginBetweenIconAndNumber + DrawHelpers.MeasureNumber(Bag.MaxStackSize, NumberScale));
            int CapacityHeight = (int)Math.Max(CapacityIconPosition.Height * CapacityIconScale, DrawHelpers.TinyDigitBaseHeight * NumberScale);

            int RequiredDigits = Math.Max(5, DrawHelpers.GetNumDigits(ItemBag.GetSingleItemPrice(Bag)));
            int ValueWidth = (int)(ValueIconPosition.Width * ValueIconScale + MarginBetweenIconAndNumber + DrawHelpers.TinyDigitBaseWidth * RequiredDigits * NumberScale);
            int ValueHeight = (int)Math.Max(ValueIconPosition.Height * ValueIconScale, DrawHelpers.TinyDigitBaseHeight * NumberScale);

            int RequiredWidth = LeftMargin + Max(ActualIconSize, CapacityWidth, ValueWidth);
            int RequiredHeight = TopMargin + ActualIconSize + MarginBetweenRows + CapacityHeight + MarginBetweenRows + ValueHeight;
            this.RelativeBounds = new Rectangle(0, 0, RequiredWidth, RequiredHeight);

            int CurrentYPosition = TopMargin;
            //this.RelativeIconBounds = new Rectangle((RelativeBounds.Width - IconSize) / 2, CurrentYPosition, IconSize, IconSize);
            this.RelativeIconBounds = new Rectangle(RelativeBounds.X + LeftMargin, CurrentYPosition, BaseIconSize, BaseIconSize);
            CurrentYPosition += ActualIconSize + MarginBetweenRows;
            //this.RelativeCapacityBounds = new Rectangle((RelativeBounds.Width - CapacityWidth) / 2, CurrentYPosition, CapacityWidth, CapacityHeight);
            this.RelativeCapacityBounds = new Rectangle(RelativeBounds.X + LeftMargin, CurrentYPosition, CapacityWidth, CapacityHeight);
            CurrentYPosition += CapacityHeight + MarginBetweenRows;
            //this.RelativeValueBounds = new Rectangle((RelativeBounds.Width - ValueWidth) / 2, CurrentYPosition, ValueWidth, ValueHeight);
            this.RelativeValueBounds = new Rectangle(RelativeBounds.X + LeftMargin, CurrentYPosition, ValueWidth, ValueHeight);
        }

        public void Draw(SpriteBatch b)
        {
            //b.Draw(TextureHelpers.GetSolidColorTexture(Game1.graphics.GraphicsDevice, Color.Red), Bounds, Color.White);

            DrawHelpers.DrawItem(b, IconBounds, Bag, false, false, 1f, 1f, Color.White, Color.White);

            int CapacityIconWidth = (int)(CapacityIconPosition.Width * CapacityIconScale);
            int CapacityIconHeight = (int)(CapacityIconPosition.Height * CapacityIconScale);
            Rectangle CapacityIconDestination = new Rectangle(CapacityBounds.X, CapacityBounds.Y + (CapacityBounds.Height - CapacityIconHeight) / 2, CapacityIconWidth, CapacityIconHeight);
            b.Draw(CapacityIconSheet, CapacityIconDestination, CapacityIconPosition, Color.White);

            float CapacityValueHeight = DrawHelpers.TinyDigitBaseHeight * NumberScale;
            Vector2 CapacityValueDestination = new Vector2(CapacityBounds.X + CapacityIconWidth + MarginBetweenIconAndNumber, CapacityBounds.Y + (CapacityBounds.Height - CapacityValueHeight) / 2);
            Utility.drawTinyDigits(Bag.MaxStackSize, b, CapacityValueDestination, NumberScale, 1f, Color.White);

            int ValueIconWidth = (int)(ValueIconPosition.Width * ValueIconScale);
            int ValueIconHeight = (int)(ValueIconPosition.Height * ValueIconScale);
            Rectangle ValueIconDestination = new Rectangle(ValueBounds.X, ValueBounds.Y + (ValueBounds.Height - CapacityIconHeight) / 2, ValueIconWidth, ValueIconHeight);
            b.Draw(ValueIconSheet, ValueIconDestination, ValueIconPosition, Color.White);

            int TotalValue = ItemBag.GetSingleItemPrice(Bag);
            float ValueNumberHeight = DrawHelpers.TinyDigitBaseHeight * NumberScale;
            Vector2 ValueNumberDestination = new Vector2(ValueBounds.X + ValueIconWidth + MarginBetweenIconAndNumber, ValueBounds.Y + (ValueBounds.Height - ValueNumberHeight) / 2);
            Utility.drawTinyDigits(TotalValue, b, ValueNumberDestination, NumberScale, 1f, Color.White);
        }
    }
}
