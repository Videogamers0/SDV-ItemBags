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
            bool CanAutofill = false;
            if (e.IsLocalPlayer && !IsHandlingInventoryChanged)
            {
                if (Game1.activeClickableMenu == null)
                    // !(Game1.activeClickableMenu is ItemBagMenu) && !(Game1.activeClickableMenu is GameMenu) && !(Game1.activeClickableMenu is ShopMenu) && !(Game1.activeClickableMenu is ItemGrabMenu))
                {
                    CanAutofill = true;
                }
                else
                {
                    //Possible TODO
                    //  Improve autofilling logic by allowing autofill even if certain menus are open.
                    //  Currently, autofilling is disabled while a menu is active, because we don't know if the inventory changed event is due to an item being picked up, or due to some other action happening.
                    //  (such as by receiving a gift in the mail, or buying from a shop, or from a quest reward, or using a mod command to spawn items in inventory etc)
                    //  Maybe I should still allow autofilling if the active menu is StardewValley.Menus.GameMenu (but ignore InventoryChanged events that are due to an inventory item being dragged/dropped to/from the cursor)
                    //  Maybe also allow autofilling if the active menu is a DialogueBox AND the player is located in the skull cavern? 
                    //  (Players might've picked up an item while inspecting a skull cavern hole to drop down through, so active menu is a DialogueBox)
                }
            }

            if (CanAutofill)
            {
                try
                {
                    IsHandlingInventoryChanged = true;

                    //  Get all bags in the player's inventory that can be autofilled
                    List<ItemBag> AutofillableBags = GetAutofillableBags(e.Player.Items, out HashSet<ItemBag> NestedBags);

                    if (AutofillableBags.Any())
                    {
                        foreach (Item NewItem in e.Added)
                        {
                            TryAutofill(AutofillableBags, NestedBags, NewItem, out int AutofilledQuantity);
                        }
                    }
                }
                finally
                {
                    IsHandlingInventoryChanged = false;
                }
            }
        }

        internal static bool TryAutofill(List<ItemBag> AutofillableBags, HashSet<ItemBag> NestedBags, Item Item, out int AutofilledQuantity)
        {
            AutofilledQuantity = 0;

            if (AutofillableBags.Any() && Item != null && Item is Object Obj && Item.Stack > 0)
            {
                List<ItemBag> ValidTargets = new List<ItemBag>();
                foreach (ItemBag Bag in AutofillableBags.Where(x => x.IsValidBagObject(Obj) && !x.IsFull(Obj)))
                {
                    //  Don't allow Rucksacks to be autofilled with the new item unless they already have an existing stack of it
                    if (!(Bag is Rucksack) || Bag.Contents.Any(x => x != null && ItemBag.AreItemsEquivalent(Obj, x, true)))
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
                            if (x.Contents.Any(BagItem => BagItem != null && ItemBag.AreItemsEquivalent(Obj, BagItem, false)))
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
                        Target.MoveToBag(Obj, Obj.Stack, out int MovedQty, false, Game1.player.Items);
                        AutofilledQuantity += MovedQty;
                        if (MovedQty > 0)
                        {
                            Game1.addHUDMessage(new HUDMessage(string.Format("Moved {0} to {1}", Item.DisplayName, Target.DisplayName), MovedQty, true, Color.White, Target));
                        }

                        if (Obj.Stack <= 0)
                            break;
                    }
                }
            }

            return AutofilledQuantity > 0;
        }

        internal static List<ItemBag> GetAutofillableBags(IList<Item> SourceItems, out HashSet<ItemBag> NestedBags)
        {
            NestedBags = new HashSet<ItemBag>();

            List<ItemBag> AutofillableBags = new List<ItemBag>();
            foreach (Item Item in SourceItems)
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
            return AutofillableBags;
        }
    }
}
