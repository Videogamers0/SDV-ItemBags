﻿using ItemBags.Helpers;
using ItemBags.Menus;
using ItemBags.Persistence;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Object = StardewValley.Object;

namespace ItemBags.Bags
{
    /// <summary>A bag that can store other bags inside of it.</summary>
    public class OmniBag : ItemBag
    {
        public const string OmniBagTypeId = "6eb4c15d-3ad3-4b47-aab5-eb2f5daa8b3f";

        public List<ItemBag> NestedBags { get; }

        /// <summary>Default parameterless constructor intended for use by XML Serialization. Do not use this constructor to instantiate a bag.</summary>
        protected OmniBag() : base(ItemBagsMod.Translate("OmniBagName"), ItemBagsMod.Translate("OmniBagDescription"), ContainerSize.Small, null, null, new Vector2(16, 16), 0.5f, 1f)
        {
            string SizeName = ItemBagsMod.Translate(string.Format("Size{0}Name", Size.GetDescription()));
            DescriptionAlias = string.Format("{0}\n({1})\n({2})",
                ItemBagsMod.Translate("OmniBagDescription"),
                ItemBagsMod.Translate("CapacityDescription", new Dictionary<string, string>() { { "count", MaxStackSize.ToString() } }),
                ItemBagsMod.Translate("OmniBagCapacityDescription", new Dictionary<string, string>() { { "size", SizeName } })
            );

            this.NestedBags = new List<ItemBag>();

            LoadTextures();
        }

        public OmniBag(ContainerSize Size)
            : base(ItemBagsMod.Translate("OmniBagName"), ItemBagsMod.Translate("OmniBagDescription"), Size, null, null, new Vector2(16, 16), 0.5f, 1f)
        {
            string SizeName = ItemBagsMod.Translate(string.Format("Size{0}Name", Size.GetDescription()));
            DescriptionAlias = string.Format("{0}\n({1})\n({2})",
                ItemBagsMod.Translate("OmniBagDescription"),
                ItemBagsMod.Translate("CapacityDescription", new Dictionary<string, string>() { { "count", MaxStackSize.ToString() } }),
                ItemBagsMod.Translate("OmniBagCapacityDescription", new Dictionary<string, string>() { { "size", SizeName } })
            );

            this.NestedBags = new List<ItemBag>();

            LoadTextures();
        }

        public OmniBag(BagInstance SavedData, Dictionary<string, BagType> IndexedBagTypes)
            : this(SavedData.Size)
        {
            this.NestedBags = new List<ItemBag>();
            foreach (BagInstance NestedInstance in SavedData.NestedBags)
            {
                if (NestedInstance.TryDecode(IndexedBagTypes, out ItemBag NestedBag))
                {
                    this.NestedBags.Add(NestedBag);
                }
            }

            if (SavedData.IsCustomIcon)
            {
                this.Icon = Game1.objectSpriteSheet;
                this.IconTexturePosition = SavedData.OverriddenIcon;
            }
        }

        /// <summary>The 16x16 portion of <see cref="CursorsTexture"/> that contains the omnibag icon</summary>
        private static Texture2D OriginalTexture { get; set; }
        /// <summary><see cref="OriginalTexture"/>, converted to Grayscale</summary>
        private static Texture2D GrayscaleTexture { get; set; }

        private void LoadTextures()
        {
            if (OriginalTexture == null || OriginalTexture.IsDisposed)
            {
                //48 640 16x16 LooseSprites/cursors.xnb
                Rectangle SourceRect = new Rectangle(48, 640, 16, 16);
                int PixelCount = SourceRect.Width * SourceRect.Height;
                Color[] PixelData = new Color[PixelCount];
                CursorsTexture.GetData(0, SourceRect, PixelData, 0, PixelCount);
                OriginalTexture = new Texture2D(Game1.graphics.GraphicsDevice, SourceRect.Width, SourceRect.Height);
                OriginalTexture.SetData(PixelData);
            }

            if (GrayscaleTexture == null || GrayscaleTexture.IsDisposed)
            {
                GrayscaleTexture = ToGrayScaledPalettable(OriginalTexture, new Rectangle(0, 0, OriginalTexture.Width, OriginalTexture.Height), false, 0);
            }
        }

        public override void drawInMenu(SpriteBatch spriteBatch, Vector2 location, float scaleSize, float transparency, float layerDepth, StackDrawType drawStackNumber, Color color, bool drawShadow)
        {
            DrawInMenu(GrayscaleTexture, null, new Vector2(16 - GrayscaleTexture.Width, 16 - GrayscaleTexture.Height) * 2f, 1f, spriteBatch, location, scaleSize, transparency, layerDepth, drawStackNumber, color, drawShadow);
        }

        public override bool IsUsingDefaultIcon() { return this.Icon == null; }
        public override void ResetIcon() { this.Icon = null; }

        public override bool IsValidBagObject(Object Item) { return false; }
        public bool IsValidBag(ItemBag IB) { return IB != null && !(IB is OmniBag) && IB.Size <= this.Size; }

        public override int GetPurchasePrice() { return ItemBagsMod.UserConfig.GetOmniBagPrice(Size); }
        public override string GetTypeId() { return OmniBagTypeId; }
        public override bool IsFull(Object Item) { return true; }
        protected override int GetMaxStackSize(Object Item) { return 0; }
        public override int MaxStackSize { get { return 1; } }

        protected override ItemBagMenu CreateMenu(IList<Item> InventorySource, int ActualCapacity)
        {
            ItemBagsMod.UserConfig.GetOmniBagMenuOptions(Size, out int NumColumns, out int SlotSize);
            return new OmniBagMenu(this, InventorySource, ActualCapacity, 12, BagInventoryMenu.DefaultInventoryIconSize, NumColumns, SlotSize, true);
        }

        public bool MoveToBag(ItemBag Bag, bool PlaySoundEffect, IList<Item> Source, bool NotifyIfContentsChanged)
        {
            if (!IsValidBag(Bag) || NestedBags.Any(x => x.GetTypeId() == Bag.GetTypeId()) || !Source.Contains(Bag))
            {
                if (PlaySoundEffect)
                    Game1.playSound(MoveContentsFailedSound);
                return false;
            }
            else
            {
                NestedBags.Add(Bag);
                Source[Source.IndexOf(Bag)] = null;

                if (NotifyIfContentsChanged)
                    OnContentsChanged?.Invoke(this, EventArgs.Empty);
                if (PlaySoundEffect)
                    Game1.playSound(MoveContentsSuccessSound);
                return true;
            }
        }

        public bool MoveFromBag(ItemBag Bag, bool PlaySoundEffect, IList<Item> Target, int ActualTargetCapacity, bool NotifyIfContentsChanged)
        {
            if (Bag == null || !NestedBags.Contains(Bag))
            {
                if (PlaySoundEffect)
                    Game1.playSound(MoveContentsFailedSound);
                return false;
            }
            else
            {
                //  Find index to place this bag at in the target list
                int TargetCapacity = Math.Max(ActualTargetCapacity, Target.Count);
                int ItemIndex = -1;
                for (int i = 0; i < TargetCapacity; i++)
                {
                    if (i >= Target.Count || Target[i] == null)
                    {
                        ItemIndex = i;
                        break;
                    }
                }

                if (ItemIndex >= 0)
                {
                    //  Put bag in target, remove from source
                    if (ItemIndex >= Target.Count)
                        Target.Add(Bag);
                    else
                        Target[ItemIndex] = Bag;
                    NestedBags.Remove(Bag);

                    if (NotifyIfContentsChanged)
                        OnContentsChanged?.Invoke(this, EventArgs.Empty);
                    if (PlaySoundEffect)
                        Game1.playSound(MoveContentsSuccessSound);
                    return true;
                }
                else
                {
                    if (PlaySoundEffect)
                        Game1.playSound(MoveContentsFailedSound);
                    return false;
                }
            }
        }

        public override bool TryRemoveInvalidItems(IList<Item> Target, int ActualTargetCapacity)
        {
            bool ChangesMade = base.TryRemoveInvalidItems(Target, ActualTargetCapacity);

            //  Group the Nested Bags by their type
            //  and only keep the largest size of each unique type id
            foreach (IGrouping<string, ItemBag> Group in NestedBags.GroupBy(x => x.GetTypeId()))
            {
                string TypeId = Group.Key;
                List<ItemBag> Instances = Group.ToList();

                int RemovedCount = 0;
                if (TypeId == OmniBagTypeId)
                {
                    RemovedCount = NestedBags.RemoveAll(x => Instances.Contains(x));
                }
                else if (Instances.Count > 1)
                {
                    ItemBag ToKeep = Instances.OrderByDescending(x => x.Size).FirstOrDefault(x => x.Size <= this.Size);
                    RemovedCount = NestedBags.RemoveAll(x => Instances.Contains(x) && x != ToKeep);
                }

                if (RemovedCount > 0)
                    ChangesMade = true;
            }

            return ChangesMade;
        }

        //Possible TODO: Override this and draw all of the bag types if in a shopmenu
        //public override void drawTooltip(SpriteBatch spriteBatch, ref int x, ref int y, SpriteFont font, float alpha, string overrideText)
    }
}