using ItemBags.Bags;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using StardewValley;
using Object = StardewValley.Object;
using Microsoft.Xna.Framework;

namespace ItemBags.Persistence
{
    [XmlRoot(ElementName = "BagItem", Namespace = "")]
    public class BagItem
    {
        [XmlElement("Id")]
        public int Id { get; set; }
        [XmlElement("Quality")]
        public int Quality { get; set; }
        [XmlElement("Quantity")]
        public int Quantity { get; set; }
        [XmlElement("IsBigCraftable")]
        public bool IsBigCraftable { get; set; }
        [XmlElement("Price")]
        public int Price { get; set; }
        [XmlElement("Name")]
        public string Name { get; set; }

        public BagItem()
        {
            InitializeDefaults();
        }

        public BagItem(Object Item)
        {
            InitializeDefaults();
            this.Id = Item.ParentSheetIndex;
            this.Quality = Item.Quality;
            this.Quantity = Item.Stack;
            this.Price = Item.Price;
            this.IsBigCraftable = Item.bigCraftable.Value;
            this.Name = Item.Name;
        }

        public Object ToObject()
        {
            if (IsBigCraftable)
            {
                Object Item = new Object(Vector2.Zero, Id, false) { Price = this.Price }; // It seems like some modded items don't have their price set properly if not explicitly specified
                ItemBag.ForceSetQuantity(Item, this.Quantity);
                return Item;
            }
            else
            {
                Object Item = new Object(Id, Quantity, false, Price <= 0 ? -1 : Price, Quality);

                //  Sanity check in case Stack > 999 and StardewValley is updated to set the Object.Stack in its constructor instead of Object.stack 
                //  (Object.Stack has a setter that restricts maximum value to the range 0-999, while Object.stack (the backing Net field) does not)
                if (Item.Stack != Quantity)
                    ItemBag.ForceSetQuantity(Item, Quantity);

                return Item;
            }
        }

        private void InitializeDefaults()
        {
            this.Id = -1;
            this.Quality = (int)ObjectQuality.Regular;
            this.Quantity = 0;
            this.IsBigCraftable = false;
            this.Price = -1;
            this.Name = "";
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
