﻿using ItemBags.Bags;
using ItemBags.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace ItemBags.Persistence
{
    [XmlRoot(ElementName = "BagSizeConfig", Namespace = "")]
    public class BagSizeConfig
    {
        [XmlElement("Size")]
        public ContainerSize Size { get; set; }
        [XmlElement("MenuOptions")]
        public BagMenuOptions MenuOptions { get; set; }
        [XmlElement("Price")]
        public int Price { get; set; }

        [XmlRoot(ElementName = "Shop", Namespace = "")]
        public enum BagShop
        {
            [XmlEnum("Pierre")]
            [Description("Pierre")]
            Pierre,
            [XmlEnum("Clint")]
            [Description("Clint")]
            Clint,
            [XmlEnum("Robin")]
            [Description("Robin")]
            Robin,
            [XmlEnum("Willy")]
            [Description("Willy")]
            Willy,
            [XmlEnum("Marnie")]
            [Description("Marnie")]
            Marnie,
            [XmlEnum("Krobus")]
            [Description("Krobus")]
            Krobus,
            [XmlEnum("Dwarf")]
            [Description("Dwarf")]
            Dwarf,
            [XmlEnum("Marlon")]
            [Description("Marlon")]
            Marlon,
            [XmlEnum("Gus")]
            [Description("Gus")]
            Gus,
            [XmlEnum("Sandy")]
            [Description("Sandy")]
            Sandy,
            [XmlEnum("TravellingCart")]
            [Description("TravellingCart")]
            TravellingCart,
            [XmlEnum("Employee")]
            [Description("Employee")]
            Employee,
            [XmlEnum("HatMouse")]
            [Description("HatMouse")]
            HatMouse,
            [XmlEnum("Khadija")]
            [Description("Khadija")]
            Khadija,
            #region SVE
            [XmlEnum("Sophia")]
            [Description("Sophia")]
            Sophia,
            [XmlEnum("Bear")]
            [Description("Bear")]
            Bear,
            [XmlEnum("Alesia")]
            [Description("Alesia")]
            Alesia,
            [XmlEnum("Isaac")]
            [Description("Isaac")]
            Isaac,
            #endregion SVE
        }

        [XmlArray("Shops")]
        [XmlArrayItem("Shop")]
        public List<BagShop> Sellers { get; set; }

        [XmlElement("CapacityMultiplier")]
        public double CapacityMultiplier { get; set; }

        [XmlArray("Items")]
        [XmlArrayItem("Item")]
        public List<StoreableBagItem> Items { get; set; }

        public BagSizeConfig()
        {
            InitializeDefaults();
        }

        private void InitializeDefaults()
        {
            this.Size = ContainerSize.Small;
            this.Price = BagTypeFactory.DefaultPrices[this.Size];
            this.Sellers = new List<BagShop>() { BagShop.Pierre };
            this.MenuOptions = new BagMenuOptions();
            this.CapacityMultiplier = 1.0;
            this.Items = new List<StoreableBagItem> { };
        }

        public void CopySettingsFrom(BagSizeConfig Other)
        {
            Size = Other.Size;
            MenuOptions.CopySettingsFrom(Other.MenuOptions);
            Price = Other.Price;
            Sellers = Other.Sellers.ToList();
            CapacityMultiplier = Other.CapacityMultiplier;
            Items = Other.Items.ToList();            
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
