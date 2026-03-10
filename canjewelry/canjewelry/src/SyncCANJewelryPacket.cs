using ProtoBuf;

namespace canjewelry.src
{
    [ProtoContract]
    public class SyncCANJewelryPacket
    {
        [ProtoMember(1)]
        public string CompressedConfig;
    }
}
