using canjewelry.src;
using canjewelry.src.CB;
using canjewelry.Tests.Stubs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace canjewelry.Tests.EncrustableCB
{
    public class GetSocketsTiersTests
    {
        [Fact]
        public void NullItemStack_ReturnsEmptyArray()
        {
            var result = src.CB.EncrustableCB.GetSocketsTiers(null);

            Assert.Empty(result);
        }

        [Fact]
        public void NoRelevantAttributes_ReturnsEmptyArray()
        {
            var item = new TestItem(); // no canhavesocketsnumber, no cancustomvariants
            var stack = new ItemStack(item);

            var result = src.CB.EncrustableCB.GetSocketsTiers(stack);

            Assert.Empty(result);
        }

        [Fact]
        public void HasTiersAttribute_ReturnsTiers()
        {
            var item = new TestItem();
            item.Attributes = new JsonObject(JToken.Parse(@"{
                ""canhavesocketsnumber"": 2,
                ""cansocketstiers"": [2, 3]
            }"));
            var stack = new ItemStack(item);

            var result = src.CB.EncrustableCB.GetSocketsTiers(stack);

            Assert.Equal(new[] { 2, 3 }, result);
        }

        [Fact]
        public void HasCustomVariants_KeyFound_ReturnsCorrectTiers()
        {
            var socketTiers = new Dictionary<string, int[]>
            {
                { "steel", new[] { 3, 3, 3 } },
                { "iron",  new[] { 2, 2 } }
            };
            var item = new TestItem();
            item.Attributes = new JsonObject(JToken.Parse($@"{{
                ""{CANJWConstants.CAN_CUSTOM_VARIANTS}"": {JsonConvert.SerializeObject(socketTiers)},
                ""{CANJWConstants.CAN_CUSTOM_VARIANTS_COMPARE_KEY}"": ""material""
            }}"));
            var stack = new ItemStack(item);
            stack.Attributes.SetString("material", "steel");

            var result = src.CB.EncrustableCB.GetSocketsTiers(stack);

            Assert.Equal(new[] { 3, 3, 3 }, result);
        }

        [Fact]
        public void HasCustomVariants_KeyNotInDict_ReturnsEmptyArray()
        {
            var socketTiers = new Dictionary<string, int[]>
            {
                { "steel", new[] { 3, 3, 3 } }
            };
            var item = new TestItem();
            item.Attributes = new JsonObject(JToken.Parse($@"{{
                ""{CANJWConstants.CAN_CUSTOM_VARIANTS}"": {JsonConvert.SerializeObject(socketTiers)},
                ""{CANJWConstants.CAN_CUSTOM_VARIANTS_COMPARE_KEY}"": ""material""
            }}"));
            var stack = new ItemStack(item);
            stack.Attributes.SetString("material", "copper"); // not in dict

            var result = src.CB.EncrustableCB.GetSocketsTiers(stack);

            Assert.Empty(result);
        }

        [Fact]
        public void HasCustomVariants_RuntimeKeyMissing_ReturnsEmptyArray()
        {
            // The compare key exists in item attributes but the runtime attribute value is not set
            var socketTiers = new Dictionary<string, int[]>
            {
                { "steel", new[] { 3 } }
            };
            var item = new TestItem();
            item.Attributes = new JsonObject(JToken.Parse($@"{{
                ""{CANJWConstants.CAN_CUSTOM_VARIANTS}"": {JsonConvert.SerializeObject(socketTiers)},
                ""{CANJWConstants.CAN_CUSTOM_VARIANTS_COMPARE_KEY}"": ""material""
            }}"));
            var stack = new ItemStack(item);
            // stack.Attributes has no "material" key → GetString returns null

            var result = src.CB.EncrustableCB.GetSocketsTiers(stack);

            Assert.Empty(result);
        }
    }
}
