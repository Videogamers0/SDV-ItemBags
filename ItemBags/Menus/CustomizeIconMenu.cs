using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemBags.Bags;
using ItemBags.Helpers;
using ItemBags.Persistence;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using StardewValley;
using Object = StardewValley.Object;

namespace ItemBags.Menus
{
    public class CustomizeIconMenu
    {
        public ItemBagMenu BagMenu { get; }
        public ItemBag Bag { get; }
        private ItemBag PreviewBag { get; }
        public Rectangle Bounds { get; private set; }

        private Texture2D Texture { get; }
        private Rectangle TextureDestination { get; set; }
        private const int TextureWidth = 384;
        private const int TextureHeight = 544;
        private const int SpriteSize = 16;

        private const int Padding = 24;
        private const int Spacing = 8;
        private const int ButtonPadding = 6;
        private const int PreviewSize = (int)(BagInventoryMenu.DefaultInventoryIconSize * 1.5);
        private Rectangle PreviewDestination { get; set; }

        private SpriteFont InstructionFont { get { return Game1.smallFont; } }
        private const float InstructionScale = 1.0f;
        private static string InstructionText { get { return ItemBagsMod.Translate("CustomizeIconInstructions"); } }
        private Vector2 InstructionDestination { get; set; }

        private SpriteFont ButtonFont { get { return Game1.smallFont; } }
        private const float ButtonFontScale = 0.5f;
        private static string DefaultButtonText { get { return ItemBagsMod.Translate("UseDefaultIconButtonText"); } }
        private Rectangle DefaultButtonDestination { get; set; }
        private Vector2 DefaultButtonTextDestination { get; set; }
        private static  string CloseButtonText { get { return ItemBagsMod.Translate("CloseButtonText"); } }
        private Rectangle CloseButtonDestination { get; set; }
        private Vector2 CloseButtonTextDestination { get; set; }

        private Rectangle? _HoveredSprite;
        /// <summary>Relative to topleft position of <see cref="TextureDestination"/></summary>
        private Rectangle? HoveredSprite
        {
            get { return _HoveredSprite; }
            set
            {
                if (HoveredSprite != value)
                {
                    _HoveredSprite = value;
                    if (HoveredSprite.HasValue)
                        this.PreviewBag.IconTexturePosition = HoveredSprite.Value;
                }
            }
        }

        public int? HoveredSpriteIndex
        {
            get
            {
                if (!HoveredSprite.HasValue)
                    return null;
                else
                {
                    int ItemsPerRow = TextureWidth / SpriteSize;
                    int HoveredRow = HoveredSprite.Value.Top / SpriteSize;
                    int HoveredColumn = HoveredSprite.Value.Left / SpriteSize;
                    return HoveredRow * ItemsPerRow + HoveredColumn;
                }
            }
        }

        public Object HoveredObject { get { return HoveredSpriteIndex.HasValue ? new Object(HoveredSpriteIndex.Value, 1) : null; } }

        private Rectangle? HoveredButton { get; set; }

        /// <param name="PreviewableCopy">A copy of Parameter=<paramref name="Bag"/>. Changes will be made to this copy while the user is previewing different icons.</param>
        public CustomizeIconMenu(ItemBagMenu BagMenu, ItemBag Bag, ItemBag PreviewableCopy)
        {
            this.Texture = Game1.objectSpriteSheet;

            this.BagMenu = BagMenu;
            this.Bag = Bag;

            this.PreviewBag = PreviewableCopy;
            this.PreviewBag.Icon = this.Texture;
            this.PreviewBag.IconTexturePosition = Bag.IconTexturePosition;

            InitializeLayout();
        }

        private static int Max(params int[] values) { return Enumerable.Max(values); }

        internal void InitializeLayout()
        {
            int WindowWidth = Game1.viewport.Size.Width;
            int WindowHeight = Game1.viewport.Size.Height;

            Vector2 InstructionSize = InstructionFont.MeasureString(InstructionText) * InstructionScale;
            Vector2 DefaultButtonTextSize = ButtonFont.MeasureString(DefaultButtonText) * ButtonFontScale;
            Vector2 CloseButtonTextSize = ButtonFont.MeasureString(CloseButtonText) * ButtonFontScale;

            int Width = Max(PreviewSize, (int)InstructionSize.X, TextureWidth) + Padding * 2;
            int Height = Padding + PreviewSize + Spacing + (int)InstructionSize.Y + Spacing + TextureHeight + Spacing + 
                (int)Math.Max(DefaultButtonTextSize.Y + ButtonPadding * 2, CloseButtonTextSize.Y + ButtonPadding * 2) + Padding;

            this.Bounds = new Rectangle((WindowWidth - Width) / 2, (WindowHeight - Height) / 2, Width, Height);

            this.PreviewDestination = new Rectangle(Bounds.X + (Bounds.Width - PreviewSize) / 2, Bounds.Y + Padding, PreviewSize, PreviewSize);
            this.InstructionDestination = new Vector2(Bounds.X + (Bounds.Width - (int)InstructionSize.X) / 2, PreviewDestination.Bottom + Spacing);
            this.TextureDestination = new Rectangle(Bounds.X + (Bounds.Width - TextureWidth) / 2, (int)(InstructionDestination.Y + InstructionSize.Y + Spacing), TextureWidth, TextureHeight);

            this.DefaultButtonDestination = new Rectangle(TextureDestination.X, TextureDestination.Bottom + Spacing, 
                (int)DefaultButtonTextSize.X + ButtonPadding * 2 * 2, (int)DefaultButtonTextSize.Y + ButtonPadding * 2);
            this.DefaultButtonTextDestination = new Vector2(DefaultButtonDestination.X + ButtonPadding * 2, DefaultButtonDestination.Y + ButtonPadding + 2);
            int CloseButtonWidth = (int)CloseButtonTextSize.X + ButtonPadding * 2 * 2;
            this.CloseButtonDestination = new Rectangle(TextureDestination.Right - CloseButtonWidth, TextureDestination.Bottom + Spacing, 
                CloseButtonWidth, (int)CloseButtonTextSize.Y + ButtonPadding * 2);
            this.CloseButtonTextDestination = new Vector2(CloseButtonDestination.X + ButtonPadding * 2, CloseButtonDestination.Y + ButtonPadding + 2);
        }

        internal void OnMouseMoved(CursorMovedEventArgs e)
        {
            Point NewPos = e.NewPosition.ScreenPixels.AsPoint();
            if (TextureDestination.Contains(NewPos))
            {
                Point RelativePos = new Point(NewPos.X - TextureDestination.X, NewPos.Y - TextureDestination.Y);
                Rectangle? Previous = HoveredSprite;
                Rectangle Current = new Rectangle(RelativePos.X / SpriteSize * SpriteSize, RelativePos.Y / SpriteSize * SpriteSize, SpriteSize, SpriteSize);
                if (!Previous.HasValue || Previous.Value.X != Current.X || Previous.Value.Y != Current.Y)
                    this.HoveredSprite = Current;
            }
            else
            {
                HoveredSprite = null;
            }

            if (DefaultButtonDestination.Contains(NewPos))
                this.HoveredButton = DefaultButtonDestination;
            else if (CloseButtonDestination.Contains(NewPos))
                this.HoveredButton = CloseButtonDestination;
            else
                this.HoveredButton = null;
        }

        internal void OnMouseButtonPressed(ButtonPressedEventArgs e)
        {
            if (!Bounds.Contains(e.Cursor.ScreenPixels.AsPoint()))
                BagMenu.CloseModalMenu();
            else
            {
                if (HoveredSprite.HasValue)
                {
                    Bag.Icon = this.Texture;
                    Bag.IconTexturePosition = HoveredSprite.Value;
                    BagMenu.CloseModalMenu();
                }
                else if (HoveredButton.HasValue)
                {
                    if (HoveredButton.Value == DefaultButtonDestination)
                    {
                        Bag.ResetIcon();
                    }
                    BagMenu.CloseModalMenu();
                }
            }
        }

        internal void OnMouseButtonReleased(ButtonReleasedEventArgs e) { }
        internal void Update(UpdateTickedEventArgs e) { }

        internal void Draw(SpriteBatch b)
        {
            Color HighlightColor = Color.Yellow;
            Texture2D Highlight = TextureHelpers.GetSolidColorTexture(Game1.graphics.GraphicsDevice, HighlightColor);

            DrawHelpers.DrawBox(b, Bounds);

            //  Draw preview image
            b.Draw(Game1.menuTexture, PreviewDestination, new Rectangle(128, 128, 64, 64), Color.White);
            ItemBag CurrentBag = HoveredSprite.HasValue ? PreviewBag : Bag;
            DrawHelpers.DrawItem(b, PreviewDestination, CurrentBag, false, false, 1f, 1f, Color.White, Color.White);

            //  Draw instructional text
            b.DrawString(InstructionFont, InstructionText, InstructionDestination, Color.Black, 0f, Vector2.Zero, InstructionScale, SpriteEffects.None, 1f);

            //  Draw spritesheet with all icons to choose from
            DrawHelpers.DrawBorder(b, new Rectangle(TextureDestination.Left - 2, TextureDestination.Top - 2, TextureDestination.Width + 4, TextureDestination.Height + 4), 2, Color.Black);
            b.Draw(Texture, TextureDestination, new Rectangle(0, 0, TextureWidth, TextureHeight), Color.White);
            if (HoveredSprite.HasValue)
            {
                Rectangle Destination = HoveredSprite.Value.GetOffseted(new Point(TextureDestination.X, TextureDestination.Y));
                b.Draw(Highlight, Destination, Color.White * 0.25f);
                int BorderThickness = Math.Max(2, HoveredSprite.Value.Width / SpriteSize);
                DrawHelpers.DrawBorder(b, Destination, BorderThickness, HighlightColor);
            }

            //  Draw buttons
            b.Draw(Game1.menuTexture, DefaultButtonDestination, new Rectangle(128, 128, 64, 64), Color.White);
            b.DrawString(ButtonFont, DefaultButtonText, DefaultButtonTextDestination, Color.Black, 0f, Vector2.Zero, ButtonFontScale, SpriteEffects.None, 1f);
            b.Draw(Game1.menuTexture, CloseButtonDestination, new Rectangle(128, 128, 64, 64), Color.White);
            b.DrawString(ButtonFont, CloseButtonText, CloseButtonTextDestination, Color.Black, 0f, Vector2.Zero, ButtonFontScale, SpriteEffects.None, 1f);
            if (HoveredButton.HasValue)
            {
                Rectangle Destination = HoveredButton.Value;
                b.Draw(Highlight, Destination, Color.White * 0.25f);
                int BorderThickness = Math.Max(2, HoveredButton.Value.Width / 16);
                DrawHelpers.DrawBorder(b, Destination, BorderThickness, HighlightColor);
            }
        }
    }
}
