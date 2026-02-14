using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace canjewelry.src.utils
{
    public sealed class RestrictionData
    {
        public string[] CollectibleTypes { get; set; }
        public string[] CollectibleCodes { get; set; }
        public string[] BlacklistedCodes { get; set; }
        public Dictionary<string, string[]> GroupingCodes { get; set; }
    }
}
