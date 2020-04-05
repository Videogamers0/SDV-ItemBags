using ItemBags.Bags;
using ItemBags.Persistence;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ItemBags.Persistence.BagSizeConfig;

namespace ItemBags
{
    public static class CommandHandler
    {
        private static IModHelper Helper { get; set; }
        private static IMonitor Monitor { get { return ItemBagsMod.ModInstance.Monitor; } }
        private static BagConfig BagConfig { get { return ItemBagsMod.BagConfig; } }

        /// <summary>Adds several SMAPI Console commands for adding bags to the player's inventory</summary>
        internal static void OnModEntry(IModHelper Helper)
        {
            CommandHandler.Helper = Helper;

            RegisterAddItemBagCommand();
            RegisterAddBundleBagCommand();
            RegisterAddRucksackCommand();
            RegisterAddOmniBagCommand();
            RegisterGenerateModdedBagCommand();
        }

        private static void RegisterAddItemBagCommand()
        {
            List<string> ValidSizes = Enum.GetValues(typeof(ContainerSize)).Cast<ContainerSize>().Select(x => x.ToString()).ToList();
            List<string> ValidTypes = BagConfig.BagTypes.Select(x => x.Name).ToList();

            //Possible TODO: Add translation support for this command
            string CommandName = Constants.TargetPlatform == GamePlatform.Android ? "addbag" : "player_additembag";
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

        private static void RegisterAddBundleBagCommand()
        {
            List<string> ValidSizes = BundleBag.ValidSizes.Select(x => x.ToString()).ToList();

            //Possible TODO: Add translation support for this command
            string CommandName = Constants.TargetPlatform == GamePlatform.Android ? "addbundlebag" : "player_addbundlebag";
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

        private static void RegisterAddRucksackCommand()
        {
            List<string> ValidSizes = Enum.GetValues(typeof(ContainerSize)).Cast<ContainerSize>().Select(x => x.ToString()).ToList();

            //Possible TODO: Add translation support for this command
            string CommandName = Constants.TargetPlatform == GamePlatform.Android ? "addrucksack" : "player_addrucksack";
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

        private static void RegisterAddOmniBagCommand()
        {
            List<string> ValidSizes = Enum.GetValues(typeof(ContainerSize)).Cast<ContainerSize>().Select(x => x.ToString()).ToList();

            //Possible TODO: Add translation support for this command
            string CommandName = Constants.TargetPlatform == GamePlatform.Android ? "addomnibag" : "player_addomnibag";
            string CommandHelp = string.Format("Adds an empty Omni Bag of the desired size to your inventory.\n"
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
                            OmniBag NewBag = new OmniBag(Size);
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

        private static void RegisterGenerateModdedBagCommand()
        {
            string CommandName = "generate_modded_bag";
            string CommandHelp = string.Format("Creates a json file that defines a modded Item Bag for a particular mod.\n"
                + "Arguments: <ModUniqueID> (This is the 'ModUniqueID' value of the mod's manifest.json that you want to generate the file for)\n"
                + "Example: {0} ppja.artisanvalleymachinegoods\n\n",
                CommandName);
            Helper.ConsoleCommands.Add(CommandName, CommandHelp, (string Name, string[] Args) =>
            {
                try
                {
                    if (!Helper.ModRegistry.IsLoaded(ItemBagsMod.JAUniqueId))
                    {
                        Monitor.Log("Unable to execute command: JsonAssets mod is not installed. Modded bags only support modded objects added through JsonAssets.", LogLevel.Alert);
                    }
                    else if (!ModdedBag.HasImportedItems)
                    {
                        Monitor.Log("Unable to execute command: JsonAssets has not finished loading modded items. You must load a save file before using this command.", LogLevel.Alert);
                    }
                    else
                    {
                        string ModUniqueId = string.Join(" ", Args);
                        if (!Helper.ModRegistry.IsLoaded(ModUniqueId))
                        {
                            string Message = string.Format("Unable to execute command: ModUniqueID = '{0}' is not installed. "
                                + "Either install this mod first, or double check that you used the correct value for ModUniqueID. "
                                + "The ModUniqueID can be found in the mod's manifest.json file.", ModUniqueId);
                            Monitor.Log(Message, LogLevel.Alert);
                        }
                        else
                        {
                            IJsonAssetsAPI API = Helper.ModRegistry.GetApi<IJsonAssetsAPI>(ItemBagsMod.JAUniqueId);
                            if (API != null)
                            {
#if NEVER //DEBUG
                                //  Trying to figure out how to get seed ids belonging to a particular mod.
                                //  It seems like GetAllObjectsFromContentPack doesn't include the seeds, even though they are in GetAllObjectIds
                                string TestSeedName = "Adzuki Bean Seeds";
                                IDictionary<string, int> AllObjectIds = API.GetAllObjectIds();
                                List<string> AllObjectsInMod = API.GetAllObjectsFromContentPack(ModUniqueId);
                                bool IsSeedFoundAnywhere = AllObjectIds != null && AllObjectIds.ContainsKey(TestSeedName);
                                bool IsSeedFoundInMod =  AllObjectsInMod != null && AllObjectsInMod.Contains(TestSeedName);
                                int SeedId = API.GetObjectId(TestSeedName);
#endif

                                string BagName = string.Format("{0} Bag", ModUniqueId);
                                ModdedBag ModdedBag = new ModdedBag()
                                {
                                    IsEnabled = true,
                                    ModUniqueId = ModUniqueId,
                                    Guid = ModdedBag.StringToGUID(ModUniqueId + BagName).ToString(),
                                    BagName = BagName,
                                    BagDescription = string.Format("A bag for storing items belonging to {0} mod", ModUniqueId),
                                    Price = 100000,
                                    Capacity = 9999,
                                    Sellers = new List<BagShop>() { BagShop.Pierre },
                                    MenuOptions = new BagMenuOptions()
                                    {
                                        GroupedLayoutOptions = new BagMenuOptions.GroupedLayout()
                                        {
                                            GroupsPerRow = 5
                                        }
                                    },
                                    Items = ModdedBag.GetModdedItems(ModUniqueId)
                                };

                                string OutputDirectory = Path.Combine(Helper.DirectoryPath, "assets", "Modded Bags");
                                string DesiredFilename = ModdedBag.ModUniqueId;
                                string CurrentFilename = DesiredFilename;
                                int CurrentIndex = 0;
                                while (File.Exists(Path.Combine(OutputDirectory, CurrentFilename + ".json")))
                                {
                                    CurrentIndex++;
                                    CurrentFilename = string.Format("{0} ({1})", DesiredFilename, CurrentIndex);
                                }

                                string RelativePath = Path.Combine("assets", "Modded Bags", CurrentFilename + ".json");
                                Helper.Data.WriteJsonFile(RelativePath, ModdedBag);

                                Monitor.Log(string.Format("File exported to: {0}\nYou will need to re-launch the game for this file to be loaded.", Path.Combine(Helper.DirectoryPath, RelativePath)), LogLevel.Alert);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Monitor.Log(string.Format("ItemBags: Unhandled error while executing command: {0}", ex.Message), LogLevel.Error);
                }
            });
        }
    }
}
