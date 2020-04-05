using ItemBags.Bags;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SObject = StardewValley.Object;

namespace ItemBags
{
    public interface IItemBagsAPI
    {
        /// <summary>Returns all <see cref="StardewValley.Object"/> items that are stored inside of the given bag.</summary>
        /// <param name="includeNestedBags">OmniBags are specialized types of bags that can hold other bags inside of them. If includeNestedBags is true, then items inside of bags that are nested within Omnibags will also be included in the result list.</param>
        IList<SObject> GetObjectsInsideBag(ItemBag bag, bool includeNestedBags);

        /// <summary>Returns all <see cref="StardewValley.Object"/> items that are stored inside of any bags found in the given <paramref name="source"/> list.</summary>
        /// <param name="source">The list of items that will be searched for bags.</param>
        /// <param name="includeNestedBags">OmniBags are specialized types of bags that can hold other bags inside of them. If includeNestedBags is true, then items inside of bags that are nested within Omnibags will also be included in the result list.</param>
        IList<SObject> GetObjectsInsideBags(IList<Item> source, bool includeNestedBags);

        /// <summary>Attempts to autofill the given item into the given target bag.</summary>
        /// <param name="autofilledQuantity">The <see cref="StardewValley.Item.Stack"/> amount that was autofilled into the target bag.</param>
        /// <returns>True if at least one quantity of the item was autofilled.</returns>
        bool TryAutofill(SObject item, ItemBag target, out int autofilledQuantity);

        /// <summary>Attempts to autofill the given item into any bags found in the given list of items.</summary>
        /// <param name="item">The item to autofill into a bag.</param>
        /// <param name="container">The list of items that will be searched for valid auto-fillable bags.</param>
        /// <param name="autofilledQuantity">The <see cref="StardewValley.Item.Stack"/> amount that was autofilled into the target bag.</param>
        /// <returns>True if at least one quantity of the item was autofilled.</returns>
        bool TryAutofillToAnyBagsInContainer(SObject item, IList<Item> container, out int autofilledQuantity);

        /// <summary>Attempts to move the given quantities of the given items (each item must have a corresponding int in the quantities list) into the target bag.</summary>
        /// <param name="items">The items to move.</param>
        /// <param name="quantities">The quantities of each item to move.</param>
        /// <param name="itemsSourceContainer">The parent list of items that the items being moved belong to. This list may have items removed from it if their entire Stack is successfully moved into the bag.</param>
        /// <returns>True if at least 1 quantity of at least 1 item is successfully moved.</returns>
        bool TryMoveObjectsToBag(List<SObject> items, List<int> quantities, IList<Item> itemsSourceContainer, ItemBag target, out int totalMovedQty, bool playSoundEffect);

        /// <summary>
        /// Attempts to move the given quantity of the given item into the target bag.<para/>
        /// For performance purposes, it is recommended to use <see cref="TryMoveObjectsToBag(List{SObject}, List{int}, IList{Item}, ItemBag, out int, bool)"/> if you are moving several items at once.
        /// </summary>
        /// <param name="item">The item to move.</param>
        /// <param name="quantity">The quantity of the item to move.</param>
        /// <param name="itemSourceContainer">The parent list of items that the items being moved belong to. This list may have items removed from it if their entire Stack is successfully moved into the bag.</param>
        /// <returns>True if at least 1 quantity of the item is successfully moved.</returns>
        bool TryMoveObjectToBag(SObject item, int quantity, IList<Item> itemSourceContainer, ItemBag target, out int movedQty, bool playSoundEffect);

        /// <summary>Attempts to remove the given quantities of the given items (each item must have a corresponding int in the quantities list) from the bag, to the target list of items.</summary>
        /// <param name="items">The items to remove.</param>
        /// <param name="quantities">The quantities of each item to remove.</param>
        /// <param name="targetContainer">The list of items that removed items should be placed in.</param>
        /// <param name="targetContainerCapacity">The maximum number of item stacks that can fit in the <paramref name="targetContainer"/>. Use <see cref="StardewValley.Farmer.MaxItems"/> if removing to a player's inventory.</param>
        /// <returns>True if at least 1 quantity of at least 1 item is successfully moved.</returns>
        bool TryRemoveObjectsFromBag(ItemBag bag, List<SObject> items, List<int> quantities, IList<Item> targetContainer, int targetContainerCapacity, bool playSoundEffect, out int totalMovedQty);

        /// <summary>
        /// Attempts to remove the given quantity of the given item from the bag, into the target list of items.<para/>
        /// For performance purposes, it is recommended to use <see cref="TryRemoveObjectsFromBag(ItemBag, List{SObject}, List{Item}, IList{Item}, int, bool, out int)"/> if you are moving several items at once.
        /// </summary>
        /// <param name="item">The item to remove</param>
        /// <param name="quantity">The quantity of the item to remove</param>
        /// <param name="targetContainer">The list of items that removed items should be placed in.</param>
        /// <param name="targetContainerCapacity">The maximum number of item stacks that can fit in the <paramref name="targetContainer"/>. Use <see cref="StardewValley.Farmer.MaxItems"/> if removing to a player's inventory.</param>
        /// <returns>True if at least 1 quantity of the item is successfully removed.</returns>
        bool TryRemoveObjectFromBag(ItemBag bag, SObject item, int quantity, IList<Item> targetContainer, int targetContainerCapacity, bool playSoundEffect, out int movedQty);

        /// <summary>Opens the bag, and replaces the <see cref="StardewValley.Game1.activeClickableMenu"/> with the new-created bag menu.</summary>
        /// <param name="sourceItems">The list of items that will be displayed in the bottom-half of the bag menu. Typically this is just <see cref="StardewValley.Game1.player.Items"/> if transferring items to/from the player's inventory.</param>
        /// <param name="sourceCapacity">The maximum number of item stacks that can fit in the <paramref name="sourceItems"/>. Use <see cref="StardewValley.Farmer.MaxItems"/> if transferring to/from a player's inventory.</param>
        void Open(ItemBag bag, IList<Item> sourceItems, int sourceCapacity);
    }

    public class ItemBagsAPI : IItemBagsAPI
    {
        public IList<SObject> GetObjectsInsideBag(ItemBag bag, bool includeNestedBags)
        {
            if (bag == null)
                return new List<SObject>();
            else if (bag is OmniBag omniBag)
            {
                if (includeNestedBags)
                    return omniBag.NestedBags.SelectMany(x => GetObjectsInsideBag(x, includeNestedBags)).ToList();
                else
                    return new List<SObject>();
            }
            else
                return new List<SObject>(bag.Contents);
        }

        public IList<SObject> GetObjectsInsideBags(IList<Item> source, bool includeNestedBags)
        {
            if (source == null)
                return new List<SObject>();
            else
            {
                IEnumerable<ItemBag> bags = source.Where(x => x is ItemBag).Cast<ItemBag>();
                return bags.SelectMany(x => GetObjectsInsideBag(x, includeNestedBags)).ToList();
            }
        }

        public bool TryAutofill(SObject item, ItemBag target, out int autofilledQuantity)
        {
            if (target == null)
            {
                autofilledQuantity = 0;
                return false;
            }
            else
                return TryAutofillToAnyBagsInContainer(item, new List<Item>() { target }, out autofilledQuantity);
        }

        public bool TryAutofillToAnyBagsInContainer(SObject item, IList<Item> container, out int autofilledQuantity)
        {
            List<ItemBag> autofillableBags = AutofillHandler.GetAutofillableBags(container, out HashSet<ItemBag> NestedBags);
            if (autofillableBags.Any())
            {
                if (AutofillHandler.TryAutofill(autofillableBags, NestedBags, item, out int qty))
                {
                    autofilledQuantity = qty;
                    return true;
                }
            }

            autofilledQuantity = 0;
            return false;
        }

        public bool TryMoveObjectsToBag(List<SObject> items, List<int> quantities, IList<Item> itemsSourceContainer, ItemBag target, out int totalMovedQty, bool playSoundEffect)
        {
            if (target == null || items == null || quantities == null || itemsSourceContainer == null)
            {
                totalMovedQty = 0;
                return false;
            }
            else
            {
                return target.MoveToBag(items, quantities, out totalMovedQty, playSoundEffect, itemsSourceContainer);
            }
        }

        public bool TryMoveObjectToBag(SObject item, int quantity, IList<Item> itemSourceContainer, ItemBag target, out int movedQty, bool playSoundEffect)
        {
            if (target == null || item == null || itemSourceContainer == null)
            {
                movedQty = 0;
                return false;
            }
            else
            {
                return target.MoveToBag(item, quantity, out movedQty, playSoundEffect, itemSourceContainer);
            }
        }

        public bool TryRemoveObjectsFromBag(ItemBag bag, List<SObject> items, List<int> quantities, IList<Item> targetContainer, int targetContainerCapacity, bool playSoundEffect, out int totalMovedQty)
        {
            if (bag == null || items == null || quantities == null || targetContainer == null)
            {
                totalMovedQty = 0;
                return false;
            }
            else
            {
                return bag.MoveFromBag(items, quantities, out totalMovedQty, playSoundEffect, targetContainer, targetContainerCapacity);
            }
        }

        public bool TryRemoveObjectFromBag(ItemBag bag, SObject item, int quantity, IList<Item> targetContainer, int targetContainerCapacity, bool playSoundEffect, out int movedQty)
        {
            if (bag == null || item == null || targetContainer == null)
            {
                movedQty = 0;
                return false;
            }
            else
            {
                return bag.MoveFromBag(item, quantity, out movedQty, playSoundEffect, targetContainer, targetContainerCapacity);
            }
        }

        public void Open(ItemBag bag, IList<Item> sourceItems, int sourceCapacity)
        {
            bag.OpenContents(sourceItems, sourceCapacity);
        }
    }
}
