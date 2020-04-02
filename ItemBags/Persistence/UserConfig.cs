using ItemBags.Bags;
using ItemBags.Menus;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using static ItemBags.Bags.ItemBag;
using static ItemBags.Persistence.BagSizeConfig;

namespace ItemBags.Persistence
{
    [XmlRoot(ElementName = "Config", Namespace = "")]
    public class UserConfig
    {
        /// <summary>This property is only public for serialization purposes. Use <see cref="CreatedByVersion"/> instead.</summary>
        [XmlElement("CreatedByVersion")]
        public string CreatedByVersionString { get; set; }
        /// <summary>Warning - in old versions of the mod, this value may be null. This feature was added with v1.0.4</summary>
        [JsonIgnore]
        [XmlIgnore]
        public Version CreatedByVersion
        {
            get { return string.IsNullOrEmpty(CreatedByVersionString) ? null : Version.Parse(CreatedByVersionString); }
            set { CreatedByVersionString = value == null ? null : value.ToString(); }
        }

        [XmlElement("GlobalPriceModifier")]
        public double GlobalPriceModifier { get; set; }
        [XmlElement("GlobalCapacityModifier")]
        public double GlobalCapacityModifier { get; set; }

        [XmlArray("StandardBagSettings")]
        [XmlArrayItem("StandardBagSizeConfig")]
        public StandardBagSizeConfig[] StandardBagSettings { get; set; }
        [XmlArray("BundleBagSettings")]
        [XmlArrayItem("BundleBagSizeConfig")]
        public BundleBagSizeConfig[] BundleBagSettings { get; set; }
        [XmlArray("RucksackSettings")]
        [XmlArrayItem("RucksackSizeConfig")]
        public RucksackSizeConfig[] RucksackSettings { get; set; }
        [XmlArray("OmniBagSettings")]
        [XmlArrayItem("OmniBagSizeConfig")]
        public OmniBagSizeConfig[] OmniBagSettings { get; set; }

        [XmlElement("HideSmallBagsFromShops")]
        public bool HideSmallBagsFromShops { get; set; }
        [XmlElement("HideMediumBagsFromShops")]
        public bool HideMediumBagsFromShops { get; set; }
        [XmlElement("HideLargeBagsFromShops")]
        public bool HideLargeBagsFromShops { get; set; }
        [XmlElement("HideGiantBagsFromShops")]
        public bool HideGiantBagsFromShops { get; set; }
        [XmlElement("HideMassiveBagsFromShops")]
        public bool HideMassiveBagsFromShops { get; set; }

        [XmlElement("HideObsoleteBagsFromShops")]
        public bool HideObsoleteBagsFromShops { get; set; }

        public UserConfig()
        {
            InitializeDefaults();
        }

        private void InitializeDefaults()
        {
            this.GlobalPriceModifier = 1.0;
            this.GlobalCapacityModifier = 1.0;

            this.StandardBagSettings = new StandardBagSizeConfig[]
            {
                new StandardBagSizeConfig(ContainerSize.Small, 1.0, 1.0),
                new StandardBagSizeConfig(ContainerSize.Medium, 1.0, 1.0),
                new StandardBagSizeConfig(ContainerSize.Large, 1.0, 1.0),
                new StandardBagSizeConfig(ContainerSize.Giant, 1.0, 1.0),
                new StandardBagSizeConfig(ContainerSize.Massive, 1.0, 1.0)
            };

            this.BundleBagSettings = new BundleBagSizeConfig[]
            {
                new BundleBagSizeConfig(ContainerSize.Large, 1.0, 2500),
                new BundleBagSizeConfig(ContainerSize.Massive, 1.0, 10000)
            };

            this.RucksackSettings = new RucksackSizeConfig[]
            {
                new RucksackSizeConfig(ContainerSize.Small, 1.0, 1.0, 24000, 30, 6, 12, BagInventoryMenu.DefaultInventoryIconSize),
                new RucksackSizeConfig(ContainerSize.Medium, 1.0, 1.0, 65000, 99, 12, 12, BagInventoryMenu.DefaultInventoryIconSize),
                new RucksackSizeConfig(ContainerSize.Large, 1.0, 1.0, 175000, 300, 24, 12, BagInventoryMenu.DefaultInventoryIconSize),
                new RucksackSizeConfig(ContainerSize.Giant, 1.0, 1.0, 350000, 999, 36, 12, BagInventoryMenu.DefaultInventoryIconSize),
                new RucksackSizeConfig(ContainerSize.Massive, 1.0, 1.0, 1000000, 9999, 72, 12, BagInventoryMenu.DefaultInventoryIconSize)
            };

            this.OmniBagSettings = new OmniBagSizeConfig[]
            {
                new OmniBagSizeConfig(ContainerSize.Small, 1.0, 12000, 8, BagInventoryMenu.DefaultInventoryIconSize),
                new OmniBagSizeConfig(ContainerSize.Medium, 1.0, 25000, 8, BagInventoryMenu.DefaultInventoryIconSize),
                new OmniBagSizeConfig(ContainerSize.Large, 1.0, 75000, 8, BagInventoryMenu.DefaultInventoryIconSize),
                new OmniBagSizeConfig(ContainerSize.Giant, 1.0, 300000, 8, BagInventoryMenu.DefaultInventoryIconSize),
                new OmniBagSizeConfig(ContainerSize.Massive, 1.0, 1500000, 8, BagInventoryMenu.DefaultInventoryIconSize)
            };

            this.HideSmallBagsFromShops = false;
            this.HideMediumBagsFromShops = false;
            this.HideLargeBagsFromShops = false;
            this.HideGiantBagsFromShops = false;
            this.HideMassiveBagsFromShops = false;

            this.HideObsoleteBagsFromShops = true;
        }

        public bool IsSizeVisibleInShops(ContainerSize Size)
        {
            if (Size == ContainerSize.Small)
                return !HideSmallBagsFromShops;
            else if (Size == ContainerSize.Medium)
                return !HideMediumBagsFromShops;
            else if (Size == ContainerSize.Large)
                return !HideLargeBagsFromShops;
            else if (Size == ContainerSize.Giant)
                return !HideGiantBagsFromShops;
            else if (Size == ContainerSize.Massive)
                return !HideMassiveBagsFromShops;
            else
                return true;
        }

        public int GetStandardBagPrice(ContainerSize Size, BagType Type)
        {
            StandardBagSizeConfig SizeCfg = StandardBagSettings.First(x => x.Size == Size);
            int BasePrice = Type.SizeSettings.First(x => x.Size == Size).Price;
            double Multiplier = GlobalPriceModifier * SizeCfg.PriceModifier;
            if (Multiplier == 1.0)
                return BasePrice;
            else
                return RoundIntegerToSecondMostSignificantDigit((int)(BasePrice * Multiplier), RoundingMode.Floor);
        }

        public int GetStandardBagCapacity(ContainerSize Size, BagType Type)
        {
            StandardBagSizeConfig SizeCfg = StandardBagSettings.First(x => x.Size == Size);
            return SizeCfg.GetCapacity(Type, GlobalCapacityModifier);
        }

        public int GetBundleBagPrice(ContainerSize Size)
        {
            BundleBagSizeConfig SizeCfg = BundleBagSettings.First(x => x.Size == Size);
            int BasePrice = SizeCfg.BasePrice;
            double Multiplier = GlobalPriceModifier * SizeCfg.PriceModifier;
            if (Multiplier == 1.0)
                return BasePrice;
            else
                return RoundIntegerToSecondMostSignificantDigit((int)(BasePrice * Multiplier), RoundingMode.Round);
        }

        public int GetRucksackPrice(ContainerSize Size)
        {
            RucksackSizeConfig SizeCfg = RucksackSettings.First(x => x.Size == Size);
            int BasePrice = SizeCfg.BasePrice;
            double Multiplier = GlobalPriceModifier * SizeCfg.PriceModifier;
            if (Multiplier == 1.0)
                return BasePrice;
            else
                return RoundIntegerToSecondMostSignificantDigit((int)(BasePrice * Multiplier), RoundingMode.Round);
        }

        public int GetRucksackCapacity(ContainerSize Size)
        {
            RucksackSizeConfig SizeCfg = RucksackSettings.First(x => x.Size == Size);
            int BaseCapacity = SizeCfg.BaseCapacity;
            double Multiplier = GlobalCapacityModifier * SizeCfg.CapacityModifier;
            if (Multiplier == 1.0)
                return BaseCapacity;
            else
                return Math.Max(1, RoundIntegerToSecondMostSignificantDigit((int)(BaseCapacity * Multiplier), RoundingMode.Round));
        }

        public int GetRucksackSlotCount(ContainerSize Size)
        {
            return RucksackSettings.First(x => x.Size == Size).Slots;
        }

        public void GetRucksackMenuOptions(ContainerSize Size, out int NumColumns, out int SlotSize)
        {
            RucksackSizeConfig SizeCfg = RucksackSettings.First(x => x.Size == Size);
            NumColumns = SizeCfg.MenuColumns;
            SlotSize = SizeCfg.MenuSlotSize;
        }

        public int GetOmniBagPrice(ContainerSize Size)
        {
            OmniBagSizeConfig SizeCfg = OmniBagSettings.First(x => x.Size == Size);
            int BasePrice = SizeCfg.BasePrice;
            double Multiplier = GlobalPriceModifier * SizeCfg.PriceModifier;
            if (Multiplier == 1.0)
                return BasePrice;
            else
                return RoundIntegerToSecondMostSignificantDigit((int)(BasePrice * Multiplier), RoundingMode.Round);
        }

        public void GetOmniBagMenuOptions(ContainerSize Size, out int NumColumns, out int SlotSize)
        {
            OmniBagSizeConfig SizeCfg = OmniBagSettings.First(x => x.Size == Size);
            NumColumns = SizeCfg.MenuColumns;
            SlotSize = SizeCfg.MenuSlotSize;
        }

        [OnSerializing]
        private void OnSerializing(StreamingContext sc) { }
        [OnSerialized]
        private void OnSerialized(StreamingContext sc) { }
        [OnDeserializing]
        private void OnDeserializing(StreamingContext sc) { InitializeDefaults(); }
        [OnDeserialized]
        private void OnDeserialized(StreamingContext sc) { }
    }

    [XmlRoot(ElementName = "StandardBagSizeConfig", Namespace = "")]
    public class StandardBagSizeConfig
    {
        private static readonly Dictionary<ContainerSize, int> BaseCapacities = new Dictionary<ContainerSize, int>()
        {
            { ContainerSize.Small, 30 },
            { ContainerSize.Medium, 99 },
            { ContainerSize.Large, 300 },
            { ContainerSize.Giant, 999 },
            { ContainerSize.Massive, 9999 },
        };

        [JsonIgnore]
        [XmlIgnore]
        public ContainerSize Size { get; private set; }
        [XmlElement("Size")]
        [JsonProperty("Size")]
        public string SizeName { get { return Size.ToString(); } set { Size = (ContainerSize)Enum.Parse(typeof(ContainerSize), value); } }

        [XmlElement("PriceModifier")]
        public double PriceModifier { get; set; }
        [XmlElement("CapacityModifier")]
        public double CapacityModifier { get; set; }

        public StandardBagSizeConfig()
        {
            InitializeDefaults();
        }

        public StandardBagSizeConfig(ContainerSize Size, double PriceModifier, double CapacityModifier)
        {
            InitializeDefaults();
            this.Size = Size;
            this.PriceModifier = PriceModifier;
            this.CapacityModifier = CapacityModifier;
        }

        private void InitializeDefaults()
        {
            this.Size = ContainerSize.Small;
            this.PriceModifier = 1.0;
            this.CapacityModifier = 1.0;
        }

        public int GetCapacity(BagType Type, double GlobalCapacityModifier)
        {
            int BaseCapacity = BaseCapacities[Size];
            double Multiplier = GlobalCapacityModifier * Type.SizeSettings.First(x => x.Size == Size).CapacityMultiplier;
            if (Multiplier == 1.0)
                return BaseCapacity;
            else
                return Math.Max(1, RoundIntegerToSecondMostSignificantDigit((int)(BaseCapacity * Multiplier), RoundingMode.Round));
        }

        [OnSerializing]
        private void OnSerializing(StreamingContext sc) { }
        [OnSerialized]
        private void OnSerialized(StreamingContext sc) { }
        [OnDeserializing]
        private void OnDeserializing(StreamingContext sc) { InitializeDefaults(); }
        [OnDeserialized]
        private void OnDeserialized(StreamingContext sc) { }
    }

    [XmlRoot(ElementName = "BundleBagSizeConfig", Namespace = "")]
    public class BundleBagSizeConfig
    {
        [JsonIgnore]
        [XmlIgnore]
        public ContainerSize Size { get; private set; }
        [XmlElement("Size")]
        [JsonProperty("Size")]
        public string SizeName { get { return Size.ToString(); } set { Size = (ContainerSize)Enum.Parse(typeof(ContainerSize), value); } }

        [XmlElement("PriceModifier")]
        public double PriceModifier { get; set; }
        [XmlElement("BasePrice")]
        public int BasePrice { get; set; }

        [XmlArray("Shops")]
        [XmlArrayItem("Shop")]
        public BagShop[] Sellers { get; set; }

        public BundleBagSizeConfig()
        {
            InitializeDefaults();
        }

        public BundleBagSizeConfig(ContainerSize Size, double PriceModifier, int BasePrice)
        {
            InitializeDefaults();
            this.Size = Size;
            this.PriceModifier = PriceModifier;
            this.BasePrice = BasePrice;
        }

        private void InitializeDefaults()
        {
            this.Size = BundleBag.ValidSizes.First();
            this.Sellers = new BagShop[] { BagShop.TravellingCart };
            this.PriceModifier = 1.0;
            this.BasePrice = 0;
        }

        [OnSerializing]
        private void OnSerializing(StreamingContext sc) { }
        [OnSerialized]
        private void OnSerialized(StreamingContext sc) { }
        [OnDeserializing]
        private void OnDeserializing(StreamingContext sc) { InitializeDefaults(); }
        [OnDeserialized]
        private void OnDeserialized(StreamingContext sc) { }
    }

    [XmlRoot(ElementName = "RucksackSizeConfig", Namespace = "")]
    public class RucksackSizeConfig
    {
        [JsonIgnore]
        [XmlIgnore]
        public ContainerSize Size { get; private set; }
        [XmlElement("Size")]
        [JsonProperty("Size")]
        public string SizeName { get { return Size.ToString(); } set { Size = (ContainerSize)Enum.Parse(typeof(ContainerSize), value); } }

        [XmlElement("PriceModifier")]
        public double PriceModifier { get; set; }
        [XmlElement("CapacityModifier")]
        public double CapacityModifier { get; set; }
        [XmlElement("BasePrice")]
        public int BasePrice { get; set; }
        [XmlElement("BaseCapacity")]
        public int BaseCapacity { get; set; }
        [XmlElement("Slots")]
        public int Slots { get; set; }
        [XmlElement("MenuColumns")]
        public int MenuColumns { get; set; }
        [XmlElement("MenuSlotSize")]
        public int MenuSlotSize { get; set; }

        [XmlArray("Shops")]
        [XmlArrayItem("Shop")]
        public BagShop[] Sellers { get; set; }

        public RucksackSizeConfig()
        {
            InitializeDefaults();
        }

        public RucksackSizeConfig(ContainerSize Size, double PriceModifier, double CapacityModifier, int BasePrice, int BaseCapacity, int Slots, int MenuColumns, int MenuSlotSize)
        {
            InitializeDefaults();
            this.Size = Size;
            this.PriceModifier = PriceModifier;
            this.CapacityModifier = CapacityModifier;
            this.BasePrice = BasePrice;
            this.BaseCapacity = BaseCapacity;
            this.Slots = Slots;
            this.MenuColumns = MenuColumns;
            this.MenuSlotSize = MenuSlotSize;
        }

        private void InitializeDefaults()
        {
            this.Size = ContainerSize.Small;
            this.PriceModifier = 1.0;
            this.CapacityModifier = 1.0;
            this.BasePrice = 0;
            this.BaseCapacity = 1;
            this.Slots = 1;
            this.MenuColumns = 12;
            this.MenuSlotSize = BagInventoryMenu.DefaultInventoryIconSize;
            this.Sellers = new BagShop[] { BagShop.Pierre };
        }

        [OnSerializing]
        private void OnSerializing(StreamingContext sc) { }
        [OnSerialized]
        private void OnSerialized(StreamingContext sc) { }
        [OnDeserializing]
        private void OnDeserializing(StreamingContext sc) { InitializeDefaults(); }
        [OnDeserialized]
        private void OnDeserialized(StreamingContext sc) { }
    }

    [XmlRoot(ElementName = "OmniBagSizeConfig", Namespace = "")]
    public class OmniBagSizeConfig
    {
        [JsonIgnore]
        [XmlIgnore]
        public ContainerSize Size { get; private set; }
        [XmlElement("Size")]
        [JsonProperty("Size")]
        public string SizeName { get { return Size.ToString(); } set { Size = (ContainerSize)Enum.Parse(typeof(ContainerSize), value); } }

        [XmlElement("PriceModifier")]
        public double PriceModifier { get; set; }
        [XmlElement("BasePrice")]
        public int BasePrice { get; set; }
        [XmlElement("MenuColumns")]
        public int MenuColumns { get; set; }
        [XmlElement("MenuSlotSize")]
        public int MenuSlotSize { get; set; }

        [XmlArray("Shops")]
        [XmlArrayItem("Shop")]
        public BagShop[] Sellers { get; set; }

        public OmniBagSizeConfig()
        {
            InitializeDefaults();
        }

        public OmniBagSizeConfig(ContainerSize Size, double PriceModifier, int BasePrice, int MenuColumns, int MenuSlotSize)
        {
            InitializeDefaults();
            this.Size = Size;
            this.PriceModifier = PriceModifier;
            this.BasePrice = BasePrice;
            this.MenuColumns = MenuColumns;
            this.MenuSlotSize = MenuSlotSize;
        }

        private void InitializeDefaults()
        {
            this.Size = ContainerSize.Small;
            this.PriceModifier = 1.0;
            this.BasePrice = 0;
            this.MenuColumns = 12;
            this.MenuSlotSize = BagInventoryMenu.DefaultInventoryIconSize;
            this.Sellers = new BagShop[] { BagShop.Pierre };
        }

        [OnSerializing]
        private void OnSerializing(StreamingContext sc) { }
        [OnSerialized]
        private void OnSerialized(StreamingContext sc) { }
        [OnDeserializing]
        private void OnDeserializing(StreamingContext sc) { InitializeDefaults(); }
        [OnDeserialized]
        private void OnDeserialized(StreamingContext sc) { }
    }
}
