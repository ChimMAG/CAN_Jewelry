using ProtoBuf;

namespace canjewelry.src
{
    [ProtoContract]
    public class CANGuideRequestDemoPacket
    {
    }

    [ProtoContract]
    public class CANGuideDemoItemsPacket
    {
        [ProtoMember(1)]
        public int[] Slots;

        [ProtoMember(2)]
        public byte[][] Stacks;
    }
}
