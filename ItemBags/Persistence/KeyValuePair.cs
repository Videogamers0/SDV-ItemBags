using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ItemBags.Persistence
{
    [Serializable]
    [XmlRoot(ElementName = "KeyValuePair", Namespace = "")]
    public struct KeyValuePair<K, V>
    {
        public K Key { get; set; }
        public V Value { get; set; }
        public KeyValuePair(K Key, V Value)
        {
            this.Key = Key;
            this.Value = Value;
        }

        public override string ToString()
        {
            return string.Format("Key = {0}, Value = {1}", Key.ToString(), Value.ToString());
        }
    }
}
