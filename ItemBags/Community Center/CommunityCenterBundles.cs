using ItemBags.Bags;
using StardewValley;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItemBags.Community_Center
{
    public class CommunityCenterBundles
    {
        public static CommunityCenterBundles Instance { get; set; }

        public CommunityCenter Building { get; }
        public bool IsJojaMember { get; }

        /// <summary>Warning - The Vault room has been omitted since its Tasks require Gold instead of Items.</summary>
        public ReadOnlyCollection<BundleRoom> Rooms { get; }

        /// <summary>Key = Item Id, Value = All distinct minimum qualities for the item.<para/>
        /// For example, Parsnip Quality = 0 is required by Spring Crops Bundle, while Parsnip Quality = 2 is required by Quality Crops Bundle. 
        /// So the Value at Parsnip's Id would be a set containing <see cref="ObjectQuality.Regular"/> and <see cref="ObjectQuality.Gold"/></summary>
        public Dictionary<int, HashSet<ObjectQuality>> IncompleteBundleItemIds { get; }

        public CommunityCenterBundles()
        {
            try
            {
                //Possible TODO: Load the current language's Bundle .xnb file (Does SMAPI Automatically do this for us when loading game content?)
                //Refer to: LocalizedContentManager.CurrentLanguageCode and use that code to build the string of the content filename, such as Data\Bundles.pt-BR if language code is portuguese

                this.Building = Game1.getLocationFromName("CommunityCenter") as CommunityCenter;
                this.IsJojaMember = Game1.MasterPlayer.mailReceived.Contains("JojaMember"); // Possible TODO Do names of received mail depend on current language?

                string DataPath = @"Data\Bundles";
                Dictionary<string, string> RawBundleData = Game1.content.Load<Dictionary<string, string>>(DataPath);
                Dictionary<string, List<Tuple<int, string>>> GroupedByRoomName = new Dictionary<string, List<Tuple<int, string>>>();
                foreach (KeyValuePair<string, string> KVP in RawBundleData)
                {
                    string RoomName = KVP.Key.Split('/').First();
                    int TaskIndex = int.Parse(KVP.Key.Split('/').Last());

                    List<Tuple<int, string>> Tasks;
                    if (!GroupedByRoomName.TryGetValue(RoomName, out Tasks))
                    {
                        Tasks = new List<Tuple<int, string>>();
                        GroupedByRoomName.Add(RoomName, Tasks);
                    }
                    Tasks.Add(Tuple.Create(TaskIndex, KVP.Value));
                }
                GroupedByRoomName.Remove("Vault");
                this.Rooms = GroupedByRoomName.Select(x => new BundleRoom(this, x.Key, x.Value)).ToList().AsReadOnly();

                Dictionary<int, BundleTask> IndexedTasks = Rooms.SelectMany(x => x.Tasks).ToDictionary(x => x.BundleIndex);

                //  Fill in data for which items of which tasks have been completed
                foreach (var KVP in Building.bundlesDict())
                {
                    //Possible TODO: Building.bundlesDict() doesn't seem to have the latest completion data if using Minerva's harder community center mod
                    //so with that mod installed, the Bundle Bag cannot properly detect partially completed rooms. (Still works with Building.isBundleComplete(...) for checking full room completion

                    BundleTask Task;
                    if (IndexedTasks.TryGetValue(KVP.Key, out Task))
                    {
                        //  For some strange reason, the bundlesDict bool[] is storing a boolean for every single number in the bundle's required items,
                        //  rather than a bool for every item in the bundle's required items. (An item is composed of 3 numbers, Id, Qty, Quality)
                        //  EX: If the data is "16 1 0", that means the bundle requires 1 horseradish of quality >= 0, but the bool[] would store { true, false, false } if that item is fulfilled.
                        //  So we're only looking at every third boolean
                        List<bool> CompletedItems = new List<bool>();
                        for (int i = 0; i < KVP.Value.Length; i += 3)
                        {
                            CompletedItems.Add(KVP.Value[i]);
                        }

                        for (int i = 0; i < CompletedItems.Count; i++)
                        {
                            if (Task.Items.Count > i) // User could have loaded a save file that has already completed the task, but the task now requires less items due to installing a Community Center mod
                                Task.Items[i].IsCompleted = CompletedItems[i];
                        }
                    }
                }

                //  Fill in data for which tasks have been completed
                foreach (BundleTask Task in IndexedTasks.Values)
                {
                    if (Building.isBundleComplete(Task.BundleIndex) || IsJojaMember)
                    {
                        Task.Items.ToList().ForEach(x => x.IsCompleted = true);
                    }
                }

                //  Index the required bundle items by their Id and accepted Qualities
                this.IncompleteBundleItemIds = new Dictionary<int, HashSet<ObjectQuality>>();
                IterateAllBundleItems(Item =>
                {
                    if (!Item.IsCompleted)
                    {
                        int Id = Item.Id;
                        ObjectQuality Quality = Item.MinQuality;

                        HashSet<ObjectQuality> Qualities;
                        if (!IncompleteBundleItemIds.TryGetValue(Id, out Qualities))
                        {
                            Qualities = new HashSet<ObjectQuality>();
                            IncompleteBundleItemIds.Add(Id, Qualities);
                        }

                        Qualities.Add(Quality);
                    }
                });
            }
            catch (Exception ex)
            {
                this.IsJojaMember = false;
                this.Rooms = new List<BundleRoom>().AsReadOnly();
                this.IncompleteBundleItemIds = new Dictionary<int, HashSet<ObjectQuality>>();

                ItemBagsMod.ModInstance.Monitor.Log(string.Format("Error while instantiating CommunityCenterBundles: {0}", ex.Message), StardewModdingAPI.LogLevel.Error);
                ItemBagsMod.ModInstance.Monitor.Log(string.Format("Error while instantiating CommunityCenterBundles: {0}", ex.ToString()), StardewModdingAPI.LogLevel.Error);
            }
        }

        /// <summary>Invokes the given Action on all <see cref="BundleItem"/> within every <see cref="BundleTask"/> of every <see cref="BundleRoom"/></summary>
        public void IterateAllBundleItems(Action<BundleItem> Action)
        {
            foreach (BundleRoom Room in Rooms)
            {
                foreach (BundleTask Task in Room.Tasks)
                {
                    foreach (BundleItem Item in Task.Items)
                    {
                        Action(Item);
                    }
                }
            }
        }
    }
}
