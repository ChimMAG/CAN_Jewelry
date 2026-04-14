using canjewelry.src;
using canjewelry.src.eb;
using canjewelry.Tests.Helpers;
using canjewelry.Tests.Stubs;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace canjewelry.Tests.CANGemBuffAffected
{
    /// <summary>
    /// Tests for GetItemStackBuffs.
    /// The method only reads from ItemStack — no entity, no world, no config needed.
    /// We pass null as the entity since the method doesn't use it.
    /// </summary>
    public class GetItemStackBuffsTests
    {
        // GetItemStackBuffs is an instance method but uses no 'this' state,
        // so we create one instance per class and reuse it.
        private readonly canjewelry.src.eb.CANGemBuffAffected _sut = new(null!);

        [Fact]
        public void NullStack_ReturnsEmptyDict()
        {
            var result = _sut.GetItemStackBuffs(null!);

            Assert.Empty(result);
        }

        [Fact]
        public void NoEncrustedAttribute_ReturnsEmptyDict()
        {
            var item  = new TestItem();
            var stack = new ItemStack(item);

            var result = _sut.GetItemStackBuffs(stack);

            Assert.Empty(result);
        }

        [Fact]
        public void SingleSocket_NewFormat_ReturnsCorrectBuff()
        {
            var stack = MakeItemWithSockets(maxSockets: 1);
            AddGemToSocket(stack, socketNumber: 0, new[] { "canspeed" }, new[] { 0.1f });

            var result = _sut.GetItemStackBuffs(stack);

            Assert.Single(result);
            Assert.Equal(0.1f, result["canspeed"]);
        }

        [Fact]
        public void TwoSockets_SameBuff_SumsValues()
        {
            var stack = MakeItemWithSockets(maxSockets: 2);
            AddGemToSocket(stack, 0, new[] { "canspeed" }, new[] { 0.1f });
            AddGemToSocket(stack, 1, new[] { "canspeed" }, new[] { 0.2f });

            var result = _sut.GetItemStackBuffs(stack);

            Assert.Equal(0.3f, result["canspeed"], precision: 5);
        }

        [Fact]
        public void TwoSockets_DifferentBuffs_ReturnsBoth()
        {
            var stack = MakeItemWithSockets(maxSockets: 2);
            AddGemToSocket(stack, 0, new[] { "canspeed" },   new[] { 0.1f });
            AddGemToSocket(stack, 1, new[] { "candamage" }, new[] { 0.05f });

            var result = _sut.GetItemStackBuffs(stack);

            Assert.Equal(2, result.Count);
            Assert.Equal(0.1f,  result["canspeed"]);
            Assert.Equal(0.05f, result["candamage"]);
        }

        [Fact]
        public void Socket_WithCandurabilityBuff_IsSkipped()
        {
            var stack = MakeItemWithSockets(maxSockets: 1);
            AddGemToSocket(stack, 0, new[] { "candurability" }, new[] { 0.5f });

            var result = _sut.GetItemStackBuffs(stack);

            Assert.Empty(result);
        }

        [Fact]
        public void Socket_WithTemporalGraspBuff_IsSkipped()
        {
            var stack = MakeItemWithSockets(maxSockets: 1);
            AddGemToSocket(stack, 0, new[] { "temporalgrasp" }, new[] { 0.3f });

            var result = _sut.GetItemStackBuffs(stack);

            Assert.Empty(result);
        }

        [Fact]
        public void Socket_WithMultipleBuffs_OnlySkipsSpecialOnes()
        {
            var stack = MakeItemWithSockets(maxSockets: 1);
            AddGemToSocket(stack, 0,
                new[] { "candurability", "canspeed", "temporalgrasp" },
                new[] { 0.5f, 0.1f, 0.3f });

            var result = _sut.GetItemStackBuffs(stack);

            Assert.Single(result);
            Assert.Equal(0.1f, result["canspeed"]);
        }

        [Fact]
        public void EmptySocket_NoBuffData_IsSkipped()
        {
            var stack = MakeItemWithSockets(maxSockets: 1);
            // Add socket but no gem (no ENCRUSTABLE_BUFFS_NAMES attribute)
            ItemStackBuilder.AddSocket(stack, socketNumber: 0, socketLevel: 2);

            var result = _sut.GetItemStackBuffs(stack);

            Assert.Empty(result);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static ItemStack MakeItemWithSockets(int maxSockets)
        {
            var tiers = new int[maxSockets];
            Array.Fill(tiers, 2);
            return ItemStackBuilder.CreateSocketableItem(tiers);
        }

        private static void AddGemToSocket(ItemStack stack, int socketNumber,
            string[] buffNames, float[] buffValues)
        {
            // Ensure canencrusted tree exists
            if (!stack.Attributes.HasAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING))
            {
                ITreeAttribute root = new TreeAttribute();
                root.SetInt(CANJWConstants.SOCKET_ADDED_NUMBER, 0);
                stack.Attributes[CANJWConstants.ITEM_ENCRUSTED_STRING] = root;
            }

            var encrusted = stack.Attributes.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);
            ITreeAttribute slot = new TreeAttribute();
            slot.SetInt(CANJWConstants.ADDED_SOCKET_TYPE, 2);
            slot[CANJWConstants.ENCRUSTABLE_BUFFS_NAMES]  = new StringArrayAttribute(buffNames);
            slot[CANJWConstants.ENCRUSTABLE_BUFFS_VALUES] = new FloatArrayAttribute(buffValues);
            encrusted["slot" + socketNumber] = slot;
            encrusted.SetInt(CANJWConstants.SOCKET_ADDED_NUMBER,
                encrusted.GetInt(CANJWConstants.SOCKET_ADDED_NUMBER) + 1);
        }
    }
}
