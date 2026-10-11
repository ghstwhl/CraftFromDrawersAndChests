using System;
using System.Collections.Generic;
using CraftFromDrawersAndChests.Logic;
using NUnit.Framework;

namespace CraftFromDrawersAndChests.Tests.Logic
{
    /// <summary>
    /// The superset invariant that keeps a craft or a placement from ever costing nothing (Task 14b
    /// fix round 2, R28). A pull plans <c>GetAmount - held</c>, so the safety chain
    /// <i>vanilla returned true ⇒ our shortfall is 0 ⇒ nothing is requested ⇒ the consume prefix
    /// cannot decline</i> holds only while the amount this mod counts as <b>held</b> is at least the
    /// amount vanilla counts. Fix round 1 made the source half count by
    /// <see cref="DrawerTake.MatchingAmount"/> (name + quality + world level), which is a
    /// <b>subset</b> of what vanilla consumes: a same-name stack at a different quality or world level
    /// counted zero, inflated the shortfall to the whole requirement, and the game then created the
    /// item with nothing to eat. This fixture proves the relationship instead of asserting it — over
    /// generated inventories it pins, for every identity vanilla could use,
    /// <c>DrawerTake.HeldAmount ≥ that count</c>.
    /// <para>
    /// The inventory model is the project's own pure one, <see cref="DrawerTake.InventoryStack"/>, so
    /// the property runs without the game. <c>PullEngine.HeldCounts</c> (game-facing) feeds the very
    /// same <see cref="DrawerTake.HeldAmount"/> the requirement's shared name, so this is the
    /// production function being pinned, not a copy of it.
    /// </para>
    /// </summary>
    [TestFixture]
    public class HeldAmountTests
    {
        /// <summary>The shared names the generator draws from.</summary>
        private static readonly string[] Names = { "$item_wood", "$item_stone", "$item_iron" };

        private static DrawerTake.InventoryStack Stack(string name, int quality, int worldLevel, int stack, int maxStack)
        {
            return new DrawerTake.InventoryStack(name, quality, worldLevel, stack, maxStack);
        }

        /// <summary>
        /// A plausible inventory: zero to six stacks of the three materials, qualities 0-4, world
        /// levels 0-2, occasionally an unnamed stack, and stack sizes that are mostly a full material
        /// stack (20) or a double one (30) but sometimes one (a non-stackable). Partial stacks, full
        /// stacks and empty stacks all occur.
        /// </summary>
        private static List<DrawerTake.InventoryStack> RandomInventory(Random rng)
        {
            int count = rng.Next(0, 7);
            var stacks = new List<DrawerTake.InventoryStack>(count);
            for (int i = 0; i < count; i++)
            {
                string name = rng.Next(0, 8) == 0 ? null : Names[rng.Next(Names.Length)];
                int quality = rng.Next(0, 5);
                int worldLevel = rng.Next(0, 3);
                int maxStack = rng.Next(0, 5) == 0 ? 1 : 20 + rng.Next(0, 2) * 10;
                int stack = rng.Next(0, maxStack + 1);
                stacks.Add(new DrawerTake.InventoryStack(name, quality, worldLevel, stack, maxStack));
            }
            return stacks;
        }

        /// <summary>
        /// The pure model of vanilla's <c>Inventory.CountItems(string name, int quality, bool
        /// matchWorldLevel)</c> — the count vanilla's requirement test and consume path decide on. A
        /// <paramref name="quality"/> of <c>-1</c> means "any quality", the value vanilla's own
        /// requirement test passes; <paramref name="matchWorldLevel"/> false means "any world level".
        /// Only stacks whose shared name matches by ordinal are ever counted, which is the identity
        /// vanilla's <c>Inventory.RemoveItem(String, Int32, Int32, Boolean)</c> consumes by.
        /// </summary>
        private static int VanillaCountItems(
            string name,
            int quality,
            bool matchWorldLevel,
            int worldLevel,
            IReadOnlyList<DrawerTake.InventoryStack> stacks)
        {
            int total = 0;
            for (int i = 0; i < stacks.Count; i++)
            {
                DrawerTake.InventoryStack stack = stacks[i];
                if (!string.Equals(stack.Name, name, StringComparison.Ordinal))
                    continue;
                if (quality != -1 && stack.Quality != quality)
                    continue;
                if (matchWorldLevel && stack.WorldLevel != worldLevel)
                    continue;

                total += stack.Stack;
            }
            return total;
        }

        [Test]
        public void HeldAmount_IsNeverBelowVanillasCount_ForAnyIdentityVanillaCouldUse()
        {
            var rng = new Random(20261011);

            for (int iteration = 0; iteration < 3000; iteration++)
            {
                List<DrawerTake.InventoryStack> inventory = RandomInventory(rng);
                string name = Names[rng.Next(Names.Length)];
                int quality = rng.Next(0, 5);
                int worldLevel = rng.Next(0, 3);
                int needed = rng.Next(0, 40);

                int held = DrawerTake.HeldAmount(name, inventory);

                // Every count vanilla could be making: quality -1 ("any", what its requirement test
                // passes) or the requirement's own, with the world level ignored or pinned.
                foreach (int vanillaQuality in new[] { -1, quality })
                {
                    foreach (bool matchWorldLevel in new[] { false, true })
                    {
                        int vanillaHeld = VanillaCountItems(
                            name, vanillaQuality, matchWorldLevel, worldLevel, inventory);

                        Assert.That(
                            held, Is.GreaterThanOrEqualTo(vanillaHeld),
                            "held=" + held + " vanilla=" + vanillaHeld + " name=" + name +
                            " q=" + vanillaQuality + " wl=" + worldLevel + " match=" + matchWorldLevel +
                            " at iteration " + iteration);

                        // The chain the invariant exists for: if vanilla believes the player has
                        // enough, our shortfall is zero, so nothing is requested and the consume prefix
                        // has no reason to decline a path vanilla had already cleared.
                        if (vanillaHeld >= needed)
                        {
                            Assert.That(
                                PullMath.Shortfall(needed, held), Is.EqualTo(0),
                                "needed=" + needed + " held=" + held + " vanilla=" + vanillaHeld +
                                " at iteration " + iteration);
                        }
                    }
                }
            }
        }

        [Test]
        public void HeldAmount_DominatesTheStrictThreePartCount_ThatWasTheRegression()
        {
            // Fix round 1's source half. MatchingAmount is a SUBSET of what vanilla consumes (it also
            // requires the quality and world level to be equal), so a held amount below it is exactly
            // the free-craft condition. Pinning the domination pins the regression for every generated
            // state, not just the hand-written one below.
            var rng = new Random(20261012);

            for (int iteration = 0; iteration < 3000; iteration++)
            {
                List<DrawerTake.InventoryStack> inventory = RandomInventory(rng);
                string name = Names[rng.Next(Names.Length)];
                int quality = rng.Next(0, 5);
                int worldLevel = rng.Next(0, 3);

                Assert.That(
                    DrawerTake.HeldAmount(name, inventory),
                    Is.GreaterThanOrEqualTo(DrawerTake.MatchingAmount(name, quality, worldLevel, inventory)),
                    "name=" + name + " q=" + quality + " wl=" + worldLevel + " at iteration " + iteration);
            }
        }

        [Test]
        public void HeldAmount_CountsASameNameStackAtADifferentQualityAndWorldLevel()
        {
            // The exact free-craft case the review found: the requirement names $item_wood at quality
            // 1 / world level 0 and the player holds it at quality 2 / world level 1. Vanilla consumes
            // it (RemoveItem is keyed by the shared name), so it must count as held — anything else
            // inflates the shortfall to the whole requirement.
            var inventory = new[]
            {
                Stack("$item_wood", 2, 1, 7, 20),
                Stack("$item_wood", 1, 0, 5, 20),
                Stack("$item_stone", 1, 0, 9, 20),
            };

            Assert.That(DrawerTake.HeldAmount("$item_wood", inventory), Is.EqualTo(12));
            // ... where the strict count sees only the exactly-matching stack.
            Assert.That(DrawerTake.MatchingAmount("$item_wood", 1, 0, inventory), Is.EqualTo(5));
        }

        [Test]
        public void HeldAmount_IncludesFullAndEmptyStacks()
        {
            // A full stack is still held, and an empty one contributes nothing without breaking the sum.
            var inventory = new[]
            {
                Stack("$item_wood", 1, 0, 20, 20),
                Stack("$item_wood", 3, 2, 0, 20),
            };

            Assert.That(DrawerTake.HeldAmount("$item_wood", inventory), Is.EqualTo(20));
        }

        [Test]
        public void HeldAmount_MatchesTheNameByOrdinal()
        {
            var inventory = new[] { Stack("$item_wood", 1, 0, 5, 20) };

            Assert.That(DrawerTake.HeldAmount("$ITEM_WOOD", inventory), Is.EqualTo(0));
            Assert.That(DrawerTake.HeldAmount("$item_wood", inventory), Is.EqualTo(5));
        }

        [Test]
        public void HeldAmount_NoInventoryOrNoName_IsZero()
        {
            Assert.That(DrawerTake.HeldAmount("$item_wood", null), Is.EqualTo(0));
            Assert.That(DrawerTake.HeldAmount(null, new DrawerTake.InventoryStack[0]), Is.EqualTo(0));
            Assert.That(DrawerTake.HeldAmount(string.Empty, new DrawerTake.InventoryStack[0]), Is.EqualTo(0));
        }
    }
}
