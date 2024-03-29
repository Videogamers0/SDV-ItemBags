﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItemBags.Helpers
{
    public static class TextureHelpers
    {
        private static Texture2D _EmojiSpritesheet;
        public static Texture2D EmojiSpritesheet
        {
            get
            {
                if (_EmojiSpritesheet == null || _EmojiSpritesheet.IsDisposed)
                {
                    _EmojiSpritesheet = ItemBagsMod.ModInstance.Helper.GameContent.Load<Texture2D>("LooseSprites/emojis");
                }
                return _EmojiSpritesheet;
            }
        }

        private static Texture2D _JunimoNoteTexture;
        public static Texture2D JunimoNoteTexture
        {
            get
            {
                if (_JunimoNoteTexture == null || _JunimoNoteTexture.IsDisposed)
                {
                    _JunimoNoteTexture = ItemBagsMod.ModInstance.Helper.GameContent.Load<Texture2D>(JunimoNoteMenu.noteTextureName);
                }
                return _JunimoNoteTexture;
            }
        }

        private static Texture2D _PlayerStatusList;
        public static Texture2D PlayerStatusList
        {
            get
            {
                if (_PlayerStatusList == null || _PlayerStatusList.IsDisposed)
                {
                    _PlayerStatusList = ItemBagsMod.ModInstance.Helper.GameContent.Load<Texture2D>("LooseSprites/PlayerStatusList");
                }
                return _PlayerStatusList;
            }
        }

        private static Texture2D _JojaCDForm;
        public static Texture2D JojaCDForm
        {
            get
            {
                if (_JojaCDForm == null || _JojaCDForm.IsDisposed)
                {
                    _JojaCDForm = ItemBagsMod.ModInstance.Helper.GameContent.Load<Texture2D>("LooseSprites/JojaCDForm");
                }
                return _JojaCDForm;
            }
        }

        private static Dictionary<uint, SolidColorTexture> IndexedColorTextures { get; } = new Dictionary<uint, SolidColorTexture>();

        public static SolidColorTexture GetSolidColorTexture(GraphicsDevice GD, Color color)
        {
            if (IndexedColorTextures.TryGetValue(color.PackedValue, out SolidColorTexture ExistingTexture))
                return ExistingTexture;
            else
            {
                SolidColorTexture Texture = new SolidColorTexture(GD, color);
                IndexedColorTextures.Add(color.PackedValue, Texture);
                return Texture;
            }
        }
    }

    /// <summary>Creates a 1x1 pixel texture of a solid color</summary>
    public class SolidColorTexture : Texture2D
    {
        private Color _color;
        public Color Color
        {
            get { return _color; }
            set
            {
                if (value != _color)
                {
                    _color = value;
                    SetData<Color>(new Color[] { _color });
                }
            }
        }

        public SolidColorTexture(GraphicsDevice GraphicsDevice) : base(GraphicsDevice, 1, 1) { }
        public SolidColorTexture(GraphicsDevice GraphicsDevice, Color color)
            : base(GraphicsDevice, 1, 1)
        {
            Color = color;
        }
    }
}
