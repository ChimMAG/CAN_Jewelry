using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace canjewelry.src.items.interfaces
{
    public interface ITrasparentTextureUser
    {
        public static AssetLocation NotVisTexture = new AssetLocation("canjewelry:item/gem/notvis.png");
        static AssetLocation GetNotVisTexture()
        {
            return NotVisTexture;
        }
    }
}
