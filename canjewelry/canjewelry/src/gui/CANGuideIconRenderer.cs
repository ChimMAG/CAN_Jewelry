using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace canjewelry.src.gui
{
    public struct IconRenderRequest
    {
        public float X, Y, Size;
        public int SlotId;
        public IconRenderRequest(float x, float y, float size, int slotId)
        {
            X = x; Y = y; Size = size; SlotId = slotId;
        }
    }

    public class CANGuideIconRenderer : IRenderer
    {
        private readonly ICoreClientAPI _capi;
        private readonly InventoryGeneric _inv;

        public double RenderOrder => 0.99;
        public int RenderRange => 0;

        public List<IconRenderRequest> Requests { get; } = new();

        public CANGuideIconRenderer(ICoreClientAPI capi, InventoryGeneric inv)
        {
            _capi = capi;
            _inv = inv;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            foreach (var r in Requests)
            {
                var slot = _inv[r.SlotId];
                if (slot?.Itemstack == null) continue;
                _capi.Render.RenderItemstackToGui(slot,
                    r.X + r.Size * 0.5f,
                    r.Y + r.Size * 0.5f,
                    100,
                    r.Size * 0.5f,
                    -1,
                    showStackSize: false);
            }
        }

        public void Dispose() { }
    }
}
