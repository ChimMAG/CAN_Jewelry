using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace canjewelry.src.be
{
    public interface IPurposefulStorageContainer
    {
        public ITreeAttribute VariantAttributes { get; set; }
        public bool OnInteract(IPlayer byPlayer, BlockSelection blockSel);
    }
}
