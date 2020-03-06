using ItemBags.Bags;
using ItemBags.Helpers;
using ItemBags.Menus;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace ItemBags.Persistence
{
    [XmlRoot(ElementName = "BagConfig", Namespace = "")]
    public class BagConfig
    {
        [XmlArray("BagTypes")]
        [XmlArrayItem("BagType")]
        public BagType[] BagTypes { get; set; }

        /// <summary>This property is only public for serialization purposes. Use <see cref="CreatedByVersion"/> instead.</summary>
        [XmlElement("CreatedByVersion")]
        public string CreatedByVersionString { get; set; }
        /// <summary>Warning - in old versions of the mod, this value may be null. This feature was added with v1.0.4</summary>
        [JsonIgnore]
        [XmlIgnore]
        public Version CreatedByVersion {
            get { return string.IsNullOrEmpty(CreatedByVersionString) ? null : Version.Parse(CreatedByVersionString); }
            set { CreatedByVersionString = value == null ? null : value.ToString(); }
        }

        public BagConfig()
        {
            InitializeDefaults();
        }

        internal BagType GetDefaultBoundedBagType()
        {
            return BagTypes.First(x => x.Id != Rucksack.RucksackTypeId && x.Id != OmniBag.OmniBagTypeId && x.Id != BundleBag.BundleBagTypeId);
        }

        private void InitializeDefaults()
        {
            this.BagTypes = new BagType[]
            {
                BagTypeFactory.GetGemBagType(),
                BagTypeFactory.GetSmithingBagType(),
                BagTypeFactory.GetMineralBagType(),
                BagTypeFactory.GetMiningBagType(),
                BagTypeFactory.GetResourceBagType(),
                BagTypeFactory.GetConstructionBagType(),
                BagTypeFactory.GetTreeBagType(),
                BagTypeFactory.GetAnimalProductBagType(),
                BagTypeFactory.GetRecycleBagType(),
                BagTypeFactory.GetLootBagType(),
                BagTypeFactory.GetForagingBagType(),
                BagTypeFactory.GetArtifactBagType(),
                BagTypeFactory.GetSeedBagType(),
                BagTypeFactory.GetOceanFishBagType(),
                BagTypeFactory.GetRiverFishBagType(),
                BagTypeFactory.GetLakeFishBagType(),
                BagTypeFactory.GetMiscFishBagType(),
                BagTypeFactory.GetFishBagType(),
                BagTypeFactory.GetFarmerBagType(),
                BagTypeFactory.GetFoodBagType()
            };

            //this.CreatedByVersion = ItemBagsMod.CurrentVersion;
        }

        internal bool EnsureBagTypesExist(params BagType[] Types)
        {
            List<BagType> DistinctTypes = Types.GroupBy(x => x.Id).Select(x => x.First()).ToList();
            List<BagType> MissingTypes = DistinctTypes.Where(x => !this.BagTypes.Any(y => x.Id == y.Id)).ToList();
            if (MissingTypes.Any())
            {
                this.BagTypes = new List<BagType>(BagTypes).Union(MissingTypes).ToArray();
                return true;
            }
            else
            {
                return false;
            }
        }

        /*public const string SettingsFilename = "BagConfig.xml";
        public static string SettingsFilePath { get { return Path.Combine(ItemBagsMod.ModInstance.Helper.DirectoryPath, SettingsFilename); } }

        public void Serialize(out bool Successful, out Exception SerializationError)
        {
            XMLSerializer.Serialize(this, SettingsFilePath, out Successful, out SerializationError);
        }

        public static BagConfig Deserialize(out bool Successful, out Exception DeserializationError)
        {
            return XMLSerializer.Deserialize<BagConfig>(SettingsFilePath, out Successful, out DeserializationError);
        }*/

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
