﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley.Menus;
using StardewValley.Tools;
using Microsoft.Xna.Framework.Graphics;
using System.Xml.Serialization;
using StardewValley.Objects;
using ItemBags.Helpers;
using ItemBags.Menus;
using ItemBags.Bags;
using Object = StardewValley.Object;
using Microsoft.Xna.Framework.Input;
using ItemBags.Persistence;
using System.Reflection;
using StardewValley.Locations;
using ItemBags.Community_Center;
using static ItemBags.Persistence.BagSizeConfig;
using System.IO;

namespace ItemBags
{
    public class ItemBagsMod : Mod
    {
        public static Version CurrentVersion = new Version(1, 4, 9); // Last updated 6/3/2020 (Don't forget to update manifest.json)
        public const string ModUniqueId = "SlayerDharok.Item_Bags";
        public const string JAUniqueId = "spacechase0.JsonAssets";

        //Unreleased changes:
        //  Minor bugfix with menu handling for Alex's ice cream shop
        //  Items added to a chest can now be autofilled into bags inside of the chest
        //  Minor bugfix for the way HUDMessages of autofilled items are grouped together
        //  Fixed issue with the "generate_modded_bag" console command
        //  Added Gamepad support to most of the menus
        //  Updated Android version

        //Possible TODO 
        //  "Equipment Bag" : subclass of BoundedBag - has a List<Weapon>, List<Hat> etc. List<AllowedHat> AllowedHats List<AllowedWeapon> AllowedWeapons etc
        //      would need to override IsValidBagItem, and the MoveToBag/MoveFromBag needs a new implementation to handle non-Objects. Allow the items to stack even if item.maximumStackSize == 1
        //  Gamepad support:
        //      BundleBagMenu - Allow navigating to empty slots? Store a HoveredSlot rectangle and use that when calling TryNavigate
        //      Make CustomizeIconMenu implement IGamepadControllable
        //      Make Gamepad keybinds configurable (The consts in GamepadControls.cs)
        //      When calling TryNavigateEnter, pass in a nullable Rectangle of the last hovered screen position. Try to find closest selectable rectangle when navigating into neighbor.

        internal static ItemBagsMod ModInstance { get; private set; }
        internal static string Translate(string Key, Dictionary<string, string> Parameters = null)
        {
            Translation Result;
            if (Parameters != null)
                Result = ModInstance.Helper.Translation.Get(Key, Parameters);
            else
                Result = ModInstance.Helper.Translation.Get(Key);

            if (!Result.HasValue())
                return "";
            else
                return Result;
        }

        public const string BagConfigDataKey = "bagconfig"; //  Note that SMAPI saves the global data to AppData\Roaming\StardewValley\.smapi\mod-data\SlayerDharok.Item_Bags
        public static BagConfig BagConfig { get; private set; }
        private const string UserConfigFilename = "config.json";
        public static UserConfig UserConfig { get; private set; }
        private const string ModdedItemsFilename = "modded_items.json";
        public static ModdedItems ModdedItems { get; private set; }

        internal static Dictionary<ModdedBag, BagType> TemporaryModdedBagTypes { get; private set; }

        public override void Entry(IModHelper helper)
        {
            ModInstance = this;

            LoadUserConfig();
            LoadGlobalConfig();
            LoadModdedItems();
            LoadModdedBags();
            BagConfig.AfterLoaded();

            helper.Events.Display.MenuChanged += Display_MenuChanged;
            helper.Events.Display.WindowResized += Display_WindowResized;

            helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;

            helper.Events.GameLoop.Saving += (sender, e) => { SaveLoadHelpers.OnSaving(); };
            helper.Events.GameLoop.Saved += (sender, e) => { SaveLoadHelpers.OnSaved(); };
            helper.Events.GameLoop.SaveLoaded += (sender, e) => { SaveLoadHelpers.OnLoaded(); };

            helper.Events.GameLoop.GameLaunched += (sender, e) =>
            {
                //  Add compatibility with the Save Anywhere mod
                string SaveAnywhereUniqueId = "Omegasis.SaveAnywhere";
                bool IsSaveAnywhereInstalled = Helper.ModRegistry.IsLoaded(SaveAnywhereUniqueId) ||
                    Helper.ModRegistry.GetAll().Any(x => x.Manifest.Name.Equals("Save Anywhere", StringComparison.CurrentCultureIgnoreCase));
                if (IsSaveAnywhereInstalled)
                {
                    try
                    {
                        ISaveAnywhereAPI API = Helper.ModRegistry.GetApi<ISaveAnywhereAPI>(SaveAnywhereUniqueId);
                        if (API != null)
                        {
                            API.addBeforeSaveEvent(ModUniqueId, () => { SaveLoadHelpers.OnSaving(); });
                            API.addAfterSaveEvent(ModUniqueId, () => { SaveLoadHelpers.OnSaved(); });
                            API.addAfterLoadEvent(ModUniqueId, () => { SaveLoadHelpers.OnLoaded(); });
                        }
                    }
                    catch (Exception ex)
                    {
                        Monitor.Log(string.Format("Failed to bind to Save Anywhere's Mod API. Your game may crash while saving with Save Anywhere! Error: {0}", ex.Message), LogLevel.Warn);
                    }
                }

                ModdedBag.OnGameLaunched();
            };

            InputHandler.OnModEntry(helper);
            CraftingHandler.OnModEntry(helper);
            CommandHandler.OnModEntry(helper);
            AutofillHandler.OnModEntry(helper);
            MultiplayerHandler.OnModEntry(helper);
            MonsterLootHandler.OnModEntry(helper);
        }

        internal static void LoadUserConfig()
        {
            //  Load global user settings into memory
            UserConfig GlobalUserConfig = ModInstance.Helper.Data.ReadJsonFile<UserConfig>(UserConfigFilename);
            if (GlobalUserConfig != null)
            {
                bool RewriteConfig = false;

                //  Update config with settings for managing which bags are sold at shops (Added in v1.2.3)
                if (GlobalUserConfig.CreatedByVersion == null || GlobalUserConfig.CreatedByVersion < new Version(1, 2, 3))
                {
                    RewriteConfig = true;
                }
                //  Update config with settings for managing bag drop rates (Added in v1.4.5)
                if (GlobalUserConfig.CreatedByVersion == null || GlobalUserConfig.CreatedByVersion < new Version(1, 4, 5))
                {
                    GlobalUserConfig.MonsterLootSettings = new MonsterLootSettings();
                    RewriteConfig = true;
                }

                if (RewriteConfig)
                {
                    GlobalUserConfig.CreatedByVersion = CurrentVersion;
                    ModInstance.Helper.Data.WriteJsonFile(UserConfigFilename, GlobalUserConfig);
                }
            }
            else
            {
                GlobalUserConfig = new UserConfig() { CreatedByVersion = CurrentVersion };
                ModInstance.Helper.Data.WriteJsonFile<UserConfig>(UserConfigFilename, GlobalUserConfig);
            }
            UserConfig = GlobalUserConfig;
        }

        private static void LoadGlobalConfig()
        {
            BagConfig GlobalBagConfig = ModInstance.Helper.Data.ReadGlobalData<BagConfig>(BagConfigDataKey);
#if DEBUG
            //GlobalBagConfig = null; // force full re-creation of types for testing
#endif
            if (GlobalBagConfig != null)
            {
                bool RewriteConfig = false;

                //  Update the config with new Bag Types that were added in later versions
                if (GlobalBagConfig.CreatedByVersion == null)
                {
                    GlobalBagConfig.EnsureBagTypesExist(
                        BagTypeFactory.GetOceanFishBagType(),
                        BagTypeFactory.GetRiverFishBagType(),
                        BagTypeFactory.GetLakeFishBagType(),
                        BagTypeFactory.GetMiscFishBagType()
                    );
                    RewriteConfig = true;
                }
                if (GlobalBagConfig.CreatedByVersion == null || GlobalBagConfig.CreatedByVersion < new Version(1, 2, 4))
                {
                    if (Constants.TargetPlatform != GamePlatform.Android)
                    {
                        GlobalBagConfig.EnsureBagTypesExist(BagTypeFactory.GetFishBagType());
                        RewriteConfig = true;
                    }
                }
                if (GlobalBagConfig.CreatedByVersion == null || GlobalBagConfig.CreatedByVersion < new Version(1, 3, 1))
                {
                    GlobalBagConfig.EnsureBagTypesExist(
                        BagTypeFactory.GetFarmerBagType(),
                        BagTypeFactory.GetFoodBagType()
                    );
                    RewriteConfig = true;
                }
                if (GlobalBagConfig.CreatedByVersion == null || GlobalBagConfig.CreatedByVersion < new Version(1, 3, 3))
                {
                    GlobalBagConfig.EnsureBagTypesExist(BagTypeFactory.GetCropBagType());
                    RewriteConfig = true;
                }
                if (GlobalBagConfig.CreatedByVersion == null || GlobalBagConfig.CreatedByVersion < new Version(1, 4, 6))
                {
                    //  I was accidentally serializing BagConfig.IndexedBagTypes which doubled the file size. Whatever, doesn't really matter since it's a small file
                    RewriteConfig = true;

                    //  Lots of rebalancing happened in v1.4.6, so completely remake the config but save a backup copy of the existing file in case user manually edited it
                    ModInstance.Helper.Data.WriteGlobalData(BagConfigDataKey + "-backup_before_v1.4.6_update", GlobalBagConfig);
                    GlobalBagConfig = new BagConfig() { CreatedByVersion = CurrentVersion };
                }
                if (GlobalBagConfig.CreatedByVersion == null || GlobalBagConfig.CreatedByVersion < new Version(1, 4, 8))
                {
                    //  Added a new setting, "AllowAutofillInsideChest"
                    RewriteConfig = true;
                }

                //  Suppose you just added a new BagType "Scarecrow Bag" to version 1.0.12
                //  Then keep the BagConfig up-to-date by doing:
                /*if (GlobalBagConfig.CreatedByVersion == null || GlobalBagConfig.CreatedByVersion < new Version(1, 0, 12))
                {
                    GlobalBagConfig.EnsureBagTypesExist(
                        BagTypeFactory.GetScarecrowBagType()    
                    );
                    ChangesMade = true;
                }*/
                //  Would also need to add more entries to the i18n/default.json and other translation files

                if (RewriteConfig)
                {
                    GlobalBagConfig.CreatedByVersion = CurrentVersion;
                    ModInstance.Helper.Data.WriteGlobalData(BagConfigDataKey, GlobalBagConfig);
                }
            }
            else
            {
                GlobalBagConfig = new BagConfig() { CreatedByVersion = CurrentVersion };
                ModInstance.Helper.Data.WriteGlobalData(BagConfigDataKey, GlobalBagConfig);
            }
            BagConfig = GlobalBagConfig;
        }

        private static void LoadModdedItems()
        {
            ModdedItems GlobalModdedItems = ModInstance.Helper.Data.ReadJsonFile<ModdedItems>(ModdedItemsFilename);
#if DEBUG
            //GlobalModdedItems = null; // force full re-creation for testing
#endif
            if (GlobalModdedItems != null)
            {
                bool RewriteConfig = false;

                //  Placeholder - keep the json file up-to-date if you make changes to it

                if (RewriteConfig)
                {
                    GlobalModdedItems.CreatedByVersion = CurrentVersion;
                    ModInstance.Helper.Data.WriteJsonFile(ModdedItemsFilename, GlobalModdedItems);
                }
            }
            else
            {
                GlobalModdedItems = new ModdedItems { CreatedByVersion = CurrentVersion };
#if NEVER // DEBUG // Testing
                GlobalModdedItems.ModAddons = new List<ModAddon>()
                {
                    new ModAddon()
                    {
                        UniqueId = "ppja.artisanvalleyPFM",
                        BagAddons = new List<BagAddon>()
                        {
                            new BagAddon()
                            {
                                Name = "Crop Bag",
                                Items = new List<ModdedItem>()
                                {
                                    new ModdedItem()
                                    {
                                        Name = "Drying Rack",
                                        IsBigCraftable = true,
                                        HasQualities = false,
                                        SizeString = "Large"
                                    }
                                }
                            }
                        }
                    }
                };
#endif
                ModInstance.Helper.Data.WriteJsonFile(ModdedItemsFilename, GlobalModdedItems);
            }
            ModdedItems = GlobalModdedItems;
        }

        private static void LoadModdedBags()
        {
            try
            {
                List<ModdedBag> ModdedBags = new List<ModdedBag>();
                string ModdedBagsDirectory = Path.Combine(ModInstance.Helper.DirectoryPath, "assets", "Modded Bags");
                string[] ModdedBagFiles = Directory.GetFiles(ModdedBagsDirectory, "*.json", SearchOption.TopDirectoryOnly);
                if (ModdedBagFiles.Length > 0)
                {
                    if (!ModInstance.Helper.ModRegistry.IsLoaded(JAUniqueId))
                    {
                        ModInstance.Monitor.Log("Modded bags could not be loaded because you do not have Json Assets mod installed.", LogLevel.Warn);
                    }
                    else
                    {
                        foreach (string File in ModdedBagFiles)
                        {
                            string RelativePath = File.Replace(ModInstance.Helper.DirectoryPath + Path.DirectorySeparatorChar, "");
                            ModdedBag ModdedBag = ModInstance.Helper.Data.ReadJsonFile<ModdedBag>(RelativePath);

                            if (ModdedBag.IsEnabled && (string.IsNullOrEmpty(ModdedBag.ModUniqueId) || ModInstance.Helper.ModRegistry.IsLoaded(ModdedBag.ModUniqueId)))
                            {
                                if (!ModdedBags.Any(x => x.Guid == ModdedBag.Guid))
                                {
                                    ModdedBags.Add(ModdedBag);
                                }
                                else
                                {
                                    ModInstance.Monitor.Log(string.Format("Failed to load modded bag '{0}' because there is already another modded bag with the same Id", ModdedBag.BagName), LogLevel.Warn);
                                }
                            }
                        }

                        ModInstance.Monitor.Log(string.Format("Loaded {0} modded bag(s): {1}", ModdedBags.Count, string.Join(", ", ModdedBags.Select(x => x.BagName))), LogLevel.Info);
                    }
                }

                TemporaryModdedBagTypes = new Dictionary<ModdedBag, BagType>();
                foreach (ModdedBag Bag in ModdedBags)
                {
                    BagType Placeholder = Bag.GetBagTypePlaceholder();
                    TemporaryModdedBagTypes.Add(Bag, Placeholder);
                    BagConfig.BagTypes.Add(Placeholder);
                }
            }
            catch (Exception ex)
            {
                ModInstance.Monitor.Log(string.Format("Error while loading modded bag json files: {0}\n\n{1}", ex.Message, ex.ToString()), LogLevel.Error);
            }
        }

        public override object GetApi()
        {
            return new ItemBagsAPI();
        }

        private void Display_WindowResized(object sender, WindowResizedEventArgs e)
        {
            if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is ItemBagMenu IBM)
                IBM.OnWindowSizeChanged();
        }

        private void GameLoop_UpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is ItemBagMenu IBM)
                IBM.Update(e);
        }

        private void Display_MenuChanged(object sender, MenuChangedEventArgs e)
        {
            //  Refresh completed Bundles in the community center
            if (e.OldMenu != null && e.OldMenu is JunimoNoteMenu)
            {
                CommunityCenterBundles.Instance = new CommunityCenterBundles();
            }

            if (e.NewMenu is ShopMenu SM)
            {
                //  Determine if the shop menu belongs to one of our managed shops
                bool IsModifiableShop = false;
                BagShop BagShop = BagShop.Pierre;
                if (SM.portraitPerson?.Name != null)
                {
                    //TODO test if the Stardew Valley Expanded shops like Isaac/Sophia/Alesia have non-null values for ShopMenu.portraitPerson.Name
                    if (Enum.TryParse(SM.portraitPerson.Name, out BagShop))
                    {
                        IsModifiableShop = true;
                    }
                }
                else if (SM.storeContext != null)
                {
                    if (SM.storeContext.Equals("Forest", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (SM.onPurchase?.GetMethodInfo().Name == "onTravelingMerchantShopPurchase")
                            BagShop = BagShop.TravellingCart;
                        else
                            BagShop = BagShop.HatMouse;
                        IsModifiableShop = true;
                    }
                    else if (SM.storeContext.Equals("Town", StringComparison.CurrentCultureIgnoreCase) && SM.potraitPersonDialogue != null && SM.potraitPersonDialogue.Contains("Khadija"))
                    {
                        BagShop = BagShop.Khadija;
                        IsModifiableShop = true;
                    }
                }

                //  Add Bag items to the shop's stock
                if (IsModifiableShop)
                {
                    Dictionary<ISalable, int[]> Stock = SM.itemPriceAndStock;

                    bool ShouldModifyStock = true;
                    if (BagShop == BagShop.Clint)
                    {
                        //  Assume user is viewing Clint tool upgrades if the stock doesn't contain Coal
                        if (!Stock.Any(x => x.Key is Object Obj && Obj.Name.Equals("Coal", StringComparison.CurrentCultureIgnoreCase)))
                            ShouldModifyStock = false;
                    }

                    if (ShouldModifyStock)
                    {
                        bool HasChangedStock = false;

                        List<ItemBag> OwnedBags = UserConfig.HideObsoleteBagsFromShops ? ItemBag.GetAllBags(true) : new List<ItemBag>();

                        //  Add Bounded Bags to stock
                        foreach (BagType Type in BagConfig.BagTypes)
                        {
                            foreach (BagSizeConfig SizeCfg in Type.SizeSettings)
                            {
                                bool IsSoldByShop = UserConfig.IsSizeVisibleInShops(SizeCfg.Size) && SizeCfg.Sellers.Contains(BagShop);
#if DEBUG
                                //IsSoldByShop = true;
#endif
                                if (IsSoldByShop)
                                {
                                    bool IsObsolete = false;
                                    if (UserConfig.HideObsoleteBagsFromShops)
                                    {
                                        IsObsolete = OwnedBags.Any(x => x is BoundedBag BB && BB.TypeInfo == Type && (int)BB.Size > (int)SizeCfg.Size);
                                    }

                                    if (!IsObsolete)
                                    {
                                        BoundedBag SellableInstance = new BoundedBag(Type, SizeCfg.Size, false);
                                        int Price = SellableInstance.GetPurchasePrice();
#if DEBUG
                                        //Price = 1 + (int)SizeCfg.Size;
#endif
                                        Stock.Add(SellableInstance, new int[] { Price, ShopMenu.infiniteStock });
                                        HasChangedStock = true;
                                    }
                                }
                            }
                        }

                        //  Add Bundle Bags to stock
                        foreach (BundleBagSizeConfig SizeCfg in UserConfig.BundleBagSettings)
                        {
                            ContainerSize Size = SizeCfg.Size;
                            if (BundleBag.ValidSizes.Contains(Size) && SizeCfg.Sellers.Contains(BagShop) && UserConfig.IsSizeVisibleInShops(Size))
                            {
                                bool IsObsolete = false;
                                if (UserConfig.HideObsoleteBagsFromShops)
                                {
                                    IsObsolete = OwnedBags.Any(x => x is BundleBag BB && (int)BB.Size > (int)Size);
                                }

                                if (!IsObsolete)
                                {
                                    BundleBag BundleBag = new BundleBag(Size, true);
                                    int Price = BundleBag.GetPurchasePrice();
                                    Stock.Add(BundleBag, new int[] { Price, ShopMenu.infiniteStock });
                                    HasChangedStock = true;
                                }
                            }
                        }

                        //  Add Rucksacks to stock
                        foreach (RucksackSizeConfig SizeCfg in UserConfig.RucksackSettings)
                        {
                            ContainerSize Size = SizeCfg.Size;
                            if (SizeCfg.Sellers.Contains(BagShop) && UserConfig.IsSizeVisibleInShops(Size))
                            {
                                bool IsObsolete = false;
                                if (UserConfig.HideObsoleteBagsFromShops)
                                {
                                    IsObsolete = OwnedBags.Any(x => x is Rucksack RS && (int)RS.Size > (int)Size);
                                }

                                if (!IsObsolete)
                                {
                                    Rucksack Rucksack = new Rucksack(Size, false, AutofillPriority.Low);
                                    int Price = Rucksack.GetPurchasePrice();
                                    Stock.Add(Rucksack, new int[] { Price, ShopMenu.infiniteStock });
                                    HasChangedStock = true;
                                }
                            }
                        }

                        //  Add Omni Bags to stock
                        foreach(OmniBagSizeConfig SizeCfg in UserConfig.OmniBagSettings)
                        {
                            ContainerSize Size = SizeCfg.Size;
                            if (SizeCfg.Sellers.Contains(BagShop) && UserConfig.IsSizeVisibleInShops(Size))
                            {
                                bool IsObsolete = false;
                                if (UserConfig.HideObsoleteBagsFromShops)
                                {
                                    IsObsolete = OwnedBags.Any(x => x is OmniBag OB && (int)OB.Size > (int)Size);
                                }

                                if (!IsObsolete)
                                {
                                    OmniBag OmniBag = new OmniBag(Size);
                                    int Price = OmniBag.GetPurchasePrice();
                                    Stock.Add(OmniBag, new int[] { Price, ShopMenu.infiniteStock });
                                    HasChangedStock = true;
                                }
                            }
                        }

                        if (HasChangedStock)
                        {
                            SM.setItemPriceAndStock(Stock);
                        }
                    }
                }
            }
        }
    }
}
