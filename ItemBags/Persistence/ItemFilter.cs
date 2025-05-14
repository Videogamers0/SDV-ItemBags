using ItemBags.Bags;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Objects;
using StardewValley.Internal;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ItemBags.Persistence
{
    public enum ItemFilterType
    {
        BagSize,
        IsVanillaItem,
        FromMod,
        HasMod,
        CategoryId,
        IsBigCraftable,
        Quality,
        HasContextTag,
        HasBuffs,
        /// <summary>Matches items that can be donated to the museum, including items that have already been donated</summary>
        IsDonatable,
        /// <summary>Matches items that can be donated to the museum, AND have not yet been donated</summary>
        IsPendingDonation,

        QualifiedId,
        QualifiedIdPrefix,
        QualifiedIdSuffix,
        QualifiedIdContains,

        //Unqualified Ids
        LocalId,
        LocalIdPrefix,
        LocalIdSuffix,
        LocalIdContains,

        //Uses InternalName, not localized name (DisplayName)
        Name,
        NamePrefix,
        NameSuffix,
        NameContains,
        //Uses DisplayName
        DisplayName,
        DisplayNamePrefix,
        DisplayNameSuffix,
        DisplayNameContains,

        Regex,
        /// <summary>Matches items that can be shipped AND have not yet been shipped</summary>
        IsPendingShipment
    }

    public enum CompositionType
    {
        LogicalOR,
        LogicalAND
    }

    public interface IItemFilter
    {
        /// <summary>The maximum number of results that this filter can match, or <see langword="null"/> if it can match unlimited number of results</summary>
        public int? Limit { get; }
        /// <summary>The number of results to skip before matching anything</summary>
        public int? Offset { get; }
        void ResetPaginationCounter();
        void IncrementPaginationCounter();

        public bool IsPaginated => Limit.HasValue || Offset.HasValue;

        bool IsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality, bool CheckPagination, bool IncrementPagination);
        bool IsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality, bool CheckPagination, bool IncrementPagination);
    }

    public class ItemFilterGroup : IItemFilter
    {
        public CompositionType Type { get; }
        public IReadOnlyList<IItemFilter> Filters { get; }
        private IReadOnlyList<ItemFilter> PaginatedFilters { get; }

        /// <summary>The maximum number of results that this filter can match, or <see langword="null"/> if it can match unlimited number of results</summary>
        public int? Limit { get; }
        /// <summary>The number of results to skip before matching anything</summary>
        public int? Offset { get; }

        public int ResultCounter { get; private set; }
        public void ResetPaginationCounter() => ResultCounter = 0;
        public void IncrementPaginationCounter() => ResultCounter += 1;
        private int MinResultIndex { get; }
        private int MaxResultIndex { get; }

        public ItemFilterGroup(CompositionType Type, int? Limit, int? Offset, params IItemFilter[] Filters)
        {
            this.Type = Type;
            this.Limit = Limit.HasValue && Limit.Value <= 0 ? null : Limit;
            this.Offset = Offset;
            this.Filters = Filters.ToList();

            this.PaginatedFilters = ItemFilter.EnumerateFilters(this).Where(x => x.Limit.HasValue || x.Offset.HasValue).ToList();
            ResetPaginationCounter();
            MinResultIndex = Offset ?? 0;
            MaxResultIndex = Limit.HasValue ? Limit.Value + MinResultIndex : int.MaxValue;
        }

        public bool IsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality, bool CheckPagination, bool IncrementPagination)
        {
            bool BaseResult = Type switch
            {
                CompositionType.LogicalOR => !Filters.Any() || Filters.Any(x => x.IsMatch(data, parsedData, size, quality, CheckPagination, false)),
                CompositionType.LogicalAND => !Filters.Any() || Filters.All(x => x.IsMatch(data, parsedData, size, quality, CheckPagination, false)),
                _ => throw new NotImplementedException($"Unrecognized {nameof(CompositionType)}: {Type}"),
            };

            if (IncrementPagination)
            {
                foreach (ItemFilter Filter in PaginatedFilters)
                    _ = Filter.IsMatch(data, parsedData, size, quality, false, true);
            }

            bool ActualResult = BaseResult && (!CheckPagination || (ResultCounter >= MinResultIndex && ResultCounter < MaxResultIndex));
            if (BaseResult && IncrementPagination)
                ResultCounter++;
            return ActualResult;
        }

        public bool IsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality, bool CheckPagination, bool IncrementPagination)
        {
            bool BaseResult = Type switch
            {
                CompositionType.LogicalOR => !Filters.Any() || Filters.Any(x => x.IsMatch(data, parsedData, size, quality, CheckPagination, false)),
                CompositionType.LogicalAND => !Filters.Any() || Filters.All(x => x.IsMatch(data, parsedData, size, quality, CheckPagination, false)),
                _ => throw new NotImplementedException($"Unrecognized {nameof(CompositionType)}: {Type}"),
            };

            if (IncrementPagination)
            {
                foreach (ItemFilter Filter in PaginatedFilters)
                    _ = Filter.IsMatch(data, parsedData, size, quality, false, true);
            }

            bool ActualResult = BaseResult && (!CheckPagination || (ResultCounter >= MinResultIndex && ResultCounter < MaxResultIndex));
            if (BaseResult && IncrementPagination)
                ResultCounter++;
            return ActualResult;
        }

        public override string ToString() => $"{nameof(ItemFilterGroup)}: {Type} ({Filters.Count} filter(s))";
    }

    public abstract class ItemFilter : IItemFilter
    {
        public ItemFilterType Type { get; }
        public bool IsNegated { get; }

        /// <summary>The maximum number of results that this filter can match, or <see langword="null"/> if it can match unlimited number of results</summary>
        public int? Limit { get; }
        /// <summary>The number of results to skip before matching anything</summary>
        public int? Offset { get; }

        public int ResultCounter { get; private set; }
        public void ResetPaginationCounter() => ResultCounter = 0;
        public void IncrementPaginationCounter() => ResultCounter += 1;
        private int MinResultIndex { get; }
        private int MaxResultIndex { get; }

        public virtual bool UsesBagSize => false;
        public virtual bool UsesQuality => false;

        public ItemFilter(ItemFilterType Type, bool IsNegated, int? Limit, int? Offset)
        {
            this.Type = Type;
            this.IsNegated = IsNegated;
            this.Limit = Limit;
            this.Offset = Offset;
            ResetPaginationCounter();

            MinResultIndex = Offset ?? 0;
            MaxResultIndex = Limit.HasValue ? Limit.Value + MinResultIndex : int.MaxValue;
        }

        public bool IsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality, bool CheckPagination, bool IncrementPagination)
        {
            bool BaseResult = IsNegated ? !DerivedIsMatch(data, parsedData, size, quality) : DerivedIsMatch(data, parsedData, size, quality);
            bool ActualResult = BaseResult && (!CheckPagination || (ResultCounter >= MinResultIndex && ResultCounter < MaxResultIndex));
            if (BaseResult && IncrementPagination)
                ResultCounter++;
            return ActualResult;
        }

        public bool IsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality, bool CheckPagination, bool IncrementPagination)
        {
            bool BaseResult = IsNegated ? !DerivedIsMatch(data, parsedData, size, quality) : DerivedIsMatch(data, parsedData, size, quality);
            bool ActualResult = BaseResult && (!CheckPagination || (ResultCounter >= MinResultIndex && ResultCounter < MaxResultIndex));
            if (BaseResult && IncrementPagination)
                ResultCounter++;
            return ActualResult;
        }

        protected abstract bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality);
        protected abstract bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality);

        public static IEnumerable<ItemFilter> EnumerateFilters(IItemFilter Filter)
        {
            if (Filter is ItemFilterGroup Group)
            {
                foreach (ItemFilter Nested in Group.Filters.SelectMany(x => EnumerateFilters(x)))
                {
                    yield return Nested;
                }
            }
            else if (Filter is ItemFilter Singleton)
                yield return Singleton;
            else
                throw new NotImplementedException($"Unrecognized {nameof(IItemFilter)} type: {Filter.GetType().Name}");
        }

        public static IEnumerable<ItemFilterGroup> EnumerateGroups(ItemFilterGroup Group, bool IncludeSelf)
        {
            if (IncludeSelf)
                yield return Group;

            foreach (ItemFilterGroup NestedGroup in Group.Filters.Where(x => x is ItemFilterGroup).Cast<ItemFilterGroup>())
            {
                foreach (var Item in EnumerateGroups(NestedGroup, true))
                    yield return Item;
            }
        }

        private const string NegationOperator = "!";
        private const string FilterIsNegatedPattern = @"(?<IsNegated>!)?";
        private const string FilterTypePattern = @"(?<FilterType>[^:]+?)";
        private const string FilterValuePattern = @"(:(?<FilterValue>.+))?";
        private const string FilterPaginationPattern = @"(-(?<Limit>\d+),(?<Offset>\d+))?";

        //  Each filter is expected to be in this format: "{FilterType}:{FilterValue}"
        //  Can optionally be prefixed with the NegationOperator such as "!Quality:Iridium" to match non-iridium items
        //  And the filter type can optionally be suffixed with pagination settings such as "FromMod-30,10:Cornucopia"
        //      to match up to 30 results, skipping the first 10 matches
        private static readonly Regex FilterParser = new Regex($@"^{FilterIsNegatedPattern}{FilterTypePattern}{FilterPaginationPattern}{FilterValuePattern}$");
        //^(?<IsNegated>!)?(?<FilterType>[^:]+?)(-(?<MaxResults>\d+),(?<Offset>\d+))?:(?<FilterValue>.+)$

        public static IItemFilter Parse(ModdedBag bag, string data, string delimiter)
        {
            List<ItemFilter> filters = new List<ItemFilter>();
            foreach (string filterString in data.Split(delimiter))
            {
#if true
                Match m = FilterParser.Match(filterString);
                bool IsNegated = m.Groups["IsNegated"].Success;
                string filterType = m.Groups["FilterType"].Value;
                string filterValue = m.Groups["FilterValue"].Value;
                int? Limit = m.Groups["Limit"].Success ? int.Parse(m.Groups["Limit"].Value) : null;
                int? Offset = m.Groups["Offset"].Success ? int.Parse(m.Groups["Offset"].Value) : null;
#else
                string filterType;
                string filterValue;

                int delimiterIndex = filterString.IndexOf(':');
                if (delimiterIndex < 0)
                {
                    filterType = filterString;
                    filterValue = "";
                }
                else
                {
                    filterType = filterString.Substring(0, delimiterIndex);
                    filterValue = filterString.Substring(delimiterIndex + 1);
                }

                bool IsNegated = false;
                if (filterType.StartsWith(NegationOperator))
                {
                    IsNegated = true;
                    filterType = filterType.Substring(NegationOperator.Length);
                }
#endif

                if (Enum.TryParse(filterType, true, out ItemFilterType parsedFilterType))
                {
                    ItemFilter filter = parsedFilterType switch
                    {
                        ItemFilterType.BagSize => BagSizeFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.IsVanillaItem => VanillaItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.FromMod => FromModItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.HasMod => HasModItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.CategoryId => CategoryItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.IsBigCraftable => BigCraftableItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.Quality => QualityItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.HasContextTag => ContextTagItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.HasBuffs => BuffsItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.IsDonatable => DonateableItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.IsPendingDonation => PendingDonationItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.QualifiedId => QualifiedIdItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.QualifiedIdPrefix => QualifiedIdPrefixItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.QualifiedIdSuffix => QualifiedIdSuffixItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.QualifiedIdContains => QualifiedIdContainsItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.LocalId => LocalIdItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.LocalIdPrefix => LocalIdPrefixItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.LocalIdSuffix => LocalIdSuffixItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.LocalIdContains => LocalIdContainsItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.Name => NameItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.NamePrefix => NamePrefixItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.NameSuffix => NameSuffixItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.NameContains => NameContainsItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.DisplayName => DisplayNameItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.DisplayNamePrefix => DisplayNamePrefixItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.DisplayNameSuffix => DisplayNameSuffixItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.DisplayNameContains => DisplayNameContainsItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        ItemFilterType.IsPendingShipment => PendingShipmentItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        //ItemFilterType.Sample => SampleItemFilter.Parse(IsNegated, Limit, Offset, filterValue),
                        _ => throw new NotImplementedException($"Unrecognized {nameof(ItemFilterType)}: {parsedFilterType}"),
                    };
                    filters.Add(filter);
                }
                else if (filterType.EndsWith(ItemFilterType.Regex.ToString()))
                {
                    string PropertyName = ReplaceLastOccurrence(filterType, ItemFilterType.Regex.ToString(), "");
                    if (!Enum.TryParse(PropertyName, true, out RegexItemFilter.RegexFilterProperty ParsedProperty))
                    {
                        IEnumerable<string> ValidPropertyNames = Enum.GetValues(typeof(RegexItemFilter.RegexFilterProperty)).Cast<RegexItemFilter.RegexFilterProperty>().Select(x => x.ToString());
                        ItemBagsMod.ModInstance.Monitor.Log($"Failed to parse an item filter for bag '{bag.BagName}'. " +
                            $"Regex filters must use one of the following properties: {string.Join(", ", ValidPropertyNames)}. Full value: \"{data}\".", LogLevel.Error);
                    }
                    else
                    {
                        ItemFilter filter = RegexItemFilter.Parse(IsNegated, Limit, Offset, ParsedProperty, filterValue);
                        filters.Add(filter);
                    }
                }
                else
                {
                    ItemBagsMod.ModInstance.Monitor.Log($"Failed to parse an item filter for bag '{bag.BagName}'. {filterType} is unrecognized. Full value: \"{data}\".", LogLevel.Error);
                }
            }

            if (filters.Count == 1)
                return filters.First();
            else
                return new ItemFilterGroup(CompositionType.LogicalOR, null, 0, filters.ToArray());
        }

        private static string ReplaceLastOccurrence(string source, string find, string replace)
        {
            int place = source.LastIndexOf(find);

            if (place == -1)
                return source;

            return source.Remove(place, find.Length).Insert(place, replace);
        }

        public override string ToString() => $"{nameof(ItemFilter)}";
    }

    public class BagSizeFilter : ItemFilter
    {
        public IReadOnlyList<ContainerSize> Sizes { get; }

        public override bool UsesBagSize => true;

        public BagSizeFilter(bool IsNegated, int? Limit, int? Offset, params ContainerSize[] Sizes)
            : base(ItemFilterType.BagSize, IsNegated, Limit, Offset)
        {
            this.Sizes = Sizes.ToList();
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Sizes.Contains(size);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Sizes.Contains(size);

        private static readonly IReadOnlyList<ContainerSize> AllSizes = Enum.GetValues(typeof(ContainerSize)).Cast<ContainerSize>().ToList();
        private static readonly IReadOnlyList<string> Prefixes = new List<string>() { "=", "<", "<=", ">", ">=" };
        private static readonly Regex Pattern = new Regex(@"^(?<Modifier>(>=|<=|=|>|<))(?<Size>.*)$");

        public static BagSizeFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value)
        {
            if (Pattern.IsMatch(Value))
            {
                Match m = Pattern.Match(Value);
                string Modifier = m.Groups["Modifier"].Value;
                string Size = m.Groups["Size"].Value;
                ContainerSize ParsedSize = (ContainerSize)Enum.Parse(typeof(ContainerSize), Size, true);

                List<ContainerSize> Sizes = Modifier switch
                {
                    "<" => AllSizes.Where(x => x < ParsedSize).ToList(),
                    "<=" => AllSizes.Where(x => x <= ParsedSize).ToList(),
                    "=" => AllSizes.Where(x => x == ParsedSize).ToList(),
                    ">" => AllSizes.Where(x => x > ParsedSize).ToList(),
                    ">=" => AllSizes.Where(x => x >= ParsedSize).ToList(),
                    _ => throw new NotImplementedException($"Invalid format for {nameof(BagSizeFilter)} value: The prefix '{Modifier}' is unrecognized.")
                };

                return new BagSizeFilter(IsNegated, Limit, Offset, Sizes.ToArray());
            }
            else
                return new BagSizeFilter(IsNegated, Limit, Offset, (ContainerSize)Enum.Parse(typeof(ContainerSize), Value, true));
        }

        public override string ToString() => $"{nameof(BagSizeFilter)}:{string.Join(",", Sizes)}";
    }

    public class VanillaItemFilter : ItemFilter
    {
        public VanillaItemFilter(bool IsNegated, int? Limit, int? Offset)
            : base(ItemFilterType.IsVanillaItem, IsNegated, Limit, Offset)
        {

        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => !parsedData.QualifiedItemId.Contains('_');
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => !parsedData.QualifiedItemId.Contains('_');

        public static VanillaItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new VanillaItemFilter(IsNegated, Limit, Offset);

        public override string ToString() => $"{nameof(VanillaItemFilter)}";
    }

    public class FromModItemFilter : ItemFilter
    {
        public string ModUniqueId { get; }

        public FromModItemFilter(bool IsNegated, int? Limit, int? Offset, string ModUniqueId)
            : base(ItemFilterType.FromMod, IsNegated, Limit, Offset)
        {
            this.ModUniqueId = ModUniqueId;
        }

        //  Most mods use the format: "{ModId}_{ItemId}" to define their item Ids, but some mods also seem to use "{ModId}.{ItemId}"
        private static readonly IReadOnlyList<string> ModItemIdDelimiters = new List<string>() { "_", "." };

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => IsFromMod(ModUniqueId, parsedData.ItemId);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => IsFromMod(ModUniqueId, parsedData.ItemId);

        private static bool IsFromMod(string ModId, string UnqualifiedItemId) => ModItemIdDelimiters.Any(delimiter => UnqualifiedItemId.StartsWith(ModId + delimiter));

        public static FromModItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new FromModItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(FromModItemFilter)}:{ModUniqueId}";
    }

    public class HasModItemFilter : ItemFilter
    {
        public string ModUniqueId { get; }
        public string MinimumVersion { get; }

        public HasModItemFilter(bool IsNegated, int? Limit, int? Offset, string ModUniqueId, string MinimumVersion)
            : base(ItemFilterType.HasMod, IsNegated, Limit, Offset)
        {
            this.ModUniqueId = ModUniqueId;
            this.MinimumVersion = MinimumVersion;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => IsLoaded(ModUniqueId, MinimumVersion);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => IsLoaded(ModUniqueId, MinimumVersion);

        private static readonly Dictionary<string, bool> ModRegistryLookup = new Dictionary<string, bool>();
        private static bool IsLoaded(string ModId, string MinimumVersion)
        {
            string Key = ModId;
            if (MinimumVersion != null)
                Key += "|" + MinimumVersion;

            if (ModRegistryLookup.TryGetValue(Key, out bool CachedValue))
                return CachedValue;

            bool Result = true;
            if (!ItemBagsMod.ModInstance.Helper.ModRegistry.IsLoaded(ModId))
                Result = false;
            else if (MinimumVersion != null)
            {
                ISemanticVersion Version = ItemBagsMod.ModInstance.Helper.ModRegistry.Get(ModId).Manifest.Version;
                if (Version.IsOlderThan(MinimumVersion))
                    Result = false;
            }

            ModRegistryLookup.Add(Key, Result);
            return Result;
        }

        private static readonly Regex Pattern = new Regex(@"^(?<ModId>.+)-(?<MinVersion>\d+\.\d+\.\d+.*)$");
        public static HasModItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value)
        {
            //  Attempt to parse values in the format: "{ModId}-{MinVersion}", such as: "Rafseazz.RSVCP-2.5.17"
            if (Pattern.IsMatch(Value))
            {
                Match m = Pattern.Match(Value);
                string ModId = m.Groups["ModId"].Value;
                string MinVersion = m.Groups["MinVersion"].Value;
                if (SemanticVersion.TryParse(MinVersion, out _))
                    return new HasModItemFilter(IsNegated, Limit, Offset, ModId, MinVersion);
            }

            return new HasModItemFilter(IsNegated, Limit, Offset, Value, null);
        }

        public override string ToString() => $"{nameof(HasModItemFilter)}:{ModUniqueId}-{MinimumVersion ?? "any version"}";
    }

    public class CategoryItemFilter : ItemFilter
    {
        public IReadOnlyList<int> CategoryIds { get; }

        public CategoryItemFilter(bool IsNegated, int? Limit, int? Offset, params int[] CategoryIds)
            : base(ItemFilterType.CategoryId, IsNegated, Limit, Offset)
        {
            this.CategoryIds = CategoryIds.ToList();
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => CategoryIds.Contains(parsedData.Category);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => CategoryIds.Contains(parsedData.Category);

        public static CategoryItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value)
            => new CategoryItemFilter(IsNegated, Limit, Offset, Value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.Parse(x)).ToArray());

        public override string ToString() => $"{nameof(CategoryItemFilter)}:{string.Join(",", CategoryIds)}";
    }

    public class BigCraftableItemFilter : ItemFilter
    {
        public BigCraftableItemFilter(bool IsNegated, int? Limit, int? Offset)
            : base(ItemFilterType.IsBigCraftable, IsNegated, Limit, Offset)
        {

        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => false;
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => true;

        public static BigCraftableItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new BigCraftableItemFilter(IsNegated, Limit, Offset);

        public override string ToString() => $"{nameof(BigCraftableItemFilter)}";
    }

    public class QualityItemFilter : ItemFilter
    {
        public IReadOnlyList<ObjectQuality> Qualities { get; }

        public override bool UsesQuality => true;

        public QualityItemFilter(bool IsNegated, int? Limit, int? Offset, params ObjectQuality[] Qualities)
            : base(ItemFilterType.Quality, IsNegated, Limit, Offset)
        {
            this.Qualities = Qualities.ToList();
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => !Qualities.Any() || Qualities.Contains(quality);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => !Qualities.Any() || Qualities.Contains(quality);

        private static IReadOnlyDictionary<string, ObjectQuality> QualityNameLookup = new Dictionary<string, ObjectQuality>()
        {
            { "regular", ObjectQuality.Regular },
            { "normal", ObjectQuality.Regular },
            { "silver", ObjectQuality.Silver },
            { "gold", ObjectQuality.Gold },
            { "iridium", ObjectQuality.Iridium }
        };

        public static QualityItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value)
        {
            if (int.TryParse(Value, out int ValueInt))
            {
                ObjectQuality QualityValue = ValueInt switch
                {
                    0 => ObjectQuality.Regular,
                    1 => ObjectQuality.Silver,
                    2 => ObjectQuality.Gold,
                    3 => ObjectQuality.Iridium, // Technically 3 is not a valid value but users might think 3=Iridium
                    4 => ObjectQuality.Iridium,
                    _ => ObjectQuality.Regular
                };
                return new QualityItemFilter(IsNegated, Limit, Offset, QualityValue);
            }
            else
            {
                if (!QualityNameLookup.TryGetValue(Value.ToLower(), out ObjectQuality QualityValue))
                    QualityValue = ObjectQuality.Regular;
                return new QualityItemFilter(IsNegated, Limit, Offset, QualityValue);
            }
        }

        public override string ToString() => $"{nameof(QualityItemFilter)}:{string.Join(",", Qualities)}";
    }

    public class ContextTagItemFilter : ItemFilter
    {
        public string ContextTag { get; }

        public ContextTagItemFilter(bool IsNegated, int? Limit, int? Offset, string ContextTag)
            : base(ItemFilterType.HasContextTag, IsNegated, Limit, Offset)
        {
            this.ContextTag = ContextTag;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) 
            => data.ContextTags?.Contains(ContextTag) == true || HasTag(parsedData, ContextTag);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) 
            => data.ContextTags?.Contains(ContextTag) == true || HasTag(parsedData, ContextTag);

        private static readonly Dictionary<string, Item> ItemInstances = new Dictionary<string, Item>();

        private static bool HasTag(ParsedItemData ItemData, string Tag)
        {
            //  Some ContextTags are added dynamically after the item is created, so we need a sample instance to read data from
            if (!ItemInstances.TryGetValue(ItemData.QualifiedItemId, out Item Instance))
            {
                Instance = ItemRegistry.Create(ItemData.QualifiedItemId);
                ItemInstances.Add(ItemData.QualifiedItemId, Instance);
            }

            return Instance.GetContextTags().Contains(Tag);
        }

        public static ContextTagItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new ContextTagItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(ContextTagItemFilter)}:{ContextTag}";
    }

    public class BuffsItemFilter : ItemFilter
    {
        public BuffsItemFilter(bool IsNegated, int? Limit, int? Offset)
            : base(ItemFilterType.HasBuffs, IsNegated, Limit, Offset)
        {

        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => data.Buffs?.Any() == true;
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => false;

        public static BuffsItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new BuffsItemFilter(IsNegated, Limit, Offset);

        public override string ToString() => $"{nameof(BuffsItemFilter)}";
    }

    #region Museum filters
    public class DonateableItemFilter : ItemFilter
    {
        public DonateableItemFilter(bool IsNegated, int? Limit, int? Offset)
            : base(ItemFilterType.IsDonatable, IsNegated, Limit, Offset)
        {

        }

        //contexttag: "not_museum_donatable" / "museum_donatable"
        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => 
            LibraryMuseum.IsItemSuitableForDonation(parsedData.QualifiedItemId, false);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) =>
            false; // LibraryMuseum.IsItemSuitableForDonation(parsedData.QualifiedItemId, false); // I don't think BigCraftables are ever valid for donating

        public static DonateableItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new DonateableItemFilter(IsNegated, Limit, Offset);

        public override string ToString() => $"{nameof(DonateableItemFilter)}";
    }

    public class PendingDonationItemFilter : ItemFilter
    {
        public PendingDonationItemFilter(bool IsNegated, int? Limit, int? Offset)
            : base(ItemFilterType.IsPendingDonation, IsNegated, Limit, Offset)
        {

        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => 
            LibraryMuseum.IsItemSuitableForDonation(parsedData.QualifiedItemId, true);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) =>
            false; // LibraryMuseum.IsItemSuitableForDonation(parsedData.QualifiedItemId, true); // I don't think BigCraftables are ever valid for donating

        public static PendingDonationItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new PendingDonationItemFilter(IsNegated, Limit, Offset);

        public override string ToString() => $"{nameof(PendingDonationItemFilter)}";
    }
    #endregion Museum filters

    #region Qualified Id filters
    public class QualifiedIdItemFilter : ItemFilter
    {
        public string Id { get; }

        public QualifiedIdItemFilter(bool IsNegated, int? Limit, int? Offset, string Id)
            : base(ItemFilterType.QualifiedId, IsNegated, Limit, Offset)
        {
            this.Id = Id;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Id == parsedData.QualifiedItemId;
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Id == parsedData.QualifiedItemId;

        public static QualifiedIdItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new QualifiedIdItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(QualifiedIdItemFilter)}:{Id}";
    }

    public class QualifiedIdPrefixItemFilter : ItemFilter
    {
        public string Prefix { get; }

        public QualifiedIdPrefixItemFilter(bool IsNegated, int? Limit, int? Offset, string Prefix)
            : base(ItemFilterType.QualifiedIdPrefix, IsNegated, Limit, Offset)
        {
            this.Prefix = Prefix;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.QualifiedItemId.StartsWith(Prefix);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.QualifiedItemId.StartsWith(Prefix);

        public static QualifiedIdPrefixItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new QualifiedIdPrefixItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(QualifiedIdPrefixItemFilter)}:{Prefix}";
    }

    public class QualifiedIdSuffixItemFilter : ItemFilter
    {
        public string Suffix { get; }

        public QualifiedIdSuffixItemFilter(bool IsNegated, int? Limit, int? Offset, string Suffix)
            : base(ItemFilterType.QualifiedIdSuffix, IsNegated, Limit, Offset)
        {
            this.Suffix = Suffix;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.QualifiedItemId.EndsWith(Suffix);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.QualifiedItemId.EndsWith(Suffix);

        public static QualifiedIdSuffixItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new QualifiedIdSuffixItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(QualifiedIdSuffixItemFilter)}:{Suffix}";
    }

    public class QualifiedIdContainsItemFilter : ItemFilter
    {
        public string Text { get; }

        public QualifiedIdContainsItemFilter(bool IsNegated, int? Limit, int? Offset, string Text)
            : base(ItemFilterType.QualifiedIdContains, IsNegated, Limit, Offset)
        {
            this.Text = Text;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.QualifiedItemId.Contains(Text);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.QualifiedItemId.Contains(Text);

        public static QualifiedIdContainsItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new QualifiedIdContainsItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(QualifiedIdContainsItemFilter)}:{Text}";
    }
    #endregion Qualified Id filters

    #region Local Id filters
    public class LocalIdItemFilter : ItemFilter
    {
        public string Id { get; }

        public LocalIdItemFilter(bool IsNegated, int? Limit, int? Offset, string Id)
            : base(ItemFilterType.LocalId, IsNegated, Limit, Offset)
        {
            this.Id = Id;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Id == parsedData.ItemId;
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Id == parsedData.ItemId;

        public static LocalIdItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new LocalIdItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(LocalIdItemFilter)}:{Id}";
    }

    public class LocalIdPrefixItemFilter : ItemFilter
    {
        public string Prefix { get; }

        public LocalIdPrefixItemFilter(bool IsNegated, int? Limit, int? Offset, string Prefix)
            : base(ItemFilterType.LocalIdPrefix, IsNegated, Limit, Offset)
        {
            this.Prefix = Prefix;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.ItemId.StartsWith(Prefix);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.ItemId.StartsWith(Prefix);

        public static LocalIdPrefixItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new LocalIdPrefixItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(LocalIdPrefixItemFilter)}:{Prefix}";
    }

    public class LocalIdSuffixItemFilter : ItemFilter
    {
        public string Suffix { get; }

        public LocalIdSuffixItemFilter(bool IsNegated, int? Limit, int? Offset, string Suffix)
            : base(ItemFilterType.LocalIdSuffix, IsNegated, Limit, Offset)
        {
            this.Suffix = Suffix;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.ItemId.EndsWith(Suffix);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.ItemId.EndsWith(Suffix);

        public static LocalIdSuffixItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new LocalIdSuffixItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(LocalIdSuffixItemFilter)}:{Suffix}";
    }

    public class LocalIdContainsItemFilter : ItemFilter
    {
        public string Text { get; }

        public LocalIdContainsItemFilter(bool IsNegated, int? Limit, int? Offset, string Text)
            : base(ItemFilterType.LocalIdContains, IsNegated, Limit, Offset)
        {
            this.Text = Text;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.ItemId.Contains(Text);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.ItemId.Contains(Text);

        public static LocalIdContainsItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new LocalIdContainsItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(LocalIdContainsItemFilter)}:{Text}";
    }
    #endregion Local Id filters

    #region Name filters
    public class NameItemFilter : ItemFilter
    {
        public string Name { get; }

        public NameItemFilter(bool IsNegated, int? Limit, int? Offset, string Name)
            : base(ItemFilterType.Name, IsNegated, Limit, Offset)
        {
            this.Name = Name;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName == Name;
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName == Name;

        public static NameItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value)
            => new NameItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(NameItemFilter)}:{Name}";
    }

    public class NamePrefixItemFilter : ItemFilter
    {
        public string Prefix { get; }

        public NamePrefixItemFilter(bool IsNegated, int? Limit, int? Offset, string Prefix)
            : base(ItemFilterType.NamePrefix, IsNegated, Limit, Offset)
        {
            this.Prefix = Prefix;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName.StartsWith(Prefix);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName.StartsWith(Prefix);

        public static NamePrefixItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new NamePrefixItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(NamePrefixItemFilter)}:{Prefix}";
    }

    public class NameSuffixItemFilter : ItemFilter
    {
        public string Suffix { get; }

        public NameSuffixItemFilter(bool IsNegated, int? Limit, int? Offset, string Suffix)
            : base(ItemFilterType.NameSuffix, IsNegated, Limit, Offset)
        {
            this.Suffix = Suffix;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName.EndsWith(Suffix);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName.EndsWith(Suffix);

        public static NameSuffixItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new NameSuffixItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(NameSuffixItemFilter)}:{Suffix}";
    }

    public class NameContainsItemFilter : ItemFilter
    {
        public string Text { get; }

        public NameContainsItemFilter(bool IsNegated, int? Limit, int? Offset, string Text)
            : base(ItemFilterType.NameContains, IsNegated, Limit, Offset)
        {
            this.Text = Text;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName.Contains(Text);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName.Contains(Text);

        public static NameContainsItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new NameContainsItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(NameContainsItemFilter)}:{Text}";
    }
    #endregion Name filters

    #region DisplayName filters
    public class DisplayNameItemFilter : ItemFilter
    {
        public string DisplayName { get; }

        public DisplayNameItemFilter(bool IsNegated, int? Limit, int? Offset, string DisplayName)
            : base(ItemFilterType.DisplayName, IsNegated, Limit, Offset)
        {
            this.DisplayName = DisplayName;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.DisplayName == DisplayName;
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.DisplayName == DisplayName;

        public static DisplayNameItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new DisplayNameItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(DisplayNameItemFilter)}:{DisplayName}";
    }

    public class DisplayNamePrefixItemFilter : ItemFilter
    {
        public string Prefix { get; }

        public DisplayNamePrefixItemFilter(bool IsNegated, int? Limit, int? Offset, string Prefix)
            : base(ItemFilterType.DisplayNamePrefix, IsNegated, Limit, Offset)
        {
            this.Prefix = Prefix;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.DisplayName.StartsWith(Prefix);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.DisplayName.StartsWith(Prefix);

        public static DisplayNamePrefixItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new DisplayNamePrefixItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(DisplayNamePrefixItemFilter)}:{Prefix}";
    }

    public class DisplayNameSuffixItemFilter : ItemFilter
    {
        public string Suffix { get; }

        public DisplayNameSuffixItemFilter(bool IsNegated, int? Limit, int? Offset, string Suffix)
            : base(ItemFilterType.DisplayNameSuffix, IsNegated, Limit, Offset)
        {
            this.Suffix = Suffix;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.DisplayName.EndsWith(Suffix);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.DisplayName.EndsWith(Suffix);

        public static DisplayNameSuffixItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new DisplayNameSuffixItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(DisplayNameSuffixItemFilter)}:{Suffix}";
    }

    public class DisplayNameContainsItemFilter : ItemFilter
    {
        public string Text { get; }

        public DisplayNameContainsItemFilter(bool IsNegated, int? Limit, int? Offset, string Text)
            : base(ItemFilterType.DisplayNameContains, IsNegated, Limit, Offset)
        {
            this.Text = Text;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.DisplayName.Contains(Text);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.DisplayName.Contains(Text);

        public static DisplayNameContainsItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) 
            => new DisplayNameContainsItemFilter(IsNegated, Limit, Offset, Value);

        public override string ToString() => $"{nameof(DisplayNameContainsItemFilter)}:{Text}";
    }
    #endregion DisplayName filters

    public class RegexItemFilter : ItemFilter
    {
        public enum RegexFilterProperty
        {
            InternalName,
            DisplayName,
            LocalId,
            QualifiedId
        }

        public RegexFilterProperty Property { get; }
        public string Pattern { get; }
        public Regex Regex { get; }

        /// <param name="Pattern">The Regex pattern string to match against</param>
        public RegexItemFilter(bool IsNegated, int? Limit, int? Offset, RegexFilterProperty Property, string Pattern)
            : base(ItemFilterType.Regex, IsNegated, Limit, Offset)
        {
            this.Property = Property;
            this.Pattern = Pattern;
            this.Regex = new Regex(Pattern);
        }

        private static string GetPropertyValue(RegexFilterProperty property, ParsedItemData data) => property switch
        {
            RegexFilterProperty.InternalName => data.InternalName,
            RegexFilterProperty.DisplayName => data.DisplayName,
            RegexFilterProperty.LocalId => data.ItemId,
            RegexFilterProperty.QualifiedId => data.QualifiedItemId,
            _ => throw new NotImplementedException($"Unrecognized {nameof(RegexFilterProperty)}: {property}"),
        };

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Regex.IsMatch(GetPropertyValue(Property, parsedData));
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Regex.IsMatch(GetPropertyValue(Property, parsedData));

        public static RegexItemFilter Parse(bool IsNegated, int? Limit, int? Offset, RegexFilterProperty Property, string Value) 
            => new RegexItemFilter(IsNegated, Limit, Offset, Property, Value);
    }

    public class PendingShipmentItemFilter : ItemFilter
    {
        public PendingShipmentItemFilter(bool IsNegated, int? Limit, int? Offset)
            : base(ItemFilterType.IsPendingShipment, IsNegated, Limit, Offset)
        {

        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => !data.ExcludeFromShippingCollection && IsUnshipped(parsedData);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => IsUnshipped(parsedData);

        private static readonly Dictionary<string, Item> ItemInstances = new Dictionary<string, Item>();

        private static bool IsUnshipped(ParsedItemData Data)
        {
            if (!StardewValley.Object.isPotentialBasicShipped(Data.ItemId, Data.Category, Data.ObjectType))
                return false;
            if (Game1.player.basicShipped.TryGetValue(Data.ItemId, out int ShippedQty) && ShippedQty > 0)
                return false;

            //  Get an instance of the item so we can read the 'canBeShipped' value
            if (!ItemInstances.TryGetValue(Data.QualifiedItemId, out Item Instance))
            {
                Instance = ItemRegistry.Create(Data.QualifiedItemId);
                ItemInstances.Add(Data.QualifiedItemId, Instance);
            }

            return Instance.canBeShipped();
        }

        public static PendingShipmentItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) => new PendingShipmentItemFilter(IsNegated, Limit, Offset);

        public override string ToString() => $"{nameof(PendingShipmentItemFilter)}";
    }

#if NEVER // for copy-pasting purposes...
    public class SampleItemFilter : ItemFilter
    {
        public SampleItemFilter(bool IsNegated, int? Limit, int? Offset)
            : base(ItemFilterType.Sample, IsNegated, Limit, Offset)
        {

        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => aaaaaaaa;
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => aaaaaaaaaaa;

        public static SomeItemFilter Parse(bool IsNegated, int? Limit, int? Offset, string Value) => new SomeItemFilter(IsNegated, Limit, Offset);

        public override string ToString() => "${nameof(SampleItemFilter)}";
    }
#endif
}
