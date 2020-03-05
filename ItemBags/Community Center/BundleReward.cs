using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Object = StardewValley.Object;

namespace ItemBags.Community_Center
{
    public class BundleReward
    {
        public BundleTask Task { get; }
        public int Id { get; }
        public int Quantity { get; }
        public bool IsBigCraftable { get; }
        public bool IsRing { get; }
        public bool IsWeapon { get; }

        public enum BundleRewardType
        {
            Object,
            BigCraftable,
            Ring,
            Weapon
        }

        public BundleReward(BundleTask Task, int Id, int Quantity, BundleRewardType Type)
        {
            this.Task = Task;
            this.Id = Id;
            this.Quantity = Quantity;
            this.IsBigCraftable = Type == BundleRewardType.BigCraftable;
            this.IsRing = Type == BundleRewardType.Ring;
            this.IsWeapon = Type == BundleRewardType.Weapon;
        }

        /// <param name="RawData">The raw data string from the game's bundle content. EX: "O 495 30".<para/>
        /// This format is described here: <see cref="https://stardewvalleywiki.com/Modding:Bundles"/></param>
        public BundleReward(BundleTask Task, string RawData)
        {
            this.Task = Task;

            List<string> Entries = RawData.Split(' ').ToList();
            BundleRewardType RewardType;
            if (Entries[0].Equals("O", StringComparison.CurrentCultureIgnoreCase))
                RewardType = BundleRewardType.Object;
            else if (Entries[0].Equals("BO", StringComparison.CurrentCultureIgnoreCase))
                RewardType = BundleRewardType.BigCraftable;
            else if (Entries[0].Equals("R", StringComparison.CurrentCultureIgnoreCase))
                RewardType = BundleRewardType.Ring;
            else if (Entries[0].Equals("W", StringComparison.CurrentCultureIgnoreCase))
                RewardType = BundleRewardType.Weapon;
            else
                throw new NotImplementedException(string.Format("Unrecognized Bundle Reward Type: {0}", Entries[0]));

            this.Id = int.Parse(Entries[1]);
            this.Quantity = int.Parse(Entries[2]);
            this.IsBigCraftable = RewardType == BundleRewardType.BigCraftable;
            this.IsRing = RewardType == BundleRewardType.Ring;
            this.IsWeapon = RewardType == BundleRewardType.Weapon;
        }

        public Item ToItem()
        {
            if (IsBigCraftable)
            {
                return new Object(Vector2.Zero, Id, false);
            }
            else if (IsRing)
            {
                return new Ring(Id);
            }
            else if (IsWeapon)
            {
                return new MeleeWeapon(Id);
            }
            else
            {
                return new Object(Id, Quantity, false, -1, 0);
            }
        }
    }
}
