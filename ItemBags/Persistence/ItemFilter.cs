using ItemBags.Bags;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Objects;
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
        //Uses InternalName, not localized name
        Name,
        NamePrefix,
        NameSuffix,
        NameContains
    }

    public enum CompositionType
    {
        LogicalOR,
        LogicalAND
    }

    public interface IItemFilter
    {
        bool IsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality);
        bool IsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality);
    }

    public class ItemFilterGroup : IItemFilter
    {
        public CompositionType Type { get; }
        public IReadOnlyList<IItemFilter> Filters { get; }

        public ItemFilterGroup(CompositionType Type, params IItemFilter[] Filters)
        {
            this.Type = Type;
            this.Filters = Filters.ToList();           
        }

        public bool IsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Type switch
        {
            CompositionType.LogicalOR => !Filters.Any() || Filters.Any(x => x.IsMatch(data, parsedData, size, quality)),
            CompositionType.LogicalAND => !Filters.Any() || Filters.All(x => x.IsMatch(data, parsedData, size, quality)),
            _ => throw new NotImplementedException($"Unrecognized {nameof(CompositionType)}: {Type}"),
        };

        public bool IsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Type switch
        {
            CompositionType.LogicalOR => !Filters.Any() || Filters.Any(x => x.IsMatch(data, parsedData, size, quality)),
            CompositionType.LogicalAND => !Filters.Any() || Filters.All(x => x.IsMatch(data, parsedData, size, quality)),
            _ => throw new NotImplementedException($"Unrecognized {nameof(CompositionType)}: {Type}"),
        };
    }

    public abstract class ItemFilter : IItemFilter
    {
        public ItemFilterType Type { get; }
        public bool IsNegated { get; }

        public virtual bool UsesBagSize => false;
        public virtual bool UsesQuality => false;

        public ItemFilter(ItemFilterType Type, bool IsNegated)
        {
            this.Type = Type;
            this.IsNegated = IsNegated;
        }

        public bool IsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) =>
            IsNegated ? !DerivedIsMatch(data, parsedData, size, quality) : DerivedIsMatch(data, parsedData, size, quality);

        public bool IsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) =>
            IsNegated ? !DerivedIsMatch(data, parsedData, size, quality) : DerivedIsMatch(data, parsedData, size, quality);

        protected abstract bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality);
        protected abstract bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality);

        private const string NegationOperator = "!";

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

        public static IItemFilter Parse(ModdedBag bag, string data)
        {
            List<ItemFilter> filters = new List<ItemFilter>();

            //  Each filter is expected to be in this format: "{FilterType}:{FilterValue}",
            //  and can optionally be prefixed with the NegationOperator such as "!Quality:Iridium" to match non-iridium items
            foreach (string filterString in data.Split('|'))
            {
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

                if (Enum.TryParse(filterType, true, out ItemFilterType parsedFilterType))
                {
                    ItemFilter filter = parsedFilterType switch
                    {
                        ItemFilterType.BagSize => BagSizeFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.IsVanillaItem => VanillaItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.FromMod => FromModItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.HasMod => HasModItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.CategoryId => CategoryItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.IsBigCraftable => BigCraftableItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.Quality => QualityItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.HasContextTag => ContextTagItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.HasBuffs => BuffsItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.IsDonatable => DonateableItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.IsPendingDonation => PendingDonationItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.QualifiedId => QualifiedIdItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.QualifiedIdPrefix => QualifiedIdPrefixItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.QualifiedIdSuffix => QualifiedIdSuffixItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.QualifiedIdContains => QualifiedIdContainsItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.LocalId => LocalIdItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.LocalIdPrefix => LocalIdPrefixItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.LocalIdSuffix => LocalIdSuffixItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.LocalIdContains => LocalIdContainsItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.Name => NameItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.NamePrefix => NamePrefixItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.NameSuffix => NameSuffixItemFilter.Parse(IsNegated, filterValue),
                        ItemFilterType.NameContains => NameContainsItemFilter.Parse(IsNegated, filterValue),
                        _ => throw new NotImplementedException($"Unrecognized {nameof(ItemFilterType)}: {parsedFilterType}"),
                    };
                    filters.Add(filter);
                }
                else
                {
                    ItemBagsMod.ModInstance.Monitor.Log($"Failed to parse an item filter for bag '{bag.BagName}'. {filterType} is unrecognized. Full value: \"{data}\".");
                }
            }

            if (filters.Count == 1)
                return filters.First();
            else
                return new ItemFilterGroup(CompositionType.LogicalOR, filters.ToArray());
        }
    }

    public class BagSizeFilter : ItemFilter
    {
        public IReadOnlyList<ContainerSize> Sizes { get; }

        public override bool UsesBagSize => true;

        public BagSizeFilter(bool IsNegated, params ContainerSize[] Sizes)
            : base(ItemFilterType.BagSize, IsNegated)
        {
            this.Sizes = Sizes.ToList();
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Sizes.Contains(size);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Sizes.Contains(size);

        private static readonly IReadOnlyList<ContainerSize> AllSizes = Enum.GetValues(typeof(ContainerSize)).Cast<ContainerSize>().ToList();
        private static readonly IReadOnlyList<string> Prefixes = new List<string>() { "=", "<", "<=", ">", ">=" };
        private static readonly Regex Pattern = new Regex(@"^(?<Modifier>(>=|<=|=|>|<))(?<Size>.*)$");

        public static BagSizeFilter Parse(bool IsNegated, string Value)
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

                return new BagSizeFilter(IsNegated, Sizes.ToArray());
            }
            else
                return new BagSizeFilter(IsNegated, (ContainerSize)Enum.Parse(typeof(ContainerSize), Value, true));
        }
    }

    public class VanillaItemFilter : ItemFilter
    {
        public VanillaItemFilter(bool IsNegated)
            : base(ItemFilterType.IsVanillaItem, IsNegated)
        {

        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => !parsedData.QualifiedItemId.Contains('_');
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => !parsedData.QualifiedItemId.Contains('_');

        public static VanillaItemFilter Parse(bool IsNegated, string Value) => new VanillaItemFilter(IsNegated);
    }

    public class FromModItemFilter : ItemFilter
    {
        public string ModUniqueId { get; }

        public FromModItemFilter(bool IsNegated, string ModUniqueId)
            : base(ItemFilterType.FromMod, IsNegated)
        {
            this.ModUniqueId = ModUniqueId;
        }

        //  Most mods use the format: "{ModId}_{ItemId}" to define their item Ids, but some mods also seem to use "{ModId}.{ItemId}"
        private static readonly IReadOnlyList<string> ModItemIdDelimiters = new List<string>() { "_", "." };

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => IsFromMod(ModUniqueId, parsedData.ItemId);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => IsFromMod(ModUniqueId, parsedData.ItemId);

        private static bool IsFromMod(string ModId, string UnqualifiedItemId) => ModItemIdDelimiters.Any(delimiter => UnqualifiedItemId.StartsWith(ModId + delimiter));

        public static FromModItemFilter Parse(bool IsNegated, string Value) => new FromModItemFilter(IsNegated, Value);
    }

    public class HasModItemFilter : ItemFilter
    {
        public string ModUniqueId { get; }
        public string MinimumVersion { get; }

        public HasModItemFilter(bool IsNegated, string ModUniqueId, string MinimumVersion)
            : base(ItemFilterType.HasMod, IsNegated)
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
        public static HasModItemFilter Parse(bool IsNegated, string Value)
        {
            //  Attempt to parse values in the format: "{ModId}-{MinVersion}", such as: "Rafseazz.RSVCP-2.5.17"
            if (Pattern.IsMatch(Value))
            {
                Match m = Pattern.Match(Value);
                string ModId = m.Groups["ModId"].Value;
                string MinVersion = m.Groups["MinVersion"].Value;
                if (SemanticVersion.TryParse(MinVersion, out _))
                    return new HasModItemFilter(IsNegated, ModId, MinVersion);
            }

            return new HasModItemFilter(IsNegated, Value, null);
        }
    }

    public class CategoryItemFilter : ItemFilter
    {
        public int CategoryId { get; }

        public CategoryItemFilter(bool IsNegated, int CategoryId)
            : base(ItemFilterType.CategoryId, IsNegated)
        {
            this.CategoryId = CategoryId;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.Category == CategoryId;
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.Category == CategoryId;

        public static CategoryItemFilter Parse(bool IsNegated, string Value) => new CategoryItemFilter(IsNegated, int.Parse(Value));
    }

    public class BigCraftableItemFilter : ItemFilter
    {
        public BigCraftableItemFilter(bool IsNegated)
            : base(ItemFilterType.IsBigCraftable, IsNegated)
        {

        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => false;
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => true;

        public static BigCraftableItemFilter Parse(bool IsNegated, string Value) => new BigCraftableItemFilter(IsNegated);
    }

    public class QualityItemFilter : ItemFilter
    {
        public IReadOnlyList<ObjectQuality> Qualities { get; }

        public override bool UsesQuality => true;

        public QualityItemFilter(bool IsNegated, params ObjectQuality[] Qualities)
            : base(ItemFilterType.Quality, IsNegated)
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

        public static QualityItemFilter Parse(bool IsNegated, string Value)
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
                return new QualityItemFilter(IsNegated, QualityValue);
            }
            else
            {
                if (!QualityNameLookup.TryGetValue(Value.ToLower(), out ObjectQuality QualityValue))
                    QualityValue = ObjectQuality.Regular;
                return new QualityItemFilter(IsNegated, QualityValue);
            }
        }
    }

    public class ContextTagItemFilter : ItemFilter
    {
        public string ContextTag { get; }

        public ContextTagItemFilter(bool IsNegated, string ContextTag)
            : base(ItemFilterType.HasContextTag, IsNegated)
        {
            this.ContextTag = ContextTag;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => data.ContextTags?.Contains(ContextTag) == true;
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => data.ContextTags?.Contains(ContextTag) == true;

        public static ContextTagItemFilter Parse(bool IsNegated, string Value) => new ContextTagItemFilter(IsNegated, Value);
    }

    public class BuffsItemFilter : ItemFilter
    {
        public BuffsItemFilter(bool IsNegated)
            : base(ItemFilterType.HasBuffs, IsNegated)
        {

        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => data.Buffs?.Any() == true;
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => false;

        public static BuffsItemFilter Parse(bool IsNegated, string Value) => new BuffsItemFilter(IsNegated);
    }

    #region Museum filters
    public class DonateableItemFilter : ItemFilter
    {
        public DonateableItemFilter(bool IsNegated)
            : base(ItemFilterType.IsDonatable, IsNegated)
        {

        }

        //contexttag: "not_museum_donatable" / "museum_donatable"
        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => 
            LibraryMuseum.IsItemSuitableForDonation(parsedData.QualifiedItemId, false);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) =>
            false; // LibraryMuseum.IsItemSuitableForDonation(parsedData.QualifiedItemId, false); // I don't think BigCraftables are ever valid for donating

        public static DonateableItemFilter Parse(bool IsNegated, string Value) => new DonateableItemFilter(IsNegated);
    }

    public class PendingDonationItemFilter : ItemFilter
    {
        public PendingDonationItemFilter(bool IsNegated)
            : base(ItemFilterType.IsPendingDonation, IsNegated)
        {

        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => 
            LibraryMuseum.IsItemSuitableForDonation(parsedData.QualifiedItemId, true);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) =>
            false; // LibraryMuseum.IsItemSuitableForDonation(parsedData.QualifiedItemId, true); // I don't think BigCraftables are ever valid for donating

        public static PendingDonationItemFilter Parse(bool IsNegated, string Value) => new PendingDonationItemFilter(IsNegated);
    }
    #endregion Museum filters

    #region Qualified Id filters
    public class QualifiedIdItemFilter : ItemFilter
    {
        public string Id { get; }

        public QualifiedIdItemFilter(bool IsNegated, string Id)
            : base(ItemFilterType.QualifiedId, IsNegated)
        {
            this.Id = Id;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Id == parsedData.QualifiedItemId;
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Id == parsedData.QualifiedItemId;

        public static QualifiedIdItemFilter Parse(bool IsNegated, string Value) => new QualifiedIdItemFilter(IsNegated, Value);
    }

    public class QualifiedIdPrefixItemFilter : ItemFilter
    {
        public string Prefix { get; }

        public QualifiedIdPrefixItemFilter(bool IsNegated, string Prefix)
            : base(ItemFilterType.QualifiedIdPrefix, IsNegated)
        {
            this.Prefix = Prefix;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.QualifiedItemId.StartsWith(Prefix);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.QualifiedItemId.StartsWith(Prefix);

        public static QualifiedIdPrefixItemFilter Parse(bool IsNegated, string Value) => new QualifiedIdPrefixItemFilter(IsNegated, Value);
    }

    public class QualifiedIdSuffixItemFilter : ItemFilter
    {
        public string Suffix { get; }

        public QualifiedIdSuffixItemFilter(bool IsNegated, string Suffix)
            : base(ItemFilterType.QualifiedIdSuffix, IsNegated)
        {
            this.Suffix = Suffix;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.QualifiedItemId.EndsWith(Suffix);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.QualifiedItemId.EndsWith(Suffix);

        public static QualifiedIdSuffixItemFilter Parse(bool IsNegated, string Value) => new QualifiedIdSuffixItemFilter(IsNegated, Value);
    }

    public class QualifiedIdContainsItemFilter : ItemFilter
    {
        public string Text { get; }

        public QualifiedIdContainsItemFilter(bool IsNegated, string Text)
            : base(ItemFilterType.QualifiedIdContains, IsNegated)
        {
            this.Text = Text;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.QualifiedItemId.Contains(Text);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.QualifiedItemId.Contains(Text);

        public static QualifiedIdContainsItemFilter Parse(bool IsNegated, string Value) => new QualifiedIdContainsItemFilter(IsNegated, Value);
    }
    #endregion Qualified Id filters

    #region Local Id filters
    public class LocalIdItemFilter : ItemFilter
    {
        public string Id { get; }

        public LocalIdItemFilter(bool IsNegated, string Id)
            : base(ItemFilterType.LocalId, IsNegated)
        {
            this.Id = Id;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Id == parsedData.ItemId;
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => Id == parsedData.ItemId;

        public static LocalIdItemFilter Parse(bool IsNegated, string Value) => new LocalIdItemFilter(IsNegated, Value);
    }

    public class LocalIdPrefixItemFilter : ItemFilter
    {
        public string Prefix { get; }

        public LocalIdPrefixItemFilter(bool IsNegated, string Prefix)
            : base(ItemFilterType.LocalIdPrefix, IsNegated)
        {
            this.Prefix = Prefix;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.ItemId.StartsWith(Prefix);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.ItemId.StartsWith(Prefix);

        public static LocalIdPrefixItemFilter Parse(bool IsNegated, string Value) => new LocalIdPrefixItemFilter(IsNegated, Value);
    }

    public class LocalIdSuffixItemFilter : ItemFilter
    {
        public string Suffix { get; }

        public LocalIdSuffixItemFilter(bool IsNegated, string Suffix)
            : base(ItemFilterType.LocalIdSuffix, IsNegated)
        {
            this.Suffix = Suffix;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.ItemId.EndsWith(Suffix);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.ItemId.EndsWith(Suffix);

        public static LocalIdSuffixItemFilter Parse(bool IsNegated, string Value) => new LocalIdSuffixItemFilter(IsNegated, Value);
    }

    public class LocalIdContainsItemFilter : ItemFilter
    {
        public string Text { get; }

        public LocalIdContainsItemFilter(bool IsNegated, string Text)
            : base(ItemFilterType.LocalIdContains, IsNegated)
        {
            this.Text = Text;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.ItemId.Contains(Text);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.ItemId.Contains(Text);

        public static LocalIdContainsItemFilter Parse(bool IsNegated, string Value) => new LocalIdContainsItemFilter(IsNegated, Value);
    }
    #endregion Local Id filters

    #region Name filters
    public class NameItemFilter : ItemFilter
    {
        public string Name { get; }

        public NameItemFilter(bool IsNegated, string Name)
            : base(ItemFilterType.Name, IsNegated)
        {
            this.Name = Name;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName == Name;
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName == Name;

        public static NameItemFilter Parse(bool IsNegated, string Value) => new NameItemFilter(IsNegated, Value);
    }

    public class NamePrefixItemFilter : ItemFilter
    {
        public string Prefix { get; }

        public NamePrefixItemFilter(bool IsNegated, string Prefix)
            : base(ItemFilterType.NamePrefix, IsNegated)
        {
            this.Prefix = Prefix;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName.StartsWith(Prefix);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName.StartsWith(Prefix);

        public static NamePrefixItemFilter Parse(bool IsNegated, string Value) => new NamePrefixItemFilter(IsNegated, Value);
    }

    public class NameSuffixItemFilter : ItemFilter
    {
        public string Suffix { get; }

        public NameSuffixItemFilter(bool IsNegated, string Suffix)
            : base(ItemFilterType.NameSuffix, IsNegated)
        {
            this.Suffix = Suffix;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName.EndsWith(Suffix);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName.EndsWith(Suffix);

        public static NameSuffixItemFilter Parse(bool IsNegated, string Value) => new NameSuffixItemFilter(IsNegated, Value);
    }

    public class NameContainsItemFilter : ItemFilter
    {
        public string Text { get; }

        public NameContainsItemFilter(bool IsNegated, string Text)
            : base(ItemFilterType.NameContains, IsNegated)
        {
            this.Text = Text;
        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName.Contains(Text);
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => parsedData.InternalName.Contains(Text);

        public static NameContainsItemFilter Parse(bool IsNegated, string Value) => new NameContainsItemFilter(IsNegated, Value);
    }
    #endregion Name filters

#if NEVER // for copy-pasting purposes...
    public class SampleItemFilter : ItemFilter
    {
        public SampleItemFilter(bool IsNegated)
            : base(ItemFilterType.Sample, IsNegated)
        {

        }

        protected override bool DerivedIsMatch(ObjectData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => aaaaaaaa;
        protected override bool DerivedIsMatch(BigCraftableData data, ParsedItemData parsedData, ContainerSize size, ObjectQuality quality) => aaaaaaaaaaa;

        public static SomeItemFilter Parse(bool IsNegated, string Value) => new SomeItemFilter(IsNegated);
    }
#endif
}
