﻿using ItemBags.Helpers;
using ItemBags.Menus;
using ItemBags.Persistence;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Object = StardewValley.Object;

namespace ItemBags.Bags
{
    /// <summary>A bag that can store other bags inside of it.</summary>
    [XmlType("Mods_OmniBag")]
    [XmlRoot(ElementName = "OmniBag", Namespace = "")]
    public class OmniBag : ItemBag
    {
        public const string OmniBagTypeId = "6eb4c15d-3ad3-4b47-aab5-eb2f5daa8b3f";

        [XmlArray("NestedBag")]
        [XmlArrayItem("NestedBag")]
        public List<ItemBag> NestedBags { get; set; }
        public override bool IsEmpty() { return base.IsEmpty() && (NestedBags == null || NestedBags.All(x => x.IsEmpty())); }

#region Lookup Anything Compatibility
        /// <summary>
        /// This property is only intended as read-only data, for use with the Lookup Anything mod (See also: https://github.com/Pathoschild/StardewMods/tree/develop/LookupAnything#extensibility-for-modders) 
        /// <para/>If you intend to modify the contents of the chest, use <see cref="MoveToBag(Object, int, out int, bool, IList{Item}, bool, bool)"/> or <see cref="MoveFromBag(Object, int, out int, bool, IList{Item}, int, bool, bool)"/>
        /// </summary>
        [XmlIgnore]
        public override Chest heldObject { get { return new Chest(NestedBags.SelectMany(x => x.Contents).Where(x => x != null).Cast<Item>().ToList(), Vector2.Zero); } }
#endregion Lookup Anything Compatibility

        /// <summary>Default parameterless constructor intended for use by XML Serialization. Do not use this constructor to instantiate a bag.</summary>
        public OmniBag() : base(ItemBagsMod.Translate("OmniBagName"), ItemBagsMod.Translate("OmniBagDescription"), ContainerSize.Small, null, null, new Vector2(16, 16), 0.5f, 1f)
        {
            UpdateDescription();
            OnSizeChanged += OmniBag_OnSizeChanged;

            this.NestedBags = new List<ItemBag>();
        }

        private void OmniBag_OnSizeChanged(object sender, EventArgs e) => UpdateDescription();

        private void UpdateDescription()
        {
            string SizeName = ItemBagsMod.Translate(string.Format("Size{0}Name", Size.GetDescription()));
            DescriptionAlias = string.Format("{0}\n({1})\n({2})",
                ItemBagsMod.Translate("OmniBagDescription"),
                ItemBagsMod.Translate("CapacityDescription", new Dictionary<string, string>() { { "count", MaxStackSize.ToString() } }),
                ItemBagsMod.Translate("OmniBagCapacityDescription", new Dictionary<string, string>() { { "size", SizeName } })
            );
        }

        public OmniBag(ContainerSize Size)
            : base(ItemBagsMod.Translate("OmniBagName"), ItemBagsMod.Translate("OmniBagDescription"), Size, null, null, new Vector2(16, 16), 0.5f, 1f)
        {
            UpdateDescription();
            OnSizeChanged += OmniBag_OnSizeChanged;

            this.NestedBags = new List<ItemBag>();
        }

        public OmniBag(BagInstance SavedData)
            : this(SavedData.Size)
        {
            this.NestedBags = new List<ItemBag>();
            foreach (BagInstance NestedInstance in SavedData.NestedBags)
            {
                if (NestedInstance.TryDecode(out ItemBag NestedBag))
                {
                    this.NestedBags.Add(NestedBag);
                }
            }

            if (SavedData.IsCustomIcon)
            {
                this.CustomIconSourceTexture = BagType.SourceTexture.SpringObjects;
                this.CustomIconTexturePosition = SavedData.OverriddenIcon;
            }
        }

        /// <summary>Returns the <see cref="NestedBags"/> in the same order that they would appear on the <see cref="OmniBagMenu"/></summary>
        public IList<ItemBag> GetOrderedBags()
        {
            List<ItemBag> OrderedBags = new List<ItemBag>();

            Dictionary<string, ItemBag> BagsByTypeId = NestedBags.Where(x => x != null).ToDictionary(x => x.GetTypeId());
            if (BagsByTypeId.TryGetValue(BundleBag.BundleBagTypeId, out ItemBag NestedBundleBag))
                OrderedBags.Add(NestedBundleBag);
            if (BagsByTypeId.TryGetValue(Rucksack.RucksackTypeId, out ItemBag NestedRucksack))
                OrderedBags.Add(NestedRucksack);
            foreach (BagType BagType in ItemBagsMod.BagConfig.BagTypes)
            {
                if (BagsByTypeId.TryGetValue(BagType.Id, out ItemBag NestedBag))
                    OrderedBags.Add(NestedBag);
            }

            return OrderedBags;
        }

        protected override void LoadSettings(BagInstance Data)
        {
            if (Data != null)
            {
                this.Size = Data.Size;

                string SizeName = ItemBagsMod.Translate(string.Format("Size{0}Name", Size.GetDescription()));
                DescriptionAlias = string.Format("{0}\n({1})\n({2})",
                    ItemBagsMod.Translate("OmniBagDescription"),
                    ItemBagsMod.Translate("CapacityDescription", new Dictionary<string, string>() { { "count", MaxStackSize.ToString() } }),
                    ItemBagsMod.Translate("OmniBagCapacityDescription", new Dictionary<string, string>() { { "size", SizeName } })
                );

                this.NestedBags.Clear();
                foreach (BagInstance NestedInstance in Data.NestedBags)
                {
                    if (NestedInstance.TryDecode(out ItemBag NestedBag))
                    {
                        this.NestedBags.Add(NestedBag);
                    }
                }

                Contents.Clear();
                foreach (BagItem Item in Data.Contents)
                {
                    this.Contents.Add(Item.ToObject());
                }

                if (Data.IsCustomIcon)
                {
                    this.CustomIconSourceTexture = BagType.SourceTexture.SpringObjects;
                    this.CustomIconTexturePosition = Data.OverriddenIcon;
                }
                else
                {
                    ResetIcon();
                }
            }
        }

        /// <summary>The 16x16 texture that contains the omnibag icon</summary>
        [XmlIgnore]
        private static Texture2D OriginalTexture { get; set; }
        /// <summary><see cref="OriginalTexture"/>, converted to Grayscale</summary>
        [XmlIgnore]
        private static Texture2D GrayscaleTexture { get; set; }

        internal static void OnGameLaunched()
        {
            if (OriginalTexture == null || OriginalTexture.IsDisposed)
            {
#if LEGACY_CODE
                //48 640 16x16 LooseSprites/cursors.xnb
                Rectangle SourceRect = new Rectangle(48, 640, 16, 16);
                int PixelCount = SourceRect.Width * SourceRect.Height;
                Color[] PixelData = new Color[PixelCount];
                CursorsTexture.GetData(0, SourceRect, PixelData, 0, PixelCount);
#else
                Rectangle SourceRect = new Rectangle(0, 0, 16, 16);
                int PixelCount = SourceRect.Width * SourceRect.Height;
                Color[] PixelData = new Color[PixelCount];
                Texture2D Texture = ItemBagsMod.ModInstance.Helper.ModContent.Load<Texture2D>("assets/omnibag_icon.png");
                Texture.GetData(0, SourceRect, PixelData, 0, PixelCount);
#endif
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
            if (GrayscaleTexture?.IsDisposed == false)
                DrawInMenu(GrayscaleTexture, null, new Vector2(16 - GrayscaleTexture.Width, 16 - GrayscaleTexture.Height) * 2f, 1f, spriteBatch, location, scaleSize, transparency, layerDepth, drawStackNumber, color, drawShadow);
        }

        public override void ResetIcon()
        {
            this.DefaultIconTexture = null;
            this.DefaultIconTexturePosition = new Rectangle();
            this.CustomIconSourceTexture = null;
            this.CustomIconTexturePosition = null;
        }

        public override bool IsValidBagObject(Object Item) { return false; }
        public bool IsValidBag(ItemBag IB) { return IB != null && !(IB is OmniBag) && IB.Size <= this.Size; }

        public override int GetPurchasePrice() { return ItemBagsMod.UserConfig.GetOmniBagPrice(Size); }
        public override string GetTypeId() { return OmniBagTypeId; }
        public override bool IsFull(Object Item) { return true; }
        protected override int GetMaxStackSize(Object Item) { return 0; }
        [XmlIgnore]
        public override int MaxStackSize { get { return 1; } }

        protected override ItemBagMenu CreateMenu(IList<Item> InventorySource, int ActualCapacity, int? InventoryColumns)
        {
            try
            {
                ItemBagMenu Menu = new ItemBagMenu(this, InventorySource, ActualCapacity, InventoryColumns ?? 12, BagInventoryMenu.DefaultInventoryIconSize);
                ItemBagsMod.UserConfig.GetOmniBagMenuOptions(Size, out int NumColumns, out int SlotSize);
                Menu.Content = new OmniBagMenu(Menu, this, NumColumns, SlotSize, true, 12);
                return Menu;
            }
            catch (Exception ex)
            {
                ItemBagsMod.ModInstance.Monitor.Log(string.Format("Unhandled error while creating OmniBagMenu: {0}", ex.Message), LogLevel.Error);
                return null;
            }
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
                Resync();
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
                    Resync();
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
            foreach (IGrouping<string, ItemBag> Group in NestedBags.GroupBy(x => x.GetTypeId() ?? "Invalid"))
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
                {
                    ChangesMade = true;
                    Resync();
                }
            }

            return ChangesMade;
        }

        protected override Item GetOneNew() => new OmniBag(Size);
        protected override void GetOneCopyFrom(Item source)
        {
            base.GetOneCopyFrom(source);
            if (source is OmniBag bag)
            {
                Size = bag.Size;
                NestedBags = bag.NestedBags;
            }
        }

        //Possible TODO: Override this and draw all of the bag types if in a shopmenu
        //public override void drawTooltip(SpriteBatch spriteBatch, ref int x, ref int y, SpriteFont font, float alpha, string overrideText)
    }
}
