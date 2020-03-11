using ItemBags.Bags;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Object = StardewValley.Object;

namespace ItemBags
{
    public static class AutofillHandler
    {
        private static IModHelper Helper { get; set; }
        private static IMonitor Monitor { get { return ItemBagsMod.ModInstance.Monitor; } }

        /// <summary>Adds Autofill feature, which allows picked up items to automatically be placed inside of bags in the player's inventory</summary>
        internal static void OnModEntry(IModHelper Helper)
        {
            AutofillHandler.Helper = Helper;
            Helper.Events.Player.InventoryChanged += Player_InventoryChanged;
        }

        private static bool IsHandlingInventoryChanged { get; set; } = false;

        private static void Player_InventoryChanged(object sender, InventoryChangedEventArgs e)
        {
            if (e.IsLocalPlayer && !IsHandlingInventoryChanged && Game1.activeClickableMenu == null)
            // && !(Game1.activeClickableMenu is ItemBagMenu) && !(Game1.activeClickableMenu is GameMenu) && !(Game1.activeClickableMenu is ShopMenu) && !(Game1.activeClickableMenu is ItemGrabMenu))
            {
                try
                {
                    IsHandlingInventoryChanged = true;

                    HashSet<ItemBag> NestedBags = new HashSet<ItemBag>();

                    //  Get all bags in the player's inventory that can be autofilled
                    List<ItemBag> AutofillableBags = new List<ItemBag>();
                    foreach (Item Item in e.Player.Items)
                    {
                        if (Item != null && Item is ItemBag)
                        {
                            if (Item is BoundedBag BB)
                            {
                                if (BB.Autofill)
                                    AutofillableBags.Add(BB);
                            }
                            else if (Item is Rucksack RS)
                            {
                                if (RS.Autofill)
                                    AutofillableBags.Add(RS);
                            }
                            else if (Item is OmniBag OB)
                            {
                                foreach (ItemBag NestedBag in OB.NestedBags)
                                {
                                    if (NestedBag is BoundedBag NestedBB)
                                    {
                                        if (NestedBB.Autofill)
                                        {
                                            AutofillableBags.Add(NestedBB);
                                            NestedBags.Add(NestedBag);
                                        }
                                    }
                                    else if (NestedBag is Rucksack NestedRS)
                                    {
                                        if (NestedRS.Autofill)
                                        {
                                            AutofillableBags.Add(NestedRS);
                                            NestedBags.Add(NestedBag);
                                        }
                                    }
                                }
                            }
                        }
                    }

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
                                        int NestedPenalty = NestedBags.Contains(x) ? 10 : 0; // Items nested inside of Omni Bags have lower priority than non-nested bags
                                        if (x is BundleBag)
                                            return 0 + NestedPenalty; // Prioritize filling Bundle Bags first
                                        else if (x is Rucksack RS)
                                        {
                                            int Priority = RS.AutofillPriority == AutofillPriority.High ? 1 : 4;
                                            return Priority + NestedPenalty; // Prioritize Rucksacks with HighPriority over BoundedBags
                                        }
                                        else if (x is BoundedBag BB)
                                        {
                                            //  Prioritize BoundedBags that already have an existing stack of the item over BoundedBags that don't
                                            if (x.Contents.Any(BagItem => BagItem != null && ItemBag.AreItemsEquivalent(NewObject, BagItem, false)))
                                                return 2 + NestedPenalty;
                                            else
                                                return 3 + NestedPenalty;
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
                finally
                {
                    IsHandlingInventoryChanged = false;
                }
            }
        }
    }
}
