﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ItemBags.Helpers;
using ItemBags.Menus;
using ItemBags.Persistence;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using PyTK.CustomElementHandler;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using Object = StardewValley.Object;

namespace ItemBags.Bags
{
    [XmlRoot(ElementName = "AutofillPriority", Namespace = "")]
    public enum AutofillPriority
    {
        /// <summary><see cref="BoundedBag"/>s with <see cref="BoundedBag.Autofill"/>=true will be prioritized over <see cref="Rucksack"/>s</summary>
        [XmlEnum("Low")]
        [Description("Low")]
        Low,
        /// <summary><see cref="Rucksack"/>s with <see cref="Rucksack.Autofill"/>=true will be prioritized over <see cref="BoundedBag"/>s</summary>
        [XmlEnum("High")]
        [Description("High")]
        High
    }

    [XmlRoot(ElementName = "SortingMode", Namespace = "")]
    public enum SortingProperty
    {
        /// <summary>Bag contents will appear in the order that they were added to the bag</summary>
        [XmlEnum("Time")]
        [Description("Time")]
        Time,
        [XmlEnum("Name")]
        [Description("Name")]
        Name,
        [XmlEnum("Id")]
        [Description("Id")]
        Id,
        [XmlEnum("Category")]
        [Description("Category")]
        Category,
        [XmlEnum("Quantity")]
        [Description("Quantity")]
        Quantity,
        [XmlEnum("SingleValue")]
        [Description("SingleValue")]
        SingleValue,
        [XmlEnum("StackValue")]
        [Description("StackValue")]
        StackValue,
        /// <summary>Bag contents will be grouped by Id, and the groups will appear in the order that the first item with that Id was added to the bag</summary>
        [XmlEnum("Similarity")]
        [Description("Similarity")]
        Similarity
    }

    [XmlRoot(ElementName = "SortingOrder", Namespace = "")]
    public enum SortingOrder
    {
        [XmlEnum("Ascending")]
        [Description("Ascending")]
        Ascending,
        [XmlEnum("Descending")]
        [Description("Descending")]
        Descending
    }

    /// <summary>A bag that can store most stackable objects.</summary>
    [XmlRoot(ElementName = "Rucksack", Namespace = "")]
    public class Rucksack : ItemBag, ISyncableElement
    {
        public const string RucksackTypeId = "a56bbc00-9d89-4216-8e06-5ea0cfa95525";

        /// <summary>If true, then when the player picks up an item that can be stored in this bag, and there is already an existing stack of the item, 
        /// it will automatically be stored in this bag if there is space for it.<para/>
        /// If multiple <see cref="ItemBag"/> objects can store the item and have Autofill=true, 
        /// then it will first prioritize a <see cref="BundleBag"/>, then <see cref="Rucksack"/> with <see cref="Rucksack.AutofillPriority"/>=<see cref="AutofillPriority.High"/>,<para/>
        /// then standard <see cref="BoundedBag"/>, then <see cref="Rucksack"/> with <see cref="Rucksack.AutofillPriority"/>=<see cref="AutofillPriority.Low"/>.</summary>
        public bool Autofill { get; set; }

        /// <summary>Determines the priority when choosing which bag to fill with a picked up item, when there are multiple bags that can be autofilled.<para/>
        /// Only relevant if <see cref="Autofill"/>=true</summary>
        public AutofillPriority AutofillPriority { get; set; }

        public SortingProperty SortProperty { get; set; }
        public SortingOrder SortOrder { get; set; }

        /// <summary>Cycles Autofill between On (Low Priority), On (High Priority), and Off</summary>
        public void CycleAutofill()
        {
            if (this.Autofill)
            {
                if (this.AutofillPriority == AutofillPriority.Low)
                    this.AutofillPriority = AutofillPriority.High;
                else if (this.AutofillPriority == AutofillPriority.High)
                    this.Autofill = false;
                else
                    throw new NotImplementedException(string.Format("Unexpected AutofillPriority: {0}", AutofillPriority.ToString()));
            }
            else
            {
                this.Autofill = true;
                this.AutofillPriority = AutofillPriority.Low;
            }
        }

        /// <summary>The # of inventory slots within this bag. Note that a single item could take up multiple slots if its <see cref="Object.Stack"/> is > <see cref="ItemBag.MaxStackSize"/>.<para/>
        /// In these cases, <see cref="ItemBag.Contents"/> would still only have 1 instance of the Object. So <see cref="NumSlots"/> does NOT restrict the size of the <see cref="ItemBag.Contents"/> list.</summary>
        public int NumSlots { get; protected set; }

        /// <summary>Returns true if this Bag isn't capable of storing more Quantity of the given Item 
        /// (Either because the Item is not valid for this bag, or because maximum capacity has been reached and there are no empty slots to start a new stack in)</summary>
        public override bool IsFull(Object Item)
        {
            if (!IsValidBagObject(Item))
                return true;
            else
            {
                if (GetNumEmptySlots() > 0)
                {
                    return false;
                }
                else
                {
                    int MaxQty = GetMaxStackSize(Item);
                    if (MaxQty == 0)
                    {
                        return true; // No empty slots remaining, and the existing slots of this item (if any) are all completely filled
                    }
                    else
                    {
                        Object ExistingItem = this.Contents.FirstOrDefault(x => AreItemsEquivalent(x, Item, true));
                        if (ExistingItem == null)// || ExistingItem.maximumStackSize() <= 1)
                            return true;
                        else
                            return ExistingItem.Stack >= MaxQty;
                    }
                }
            }
        }

        private int GetNumEmptySlots()
        {
            //return this.Contents == null ?
            //    NumSlots :
            //    Math.Max(0, NumSlots - this.Contents.Sum(x => GetNumSlots(x.Stack, x.maximumStackSize() > 1)));
            return this.Contents == null ?
                NumSlots :
                Math.Max(0, NumSlots - this.Contents.Sum(x => GetNumSlots(x.Stack)));
        }

        /// <summary>Determines the maximum quantity of the given Item that can be stored within this bag, after accounting for the fact that some or all slots may already be filled.</summary>
        protected override int GetMaxStackSize(Object Item)
        {
            int EmptySlots = GetNumEmptySlots();
            //if (Item.maximumStackSize() > 1)
            //{
            //    Object ExistingItem = Contents.FirstOrDefault(x => AreItemsEqual(Item, x, true));
            //    int ExistingSlots = GetNumSlots(ExistingItem == null ? 0 : ExistingItem.Stack, Item.maximumStackSize() > 1);
            //    return (EmptySlots + ExistingSlots) * MaxStackSize;
            //}
            //else
            //{
            //    return EmptySlots + Contents.Count(x => AreItemsEqual(Item, x, true));
            //}
            Object ExistingItem = Contents.FirstOrDefault(x => AreItemsEquivalent(Item, x, true));
            int ExistingSlots = ExistingItem == null ? 0 : GetNumSlots(ExistingItem.Stack);
            return (EmptySlots + ExistingSlots) * MaxStackSize;
        }

        /// <summary>Returns how many Slots the given Quantity of an item would use up.</summary>
        private int GetNumSlots(int Quantity)//, bool IsStackable = true)
        {
            //int ActualMaxStackSize = IsStackable ? MaxStackSize : 1;
            //return (Quantity - 1) / ActualMaxStackSize + 1;
            return (Quantity - 1) / MaxStackSize + 1;
        }

        public override bool TryRemoveInvalidItems(IList<Item> Target, int ActualTargetCapacity)
        {
            //  Remove items that the bag isn't capable of storing
            bool ChangesMade = base.TryRemoveInvalidItems(Target, ActualTargetCapacity);

            //  Remove items until it's contents are using no more than NumSlots # of slots
            int SlotsInUse = Contents.Sum(x => GetNumSlots(x.Stack));
            while (SlotsInUse > NumSlots && SlotsInUse > 0 && Contents.Any())
            {
                Object ToRemove = Contents.Last();

                //  Reduce the quantity of the item by just enough so that it takes up 1 less Slot
                int CurrentQty = ToRemove.Stack;
                int DesiredQty = CurrentQty % MaxStackSize == 0 ? 
                    CurrentQty - MaxStackSize : 
                    CurrentQty - CurrentQty % MaxStackSize;
                int DecreaseAmt = CurrentQty - DesiredQty;

                if (MoveFromBag(ToRemove, DecreaseAmt, out int MovedQty, false, Target, ActualTargetCapacity))
                    ChangesMade = true;
                else
                    break;

                SlotsInUse = Contents.Sum(x => GetNumSlots(x.Stack));
            }

            return ChangesMade;
        }

        private int _MaxStackSize { get; set; }
        public override int MaxStackSize { get { return _MaxStackSize; } }

        /// <summary>Default parameterless constructor intended for use by XML Serialization. Do not use this constructor to instantiate a bag.</summary>
        public Rucksack() : base(ItemBagsMod.Translate("RucksackName"), ItemBagsMod.Translate("RucksackDescription"), ContainerSize.Small, null, null, new Vector2(16, 16), 0.5f, 1f)
        {
            this.syncObject = new PySync(this);

            this.NumSlots = ItemBagsMod.UserConfig.GetRucksackSlotCount(Size);
            this.Autofill = false;
            this.AutofillPriority = AutofillPriority.Low;
            this.SortProperty = SortingProperty.Similarity;
            this.SortOrder = SortingOrder.Ascending;

            _MaxStackSize = ItemBagsMod.UserConfig.GetRucksackCapacity(Size);

            DescriptionAlias = string.Format("{0}\n({1})",
                ItemBagsMod.Translate("RucksackDescription"),
                ItemBagsMod.Translate("CapacityDescription", new Dictionary<string, string>() { { "count", MaxStackSize.ToString() } }));

            LoadTextures();
        }

        public Rucksack(ContainerSize Size, bool Autofill, AutofillPriority AutofillPriority = AutofillPriority.High, SortingProperty SortProperty = SortingProperty.Time, SortingOrder SortOrder = SortingOrder.Ascending)
            : base(ItemBagsMod.Translate("RucksackName"), ItemBagsMod.Translate("RucksackDescription"), Size, null, null, new Vector2(16, 16), 0.5f, 1f)
        {
            this.syncObject = new PySync(this);

            this.NumSlots = ItemBagsMod.UserConfig.GetRucksackSlotCount(Size);
            this.Autofill = Autofill;
            this.AutofillPriority = AutofillPriority;
            this.SortProperty = SortProperty;
            this.SortOrder = SortOrder;

            _MaxStackSize = ItemBagsMod.UserConfig.GetRucksackCapacity(Size);

            DescriptionAlias = string.Format("{0}\n({1})",
                ItemBagsMod.Translate("RucksackDescription"),
                ItemBagsMod.Translate("CapacityDescription", new Dictionary<string, string>() { { "count", MaxStackSize.ToString() } }));

            LoadTextures();
        }

        public Rucksack(BagInstance SavedData)
            : this(SavedData.Size, SavedData.Autofill, SavedData.AutofillPriority, SavedData.SortProperty, SavedData.SortOrder)
        {
            foreach (BagItem Item in SavedData.Contents)
            {
                this.Contents.Add(Item.ToObject());
            }

            if (SavedData.IsCustomIcon)
            {
                this.Icon = Game1.objectSpriteSheet;
                this.IconTexturePosition = SavedData.OverriddenIcon;
            }
        }

        #region PyTK CustomElementHandler
        public object getReplacement()
        {
            return new Object(169, 1);
        }

        public Dictionary<string, string> getAdditionalSaveData()
        {
            return new BagInstance(-1, this).ToPyTKAdditionalSaveData();
        }

        public void rebuild(Dictionary<string, string> additionalSaveData, object replacement)
        {
            BagInstance Data = BagInstance.FromPyTKAdditionalSaveData(additionalSaveData);
            LoadSettings(Data);
        }

        public PySync syncObject { get; set; }

        public Dictionary<string, string> getSyncData()
        {
            return new BagInstance(-1, this).ToPyTKAdditionalSaveData();
        }

        public void sync(Dictionary<string, string> syncData)
        {
            BagInstance Data = BagInstance.FromPyTKAdditionalSaveData(syncData);
            LoadSettings(Data);
        }

        private void LoadSettings(BagInstance Data)
        {
            if (Data != null)
            {
                this.Size = Data.Size;
                this.Autofill = Data.Autofill;
                this.AutofillPriority = Data.AutofillPriority;
                this.NumSlots = ItemBagsMod.UserConfig.GetRucksackSlotCount(Size);

                this._MaxStackSize = ItemBagsMod.UserConfig.GetRucksackCapacity(Size);

                DescriptionAlias = string.Format("{0}\n({1})",
                    ItemBagsMod.Translate("RucksackDescription"),
                    ItemBagsMod.Translate("CapacityDescription", new Dictionary<string, string>() { { "count", MaxStackSize.ToString() } }));

                this.SortProperty = Data.SortProperty;
                this.SortOrder = Data.SortOrder;

                this.Contents.Clear();
                foreach (BagItem Item in Data.Contents)
                {
                    this.Contents.Add(Item.ToObject());
                }

                if (Data.IsCustomIcon)
                {
                    this.Icon = Game1.objectSpriteSheet;
                    this.IconTexturePosition = Data.OverriddenIcon;
                }
                else
                {
                    ResetIcon();
                }
            }
        }
        #endregion PyTK CustomElementHandler

        /// <summary>The 13x16 portion of <see cref="CursorsTexture"/> that contains the rucksack icon</summary>
        private static Texture2D OriginalTexture { get; set; }
        /// <summary><see cref="OriginalTexture"/>, converted to Grayscale</summary>
        private static Texture2D GrayscaleTexture { get; set; }

        private void LoadTextures()
        {
            if (OriginalTexture == null || OriginalTexture.IsDisposed)
            {
                //268 1436 11x13 LooseSprites/cursors.xnb
                Rectangle SourceRect = new Rectangle(268, 1436, 11, 13);
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
            DrawInMenu(GrayscaleTexture, null, new Vector2(16 - GrayscaleTexture.Width, 16 - GrayscaleTexture.Height) * 2f, 1.3f, spriteBatch, location, scaleSize, transparency, layerDepth, drawStackNumber, color, drawShadow);
        }

        public override bool IsUsingDefaultIcon() { return this.Icon == null; }
        public override void ResetIcon() { this.Icon = null; }

        public override int GetPurchasePrice()
        {
            return ItemBagsMod.UserConfig.GetRucksackPrice(Size);
        }

        public override string GetTypeId() { return RucksackTypeId; }

        private static readonly HashSet<int> BlacklistedItemIds = new HashSet<int>();
        private static readonly HashSet<int> WhitelistedItemIds = new HashSet<int>(new List<int>() {
            486 // Starfruit Seeds (apparently these are classified as "special items" (Item.specialItem=true), not sure why. I'd explicitly disallowed special items thinking they could cause problems.
        });

        public override bool IsValidBagObject(Object Item)
        {
            if (Item != null && WhitelistedItemIds.Contains(Item.ParentSheetIndex))
                return true;

            return base.IsValidBagObject(Item) && !BlacklistedItemIds.Contains(Item.ParentSheetIndex)
                && (!Item.GetType().IsSubclassOf(typeof(Object)) || Item is ColoredObject)
                && !Item.IsRecipe
                && Item.maximumStackSize() > 1; // Possible TODO: Remove this condition, and add logic to account for it in: IsFull, GetNumEmptySlots, GetMaxStackSize, GetNumSlots, MoveToBag, and the Menu handling logic

            //disallow things like clothing wallpaper hats rings etc, and things with usage bars like tackles? 
            //they all probably have maximumStackSize() == 1 so probably not an issue
        }

        protected override ItemBagMenu CreateMenu(IList<Item> InventorySource, int ActualCapacity)
        {
            ItemBagsMod.UserConfig.GetRucksackMenuOptions(Size, out int NumColumns, out int SlotSize);
            return new RucksackMenu(this, InventorySource, ActualCapacity, 12, BagInventoryMenu.DefaultInventoryIconSize, NumColumns, SlotSize, true);
        }

        public override void drawTooltip(SpriteBatch spriteBatch, ref int x, ref int y, SpriteFont font, float alpha, string overrideText)
        {
            //  If hovering over this bag from a Shop, draw a custom tooltip that displays icons for each item the bag is capable of storing
            if (Game1.activeClickableMenu is ShopMenu)
            {
                ItemBagsMod.UserConfig.GetRucksackMenuOptions(Size, out int MenuColumns, out int MenuSlotSize);

                int SlotSize = 32;
                int Columns = Math.Min(NumSlots, MenuColumns);
                int Rows = (NumSlots - 1) / Columns + 1;

                int TitleWidth = (int)(font.MeasureString(this.DisplayName).X * 1.5) + 24; // Not sure if this is the correct scale and margin that the game's default rendering of the title bar uses
                int TextWidth = (int)font.MeasureString(overrideText).X + 32; // Do not change this 32, it's the additional margin that the game uses around the description text
                int ItemsMargin = 24;
                int ItemsWidth = SlotSize * Columns + ItemsMargin * 2;
                int RequiredWidth = Math.Max(Math.Max(TextWidth, TitleWidth), ItemsWidth);

                int MarginAfterDescription = 24;
                int RequiredHeight = (int)font.MeasureString(overrideText).Y + MarginAfterDescription + Rows * SlotSize - 8 + (int)font.MeasureString("999").Y + 32;

                DrawHelpers.DrawBox(spriteBatch, new Rectangle(x, y, RequiredWidth, RequiredHeight));

                //  Draw the description text
                if (!string.IsNullOrEmpty(overrideText) && overrideText != " ")
                {
                    spriteBatch.DrawString(font, overrideText, new Vector2((float)(x + 16), (float)(y + 16 + 4)) + new Vector2(2f, 2f), Game1.textShadowColor * alpha);
                    spriteBatch.DrawString(font, overrideText, new Vector2((float)(x + 16), (float)(y + 16 + 4)) + new Vector2(0f, 2f), Game1.textShadowColor * alpha);
                    spriteBatch.DrawString(font, overrideText, new Vector2((float)(x + 16), (float)(y + 16 + 4)) + new Vector2(2f, 0f), Game1.textShadowColor * alpha);
                    spriteBatch.DrawString(font, overrideText, new Vector2((float)(x + 16), (float)(y + 16 + 4)), (Game1.textColor * 0.9f) * alpha);
                    y = y + (int)font.MeasureString(overrideText).Y;
                }
                y += MarginAfterDescription;

                //  Draw empty slots for each slot that this rucksack has
                int RowStartX = x + (RequiredWidth - SlotSize * Columns) / 2;
                int CurrentX = RowStartX;
                int CurrentIndex = 0;
                for (int i = 0; i < Rows * Columns; i++)
                {
                    if (CurrentIndex == Columns)
                    {
                        CurrentIndex = 0;
                        CurrentX = RowStartX;
                        y += SlotSize;
                    }

                    Rectangle Destination = new Rectangle(CurrentX, y, SlotSize, SlotSize);
                    if (i < NumSlots)
                        spriteBatch.Draw(Game1.menuTexture, Destination, new Rectangle(128, 128, 64, 64), Color.White);
                    else
                        spriteBatch.Draw(Game1.menuTexture, Destination, new Rectangle(64, 896, 64, 64), Color.White);

                    CurrentX += SlotSize;
                    CurrentIndex++;
                }

                y += SlotSize - 8;
            }
            //  If hovering over this bag from the GameMenu's inventory or from a chest, draw a scaled-down preview of the rucksack's contents
            else if ((Game1.activeClickableMenu is GameMenu GM && GM.currentTab == GameMenu.inventoryTab) ||
                (Game1.activeClickableMenu is ItemGrabMenu IGM && IGM.context is Chest))
            {
                List<Object> DrawnObjects = this.Contents.Take(72).ToList();
                if (DrawnObjects.Any())
                {
                    int DrawnSlots = DrawnObjects.Count;
                    int SlotSize = 32;
                    int Columns = Math.Min(DrawnSlots, 12);
                    int Rows = (DrawnSlots - 1) / Columns + 1;

                    int TitleWidth = (int)(font.MeasureString(this.DisplayName).X * 1.5) + 24; // Not sure if this is the correct scale and margin that the game's default rendering of the title bar uses
                    int TextWidth = (int)font.MeasureString(overrideText).X + 32; // Do not change this 32, it's the additional margin that the game uses around the description text
                    int ItemsMargin = 24;
                    int ItemsWidth = SlotSize * Columns + ItemsMargin * 2;
                    int RequiredWidth = Math.Max(Math.Max(TextWidth, TitleWidth), ItemsWidth);

                    int MarginAfterDescription = 24;
                    int RequiredHeight = (int)font.MeasureString(overrideText).Y + MarginAfterDescription + Rows * SlotSize - 8 + (int)font.MeasureString("999").Y;

                    DrawHelpers.DrawBox(spriteBatch, new Rectangle(x, y, RequiredWidth, RequiredHeight));

                    //  Draw the description text
                    if (!string.IsNullOrEmpty(overrideText) && overrideText != " ")
                    {
                        spriteBatch.DrawString(font, overrideText, new Vector2((float)(x + 16), (float)(y + 16 + 4)) + new Vector2(2f, 2f), Game1.textShadowColor * alpha);
                        spriteBatch.DrawString(font, overrideText, new Vector2((float)(x + 16), (float)(y + 16 + 4)) + new Vector2(0f, 2f), Game1.textShadowColor * alpha);
                        spriteBatch.DrawString(font, overrideText, new Vector2((float)(x + 16), (float)(y + 16 + 4)) + new Vector2(2f, 0f), Game1.textShadowColor * alpha);
                        spriteBatch.DrawString(font, overrideText, new Vector2((float)(x + 16), (float)(y + 16 + 4)), (Game1.textColor * 0.9f) * alpha);
                        y = y + (int)font.MeasureString(overrideText).Y;
                    }
                    y += MarginAfterDescription;

                    //  Draw a scaled-down copy of the bag contents on this tooltip
                    int RowStartX = x + (RequiredWidth - SlotSize * Columns) / 2;
                    int CurrentX = RowStartX;
                    int CurrentIndex = 0;
                    for (int i = 0; i < Rows * Columns; i++)
                    {
                        if (CurrentIndex == Columns)
                        {
                            CurrentIndex = 0;
                            CurrentX = RowStartX;
                            y += SlotSize;
                        }

                        Rectangle Destination = new Rectangle(CurrentX, y, SlotSize, SlotSize);
                        if (i < NumSlots)
                            spriteBatch.Draw(Game1.menuTexture, Destination, new Rectangle(128, 128, 64, 64), Color.White);
                        else
                            spriteBatch.Draw(Game1.menuTexture, Destination, new Rectangle(64, 896, 64, 64), Color.White);

                        if (i < DrawnSlots)
                            DrawHelpers.DrawItem(spriteBatch, Destination, DrawnObjects[i], false, true, 1f, 1f, Color.White, Color.Black);

                        CurrentX += SlotSize;
                        CurrentIndex++;
                    }

                    y += -8;

                }
                else
                {
                    base.drawTooltip(spriteBatch, ref x, ref y, font, alpha, overrideText);
                }
            }
            else
            {
                base.drawTooltip(spriteBatch, ref x, ref y, font, alpha, overrideText);
            }
        }
    }
}
