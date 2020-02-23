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
        //Possible TODO: should maybe just serialize the entire Object XML string?
        //Then modify BagItem.ToObject, ItemBag.AreItemsEquivalent,
        //And maybe switch the ItemBag properties over to Net versions (EX: ItemBag.Contents could be a NetRef<List<Object>>?)

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
        }

        public Object ToObject()
        {
            if (IsBigCraftable)
            {
                Object Item = new Object(Vector2.Zero, Id, false);
                ItemBag.ForceSetQuantity(Item, this.Quantity);
                return Item;
            }
            else
                return new Object(Id, Quantity, false, Price <= 0 ? -1 : Price, Quality);
        }

        private void InitializeDefaults()
        {
            this.Id = -1;
            this.Quality = (int)ObjectQuality.Regular;
            this.Quantity = 0;
            this.IsBigCraftable = false;
            this.Price = -1;
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
