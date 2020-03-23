using ItemBags.Bags;
using ItemBags.Helpers;
using Newtonsoft.Json;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ItemBags.Persistence
{
    [JsonObject(Title = "ModdedItems")]
    [DataContract(Name = "ModdedItems", Namespace = "")]
    public class ModdedItems
    {
        /// <summary>This property is only public for serialization purposes. Use <see cref="CreatedByVersion"/> instead.</summary>
        [JsonProperty("CreatedByVersion")]
        public string CreatedByVersionString { get; set; }
        /// <summary>Warning - in old versions of the mod, this value may be null. This feature was added with v1.3.4</summary>
        [JsonIgnore]
        public Version CreatedByVersion {
            get { return string.IsNullOrEmpty(CreatedByVersionString) ? null : Version.Parse(CreatedByVersionString); }
            set { CreatedByVersionString = value?.ToString(); }
        }

        [JsonProperty("Mods")]
        public List<ModAddon> ModAddons { get; set; } = new List<ModAddon>();

        internal void OnGameLaunched()
        {
            IModHelper Helper = ItemBagsMod.ModInstance.Helper;

            //  Load modded items from JsonAssets the moment it finishes registering items
            string JAUniqueId = "spacechase0.JsonAssets";
            if (Helper.ModRegistry.IsLoaded(JAUniqueId))
            {
                IJsonAssetsAPI API = Helper.ModRegistry.GetApi<IJsonAssetsAPI>(JAUniqueId);
                if (API != null)
                {
                    API.IdsFixed += (sender, e) => { ImportModdedItems(API, ItemBagsMod.BagConfig); };
                }
            }
        }

        private bool HasImportedItems { get; set; } = false;

        private void ImportModdedItems(IJsonAssetsAPI API, BagConfig Target)
        {
            try
            {
                if (HasImportedItems)
                    return;

                IModHelper Helper = ItemBagsMod.ModInstance.Helper;

                //  Index all BagTypes by their names
                Dictionary<string, BagType> IndexedTypes = new Dictionary<string, BagType>();
                foreach (BagType Type in Target.BagTypes)
                {
                    if (!IndexedTypes.ContainsKey(Type.Name))
                    {
                        IndexedTypes.Add(Type.Name, Type);
                    }
                    else
                    {
                        ItemBagsMod.ModInstance.Monitor.Log(string.Format("Warning - multiple BagTypes were found with the name: '{0}'\nDid you manually edit your bagconfig.json file?", Type.Name), LogLevel.Warn);
                    }
                }

                IDictionary<string, int> ModdedBigCraftables = API.GetAllBigCraftableIds();
                IDictionary<string, int> ModdedObjects = API.GetAllObjectIds();

                //  Import items from each ModAddon
                bool ChangesMade = false;
                foreach (ModAddon ModAddon in ModAddons)
                {
                    if (Helper.ModRegistry.IsLoaded(ModAddon.UniqueId))
                    {
                        foreach (BagAddon BagAddon in ModAddon.BagAddons)
                        {
                            if (!IndexedTypes.TryGetValue(BagAddon.Name, out BagType TargetType))
                            {
                                ItemBagsMod.ModInstance.Monitor.Log(string.Format("Warning - no BagType found with Name = '{0}'. Modded items for this bag will not be imported.", BagAddon.Name), LogLevel.Warn);
                            }
                            else
                            {
                                foreach (ModdedItem Item in BagAddon.Items)
                                {
                                    int Id = -1;
                                    if ((Item.IsBigCraftable && !ModdedBigCraftables.TryGetValue(Item.Name, out Id)) || 
                                        (!Item.IsBigCraftable && !ModdedObjects.TryGetValue(Item.Name, out Id)))
                                    {
                                        string Message = string.Format("Warning - no modded item with Name = '{0}' was found in Mod with UniqueId = '{1}'. This item will not be imported.", Item.Name, ModAddon.UniqueId);
                                        ItemBagsMod.ModInstance.Monitor.Log(Message, LogLevel.Warn);
                                    }
                                    else
                                    {
                                        foreach (BagSizeConfig SizeConfig in TargetType.SizeSettings.Where(x => x.Size >= Item.Size))
                                        {
                                            SizeConfig.Items.Add(new StoreableBagItem(Id, Item.HasQualities, null, Item.IsBigCraftable));
                                            ChangesMade = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (ChangesMade)
                {
                    ItemBag.GetAllBags(true).ForEach(x => x.OnModdedItemsImported());
                }
            }
            catch (Exception ex)
            {
                ItemBagsMod.ModInstance.Monitor.Log(string.Format("Failed to import modded items. Error: {0}", ex.Message), LogLevel.Error);
            }
            finally { HasImportedItems = true; }
        }
    }

    [JsonObject(Title = "ModAddon")]
    [DataContract(Name = "ModAddon", Namespace = "")]
    public class ModAddon
    {
        [JsonProperty("ModUniqueId")]
        public string UniqueId { get; set; } = "";
        [JsonProperty("Bags")]
        public List<BagAddon> BagAddons { get; set; } = new List<BagAddon>();
    }

    [JsonObject(Title = "BagAddon")]
    [DataContract(Name = "BagAddon", Namespace = "")]
    public class BagAddon
    {
        /// <summary>The un-translated name of the standard bag type that is being modified, without a Size prefix. Case in-sensitive. EX: "Crop Bag".</summary>
        [JsonProperty("Name")]
        public string Name { get; set; } = "";
        /// <summary>Metadata about each modded item that should be storeable inside this bag.</summary>
        [JsonProperty("Items")]
        public List<ModdedItem> Items { get; set; } = new List<ModdedItem>();
    }

    [JsonObject(Title = "Item")]
    [DataContract(Name = "Item", Namespace = "")]
    public class ModdedItem
    {
        /// <summary>The un-translated name of the modded Object.</summary>
        [JsonProperty("Name")]
        public string Name { get; set; } = "";
        /// <summary>True if this Object is a placeable Object such as a Furnace.</summary>
        [JsonProperty("IsBigCraftable")]
        public bool IsBigCraftable { get; set; } = false;
        /// <summary>True if this Object is available in multiple different Qualities (Regular/Silver/Gold/Iridium)</summary>
        [JsonProperty("HasQualities")]
        public bool HasQualities { get; set; } = false;
        /// <summary>The minimum size of the bag that is required to store this Object.</summary>
        [JsonProperty("RequiredSize")]
        public string SizeString { get; set; } = ContainerSize.Small.ToString();

        [JsonIgnore]
        public ContainerSize Size { get { return string.IsNullOrEmpty(SizeString) ? ContainerSize.Small : (ContainerSize)Enum.Parse(typeof(ContainerSize), SizeString); } }
    }
}
