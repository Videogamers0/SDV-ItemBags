using ItemBags.Bags;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ItemBags.Persistence
{
    [XmlRoot(ElementName = "BagInstance", Namespace = "")]
    public class BagInstance
    {
        [XmlElement("InstanceId")]
        public int InstanceId { get; set; }
        [XmlElement("TypeId")]
        public string TypeId { get; set; }
        [XmlElement("Size")]
        public ContainerSize Size { get; set; }
        [XmlElement("Autofill")]
        public bool Autofill { get; set; }

#region Rucksack Properties
        [XmlElement("AutofillPriority")]
        public AutofillPriority AutofillPriority { get; set; }
        [XmlElement("SortProperty")]
        public SortingProperty SortProperty { get; set; }
        [XmlElement("SortOrder")]
        public SortingOrder SortOrder { get; set; }
#endregion Rucksack Properties

        [XmlArray("Contents")]
        [XmlArrayItem("Item")]
        public BagItem[] Contents { get; set; }

        [XmlElement("IsCustomIcon")]
        public bool IsCustomIcon { get; set; }
        [XmlElement("OverriddenIcon")]
        public Rectangle OverriddenIcon { get; set; }

        public BagInstance()
        {
            InitializeDefaults();
        }

        public BagInstance(int Id, ItemBag Bag)
        {
            InitializeDefaults();
            this.InstanceId = Id;

            if (Bag is BoundedBag BoundedBag)
            {
                if (BoundedBag is BundleBag BundleBag)
                {
                    this.TypeId = BundleBag.BundleBagTypeId;
                }
                else
                {
                    this.TypeId = BoundedBag.TypeInfo.Id;
                }
                this.Autofill = BoundedBag.Autofill;
            }
            else if (Bag is Rucksack Rucksack)
            {
                this.TypeId = Rucksack.RucksackTypeId;
                this.Autofill = Rucksack.Autofill;
                this.AutofillPriority = Rucksack.AutofillPriority;
                this.SortProperty = Rucksack.SortProperty;
                this.SortOrder = Rucksack.SortOrder;
            }
            else
            {
                throw new NotImplementedException(string.Format("Logic for encoding Bag Type '{0}' is not implemented", Bag.GetType().ToString()));
            }

            this.Size = Bag.Size;
            if (Bag.Contents != null)
            {
                this.Contents = Bag.Contents.Where(x => x != null).Select(x => new BagItem(x)).ToArray();
            }

            if (Bag.IsUsingDefaultIcon() || !Bag.IconTexturePosition.HasValue)
            {
                this.IsCustomIcon = false;
                this.OverriddenIcon = new Rectangle();
            }
            else
            {
                this.IsCustomIcon = true;
                this.OverriddenIcon = Bag.IconTexturePosition.Value;
            }
        }

        private void InitializeDefaults()
        {
            this.InstanceId = -1;
            this.TypeId = Guid.Empty.ToString();
            this.Size = ContainerSize.Small;
            this.Autofill = false;
            this.Contents = new BagItem[] { };
            this.IsCustomIcon = false;
            this.OverriddenIcon = new Rectangle();
            this.AutofillPriority = AutofillPriority.Low;
            this.SortProperty = SortingProperty.Similarity;
            this.SortOrder = SortingOrder.Ascending;
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
