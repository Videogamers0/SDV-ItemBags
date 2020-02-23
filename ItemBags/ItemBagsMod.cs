using System;
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

namespace ItemBags
{
    public class ItemBagsMod : Mod
    {
        public static Version CurrentVersion = new Version(1, 2, 2); // Last updated 2/23/2020 (Don't forget to update manifest.json)

        //Possible TODO 
        //  Netplay support. Not sure where to begin but should probably start by storing the entire Object in BagItem.cs instead of just saving a handful of properties like Id/Quantity/Price.
        //      and convert ItemBag fields into their Net equivalents, such as BoundedBag.Autofill could be a NetBool instead of bool. And Add XmlElement tags to the properties that need serialized.
        //      Then probably need to add checks for IsLocalPlayer, and maybe use Game1.MasterPlayer.Items instead of Game1.player.Items? Idk
        //      Then re-work the SaveLoadHelpers. Maybe the ReplaceAllInstances needs to iterate the inventories of all the farmhands?
        //  dynamically resize interfaces to fit to screen? Maybe after InitializeLayout finishes, check if it's too big. If so, lower the SlotSize and call InitializeLayout again.
        //  dynamically load json files in the mod directory that can be deserialized into BagTypes, giving users an easier way to use custom bags without needing the edit the behemoth that is bagconfig.json.
        //  Rucksack tooltips: if not in shopmenu, draw a scaled-down version of the contents interface, maybe 32x32 slots, without quantity, and maybe cap it to the first 72 slots in case
        //      user has edited the config files to make the bag store a ridiculous amount of items
        //  An additional sidebar button in topleft of bag contents interface. hovering over it displays a tooltip with total bag content's summed values

        internal static ItemBagsMod ModInstance { get; private set; }
        internal static string Translate(string Key, Dictionary<string, string> Parameters = null)
        {
            if (Parameters != null)
                return ModInstance.Helper.Translation.Get(Key, Parameters);
            else
                return ModInstance.Helper.Translation.Get(Key);
        }

        public const string BagConfigDataKey = "bagconfig"; //  Note that SMAPI saves the global data to AppData\Roaming\StardewValley\.smapi\mod-data\SlayerDharok.Item_Bags
        public static BagConfig BagConfig { get; private set; }
        private const string UserConfigFilename = "config.json";
        public static UserConfig UserConfig { get; private set; }

        internal static bool IsMegaStorageInstalled { get; private set; } = false;

        public override void Entry(IModHelper helper)
        {
            ModInstance = this;

            IsMegaStorageInstalled = Helper.ModRegistry.IsLoaded("Alek.MegaStorage") || Helper.ModRegistry.GetAll().Any(x => x.Manifest.Name.Equals("Mega Storage", StringComparison.CurrentCultureIgnoreCase)); ;

            //BagType GemBagType = BagType.DeserializeFromXML(@"C:\Programming\Source\Personal\SDV\GemBag.xml", out bool Success, out Exception Error);
            //BagTypeFactory.GetGemBagType().SerializeToXML(@"C:\Programming\Source\Personal\SDV\GemBag.xml", out bool Success, out Exception Error);

            //  Load global user settings into memory
            UserConfig GlobalUserConfig = helper.Data.ReadJsonFile<UserConfig>(UserConfigFilename);
            if (GlobalUserConfig == null)
            {
                GlobalUserConfig = new UserConfig() { CreatedByVersion = CurrentVersion };
                helper.Data.WriteJsonFile<UserConfig>(UserConfigFilename, GlobalUserConfig);
            }
            UserConfig = GlobalUserConfig;

            //  Load global bag data into memory
            BagConfig GlobalBagConfig = helper.Data.ReadGlobalData<BagConfig>(BagConfigDataKey);
#if DEBUG
            //GlobalBagConfig = null; // force full re-creation of types for testing
#endif
            if (GlobalBagConfig != null)
            {
                bool RewriteConfig = false;
                if (GlobalBagConfig.CreatedByVersion == null)
                {
                    GlobalBagConfig.EnsureBagTypesExist(
                        BagTypeFactory.GetOceanFishBagType(),
                        BagTypeFactory.GetRiverFishBagType(),
                        BagTypeFactory.GetLakeFishBagType(),
                        BagTypeFactory.GetMiscFishBagType());
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
                    helper.Data.WriteGlobalData(BagConfigDataKey, GlobalBagConfig);
                }
            }
            else
            {
                GlobalBagConfig = new BagConfig() { CreatedByVersion = CurrentVersion };
                helper.Data.WriteGlobalData(BagConfigDataKey, GlobalBagConfig);
            }
            BagConfig = GlobalBagConfig;

            helper.Events.Display.MenuChanged += Display_MenuChanged;
            helper.Events.Display.WindowResized += Display_WindowResized;

            helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            helper.Events.GameLoop.UpdateTicking += GameLoop_UpdateTicking;
            helper.Events.Player.InventoryChanged += Player_InventoryChanged;

            helper.Events.Input.CursorMoved += Input_MouseMoved;
            helper.Events.Input.ButtonPressed += Input_ButtonPressed;
            helper.Events.Input.ButtonReleased += Input_ButtonReleased;

            helper.Events.GameLoop.Saving += (sender, e) => { SaveLoadHelpers.OnSaving(); };
            helper.Events.GameLoop.Saved += (sender, e) => { SaveLoadHelpers.OnSaved(); };
            helper.Events.GameLoop.SaveLoaded += (sender, e) => { SaveLoadHelpers.OnLoaded(); };

            RegisterCommands();
        }

        private void RegisterCommands()
        {
            RegisterAddItemBagCommand();
            RegisterAddBundleBagCommand();
            RegisterAddRucksackCommand();
        }

        private void RegisterAddItemBagCommand()
        {
            List<string> ValidSizes = Enum.GetValues(typeof(ContainerSize)).Cast<ContainerSize>().Select(x => x.ToString()).ToList();
            List<string> ValidTypes = BagConfig.BagTypes.Select(x => x.Name).ToList();

            //Possible TODO: Add translation support for this command
            string CommandName = "player_additembag";
            string CommandHelp = string.Format("Adds an empty Bag of the desired size and type to your inventory.\n"
                + "Arguments: <BagSize> <BagType>\n"
                + "Example: {0} Massive River Fish Bag\n\n"
                + "Valid values for <BagSize>: {1}\n\n"
                + "Valid values for <BagType>: {2}",
                CommandName, string.Join(", ", ValidSizes), string.Join(", ", ValidTypes));
            Helper.ConsoleCommands.Add(CommandName, CommandHelp, (string Name, string[] Args) =>
            {
                if (Game1.player.isInventoryFull())
                {
                    Monitor.Log("Unable to execute command: Inventory is full!", LogLevel.Alert);
                }
                else if (Args.Length < 2)
                {
                    Monitor.Log("Unable to execute command: Required arguments missing!", LogLevel.Alert);
                }
                else
                {
                    string SizeName = Args[0];
                    if (!Enum.TryParse(SizeName, out ContainerSize Size))
                    {
                        Monitor.Log(string.Format("Unable to execute command: <BagSize> \"{0}\" is not valid. Expected valid values: {1}", SizeName, string.Join(", ", ValidSizes)), LogLevel.Alert);
                    }
                    else
                    {
                        string TypeName = string.Join(" ", Args.Skip(1));
                        //Possible TODO: If you add translation support to this command, then find the BagType where BagType.GetTranslatedName().Equals(TypeName, StringComparison.CurrentCultureIgnoreCase));
                        BagType BagType = BagConfig.BagTypes.FirstOrDefault(x => x.Name.Equals(TypeName, StringComparison.CurrentCultureIgnoreCase));
                        if (BagType == null)
                        {
                            Monitor.Log(string.Format("Unable to execute command: <BagType> \"{0}\" is not valid. Expected valid values: {1}", TypeName, string.Join(", ", ValidTypes)), LogLevel.Alert);
                        }
                        else
                        {
                            if (!BagType.SizeSettings.Any(x => x.Size == Size))
                            {
                                Monitor.Log(string.Format("Unable to execute command: Type='{0}' does not contain a configuration for Size='{1}'", TypeName, SizeName), LogLevel.Alert);
                            }
                            else
                            {
                                try
                                {
                                    BoundedBag NewBag = new BoundedBag(BagType, Size, false);
                                    Game1.player.addItemToInventory(NewBag);
                                }
                                catch (Exception ex)
                                {
                                    Monitor.Log(string.Format("ItemBags: Unhandled error while executing command: {0}", ex.Message), LogLevel.Error);
                                }
                            }
                        }
                    }
                }
            });
        }

        private void RegisterAddBundleBagCommand()
        {
            List<string> ValidSizes = BundleBag.ValidSizes.Select(x => x.ToString()).ToList();

            //Possible TODO: Add translation support for this command
            string CommandName = "player_addbundlebag";
            string CommandHelp = string.Format("Adds an empty Bundle Bag of the desired size to your inventory.\n"
                + "Arguments: <BagSize>\n"
                + "Example: {0} Large\n\n"
                + "Valid values for <BagSize>: {1}\n\n",
                CommandName, string.Join(", ", ValidSizes));
            Helper.ConsoleCommands.Add(CommandName, CommandHelp, (string Name, string[] Args) =>
            {
                if (Game1.player.isInventoryFull())
                {
                    Monitor.Log("Unable to execute command: Inventory is full!", LogLevel.Alert);
                }
                else if (Args.Length < 1)
                {
                    Monitor.Log("Unable to execute command: Required arguments missing!", LogLevel.Alert);
                }
                else
                {
                    string SizeName = Args[0];
                    if (!Enum.TryParse(SizeName, out ContainerSize Size) || !ValidSizes.Contains(SizeName))
                    {
                        Monitor.Log(string.Format("Unable to execute command: <BagSize> \"{0}\" is not valid. Expected valid values: {1}", SizeName, string.Join(", ", ValidSizes)), LogLevel.Alert);
                    }
                    else
                    {
                        try
                        {
                            BundleBag NewBag = new BundleBag(Size, true);
                            Game1.player.addItemToInventory(NewBag);
                        }
                        catch (Exception ex)
                        {
                            Monitor.Log(string.Format("ItemBags: Unhandled error while executing command: {0}", ex.Message), LogLevel.Error);
                        }
                    }
                }
            });
        }

        private void RegisterAddRucksackCommand()
        {
            List<string> ValidSizes = Enum.GetValues(typeof(ContainerSize)).Cast<ContainerSize>().Select(x => x.ToString()).ToList();

            //Possible TODO: Add translation support for this command
            string CommandName = "player_addrucksack";
            string CommandHelp = string.Format("Adds an empty Rucksack of the desired size to your inventory.\n"
                + "Arguments: <BagSize>\n"
                + "Example: {0} Large\n\n"
                + "Valid values for <BagSize>: {1}\n\n",
                CommandName, string.Join(", ", ValidSizes));
            Helper.ConsoleCommands.Add(CommandName, CommandHelp, (string Name, string[] Args) =>
            {
                if (Game1.player.isInventoryFull())
                {
                    Monitor.Log("Unable to execute command: Inventory is full!", LogLevel.Alert);
                }
                else if (Args.Length < 1)
                {
                    Monitor.Log("Unable to execute command: Required arguments missing!", LogLevel.Alert);
                }
                else
                {
                    string SizeName = Args[0];
                    if (!Enum.TryParse(SizeName, out ContainerSize Size) || !ValidSizes.Contains(SizeName))
                    {
                        Monitor.Log(string.Format("Unable to execute command: <BagSize> \"{0}\" is not valid. Expected valid values: {1}", SizeName, string.Join(", ", ValidSizes)), LogLevel.Alert);
                    }
                    else
                    {
                        try
                        {
                            Rucksack NewBag = new Rucksack(Size, true);
                            Game1.player.addItemToInventory(NewBag);
                        }
                        catch (Exception ex)
                        {
                            Monitor.Log(string.Format("ItemBags: Unhandled error while executing command: {0}", ex.Message), LogLevel.Error);
                        }
                    }
                }
            });
        }

        private bool IsHandlingInventoryChanged { get; set; } = false;

        private void Player_InventoryChanged(object sender, InventoryChangedEventArgs e)
        {
            if (e.IsLocalPlayer && !IsHandlingInventoryChanged && Game1.activeClickableMenu == null) 
                // && !(Game1.activeClickableMenu is ItemBagMenu) && !(Game1.activeClickableMenu is GameMenu) && !(Game1.activeClickableMenu is ShopMenu) && !(Game1.activeClickableMenu is ItemGrabMenu))
            {
                try
                {
                    IsHandlingInventoryChanged = true;
                    if (e.IsLocalPlayer)
                    {
                        List<ItemBag> AutofillableBags = e.Player.Items.Where(x => x != null && ((x is BoundedBag BB && BB.Autofill) || (x is Rucksack RS && RS.Autofill))).Cast<ItemBag>().ToList();
                        if (AutofillableBags.Any())
                        {
                            foreach (Item NewItem in e.Added)
                            {
                                if (NewItem is Object NewObject && NewItem.Stack > 0)
                                {
                                    List<ItemBag> ValidTargets = new List<ItemBag>();
                                    foreach (ItemBag Bag in AutofillableBags.Where(x => x.IsValidBagObject(NewObject) && !x.IsFull(NewObject)))
                                    {
                                        //  Don't allow Rucksacks to be autofilled with the new item unless they already have an existing stack of it
                                        if (!(Bag is Rucksack) || Bag.Contents.Any(x => x != null && ItemBag.AreItemsEquivalent(NewObject, x, true)))
                                            ValidTargets.Add(Bag);
                                    }

                                    if (ValidTargets.Any())
                                    {
                                        List<ItemBag> SortedTargets = ValidTargets.OrderBy(x =>
                                        {
                                            if (x is BundleBag)
                                                return 0; // Prioritize filling Bundle Bags first
                                            else if (x is Rucksack RS)
                                                return RS.AutofillPriority == AutofillPriority.High ? 1 : 4; // Prioritize Rucksacks with HighPriority over BoundedBags
                                            else if (x is BoundedBag BB)
                                            {
                                                //  Prioritize BoundedBags that already have an existing stack of the item over BoundedBags that don't
                                                if (x.Contents.Any(BagItem => BagItem != null && ItemBag.AreItemsEquivalent(NewObject, BagItem, false)))
                                                    return 2;
                                                else
                                                    return 3;
                                            }
                                            else
                                                throw new NotImplementedException(string.Format("Unexpected Bag type in Autofill sorter: {0}", x.GetType().ToString()));
                                        }).ToList();

                                        for (int i = 0; i < SortedTargets.Count; i++)
                                        {
                                            ItemBag Target = SortedTargets[i];
                                            Target.MoveToBag(NewObject, NewObject.Stack, out int MovedQty, false, Game1.player.Items);
                                            if (MovedQty > 0)
                                            {
                                                Game1.addHUDMessage(new HUDMessage(string.Format("Moved {0} to {1}", NewItem.DisplayName, Target.DisplayName), MovedQty, true, Color.White, Target));
                                            }

                                            if (NewObject.Stack <= 0)
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    IsHandlingInventoryChanged = false;
                }
            }
        }

        private bool QueuePlaceCursorSlotItem { get; set; }
        private int? QueueCursorSlotIndex { get; set; }

        private void GameLoop_UpdateTicking(object sender, UpdateTickingEventArgs e)
        {
            try
            {
                //  Swaps the current CursorSlotItem with the inventory item at index=QueueCursorSlotIndex
                if (QueuePlaceCursorSlotItem && QueueCursorSlotIndex.HasValue)
                {
                    if (Game1.activeClickableMenu is GameMenu GM && GM.currentTab == GameMenu.inventoryTab)
                    {
                        Item Temp = Game1.player.Items[QueueCursorSlotIndex.Value];
                        Game1.player.Items[QueueCursorSlotIndex.Value] = Game1.player.CursorSlotItem;
                        Game1.player.CursorSlotItem = Temp;
                    }
                }
            }
            finally { QueuePlaceCursorSlotItem = false; QueueCursorSlotIndex = null; }
        }

        private void Input_MouseMoved(object sender, CursorMovedEventArgs e)
        {
            if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is ItemBagMenu IBM)
                IBM.OnMouseMoved(e);
        }

        private void Display_WindowResized(object sender, WindowResizedEventArgs e)
        {
            if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is ItemBagMenu IBM)
                IBM.OnWindowSizeChanged();
        }

        private void Input_ButtonReleased(object sender, ButtonReleasedEventArgs e)
        {
            if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is ItemBagMenu IBM)
                IBM.OnMouseButtonReleased(e);
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

            //  Add Bag items to the shop's stock
            if (e.NewMenu is ShopMenu SM)
            {
                bool IsTravellingMerchant = SM.portraitPerson == null && SM.storeContext != null && SM.storeContext.Equals("Forest", StringComparison.CurrentCultureIgnoreCase);
                string ShopOwnerName = IsTravellingMerchant ? "TravellingCart" : SM.portraitPerson?.Name;

                if (!string.IsNullOrEmpty(ShopOwnerName))
                {
                    Dictionary<ISalable, int[]> Stock = SM.itemPriceAndStock;

                    bool ShouldModifyStock = true;
                    if (ShopOwnerName.Equals("Clint", StringComparison.CurrentCultureIgnoreCase))
                    {
                        //  Assume user is viewing Clint tool upgrades if the stock doesn't contain Coal
                        if (!Stock.Any(x => x.Key is Object Obj && Obj.Name.Equals("Coal", StringComparison.CurrentCultureIgnoreCase)))
                            ShouldModifyStock = false;
                    }

                    if (ShouldModifyStock)
                    {
                        bool HasChangedStock = false;

                        foreach (BagType Type in BagConfig.BagTypes)
                        {
                            foreach (BagSizeConfig SizeCfg in Type.SizeSettings)
                            {
                                bool IsSoldByShop = SizeCfg.Sellers.Any(x => x.ToString() == ShopOwnerName);
#if DEBUG
                                //IsSoldByShop = true;
#endif
                                if (IsSoldByShop)
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

                        if (ShopOwnerName.Equals("Pierre"))
                        {
                            foreach (ContainerSize Size in Enum.GetValues(typeof(ContainerSize)).Cast<ContainerSize>())
                            {
                                Rucksack Rucksack = new Rucksack(Size, false, AutofillPriority.Low);
                                int Price = Rucksack.GetPurchasePrice();
                                Stock.Add(Rucksack, new int[] { Price, ShopMenu.infiniteStock });
                            }

                            HasChangedStock = true;
                        }

                        if (ShopOwnerName.Equals("TravellingCart"))
                        {
                            foreach (ContainerSize Size in BundleBag.ValidSizes)
                            {
                                BundleBag BundleBag = new BundleBag(Size, true);
                                int Price = BundleBag.GetPurchasePrice();
                                Stock.Add(BundleBag, new int[] { Price, ShopMenu.infiniteStock });
                            }

                            HasChangedStock = true;
                        }

                        if (HasChangedStock)
                        {
                            SM.setItemPriceAndStock(Stock);
                        }
                    }
                }
            }
        }

        private ItemBag LastClickedBag { get; set; }
        private int? LastClickedBagInventoryIndex { get; set; }
        private DateTime? LastClickedBagTime { get; set; }
        private const int DoubleClickThresholdMS = 300; // Clicking the same Bag in your inventory within this amount of time will register as a double-click

        public const int DefaultChestCapacity = 36;

        private void Input_ButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            try
            {
                Point CursorPos = e.Cursor.ScreenPixels.AsAndroidCompatibleCursorPoint();

                if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is ItemBagMenu IBM)
                {
                    IBM.OnMouseButtonPressed(e);
                }
                else if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is GameMenu GM && GM.currentTab == GameMenu.inventoryTab)
                {
                    InventoryPage InvPage = GM.pages.First(x => x is InventoryPage) as InventoryPage;
                    InventoryMenu InvMenu = InvPage.inventory;

                    int ClickedItemIndex = InvMenu.getInventoryPositionOfClick(CursorPos.X, CursorPos.Y);
                    bool IsValidInventorySlot = ClickedItemIndex >= 0 && ClickedItemIndex < InvMenu.actualInventory.Count;
                    if (IsValidInventorySlot)
                    {
                        Item ClickedItem = InvMenu.actualInventory[ClickedItemIndex];

                        //  Double click an ItemBag to open it
                        if (e.Button == SButton.MouseLeft) //SButtonExtensions.IsUseToolButton(e.Button))
                        {
                            //  The first time the user clicks an item in their inventory, Game1.player.CursorSlotItem is set to what they clicked (so it's like drag/drop, they're now holding the item to move it)
                            //  So to detect a double click, we can't just check if they clicked the bag twice in a row, since on the second click the item would no longer be in their inventory.
                            //  Instead, we need to check if they clicked the bag and then we need to check Game1.player.CursorSlotItem on the next click
                            if (ClickedItem is ItemBag ClickedBag && Game1.player.CursorSlotItem == null)
                            {
                                LastClickedBagInventoryIndex = ClickedItemIndex;
                                LastClickedBag = ClickedBag;
                                LastClickedBagTime = DateTime.Now;
                            }
                            else if (ClickedItem == null && Game1.player.CursorSlotItem is ItemBag DraggedBag && LastClickedBag == DraggedBag &&
                                LastClickedBagInventoryIndex.HasValue && LastClickedBagInventoryIndex.Value == ClickedItemIndex &&
                                LastClickedBagTime.HasValue && DateTime.Now.Subtract(LastClickedBagTime.Value).TotalMilliseconds <= DoubleClickThresholdMS)
                            {
                                LastClickedBag = DraggedBag;
                                LastClickedBagTime = DateTime.Now;

                                //  Put the item that's being dragged back into their inventory
                                Game1.player.addItemToInventory(Game1.player.CursorSlotItem, ClickedItemIndex);
                                Game1.player.CursorSlotItem = null;

                                DraggedBag.OpenContents(Game1.player.Items, Game1.player.MaxItems);
                            }
                        }
                        //  Right-click an ItemBag to open it
                        else if (e.Button == SButton.MouseRight && ClickedItem is ItemBag ClickedBag && Game1.player.CursorSlotItem == null)
                        {
                            ClickedBag.OpenContents(Game1.player.Items, Game1.player.MaxItems);
                        }

                        //  Handle dropping an item into a bag from the Inventory menu
                        if (ClickedItem is ItemBag IB && Game1.player.CursorSlotItem != null && Game1.player.CursorSlotItem is Object Obj)
                        {
                            if (IB.IsValidBagItem(Obj) && (e.Button == SButton.MouseLeft || e.Button == SButton.MouseRight))
                            {
                                int Qty = ItemBag.GetQuantityToTransfer(e, Obj);
                                IB.MoveToBag(Obj, Qty, out int MovedQty, true, Game1.player.Items);

                                if (e.Button == SButton.MouseLeft) 
                                    // || (MovedQty > 0 && Obj.Stack == 0) // Handle moving the last quantity with a right-click
                                {
                                    //  Clicking the bag will have made it become the held CursorSlotItem, so queue up an action that will swap them back on next game tick
                                    QueueCursorSlotIndex = ClickedItemIndex;
                                    QueuePlaceCursorSlotItem = true;
                                }
                            }
                        }
                    }
                }
                else if (Game1.activeClickableMenu == null && (e.Button == SButton.MouseLeft || e.Button == SButton.MouseRight))
                {
                    //  Check if they clicked a bag on the toolbar, open the bag if so
                    Toolbar toolbar = Game1.onScreenMenus.FirstOrDefault(x => x is Toolbar) as Toolbar;
                    if (toolbar != null)
                    {
                        try
                        {
                            List<ClickableComponent> toolbarButtons = typeof(Toolbar).GetField("buttons", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(toolbar) as List<ClickableComponent>;
                            if (toolbarButtons != null)
                            {
                                //  Find the slot on the toolbar that they clicked, if any
                                for (int i = 0; i < toolbarButtons.Count; i++)
                                {
                                    if (toolbarButtons[i].bounds.Contains(CursorPos))
                                    {
                                        int ActualIndex = i;
                                        if (Constants.TargetPlatform == GamePlatform.Android)
                                        {
                                            try
                                            {
                                                int StartIndex = Helper.Reflection.GetField<int>(toolbar, "_drawStartIndex").GetValue(); // This is completely untested
                                                ActualIndex = i + StartIndex;
                                            }
                                            catch (Exception) { }
                                        }

                                        //  Get the corresponding Item from the player's inventory
                                        Item item = Game1.player.Items[ActualIndex];
                                        if (item is ItemBag IB)
                                        {
                                            IB.OpenContents(Game1.player.Items, Game1.player.MaxItems);
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception) { }
                    }
                }
                else if (Game1.activeClickableMenu is ItemGrabMenu IGM && IGM.context is Chest ChestSource && (e.Button == SButton.MouseRight || e.Button == SButton.MouseMiddle))
                {
                    //  Check if they clicked a Bag in the inventory part of the chest interface
                    bool Handled = false;
                    for (int i = 0; i < IGM.inventory.inventory.Count; i++)
                    {
                        ClickableComponent Component = IGM.inventory.inventory[i];
                        if (Component != null && Component.bounds.Contains(CursorPos))
                        {
                            Item ClickedInvItem = i < 0 || i >= IGM.inventory.actualInventory.Count ? null : IGM.inventory.actualInventory[i];
                            if (ClickedInvItem is ItemBag IB)
                            {
                                IB.OpenContents(IGM.inventory.actualInventory, Game1.player.MaxItems);
                            }
                            Handled = true;
                            break;
                        }
                    }

                    bool IsUnmoddedChest = !IsMegaStorageInstalled || IGM.ItemsToGrabMenu.capacity == DefaultChestCapacity; //ChestSource.Name.Equals("Chest", StringComparison.CurrentCultureIgnoreCase))
                    if (!Handled && IsUnmoddedChest)
                    {
                        //  Check if they clicked a Bag in the chest part of the chest interface
                        for (int i = 0; i < IGM.ItemsToGrabMenu.inventory.Count; i++)
                        {
                            ClickableComponent Component = IGM.ItemsToGrabMenu.inventory[i];
                            if (Component != null && Component.bounds.Contains(CursorPos))
                            {
                                Item ClickedChestItem = i < 0 || i >= IGM.ItemsToGrabMenu.actualInventory.Count ? null : IGM.ItemsToGrabMenu.actualInventory[i];
                                if (ClickedChestItem is ItemBag IB)
                                {
                                    IB.OpenContents(IGM.ItemsToGrabMenu.actualInventory, IGM.ItemsToGrabMenu.capacity);
                                }
                                Handled = true;
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ItemBagsMod.ModInstance.Monitor.Log($"Unhandled error in Input_ButtonPressed: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
