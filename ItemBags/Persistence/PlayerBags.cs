using ItemBags.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ItemBags.Persistence
{
    [XmlRoot(ElementName = "PlayerBags", Namespace = "")]
    public class PlayerBags
    {
        [XmlArray("Bags")]
        [XmlArrayItem("Bag")]
        public BagInstance[] Bags { get; set; }

        public PlayerBags()
        {
            InitializeDefaults();
        }

        private void InitializeDefaults()
        {
            this.Bags = new BagInstance[] { };
        }

        /*public const string SettingsFilename = "Mod_ItemBags_Data.xml";
        //public static string SettingsFilePath { get { return Path.Combine(<Path to current player's saves>, SettingsFilename); } }

        public void Serialize(out bool Successful, out Exception SerializationError)
        {
            XMLSerializer.Serialize(this, SettingsFilePath, out Successful, out SerializationError);
        }

        public static PlayerBags Deserialize(out bool Successful, out Exception DeserializationError)
        {
            return XMLSerializer.Deserialize<PlayerBags>(SettingsFilePath, out Successful, out DeserializationError);
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
