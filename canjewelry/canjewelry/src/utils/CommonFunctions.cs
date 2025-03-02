using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.MathTools;

namespace canjewelry.src.utils
{
    public class CommonFunctions
    {
        public static bool tryFindColor(string inColorString, out int resColor)
        {
            Color clr = Color.FromName(inColorString);
            if (!clr.IsKnownColor)
            {
                resColor = Color.White.ToArgb();
                return false;
            }
            resColor = ColorUtil.ReverseColorBytes(clr.ToArgb());
            return true;
        }
    }
}
