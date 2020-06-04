using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewValley;
using ItemBags.Bags;
using Microsoft.Xna.Framework;

namespace ItemBags.Menus
{
    public static class GamepadControls
    {
        /// <summary>Opens the selected bag, if the bag is selected from the Inventory tab of the main <see cref="StardewValley.Menus.GameMenu"/></summary>
        public const Buttons OpenBagFromInventory = Buttons.Start | Buttons.B | Buttons.X;
        /// <summary>Opens the selected bag, if the bag is selected from a <see cref="StardewValley.Menus.ItemGrabMenu"/> whose <see cref="StardewValley.Menus.ItemGrabMenu.context"/> is a <see cref="StardewValley.Objects.Chest"/></summary>
        public const Buttons OpenBagFromChest = Buttons.Start | Buttons.B | Buttons.X;
        /// <summary>Opens the selected bag, if the bag is selected from the main <see cref="StardewValley.Menus.Toolbar"/>, and no other menus are currently active.</summary>
        public const Buttons OpenBagFromToolbar = Buttons.X;
        /// <summary>Closes the current bag menu.</summary>
        public const Buttons CloseBag = Buttons.B | Buttons.Y | Buttons.Start;

        /// <summary>The # of frames to wait before continuing to navigate 1 item slot in the held direction.<para/>
        /// For example, if <see cref="NavigationRepeatFrequency"/>=10, then holding the thumbstick to the right will navigate at ~60/10=6 slots per second.</summary>
        public const int NavigationRepeatFrequency = 5;
        /// <summary>The # of milliseconds to wait after a gamepad navigation button is pressed, before that navigation action can be repeated while the button remains pressed.</summary>
        public const int NavigationRepeatInitialDelay = 180;

        /// <summary>Moves cursor left by 1 slot.</summary>
        public const Buttons NavigateSingleLeft = Buttons.DPadLeft | Buttons.LeftThumbstickLeft;
        /// <summary>Moves cursor up by 1 slot.</summary>
        public const Buttons NavigateSingleUp = Buttons.DPadUp | Buttons.LeftThumbstickUp;
        /// <summary>Moves cursor right by 1 slot.</summary>
        public const Buttons NavigateSingleRight = Buttons.DPadRight | Buttons.LeftThumbstickRight;
        /// <summary>Moves cursor down by 1 slot.</summary>
        public const Buttons NavigateSingleDown = Buttons.DPadDown | Buttons.LeftThumbstickDown;

        public static readonly Dictionary<NavigationDirection, Buttons> NavigateSingleButtons = new Dictionary<NavigationDirection, Buttons>()
        {
            { NavigationDirection.Left, NavigateSingleLeft },
            { NavigationDirection.Up, NavigateSingleUp },
            { NavigationDirection.Right, NavigateSingleRight },
            { NavigationDirection.Down, NavigateSingleDown }
        };

        /// <summary>Moves cursor left by multiple slots, typically to the start of a row.</summary>
        public const Buttons NavigateMultipleLeft = Buttons.RightThumbstickLeft;
        /// <summary>Moves cursor up by multiple slots, typically to the start of a column.</summary>
        public const Buttons NavigateMultipleUp = Buttons.RightThumbstickUp;
        /// <summary>Moves cursor right by multiple slots, typically to the end of a row.</summary>
        public const Buttons NavigateMultipleRight = Buttons.RightThumbstickRight;
        /// <summary>Moves cursor down by multiple slots, typically to the end of a column.</summary>
        public const Buttons NavigateMultipleDown = Buttons.RightThumbstickDown;

        public static readonly Dictionary<NavigationDirection, Buttons> NavigateMultipleButtons = new Dictionary<NavigationDirection, Buttons>()
        {
            { NavigationDirection.Left, NavigateMultipleLeft },
            { NavigationDirection.Up, NavigateMultipleUp },
            { NavigationDirection.Right, NavigateMultipleRight },
            { NavigationDirection.Down, NavigateMultipleDown }
        };

        /// <summary>A modifier key which, if held, causes the primary transfer action to transfer several quantity of the hovered item slot.</summary>
        public const Buttons TransferMultipleModifier = Buttons.LeftTrigger;
        /// <summary>A modifier key which, if held, causes the primary transfer action to transfer half of the quantity of the hovered item slot.</summary>
        public const Buttons TransferHalfModifier = Buttons.RightTrigger;

        /// <summary>The primary action key, typically used to click buttons or item slots, invoking their primary actions.</summary>
        public const Buttons PrimaryAction = Buttons.A;
        /// <summary>The secondary action key, typically used to click buttons or item slots, invoking their secondary actions (such as transferring a single item at a time).</summary>
        public const Buttons SecondaryAction = Buttons.X;

        /// <summary>If a <see cref="RucksackMenu"/> is currently open, this key cycles the <see cref="Rucksack.SortProperty"/></summary>
        public const Buttons RucksackCycleSortProperty = Buttons.LeftShoulder;
        /// <summary>If a <see cref="RucksackMenu"/> is currently open, this key cycles the <see cref="Rucksack.SortOrder"/></summary>
        public const Buttons RucksackCycleSortOrder = Buttons.RightShoulder;
        /// <summary>If a <see cref="BoundedBagMenu"/> is currently open, this key toggles Autofill on the hovered item slot.</summary>
        public const Buttons BoundedBagToggleAutofill = Buttons.LeftShoulder | Buttons.RightShoulder;

        public static bool IsMatch(Buttons Input1, Buttons Input2)
        {
            return (Input1 & Input2) != 0;
        }

        internal static bool HandleNavigationButtons(IGamepadControllable Instance, Buttons? PressedButtons)
        {
            bool IsFocused = true;

            foreach (NavigationDirection Direction in Enum.GetValues(typeof(NavigationDirection)).Cast<NavigationDirection>())
            {
                //  Handle navigating a single slot at a time
                bool HandleSingleSlotNavigation;
                if (PressedButtons.HasValue)
                    HandleSingleSlotNavigation = IsMatch(PressedButtons.Value, NavigateSingleButtons[Direction]);
                else
                    HandleSingleSlotNavigation = InputHandler.IsNavigationButtonPressed(Direction) && DateTime.Now.Subtract(InputHandler.NavigationButtonsPressedTime[Direction]).TotalMilliseconds >= NavigationRepeatInitialDelay;
                if (HandleSingleSlotNavigation)
                {
                    NavigationWrappingMode HorizontalWrapping = NavigationWrappingMode.AllowWrapToSame;
                    NavigationWrappingMode VerticalWrapping = NavigationWrappingMode.AllowWrapToSame;

                    bool HasNeighbor = Instance.TryGetMenuNeighbor(Direction, out IGamepadControllable Neighbor);
                    if (HasNeighbor)
                    {
                        if (Direction == NavigationDirection.Left || Direction == NavigationDirection.Right)
                            HorizontalWrapping = NavigationWrappingMode.NoWrap;
                        else
                            VerticalWrapping = NavigationWrappingMode.NoWrap;
                    }

                    if (!Instance.TryNavigate(Direction, HorizontalWrapping, VerticalWrapping))
                    {
                        //  If we're unable to continue moving the cursor in the desired direction, 
                        //  then focus the gamepad controls on the appropriate neighboring UI element
                        if (HasNeighbor)
                        {
                            NavigationDirection StartingSide;
                            if (Direction == NavigationDirection.Left)
                                StartingSide = NavigationDirection.Right;
                            else if (Direction == NavigationDirection.Right)
                                StartingSide = NavigationDirection.Left;
                            else if (Direction == NavigationDirection.Up)
                                StartingSide = NavigationDirection.Down;
                            else if (Direction == NavigationDirection.Down)
                                StartingSide = NavigationDirection.Up;
                            else
                                throw new NotImplementedException(string.Format("Unexpected Navigation Direction: {0}", Direction.ToString()));

                            if (Neighbor.TryNavigateEnter(StartingSide))
                                IsFocused = false;
                        }
                    }
                }

                //  Handle navigating an entire row/column
                if (PressedButtons.HasValue && IsMatch(PressedButtons.Value, NavigateMultipleButtons[Direction]))
                {
                    while (Instance.TryNavigate(Direction, NavigationWrappingMode.NoWrap, NavigationWrappingMode.NoWrap)) { }
                }
            }

            return IsFocused;
        }

        internal static bool TryGetSlotNeighbor(IList<Rectangle> AllSlots, Rectangle? CurrentSlot, int ColumnsPerRow, NavigationDirection Direction, NavigationWrappingMode HorizontalWrapping, NavigationWrappingMode VerticalWrapping, out Rectangle? Neighbor)
        {
            Neighbor = null;
            if (!CurrentSlot.HasValue || !AllSlots.Contains(CurrentSlot.Value))
            {
                return false;
            }
            else
            {
                int Index = AllSlots.IndexOf(CurrentSlot.Value);

                int CurrentRow = Index / ColumnsPerRow;
                int CurrentColumn = Index % ColumnsPerRow;

                int MaxRow = Math.Max(0, (AllSlots.Count - 1) / ColumnsPerRow);
                int MaxColumn = Math.Min(AllSlots.Count, ColumnsPerRow) - 1;

                int NeighborRow;
                int NeighborColumn;

                if (Direction == NavigationDirection.Up)
                {
                    NeighborRow = CurrentRow - 1;
                    NeighborColumn = CurrentColumn;

                    if (NeighborRow < 0)
                    {
                        if (VerticalWrapping == NavigationWrappingMode.NoWrap)
                        {
                            return false;
                        }
                        else if (VerticalWrapping == NavigationWrappingMode.AllowWrapToSame)
                        {
                            NeighborRow = MaxRow;
                        }
                        else if (VerticalWrapping == NavigationWrappingMode.AllowWrapToPreviousOrNext)
                        {
                            NeighborRow = MaxRow;
                            NeighborColumn = CurrentColumn - 1;

                            if (NeighborColumn < 0)
                            {
                                NeighborColumn = MaxColumn;
                            }
                        }
                    }
                }
                else if (Direction == NavigationDirection.Down)
                {
                    NeighborRow = CurrentRow + 1;
                    NeighborColumn = CurrentColumn;

                    if (NeighborRow > MaxRow)
                    {
                        if (VerticalWrapping == NavigationWrappingMode.NoWrap)
                        {
                            return false;
                        }
                        else if (VerticalWrapping == NavigationWrappingMode.AllowWrapToSame)
                        {
                            NeighborRow = 0;
                        }
                        else if (VerticalWrapping == NavigationWrappingMode.AllowWrapToPreviousOrNext)
                        {
                            NeighborRow = 0;
                            NeighborColumn = CurrentColumn + 1;

                            if (NeighborColumn > MaxColumn)
                            {
                                NeighborColumn = 0;
                            }
                        }
                    }
                }
                else if (Direction == NavigationDirection.Left)
                {
                    NeighborRow = CurrentRow;
                    NeighborColumn = CurrentColumn - 1;

                    if (NeighborColumn < 0)
                    {
                        if (HorizontalWrapping == NavigationWrappingMode.NoWrap)
                        {
                            return false;
                        }
                        else if (HorizontalWrapping == NavigationWrappingMode.AllowWrapToSame)
                        {
                            NeighborColumn = MaxColumn;
                        }
                        else if (HorizontalWrapping == NavigationWrappingMode.AllowWrapToPreviousOrNext)
                        {
                            NeighborColumn = MaxColumn;
                            NeighborRow = CurrentRow - 1;

                            if (NeighborRow < 0)
                            {
                                NeighborRow = MaxRow;
                            }
                        }
                    }
                }
                else if (Direction == NavigationDirection.Right)
                {
                    NeighborRow = CurrentRow;
                    NeighborColumn = CurrentColumn + 1;

                    if (NeighborColumn > MaxColumn)
                    {
                        if (HorizontalWrapping == NavigationWrappingMode.NoWrap)
                        {
                            return false;
                        }
                        else if (HorizontalWrapping == NavigationWrappingMode.AllowWrapToSame)
                        {
                            NeighborColumn = 0;
                        }
                        else if (HorizontalWrapping == NavigationWrappingMode.AllowWrapToPreviousOrNext)
                        {
                            NeighborColumn = 0;
                            NeighborRow = CurrentRow + 1;

                            if (NeighborRow > MaxRow)
                            {
                                NeighborRow = 0;
                            }
                        }
                    }
                }
                else
                    throw new NotImplementedException(string.Format("Unrecognized Navigation direction: {0}", Direction.ToString()));

                if (NeighborRow < 0 || NeighborRow > MaxRow || NeighborColumn < 0 || NeighborColumn > MaxColumn)
                    return false;
                else
                {
                    int NeighborIndex = NeighborRow * ColumnsPerRow + NeighborColumn;
                    if (NeighborIndex >= AllSlots.Count)
                        return false;
                    else
                    {
                        Neighbor = AllSlots[NeighborIndex];
                        return true;
                    }
                }
            }
        }
    }

    public enum NavigationDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    public enum NavigationWrappingMode
    {
        /// <summary>Navigating forward from the last slot of a row/column, or navigating backwards from the first slot of a row/column is not allowed.</summary>
        NoWrap,
        /// <summary>Navigating forward from the last slot of a row/column will wrap to the first slot of the SAME row/column, or navigating backwards from the first slot of a row/column will wrap to the last slot of the SAME row/column.</summary>
        AllowWrapToSame,
        /// <summary>Navigating forward from the last slot of a row/column will wrap to the first slot of the NEXT row/column, or navigating backwards from the first slot of a row/column will wrap to the last slot of the PREVIOUS row/column.</summary>
        AllowWrapToPreviousOrNext
    }

    /// <summary>Menus that implement this interface support Gamepad controls, instead of just Mouse+Keyboard.</summary>
    public interface IGamepadControllable
    {
        /// <summary>True if this UI element just gained focus on the current game tick, so inputs that are already queued up should not be handled by this element until the following game tick.</summary>
        bool RecentlyGainedFocus { get; }
        bool IsGamepadFocused { get; }
        void GainedGamepadFocus();
        void LostGamepadFocus();

        /// <summary>Stores UI elements that are adjacent to this instance, so that the gamepad focus can be transferred to the neighbor if attempting to move the cursor out-of-bounds of the currently-focused instance.</summary>
        Dictionary<NavigationDirection, IGamepadControllable> MenuNeighbors { get; }
        bool TryGetMenuNeighbor(NavigationDirection Direction, out IGamepadControllable Neighbor);
        bool TryGetSlotNeighbor(Rectangle? ItemSlot, NavigationDirection Direction, NavigationWrappingMode HorizontalWrapping, NavigationWrappingMode VerticalWrapping, out Rectangle? Neighbor);
        bool TryNavigate(NavigationDirection Direction, NavigationWrappingMode HorizontalWrapping, NavigationWrappingMode VerticalWrapping);
        bool TryNavigateEnter(NavigationDirection StartingSide);

        bool IsNavigatingWithGamepad { get; }

        void OnGamepadButtonsPressed(Buttons GamepadButtons);
        void OnGamepadButtonsReleased(Buttons GamepadButtons);
    }
}
