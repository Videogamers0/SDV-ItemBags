using ItemBags.Community_Center;
using ItemBags.Helpers;
using ItemBags.Menus;
using ItemBags.Persistence;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PyTK.CustomElementHandler;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Object = StardewValley.Object;

namespace ItemBags.Bags
{
    /// <summary>A bag used for storing items required by incomplete Community Center Bundles</summary>
    [XmlRoot(ElementName = "BundleBag", Namespace = "")]
    public class BundleBag : BoundedBag, ISaveElement
    {
        public const string BundleBagTypeId = "c3f69b2c-6b21-477c-ad43-ee3b996a96bd";

        public static readonly ReadOnlyCollection<ContainerSize> ValidSizes = new List<ContainerSize>() {
            ContainerSize.Large,
            ContainerSize.Massive
        }.AsReadOnly();

        /// <summary>Value = The names of Community Center rooms that the given size of <see cref="BundleBag"/> is NOT capable of storing.</summary>
        public static readonly Dictionary<ContainerSize, HashSet<string>> InvalidRooms = new Dictionary<ContainerSize, HashSet<string>>()
        {
            { ContainerSize.Large, new HashSet<string>(ItemBagsMod.Translate("LargeBundleBagUnstoreableRooms").Split(',').Select(x => x.Trim())) }, // { "Bulletin Board", "Abandoned Joja Mart" }
            { ContainerSize.Massive, new HashSet<string>() { } }
        };

        public override int MaxStackSize { get { return int.MaxValue; } }

        /// <summary>Default parameterless constructor intended for use by XML Serialization. Do not use this constructor to instantiate a bag.</summary>
        public BundleBag() : base()
        {
            this.Size = ValidSizes.Min();
            this.Autofill = true;
        }

        /// <param name="Size">Must be a Size within <see cref="ValidSizes"/></param>
        public BundleBag(ContainerSize Size, bool Autofill)
            : base(ItemBagsMod.Translate("BundleBagName"), ItemBagsMod.Translate("BundleBagDescription"), Size, true)
        {
            if (!ValidSizes.Contains(Size))
                throw new InvalidOperationException(string.Format("Size '{0}' is not valid for BundleBag types", Size.ToString()));

            this.Autofill = Autofill;
        }

        public BundleBag(BagInstance SavedData)
            : this(SavedData.Size, SavedData.Autofill)
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
        public override object getReplacement()
        {
            return new Object(172, 1);
        }

        protected override void LoadSettings(BagInstance Data)
        {
            if (Data != null)
            {
                this.Size = Data.Size;
                this.Autofill = Data.Autofill;

                this.BaseName = ItemBagsMod.Translate("BundleBagName");
                this.DescriptionAlias = ItemBagsMod.Translate("BundleBagDescription");

                Contents.Clear();
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

        public override void ResetIcon()
        {
            this.Icon = TextureHelpers.JunimoNoteTexture;
            this.IconTexturePosition = new Rectangle(0, 244, 16, 16);
        }

        public override bool IsUsingDefaultIcon() { return this.Icon == TextureHelpers.JunimoNoteTexture && this.IconTexturePosition == new Rectangle(0, 244, 16, 16); }
        public override bool CanCustomizeIcon() { return false; }

        public override int GetPurchasePrice() { return ItemBagsMod.UserConfig.GetBundleBagPrice(Size); }
        public override string GetTypeId() { return BundleBagTypeId; }

        /// <param name="InventorySource">Typically this is <see cref="Game1.player.Items"/> if this menu should display the player's inventory.</param>
        /// <param name="ActualCapacity">The maximum # of items that can be stored in the InventorySource list. Use <see cref="Game1.player.MaxItems"/> if moving to/from the inventory.</param>
        protected override ItemBagMenu CreateMenu(IList<Item> InventorySource, int ActualCapacity)
        {
            return new BundleBagMenu(this, InventorySource, ActualCapacity, 12, BagInventoryMenu.DefaultInventoryIconSize, 20, 48, true);
        }

        public override bool IsValidBagObject(Object item)
        {
            if (!BaseIsValidBagObject(item) || item.bigCraftable)
            {
                return false;
            }
            else
            {
                if (CommunityCenterBundles.Instance.IsJojaMember ||
                    !CommunityCenterBundles.Instance.IncompleteBundleItemIds.TryGetValue(item.ParentSheetIndex, out HashSet<ObjectQuality> AcceptedQualities))
                {
                    return false;
                }
                else
                {
                    //  Yes, I know the Quality is a 'MinimumQuality' so technically any higher value should be accepted, but that adds too much complexity,
                    //  so for simplicity, only allow an exact quality match.
                    return Enum.IsDefined(typeof(ObjectQuality), item.Quality) && AcceptedQualities.Contains((ObjectQuality)item.Quality);
                }
            }
        }

        protected override int GetMaxStackSize(Object Item)
        {
            if (!BaseIsValidBagObject(Item) || Item.bigCraftable)
                return 0;

            ObjectQuality ItemQuality = (ObjectQuality)Item.Quality;

            //  Get all incomplete bundle items referring to the given item, and index the required quantity of each quality
            Dictionary<ObjectQuality, int> RequiredAmounts = new Dictionary<ObjectQuality, int>();
            CommunityCenterBundles.Instance.IterateAllBundleItems(x =>
            {
                if (!x.IsCompleted && x.Id == Item.ParentSheetIndex)
                {
                    if (!RequiredAmounts.ContainsKey(x.MinQuality))
                    {
                        RequiredAmounts.Add(x.MinQuality, x.Quantity);
                    }
                    else
                    {
                        RequiredAmounts[x.MinQuality] += x.Quantity;
                    }
                }
            });
            if (!RequiredAmounts.ContainsKey(ItemQuality))
                return 0;
            else
                return RequiredAmounts[ItemQuality];
        }

        public override void drawTooltip(SpriteBatch spriteBatch, ref int x, ref int y, SpriteFont font, float alpha, string overrideText)
        {
            BaseDrawToolTip(spriteBatch, ref x, ref y, font, alpha, overrideText);
        }
    }
}
