// ============================================================
// UpdateBuffsForSlotTests
//
// These tests are RED by design — UpdateBuffsForSlot does not
// exist yet. They will be uncommented and made green as part of
// шаг 5 of the refactoring plan (CANGemBuffAffected cleanup).
//
// The method signature to be extracted:
//   private void UpdateBuffsForSlot(int key, ItemStack? newItemStack)
//
// When шаг 5 is done:
//   1. Make the method internal (+ InternalsVisibleTo in main .csproj)
//   2. Uncomment all tests below
// ============================================================

/*
using canjewelry.src;
using canjewelry.src.eb;
using canjewelry.Tests.Helpers;
using canjewelry.Tests.Stubs;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace canjewelry.Tests.CANGemBuffAffected
{
    public class UpdateBuffsForSlotTests
    {
        private readonly eb.CANGemBuffAffected _sut = new(null!);

        [Fact]
        public void EmptySlot_NoSavedBuffs_DoesNothing()
        {
            _sut.UpdateBuffsForSlot(0, null);

            Assert.Empty(_sut.savedBuffs);
        }

        [Fact]
        public void NewItemWithBuffs_AppliesAndSavesBuffs()
        {
            var stack = MakeItemWithBuff("canspeed", 0.1f);
            var applied = new Dictionary<string, float>();
            // Note: ApplyBuffFromItemStack is static — hook via subclass or capture via side-effect on savedBuffs

            _sut.UpdateBuffsForSlot(0, stack);

            Assert.True(_sut.savedBuffs.ContainsKey(0));
            Assert.Equal(0.1f, _sut.savedBuffs[0]["canspeed"]);
        }

        [Fact]
        public void RemoveItem_BuffsRemovedFromSavedBuffs()
        {
            var stack = MakeItemWithBuff("canspeed", 0.1f);
            _sut.UpdateBuffsForSlot(0, stack);  // add first
            _sut.UpdateBuffsForSlot(0, null);   // then remove

            Assert.False(_sut.savedBuffs.ContainsKey(0));
        }

        [Fact]
        public void SameBuffs_NoDiff_DoesNotReapply()
        {
            var stack = MakeItemWithBuff("canspeed", 0.1f);
            _sut.UpdateBuffsForSlot(0, stack);
            int callCountBefore = _sut.ApplyCallCount; // needs a test-only counter

            _sut.UpdateBuffsForSlot(0, stack); // same item again

            Assert.Equal(callCountBefore, _sut.ApplyCallCount);
        }

        [Fact]
        public void DifferentBuffs_RemovesOldAppliesNew()
        {
            var stack1 = MakeItemWithBuff("canspeed", 0.1f);
            var stack2 = MakeItemWithBuff("candamage", 0.2f);
            _sut.UpdateBuffsForSlot(0, stack1);

            _sut.UpdateBuffsForSlot(0, stack2);

            Assert.True(_sut.savedBuffs.ContainsKey(0));
            Assert.False(_sut.savedBuffs[0].ContainsKey("canspeed"));
            Assert.Equal(0.2f, _sut.savedBuffs[0]["candamage"]);
        }

        private static ItemStack MakeItemWithBuff(string buffName, float buffValue)
        {
            var tiers = new[] { 2 };
            var stack = ItemStackBuilder.CreateSocketableItem(tiers);
            ITreeAttribute encrusted = new TreeAttribute();
            ITreeAttribute slot = new TreeAttribute();
            slot[CANJWConstants.ENCRUSTABLE_BUFFS_NAMES]  = new StringArrayAttribute(new[] { buffName });
            slot[CANJWConstants.ENCRUSTABLE_BUFFS_VALUES] = new FloatArrayAttribute(new[] { buffValue });
            encrusted["slot0"] = slot;
            encrusted.SetInt(CANJWConstants.SOCKET_ADDED_NUMBER, 1);
            stack.Attributes[CANJWConstants.ITEM_ENCRUSTED_STRING] = encrusted;
            return stack;
        }
    }
}
*/
