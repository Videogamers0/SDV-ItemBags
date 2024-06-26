﻿using ItemBags.Bags;
using ItemBags.Helpers;
using Newtonsoft.Json;
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
    [XmlRoot(ElementName = "StoreableBagItem", Namespace = "")]
    public class StoreableBagItem
    {
        [XmlElement("Id")]
        public string Id { get; set; }

        //[DefaultValue(false)]                                               // Uncomment this to make the serialized JSON and/or XML file smaller
        //[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]  // Uncomment this to make the serialized JSON file smaller
        [XmlElement("HasQualities")]
        public bool HasQualities { get; set; }

        /// <summary>If null, all qualities are accepted</summary>
        [XmlArray("Qualities")]
        [XmlArrayItem("Quality")]
        [DefaultValue(null)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ObjectQuality[] Qualities { get; set; }

        //[DefaultValue(false)]
        //[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        [XmlElement("IsBigCraftable")]
        public bool IsBigCraftable { get; set; }

        public StoreableBagItem()
        {
            InitializeDefaults();
        }

        public StoreableBagItem(int Id, bool HasQualities, IEnumerable<ObjectQuality> Qualities = null, bool IsBigCraftable = false)
            : this(Id.ToString(), HasQualities, Qualities, IsBigCraftable) { }

        /// <param name="Qualities">If null, all Qualities will be included</param>
        public StoreableBagItem(string Id, bool HasQualities, IEnumerable<ObjectQuality> Qualities = null, bool IsBigCraftable = false)
        {
            InitializeDefaults();

            this.Id = Id;
            this.HasQualities = HasQualities;
            if (!HasQualities || Qualities == null)
                this.Qualities = null;
            else
                this.Qualities = Qualities.ToArray();
            this.IsBigCraftable = IsBigCraftable;
        }

        private void InitializeDefaults()
        {
            this.Id = null;
            this.HasQualities = true;
            this.IsBigCraftable = false;
            this.Qualities = null;
        }

        public override string ToString()
        {
            return string.Format("Id={0}, Qualities={1}, IsBigCraftable={2}", 
                Id, !HasQualities ? @"N/A" : Qualities == null ? "All" : string.Join("|", Qualities.Select(x => x.GetDescription())), IsBigCraftable);
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
