using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace canjewelry.Tests.Stubs
{
    /// <summary>
    /// Minimal Item stub for unit tests.
    /// Allows setting Attributes, Variant and Code directly without game initialization.
    /// </summary>
    public class TestItem : Item
    {
        public TestItem()
        {
            Code = new AssetLocation("canjewelry:testitem");
            Variant = new Vintagestory.API.Util.RelaxedReadOnlyDictionary<string, string>(new Dictionary<string, string>());
            Attributes = new JsonObject(JToken.Parse("{}"));
        }
    }
}
