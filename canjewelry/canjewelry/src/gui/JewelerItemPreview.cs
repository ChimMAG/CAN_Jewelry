using System;
using System.Text;
using OpenTK.Graphics.OpenGL;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.Client.NoObf;

namespace canjewelry.src.gui
{
    public class JewelerItemPreview : IDisposable
    {
        private readonly ICoreClientAPI _capi;
        private readonly ClientMain _game;
        private FrameBufferRef _fbo;
        private readonly InventoryItemRenderer _itemRenderer;
        private readonly DummySlot _dummySlot = new();

        public const int FboSize = 300;
        public float RotationY { get; set; }
        public float RotationX { get; set; }

        public int TextureId => _fbo?.ColorTextureIds[0] ?? -1;

        private string _cachedGemState;

        public JewelerItemPreview(ICoreClientAPI capi)
        {
            _capi = capi;
            _game = (ClientMain)capi.World;
            _itemRenderer = new InventoryItemRenderer(_game);
            CreateFbo();
        }

        private void CreateFbo()
        {
            var attrs = new FramebufferAttrs("canjewelry-jeweler-preview", FboSize, FboSize);
            attrs.Attachments = new FramebufferAttrsAttachment[]
            {
                new()
                {
                    AttachmentType = EnumFramebufferAttachment.ColorAttachment0,
                    Texture = new()
                    {
                        Width = FboSize, Height = FboSize,
                        PixelFormat = EnumTexturePixelFormat.Rgba,
                        PixelInternalFormat = EnumTextureInternalFormat.Rgba16f
                    }
                },
                new()
                {
                    AttachmentType = EnumFramebufferAttachment.DepthAttachment,
                    Texture = new()
                    {
                        Width = FboSize, Height = FboSize,
                        PixelFormat = EnumTexturePixelFormat.DepthComponent,
                        PixelInternalFormat = EnumTextureInternalFormat.DepthComponent32
                    }
                }
            };
            _fbo = _game.Platform.CreateFramebuffer(attrs);
        }

        public void Render(ItemStack stack)
        {
            if (_fbo == null || stack?.Collectible == null) return;

            var transform = stack.Collectible.GuiTransform;
            float prevRotY = transform.Rotation.Y;
            float prevRotX = transform.Rotation.X;
            transform.Rotation.Y = prevRotY + RotationY;
            transform.Rotation.X = prevRotX + RotationX;

            try
            {
                _game.Platform.GlEnableDepthTest();
                _game.Platform.GlDisableCullFace();
                _game.Platform.GlToggleBlend(true);
                _game.Platform.ClearFrameBuffer(_fbo, new float[] { 0, 0, 0, 0 },
                    clearDepthBuffer: true, clearColorBuffers: true);

                GL.Viewport(0, 0, FboSize, FboSize);
                _game.OrthoMode(FboSize, FboSize, true);

                _dummySlot.Itemstack = stack;
                // The item is drawn from its centre, so the size is what its longest side gets.
                // Bulky pieces (armor, coronets) reach past that box once rotated, which clipped
                // their edges against the framebuffer - hence the margin rather than 0.75.
                _itemRenderer.RenderItemstackToGui(_dummySlot,
                    FboSize / 2.0, FboSize / 2.0, 100,
                    FboSize * 0.55f, -1,
                    showStackSize: false);

                _game.PerspectiveMode();
                _game.Platform.LoadFrameBuffer(EnumFrameBuffer.Default);
            }
            catch (Exception e)
            {
                _capi.Logger.Error("[CANJewelerPreview] Render crashed: {0}", e);
                try { _game.PerspectiveMode(); } catch { }
                try { _game.Platform.LoadFrameBuffer(EnumFrameBuffer.Default); } catch { }
            }
            finally
            {
                transform.Rotation.Y = prevRotY;
                transform.Rotation.X = prevRotX;
            }
        }

        public void Dispose()
        {
            _game.Platform.DisposeFrameBuffer(_fbo);
            _itemRenderer.Dispose();
        }
    }
}
