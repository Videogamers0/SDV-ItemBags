﻿using ItemBags.Bags;
using ItemBags.Helpers;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Objects;
using StardewValley.ItemTypeDefinitions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static ItemBags.Persistence.BagSizeConfig;
using Object = StardewValley.Object;

namespace ItemBags.Persistence
{
    /// <summary>Represents a <see cref="BoundedBag"/> that can store custom items belonging to other mods.</summary>
    [DataContract(Name = "ModdedBag", Namespace = "")]
    public class ModdedBag
    {
        public static readonly ReadOnlyCollection<ContainerSize> AllSizes = Enum.GetValues(typeof(ContainerSize)).Cast<ContainerSize>().ToList().AsReadOnly();

        /// <summary>If false, this data file will not be loaded on startup</summary>
        [JsonProperty("IsEnabled")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>The UniqueID property of the mod manifest that this modded bag holds items for</summary>
        [JsonProperty("ModUniqueId")]
        public string ModUniqueId { get; set; } = "";

        /// <summary>A unique identifier for this modded bag. Typically this Guid is computed using <see cref="StringToGUID(string)"/> with parameter = "<see cref="ModUniqueId"/>+<see cref="BagName"/>"</summary>
        [JsonProperty("BagId")]
        public string Guid { get; set; } = "";

        [JsonProperty("BagName")]
        public string BagName { get; set; } = "Unnamed";
        [JsonProperty("BagDescription")]
        public string BagDescription { get; set; } = "";

        [JsonProperty("IconTexture")]
        public BagType.SourceTexture IconTexture { get; set; } = BagType.SourceTexture.SpringObjects;
        [JsonProperty("IconPosition")]
        public Rectangle IconPosition { get; set; } = new Rectangle();

        [JsonProperty("Prices")]
        public Dictionary<ContainerSize, int> Prices { get; set; } = AllSizes.ToDictionary(x => x, x => BagTypeFactory.DefaultPrices[x]);
        [JsonProperty("Capacities")]
        public Dictionary<ContainerSize, int> Capacities { get; set; } = AllSizes.ToDictionary(x => x, x => BagTypeFactory.DefaultCapacities[x]);
        [JsonProperty("SizeSellers")]
        public Dictionary<ContainerSize, List<BagShop>> Sellers { get; set; } = AllSizes.ToDictionary(x => x, x => new List<BagShop>() { BagShop.Pierre });

        private static readonly BagMenuOptions DefaultMenuOptions = new BagMenuOptions() {
            GroupedLayoutOptions = new BagMenuOptions.GroupedLayout() {
                GroupsPerRow = 5
            }
        };
        [JsonProperty("SizeMenuOptions")]
        public Dictionary<ContainerSize, BagMenuOptions> MenuOptions { get; set; } = AllSizes.ToDictionary(x => x, x => DefaultMenuOptions.GetCopy());

        /// <summary>Metadata about each modded item that should be storeable inside this bag.</summary>
        [JsonProperty("Items")]
        public List<ModdedItem> Items { get; set; } = new List<ModdedItem>();

        /// <summary>Optional. If specified, this bag will be able to store all items belonging to the given category Ids.<para/>
        /// See also: <see cref="Items"/></summary>
        [JsonProperty("ItemCategories")]
        public Dictionary<ContainerSize, List<int>> ItemCategories { get; set; } = AllSizes.ToDictionary(x => x, x => new List<int>());

        [JsonProperty("ItemFilters")]
        public List<string> ItemFilters { get; set; } = new List<string>();

        [JsonProperty("CategoryQualities")]
        public string CategoryQualities { get; set; }

        //Taken from: https://weblogs.asp.net/haithamkhedre/generate-guid-from-any-string-using-c
        public static Guid StringToGUID(string value)
        {
            // Create a new instance of the MD5CryptoServiceProvider object.
            MD5 md5Hasher = MD5.Create();
            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(value));
            return new Guid(data);
        }

        //  Categories can be found here: https://stardewvalleywiki.com/Modding:Object_data#Categories
        /// <summary>Object instances belonging to these categories can have different values in <see cref="Object.Quality"/>. This list might not be accurate.</summary>
        internal static readonly ReadOnlyCollection<int> CategoriesWithQualities = new List<int>() {
            Object.FishCategory, Object.EggCategory, Object.MilkCategory, Object.meatCategory,
            Object.sellAtPierresAndMarnies, Object.artisanGoodsCategory, /*Object.syrupCategory,*/
            Object.VegetableCategory, Object.FruitsCategory, Object.flowersCategory, Object.GreensCategory
        }.AsReadOnly();

        internal static void LogInvalidObjectData(string Id, ObjectData Data) => 
            ItemBagsMod.Logger.LogOnce($"{Id} does not contain a valid Name nor DisplayName in Game1.objectData. " +
                $"If this item belongs to a modded bag, it will be skipped when initializing the bag. " +
                $"(Name={Data.Name ?? "null"}, DisplayName={Data.DisplayName ?? "null"})", LogLevel.Warn);

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
                    {
                        //  Index all regular Objects by their names
                        Dictionary<string, string> AllObjectIds = new Dictionary<string, string>();
                        foreach (System.Collections.Generic.KeyValuePair<string, ObjectData> KVP in Game1.objectData)
                        {
                            string ObjectName = KVP.Value.DisplayName ?? KVP.Value.Name;
                            if (string.IsNullOrEmpty(ObjectName))
                                LogInvalidObjectData(KVP.Key, KVP.Value);
                            else if (!AllObjectIds.ContainsKey(ObjectName))
                                AllObjectIds.Add(ObjectName, KVP.Key);
                        }

                        foreach (string ModdedItemName in Objects)
                        {
                            ModdedItem Item;
                            if (AllObjectIds.TryGetValue(ModdedItemName, out string ItemId))
                            {
                                //  Try to guess if the item has multiple different valid qualities, based on its category
                                bool HasQualities = CategoriesWithQualities.Contains(Game1.objectData[ItemId].Category);
                                Item = new ModdedItem(ItemId, true, false, HasQualities, RequiredSize);
                            }
                            else
                            {
                                Item = new ModdedItem(ModdedItemName, false, false, true, RequiredSize);
                            }

                            Items.Add(Item);
                        }
                    }

                    List<string> BigCraftables = API.GetAllBigCraftablesFromContentPack(ModUniqueId);
                    if (BigCraftables != null)
                    {
                        //  Index all BigCraftables by their names
                        Dictionary<string, string> AllBigCraftableIds = new Dictionary<string, string>();
                        foreach (System.Collections.Generic.KeyValuePair<string, BigCraftableData> KVP in Game1.bigCraftableData)
                        {
                            string BigCraftableName = KVP.Value.DisplayName ?? KVP.Value.Name;
                            if (!AllBigCraftableIds.ContainsKey(BigCraftableName))
                                AllBigCraftableIds.Add(BigCraftableName, KVP.Key);
                        }

                        foreach (string ModdedItemName in BigCraftables)
                        {
                            ModdedItem Item;
                            if (AllBigCraftableIds.TryGetValue(ModdedItemName, out string ItemId))
                            {
                                Item = new ModdedItem(ItemId, true, true, false, RequiredSize);
                            }
                            else
                            {
                                Item = new ModdedItem(ModdedItemName, false, true, false, RequiredSize);
                            }

                            Items.Add(Item);
                        }
                    }
                }
            }

            //  Try to find CP items belonging to this mod by looking through Game1.objectData for ObjectData whose name or texture property begins with the mod's unique Id
            //  (Because it's very common to prefix modded QualifiedItemIds with the mod's UniqueId)
            if (!Items.Any())
            {
                IEnumerable<ObjectData> Matches = Game1.objectData.Values
                    .Where(x => !string.IsNullOrEmpty(x.Name) && (x.Name.StartsWith(ModUniqueId) || (!string.IsNullOrEmpty(x.Texture) && x.Texture.StartsWith(ModUniqueId))))
                    .GroupBy(x => x.Name).Select(x => x.First()); // In rare cases, there may be multiple ObjectData entries with the same Id, such as "bees.pkr_combeefegg" from Pokemon Ranch mod v1.7.
                foreach (ObjectData Match in Matches)
                {
                    string Id = Match.Name;
                    bool HasQualities = CategoriesWithQualities.Contains(Match.Category);
                    ModdedItem Item = new ModdedItem(Id, true, false, HasQualities, RequiredSize);
                    ItemMetadata Metadata = ItemRegistry.ResolveMetadata(Id);
                    if (Metadata == null)
                    {
                        ItemBagsMod.ModInstance.Monitor.Log($"Failed to retrieve item metadata for {Match.Name}. ItemRegistry.ResolveMetadata(\"{Match.Name}\") returned null. If this is a valid item, it will be skipped for processing.", LogLevel.Warn);
                        continue;
                    }
                    bool IsBigCraftable = Metadata?.TypeIdentifier == "(BC)";
                    Items.Add(Item);
                }
            }

            return Items;
        }

        internal static void OnGameLaunched()
        {
            IModHelper Helper = ItemBagsMod.ModInstance.Helper;

#if LEGACY_CODE
            //  Load modded items from JsonAssets the moment it finishes registering items
            if (Helper.ModRegistry.IsLoaded(ItemBagsMod.JAUniqueId))
            {
                IJsonAssetsAPI API = Helper.ModRegistry.GetApi<IJsonAssetsAPI>(ItemBagsMod.JAUniqueId);
                if (API != null)
                {
                    //  JsonAssets removed this API call when updating for 1.6
                    //API.IdsFixed += (sender, e) => { OnJsonAssetsIdsFixed(API, ItemBagsMod.BagConfig, true); };
                }
            }
#else
            Helper.Events.GameLoop.SaveLoaded += (sender, e) =>
            {
                void DoWork()
                {
                    IJsonAssetsAPI API = Helper.ModRegistry.IsLoaded(ItemBagsMod.JAUniqueId) ? Helper.ModRegistry.GetApi<IJsonAssetsAPI>(ItemBagsMod.JAUniqueId) : null;
                    OnJsonAssetsIdsFixed(API, ItemBagsMod.BagConfig, true);
                }
                DelayHelpers.InvokeLater(1, DoWork);
            };
#endif
        }

        internal static void OnConnectedToHost()
        {
            if (!Context.IsMainPlayer)
            {
                IModHelper Helper = ItemBagsMod.ModInstance.Helper;
                if (Helper.ModRegistry.IsLoaded(ItemBagsMod.JAUniqueId))
                {
                    IJsonAssetsAPI API = Helper.ModRegistry.GetApi<IJsonAssetsAPI>(ItemBagsMod.JAUniqueId);
                    if (API != null)
                    {
                        OnJsonAssetsIdsFixed(API, ItemBagsMod.BagConfig, false);
                    }
                }
            }
        }

        private static IReadOnlyDictionary<int, IReadOnlyList<StoreableBagItem>> _ItemsByCategory;
        private static IReadOnlyDictionary<int, IReadOnlyList<StoreableBagItem>> ItemsByCategory
        {
            get
            {
                _ItemsByCategory ??= GetItemsByCategory(false);
                return _ItemsByCategory;
            }
        }

        private static IReadOnlyDictionary<int, IReadOnlyList<StoreableBagItem>> GetItemsByCategory(bool includeBigCraftables)
        {
            Dictionary<int, List<StoreableBagItem>> Tmp = new Dictionary<int, List<StoreableBagItem>>();

            if (includeBigCraftables)
            {
                foreach (System.Collections.Generic.KeyValuePair<string, BigCraftableData> KVP in Game1.bigCraftableData)
                {
                    string Id = KVP.Key;
                    BigCraftableData Data = KVP.Value;
                    ItemMetadata Meta = ItemRegistry.GetMetadata(Id);
                    int CategoryId = Meta.GetParsedData().Category;
                    if (!Tmp.TryGetValue(CategoryId, out List<StoreableBagItem> CategoryItems))
                    {
                        CategoryItems = new List<StoreableBagItem>();
                        Tmp.Add(CategoryId, CategoryItems);
                    }
                    CategoryItems.Add(new StoreableBagItem(Meta.LocalItemId, false, null, true));
                }
            }

            foreach (System.Collections.Generic.KeyValuePair<string, ObjectData> KVP in Game1.objectData)
            {
                string Id = KVP.Key;
                ObjectData Data = KVP.Value;
                ItemMetadata Meta = ItemRegistry.GetMetadata(Id);
                int CategoryId = Meta.GetParsedData().Category;
                if (!Tmp.TryGetValue(CategoryId, out List<StoreableBagItem> CategoryItems))
                {
                    CategoryItems = new List<StoreableBagItem>();
                    Tmp.Add(CategoryId, CategoryItems);
                }
                CategoryItems.Add(new StoreableBagItem(Meta.LocalItemId, CategoriesWithQualities.Contains(CategoryId), null, false));
            }

            Dictionary<int, IReadOnlyList<StoreableBagItem>> Result = new Dictionary<int, IReadOnlyList<StoreableBagItem>>();
            foreach (var KVP in Tmp)
                Result.Add(KVP.Key, KVP.Value);
            return Result;
        }

        private static void OnJsonAssetsIdsFixed(IJsonAssetsAPI API, BagConfig Target, bool RevalidateInstances)
        {
            try
            {
                ItemBagsMod.ModdedItems.ImportModdedItems(API, ItemBagsMod.BagConfig);

                if (ItemBagsMod.TemporaryModdedBagTypes.Any())
                {
                    ItemBagsMod.ModInstance.Monitor.Log("Loading Modded Bags type info", LogLevel.Debug);

                    Dictionary<string, string> AllBigCraftableIds = new Dictionary<string, string>();
                    foreach (System.Collections.Generic.KeyValuePair<string, BigCraftableData> KVP in Game1.bigCraftableData)
                    {
                        string ObjectName = KVP.Value.DisplayName ?? KVP.Value.Name;
                        if (!AllBigCraftableIds.ContainsKey(ObjectName))
                            AllBigCraftableIds.Add(ObjectName, KVP.Key);
                    }

                    Dictionary<string, string> AllObjectIds = new Dictionary<string, string>();
                    foreach (System.Collections.Generic.KeyValuePair<string, ObjectData> KVP in Game1.objectData)
                    {
                        string ObjectName = KVP.Value.DisplayName ?? KVP.Value.Name;
                        if (string.IsNullOrEmpty(ObjectName))
                            LogInvalidObjectData(KVP.Key, KVP.Value);
                        else if (!AllObjectIds.ContainsKey(ObjectName))
                            AllObjectIds.Add(ObjectName, KVP.Key);
                    }

                    //  JsonAssets removed these API calls when updating for 1.6
                    //IDictionary<string, int> JABigCraftableIds = API.GetAllBigCraftableIds();
                    //IDictionary<string, int> JAObjectIds = API.GetAllObjectIds();
                    IDictionary<string, int> JABigCraftableIds = new Dictionary<string, int>();
                    IDictionary<string, int> JAObjectIds = new Dictionary<string, int>();

                    //  Now that JsonAssets has finished loading the modded items, go through each one, and convert the items into StoreableBagItems (which requires an Id instead of just a Name)
                    foreach (System.Collections.Generic.KeyValuePair<ModdedBag, BagType> KVP in ItemBagsMod.TemporaryModdedBagTypes)
                    {
                        List<ObjectQuality> AllQualities = Enum.GetValues(typeof(ObjectQuality)).Cast<ObjectQuality>().ToList();
                        List<ObjectQuality> RegularQualites = new List<ObjectQuality>() { ObjectQuality.Regular };
                        List<int> QualityCategories = CategoriesWithQualities.ToList();

                        //  Parse category quality overrides - this property tells us which CategoryIds support items with multiple quality values (gold/silver/iridium) instead of just normal quality
                        if (!string.IsNullOrEmpty(KVP.Key.CategoryQualities))
                        {
                            try
                            {
                                List<string> CategoryOverrides = KVP.Key.CategoryQualities.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                                foreach (string Override in CategoryOverrides)
                                {
                                    int DelimiterIndex = Override.IndexOf(':');
                                    int CategoryId = int.Parse(Override.Substring(0, DelimiterIndex));
                                    bool OverrideValue = bool.Parse(Override.Substring(DelimiterIndex + 1));

                                    if (!OverrideValue && QualityCategories.Contains(CategoryId))
                                        QualityCategories.Remove(CategoryId);
                                    else if (OverrideValue && !QualityCategories.Contains(CategoryId))
                                        QualityCategories.Add(CategoryId);
                                }
                            }
                            catch (Exception ex)
                            {
                                ItemBagsMod.ModInstance.Monitor.Log($"Error while parsing {nameof(CategoryQualities)} property for bag '{KVP.Key.BagName}' with value \"{KVP.Key.CategoryQualities}\":\n{ex}");
                            }
                        }

                        foreach (BagSizeConfig SizeCfg in KVP.Value.SizeSettings)
                        {
                            HashSet<string> FailedItemNames = new HashSet<string>();
                            List<StoreableBagItem> Items = new List<StoreableBagItem>();

                            foreach (ModdedItem DesiredItem in KVP.Key.Items.Where(x => x.Size <= SizeCfg.Size))
                            {
                                StoreableBagItem Item = DesiredItem.ToStoreableBagItem(JABigCraftableIds, JAObjectIds, AllBigCraftableIds, AllObjectIds);
                                if (Item == null)
                                    FailedItemNames.Add(DesiredItem.Name);
                                else
                                    Items.Add(Item);
                            }

                            if (KVP.Key.ItemCategories != null && KVP.Key.ItemCategories.ContainsKey(SizeCfg.Size))
                            {
                                HashSet<string> ItemIds = Items.Select(x => x.Id).ToHashSet();
                                foreach (int CategoryId in KVP.Key.ItemCategories[SizeCfg.Size])
                                {
                                    if (!ItemsByCategory.TryGetValue(CategoryId, out IReadOnlyList<StoreableBagItem> CategoryItems))
                                        ItemBagsMod.ModInstance.Monitor.Log($"Warning - No category found with Id={CategoryId}. The modded bag '{KVP.Key.BagName}' will skip items of this category id.");
                                    else
                                        Items.AddRange(ItemsByCategory[CategoryId].Where(x => !ItemIds.Contains(x.Id)));
                                }
                            }

                            //  Process the ItemFilters modded bag property which allows users to specify valid bag items via filters instead of explicitly defining each item by Id
                            if (KVP.Key.ItemFilters?.Any() == true)
                            {
                                ModdedBag Bag = KVP.Key;
                                List<IItemFilter> Filters = new List<IItemFilter>();
                                foreach (string FilterString in KVP.Key.ItemFilters)
                                {
                                    try
                                    {
                                        IItemFilter CurrentFilter = ItemFilter.Parse(Bag, FilterString);
                                        Filters.Add(CurrentFilter);
                                    }
                                    catch (Exception ex)
                                    {
                                        ItemBagsMod.ModInstance.Monitor.Log($"Error while parsing filter for bag '{Bag.BagName}': {FilterString}.\n{ex}");
                                    }
                                }

                                IItemFilter Filter = new ItemFilterGroup(CompositionType.LogicalAND, Filters.ToArray());
                                bool HasQualityFilters = ItemFilter.EnumerateFilters(Filter).Any(x => x.UsesQuality);

                                HashSet<string> ItemIds = Items.Select(x => x.Id).ToHashSet();
                                foreach (var KVP2 in Game1.bigCraftableData)
                                {
                                    string Id = KVP2.Key;
                                    if (!ItemRegistry.IsQualifiedItemId(Id))
                                        Id = ItemRegistry.ManuallyQualifyItemId(Id, ItemRegistry.type_bigCraftable);
                                    if (ItemIds.Contains(Id))
                                        continue;
                                    BigCraftableData Data = KVP2.Value;
                                    ParsedItemData ParsedData = ItemRegistry.GetData(Id);
                                    if (Filter.IsMatch(Data, ParsedData, SizeCfg.Size, ObjectQuality.Regular))
                                        Items.Add(new StoreableBagItem(ParsedData.ItemId, false, null, true));
                                }

                                foreach (var KVP2 in Game1.objectData)
                                {
                                    string Id = KVP2.Key;
                                    if (ItemIds.Contains(Id))
                                        continue;
                                    ObjectData Data = KVP2.Value;
                                    ParsedItemData ParsedData = ItemRegistry.GetData(Id);
                                    int CategoryId = ParsedData.Category;
                                    bool HasQualities = QualityCategories.Contains(CategoryId);

                                    //  For performance purposes, we don't need to check every quality if the filter doesn't use that data
                                    if (!HasQualityFilters)
                                    {
                                        if (Filter.IsMatch(Data, ParsedData, SizeCfg.Size, ObjectQuality.Regular))
                                            Items.Add(new StoreableBagItem(Id, HasQualities, null, false));
                                    }
                                    else
                                    {
                                        List<ObjectQuality> ValidQualities = (HasQualities ? AllQualities : RegularQualites)
                                            .Where(x => Filter.IsMatch(Data, ParsedData, SizeCfg.Size, x)).ToList();
                                        if (ValidQualities.Any())
                                            Items.Add(new StoreableBagItem(Id, HasQualities, ValidQualities, false));
                                    }
                                }
                            }

                            SizeCfg.Items = Items;

                            if (FailedItemNames.Any())
                            {
                                int MaxNamesShown = 5;
                                string MissingItems = string.Format("{0}{1}", string.Join(", ", FailedItemNames.Take(MaxNamesShown)),
                                    FailedItemNames.Count <= MaxNamesShown ? "" : string.Format(" + {0} more", (FailedItemNames.Count - MaxNamesShown)));
                                string WarningMsg = string.Format("Warning - {0} items could not be found for modded bag '{1} {2}'. Missing Items: {3}",
                                    FailedItemNames.Count, SizeCfg.Size.ToString(), KVP.Key.BagName, MissingItems);
                                ItemBagsMod.ModInstance.Monitor.Log(WarningMsg, LogLevel.Warn);
                            }
                        }
                    }

                    if (RevalidateInstances)
                        ItemBag.GetAllBags(true).ForEach(x => x.OnJsonAssetsItemIdsFixed(API, true));
                }
            }
            catch (Exception ex)
            {
                ItemBagsMod.ModInstance.Monitor.Log(string.Format("Failed to import modded bags. Error: {0}\n\n{1}", ex.Message, ex.ToString()), LogLevel.Error);
            }
        }

        internal BagType GetBagTypePlaceholder()
        {
            return new BagType()
            {
                  Id = Guid,
                  Name = BagName,
                  Description = BagDescription,
                  IconSourceTexture = IconTexture,
                  IconSourceRect = IconPosition,
                  SizeSettings = AllSizes.Select(x => new BagSizeConfig()
                  {
                      Size = x,
                      MenuOptions = MenuOptions[x],
                      Price = Prices[x],
                      Sellers = Sellers[x],
                      CapacityMultiplier = BagTypeFactory.GetCapacityMultiplier(x, Capacities[x]),
                      Items = new List<StoreableBagItem>()
                  }).ToArray()
            };
        }

        #region Backwards Compatibility
        /// <summary>Deprecated. Use <see cref="Prices"/> instead.</summary>
        [JsonProperty("Price")]
        private int DeprecatedPrice { set { Prices = AllSizes.ToDictionary(x => x, x => value); } }
        /// <summary>Deprecated. Use <see cref="Capacities"/> instead. The maximum quantity of each item that this bag is capable of storing.</summary>
        [JsonProperty("Capacity")]
        private int DeprecatedCapacity { set { Capacities = AllSizes.ToDictionary(x => x, x => value); } }
        /// <summary>Deprecated. Use <see cref="Sellers"/> instead. The shops that will sell this bag</summary>
        [JsonProperty("Sellers")]
        private List<BagShop> DeprecatedSellers { set { Sellers = AllSizes.ToDictionary(x => x, x => new List<BagShop>(value)); } }
        /// <summary>Deprecated. Use <see cref="MenuOptions"/> instead.</summary>
        [JsonProperty("MenuOptions")]
        private BagMenuOptions DeprecatedMenuOptions { set { MenuOptions = AllSizes.ToDictionary(x => x, x => value.GetCopy()); } }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext sc)
        {
            if (string.IsNullOrEmpty(Guid))
                Guid = GetLegacyGuid(ModUniqueId);
        }

        public static string GetLegacyGuid(string ModUniqueId) { return StringToGUID(ModUniqueId).ToString(); }
        #endregion Backwards Compatibility
    }

    /// <summary>Represents modded items that should be merged into non-modded bags, such as storing a modded seed item in the built-in "Seed Bag"</summary>
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

        private bool HasImportedItems { get; set; } = false;
        internal void ImportModdedItems(IJsonAssetsAPI API, BagConfig Target)
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
                        ItemBagsMod.ModInstance.Monitor.Log(string.Format("Warning - multiple BagTypes were found with the name: '{0}'\nDid you manually edit your bagconfig.json file or do you have multiple Modded Bags with the same name?", Type.Name), LogLevel.Warn);
                    }
                }

                Dictionary<string, string> AllBigCraftableIds = new Dictionary<string, string>();
                foreach (System.Collections.Generic.KeyValuePair<string, BigCraftableData> KVP in Game1.bigCraftableData)
                {
                    string ObjectName = KVP.Value.DisplayName ?? KVP.Value.Name;
                    if (!AllBigCraftableIds.ContainsKey(ObjectName))
                        AllBigCraftableIds.Add(ObjectName, KVP.Key);
                }

                Dictionary<string, string> AllObjectIds = new Dictionary<string, string>();
                foreach (System.Collections.Generic.KeyValuePair<string, ObjectData> KVP in Game1.objectData)
                {
                    string ObjectName = KVP.Value.DisplayName ?? KVP.Value.Name;
                    if (string.IsNullOrEmpty(ObjectName))
                        ModdedBag.LogInvalidObjectData(KVP.Key, KVP.Value);
                    else if (!AllObjectIds.ContainsKey(ObjectName))
                        AllObjectIds.Add(ObjectName, KVP.Key);
                }

                //  JsonAssets removed these API calls when updating for 1.6
                //IDictionary<string, int> JABigCraftableIds = API.GetAllBigCraftableIds();
                //IDictionary<string, int> JAObjectIds = API.GetAllObjectIds();
                IDictionary<string, int> JABigCraftableIds = new Dictionary<string, int>();
                IDictionary<string, int> JAObjectIds = new Dictionary<string, int>();

                //  Import items from each ModAddon
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
                                    string Id = null;
                                    if (!string.IsNullOrEmpty(Item.ObjectId))
                                        Id = Item.ObjectId;
                                    else
                                    {
                                        if ((Item.IsBigCraftable && !JABigCraftableIds.TryGetValue(Item.Name, out int IntId) && !AllBigCraftableIds.TryGetValue(Item.Name, out Id)) ||
                                            (!Item.IsBigCraftable && !JAObjectIds.TryGetValue(Item.Name, out IntId) && !AllObjectIds.TryGetValue(Item.Name, out Id)))
                                        {
                                            string Message = string.Format("Warning - no item with Name = '{0}' was found. This item will not be imported to Bag '{1}'.", Item.Name, BagAddon.Name);
                                            ItemBagsMod.ModInstance.Monitor.Log(Message, LogLevel.Warn);
                                            continue;
                                        }
                                    }

                                    foreach (BagSizeConfig SizeConfig in TargetType.SizeSettings.Where(x => x.Size >= Item.Size))
                                    {
                                        SizeConfig.Items.Add(new StoreableBagItem(Id, Item.HasQualities, null, Item.IsBigCraftable));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ItemBagsMod.ModInstance.Monitor.Log(string.Format("Failed to import modded items. Error: {0}\n\n{1}", ex.Message, ex.ToString()), LogLevel.Error);
            }
            finally { HasImportedItems = true; }
        }
    }

    [DataContract(Name = "ModAddon", Namespace = "")]
    public class ModAddon
    {
        [JsonProperty("ModUniqueId")]
        public string UniqueId { get; set; } = "";
        [JsonProperty("Bags")]
        public List<BagAddon> BagAddons { get; set; } = new List<BagAddon>();
    }

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
        /// <summary>Optional. You only need to specify either <see cref="Name"/> or <see cref="ObjectId"/>, not both.<para/>
        /// Do not use <see cref="ObjectId"/> if the item does not have a static item id (such as a JsonAssets modded item).</summary>
        [JsonProperty("ObjectId")]
        public string ObjectId { get; set; } = null;

        public ModdedItem()
        {
            this.Name = "";
            this.IsBigCraftable = false;
            this.HasQualities = false;
            this.SizeString = ContainerSize.Small.ToString();
            this.ObjectId = null;
        }

        public ModdedItem(string NameOrId, bool IsId, bool IsBigCraftable, bool HasQualities, ContainerSize Size)
        {
            if (IsId)
            {
                this.Name = null;
                this.ObjectId = NameOrId;
            }
            else
            {
                this.Name = NameOrId;
                this.ObjectId = null;
            }

            this.IsBigCraftable = IsBigCraftable;
            this.HasQualities = HasQualities;
            this.SizeString = Size.ToString();
        }

        public ModdedItem(Object Item, bool HasStableId)
        {
            if (HasStableId)
            {
                this.Name = null;
                this.ObjectId = Item.ItemId; //Item.QualifiedItemId ?? Item.ItemId;
            }
            else
            {
                this.Name = Item.DisplayName;
                this.ObjectId = null;
            }

            this.IsBigCraftable = Item.bigCraftable.Value;
            this.HasQualities = ModdedBag.CategoriesWithQualities.Contains(Item.Category);
            this.SizeString = ContainerSize.Small.ToString();
        }

        [JsonIgnore]
        public ContainerSize Size { get { return string.IsNullOrEmpty(SizeString) ? ContainerSize.Small : (ContainerSize)Enum.Parse(typeof(ContainerSize), SizeString); } }

        /// <param name="JABigCraftableIds">Ids of BigCraftable items added through JsonAssets. See also: <see cref="IJsonAssetsAPI.GetAllBigCraftableIds"/></param>
        /// <param name="JAObjectIds">Ids of Objects added through JsonAssets. See also: <see cref="IJsonAssetsAPI.GetAllObjectIds"/></param>
        public StoreableBagItem ToStoreableBagItem(IDictionary<string, int> JABigCraftableIds, IDictionary<string, int> JAObjectIds, IDictionary<string, string> AllBigCraftableIds, IDictionary<string, string> AllObjectIds)
        {
            if (!string.IsNullOrEmpty(ObjectId))
            {
                return new StoreableBagItem(ObjectId, HasQualities, null, IsBigCraftable);
            }
            else if (IsBigCraftable)
            {
                if (JABigCraftableIds.TryGetValue(Name, out int JAId))
                    return new StoreableBagItem(JAId, HasQualities, null, IsBigCraftable);
                else if (AllBigCraftableIds.TryGetValue(Name, out string Id))
                    return new StoreableBagItem(Id, HasQualities, null, IsBigCraftable);
                else
                    return null;
            }
            else
            {
                if (JAObjectIds.TryGetValue(Name, out int JAId))
                    return new StoreableBagItem(JAId, HasQualities, null, IsBigCraftable);
                else if (AllObjectIds.TryGetValue(Name, out string Id))
                    return new StoreableBagItem(Id, HasQualities, null, IsBigCraftable);
                else
                    return null;
            }
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext sc)
        {
            //  Attempt to fix issues with the SizeString value
            if (string.IsNullOrEmpty(SizeString))
                SizeString = ContainerSize.Small.ToString();
            else
            {
                //  Fix character casing
                if (char.IsLower(SizeString[0]) || SizeString.Skip(1).Any(x => char.IsUpper(x)))
                {
                    SizeString = char.ToUpper(SizeString[0]) + string.Join("", SizeString.Skip(1).Select(x => char.ToLower(x)));
                }

                if (!Enum.TryParse(SizeString, out ContainerSize Result))
                {
                    SizeString = ContainerSize.Small.ToString();
                }
            }
        }
    }
}
