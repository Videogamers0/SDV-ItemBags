using ItemBags.Bags;
using ItemBags.Helpers;
using Newtonsoft.Json;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using static ItemBags.Persistence.BagSizeConfig;

namespace ItemBags.Persistence
{
    /// <summary>Represents a <see cref="BoundedBag"/> that only stores items belonging to a particular mod.</summary>
    [JsonObject(Title = "ModdedBag")]
    [DataContract(Name = "ModdedBag", Namespace = "")]
    public class ModdedBag
    {
        /// <summary>If false, this data file will not be loaded on startup</summary>
        [JsonProperty("IsEnabled")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>The UniqueID property of the mod manifest that this modded bag holds items for</summary>
        [JsonProperty("ModUniqueId")]
        public string ModUniqueId { get; set; } = "";
        [JsonIgnore]
        public string Guid { get { return StringToGUID(ModUniqueId).ToString(); } }
        //public string Guid { get { return StringToGUID(ModUniqueId + BagName).ToString(); } } // Could use this instead to allow multiple modded bags for the same mod. But this change wouldn't be backwards compatible with save files using the other Guids

        [JsonProperty("BagName")]
        public string BagName { get; set; } = "Unnamed";
        [JsonProperty("BagDescription")]
        public string BagDescription { get; set; } = "";

        [JsonProperty("Price")]
        public int Price { get; set; } = 10000;
        /// <summary>The maximum quantity of each item that this bag is capable of storing.</summary>
        [JsonProperty("Capacity")]
        public int Capacity { get; set; } = 9999;

        /// <summary>The shops that will sell this bag</summary>
        [JsonProperty("Sellers")]
        public List<BagShop> Sellers { get; set; } = new List<BagShop>() { BagShop.Pierre };

        [JsonProperty("MenuOptions")]
        public BagMenuOptions MenuOptions { get; set; } = new BagMenuOptions() {
            GroupedLayoutOptions = new BagMenuOptions.GroupedLayout() {
                GroupsPerRow = 5
            }
        };

        /// <summary>Metadata about each modded item that should be storeable inside this bag.</summary>
        [JsonProperty("Items")]
        public List<ModdedItem> Items { get; set; } = new List<ModdedItem>();

        //Taken from: https://weblogs.asp.net/haithamkhedre/generate-guid-from-any-string-using-c
        private static Guid StringToGUID(string value)
        {
            // Create a new instance of the MD5CryptoServiceProvider object.
            MD5 md5Hasher = MD5.Create();
            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(value));
            return new Guid(data);
        }

        /// <summary>Must be executed after <see cref="IJsonAssetsAPI.IdsFixed"/> event has fired.<para/>
        /// Returns all BigCraftable and all Objects that belong to the given mod manifest UniqueID. 
        /// Does not include other types of items such as Hats or Weapons.</summary>
        internal static List<ModdedItem> GetModdedItems(string ModUniqueId, ContainerSize RequiredSize = ContainerSize.Small)
        {
            List<ModdedItem> Items = new List<ModdedItem>();

            IModHelper Helper = ItemBagsMod.ModInstance.Helper;
            if (Helper.ModRegistry.IsLoaded(ItemBagsMod.JAUniqueId))
            {
                IJsonAssetsAPI API = Helper.ModRegistry.GetApi<IJsonAssetsAPI>(ItemBagsMod.JAUniqueId);
                if (API != null)
                {
                    List<string> Objects = API.GetAllObjectsFromContentPack(ModUniqueId);
                    if (Objects != null)
                        Items.AddRange(Objects.Select(x => new ModdedItem(x, false, true, RequiredSize)));
                    //List<string> Crops = API.GetAllCropsFromContentPack(ModUniqueId);
                    //if (Crops != null)
                    //    Items.AddRange(Crops.Select(x => new ModdedItem(x, false, false, RequiredSize)));
                    List<string> BigCraftables = API.GetAllBigCraftablesFromContentPack(ModUniqueId);
                    if (BigCraftables != null)
                        Items.AddRange(BigCraftables.Select(x => new ModdedItem(x, true, false, RequiredSize)));
                }
            }

            return Items;
        }

        internal static void OnGameLaunched()
        {
            IModHelper Helper = ItemBagsMod.ModInstance.Helper;

            //  Load modded items from JsonAssets the moment it finishes registering items
            if (Helper.ModRegistry.IsLoaded(ItemBagsMod.JAUniqueId))
            {
                IJsonAssetsAPI API = Helper.ModRegistry.GetApi<IJsonAssetsAPI>(ItemBagsMod.JAUniqueId);
                if (API != null)
                {
                    API.IdsFixed += (sender, e) => { ImportModdedBags(API, ItemBagsMod.BagConfig); };
                }
            }
        }

        internal static bool HasImportedItems { get; set; } = false;

        private static void ImportModdedBags(IJsonAssetsAPI API, BagConfig Target)
        {
            try
            {
                if (HasImportedItems)
                    return;

                IModHelper Helper = ItemBagsMod.ModInstance.Helper;

                IDictionary<string, int> BigCraftableIds = API.GetAllBigCraftableIds();
                IDictionary<string, int> ObjectIds = API.GetAllObjectIds();

                bool ChangesMade = false;

                //  Now that JsonAssets has finished loading the modded items, go through each one, and convert the items into StoreableBagItems (which requires an Id instead of just a Name)
                foreach (System.Collections.Generic.KeyValuePair<ModdedBag, BagType> KVP in ItemBagsMod.TemporaryModdedBagTypes)
                {
                    List<StoreableBagItem> Items = KVP.Key.Items.Select(x => x.ToStoreableBagItem(BigCraftableIds, ObjectIds)).Where(x => x != null).ToList();
                    if (Items.Any())
                    {
                        foreach (BagSizeConfig SizeCfg in KVP.Value.SizeSettings)
                        {
                            SizeCfg.Items.AddRange(Items);
                        }

                        ChangesMade = true;
                    }
                }

                if (ChangesMade)
                {
                    ItemBag.GetAllBags(true).ForEach(x => x.OnModdedItemsImported());
                }
            }
            catch (Exception ex)
            {
                ItemBagsMod.ModInstance.Monitor.Log(string.Format("Failed to import modded bags. Error: {0}", ex.Message), LogLevel.Error);
            }
            finally { HasImportedItems = true; }
        }

        internal BagType GetBagTypePlaceholder()
        {
            return new BagType()
            {
                  Id = Guid,
                  Name = BagName,
                  Description = BagDescription,
                  IconSourceTexture = BagType.SourceTexture.SpringObjects,
                  IconSourceRect = new Microsoft.Xna.Framework.Rectangle(),
                  SizeSettings = new BagSizeConfig[]
                  {
                      new BagSizeConfig()
                      {
                          Size = ContainerSize.Small,
                          MenuOptions = MenuOptions,
                          Price = Price,
                          Sellers = new BagShop[] { },
                          CapacityMultiplier = 1.0,
                          Items = new List<StoreableBagItem>()
                      },
                      new BagSizeConfig()
                      {
                          Size = ContainerSize.Medium,
                          MenuOptions = MenuOptions,
                          Price = Price,
                          Sellers = new BagShop[] { },
                          CapacityMultiplier = 1.0,
                          Items = new List<StoreableBagItem>()
                      },
                      new BagSizeConfig()
                      {
                          Size = ContainerSize.Large,
                          MenuOptions = MenuOptions,
                          Price = Price,
                          Sellers = new BagShop[] { },
                          CapacityMultiplier = 1.0,
                          Items = new List<StoreableBagItem>()
                      },
                      new BagSizeConfig()
                      {
                          Size = ContainerSize.Giant,
                          MenuOptions = MenuOptions,
                          Price = Price,
                          Sellers = new BagShop[] { },
                          CapacityMultiplier = 1.0,
                          Items = new List<StoreableBagItem>()
                      },
                      new BagSizeConfig()
                      {
                          Size = ContainerSize.Massive,
                          MenuOptions = MenuOptions,
                          Price = Price,
                          Sellers = Sellers.ToArray(),
                          CapacityMultiplier = 1.0,
                          Items = new List<StoreableBagItem>()
                      }
                  }
            };
        }
    }

    /// <summary>Represents modded items that should be merged into non-modded bags, such as storing a modded seed item in the built-in "Seed Bag"</summary>
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
            if (Helper.ModRegistry.IsLoaded(ItemBagsMod.JAUniqueId))
            {
                IJsonAssetsAPI API = Helper.ModRegistry.GetApi<IJsonAssetsAPI>(ItemBagsMod.JAUniqueId);
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
        /// <summary>The un-translated name of the standard bag type that is being modified, without a Size prefix. Case sensitive. EX: "Crop Bag".</summary>
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

        public ModdedItem()
        {
            this.Name = "";
            this.IsBigCraftable = false;
            this.HasQualities = false;
            this.SizeString = ContainerSize.Small.ToString();
        }

        public ModdedItem(string Name, bool IsBigCraftable, bool HasQualities, ContainerSize Size)
        {
            this.Name = Name;
            this.IsBigCraftable = IsBigCraftable;
            this.HasQualities = HasQualities;
            this.SizeString = Size.ToString();
        }

        [JsonIgnore]
        public ContainerSize Size { get { return string.IsNullOrEmpty(SizeString) ? ContainerSize.Small : (ContainerSize)Enum.Parse(typeof(ContainerSize), SizeString); } }

        public StoreableBagItem ToStoreableBagItem(IDictionary<string, int> BigcraftableIds, IDictionary<string, int> ObjectIds)
        {
            if (IsBigCraftable)
            {
                if (BigcraftableIds.TryGetValue(Name, out int Id))
                    return new StoreableBagItem(Id, HasQualities, null, IsBigCraftable);
                else
                    return null;
            }
            else
            {
                if (ObjectIds.TryGetValue(Name, out int Id))
                    return new StoreableBagItem(Id, HasQualities, null, IsBigCraftable);
                else
                    return null;
            }
        }
    }
}
