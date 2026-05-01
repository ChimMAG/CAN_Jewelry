using ImGuiNET;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;

namespace canjewelry.src.gui
{
    public class ItemIconAtlas : IDisposable
    {
        private readonly ICoreClientAPI _capi;
        private readonly ClientMain _game;
        private FrameBufferRef _frameBuffer;
        private readonly InventoryItemRenderer _itemRenderer;
        private readonly DummySlot _dummySlot = new();

        public int AtlasSize { get; }
        public int ItemSize { get; }
        public int ItemsPerRow { get; }
        public int TextureId => _frameBuffer?.ColorTextureIds[0] ?? -1;

        private readonly Dictionary<string, int> _itemIndexMap = new();

        public ItemIconAtlas(ICoreClientAPI capi, int atlasSize = 2048, int itemSize = 64)
        {
            _capi = capi;
            _game = (ClientMain)capi.World;
            _itemRenderer = new(_game);
            AtlasSize = atlasSize;
            ItemSize = itemSize;
            ItemsPerRow = atlasSize / itemSize;
            CreateFramebuffer();
        }

        private void CreateFramebuffer()
        {
            var attrs = new FramebufferAttrs("canjewelry-item-atlas", AtlasSize, AtlasSize);
            attrs.Attachments = new FramebufferAttrsAttachment[]
            {
                new()
                {
                    AttachmentType = EnumFramebufferAttachment.ColorAttachment0,
                    Texture = new()
                    {
                        Width = AtlasSize, Height = AtlasSize,
                        PixelFormat = EnumTexturePixelFormat.Rgba,
                        PixelInternalFormat = EnumTextureInternalFormat.Rgba16f
                    }
                },
                new()
                {
                    AttachmentType = EnumFramebufferAttachment.DepthAttachment,
                    Texture = new()
                    {
                        Width = AtlasSize, Height = AtlasSize,
                        PixelFormat = EnumTexturePixelFormat.DepthComponent,
                        PixelInternalFormat = EnumTextureInternalFormat.DepthComponent32
                    }
                }
            };
            _frameBuffer = _game.Platform.CreateFramebuffer(attrs);
        }

        public void Build(IEnumerable<ItemStack> stacks)
        {
            if (_frameBuffer == null) return;

            var newStacks = new List<ItemStack>();
            foreach (var stack in stacks)
            {
                if (stack == null || stack.Collectible == null) continue;
                if (!_itemIndexMap.ContainsKey(GetKey(stack)))
                    newStacks.Add(stack);
            }
            if (newStacks.Count == 0) return;

            _capi.Logger.VerboseDebug("[CANGuide atlas] Build: rendering {0} new stack(s), TextureId={1}, FBO={2}",
                newStacks.Count, TextureId, _frameBuffer != null);

            try
            {
                _game.Platform.GlEnableDepthTest();
                _game.Platform.GlDisableCullFace();
                _game.Platform.GlToggleBlend(true);
                _game.Platform.ClearFrameBuffer(_frameBuffer, new float[] { 0, 0, 0, 0 },
                    clearDepthBuffer: true, clearColorBuffers: _itemIndexMap.Count == 0);

                GL.Viewport(0, 0, AtlasSize, AtlasSize);
                _game.OrthoMode(AtlasSize, AtlasSize, true);

                int nextIndex = _itemIndexMap.Count;
                for (int i = 0; i < newStacks.Count; i++)
                {
                    int idx = nextIndex + i;
                    var stack = newStacks[i];
                    int col = idx % ItemsPerRow;
                    int row = idx / ItemsPerRow;
                    float x = col * ItemSize;
                    float y = row * ItemSize;
                    if (y >= AtlasSize) break;

                    try
                    {
                        _game.Platform.GlScissorFlag(true);
                        _game.Platform.GlScissor((int)x, (int)y, ItemSize, ItemSize);
                        _dummySlot.Itemstack = stack;
                        var prev = stack.StackSize;
                        stack.StackSize = 1;
                        _itemRenderer.RenderItemstackToGui(_dummySlot,
                            x + ItemSize / 2.0, y + ItemSize / 2.0, 100, ItemSize / 2.0f, -1,
                            showStackSize: true);
                        stack.StackSize = prev;
                        _game.Platform.GlScissorFlag(false);
                        _itemIndexMap[GetKey(stack)] = idx;
                        _capi.Logger.VerboseDebug("[CANGuide atlas]   ok: '{0}' -> idx={1} ({2},{3})",
                            GetKey(stack), idx, (int)x, (int)y);
                    }
                    catch (Exception itemEx)
                    {
                        _game.Platform.GlScissorFlag(false);
                        _capi.Logger.Error("[CANGuide atlas] failed to render item '{0}': {1}",
                            stack?.Collectible?.Code, itemEx);
                        // do not cache — leave slot empty so a future Build can retry
                    }
                }

                _game.PerspectiveMode();
                _game.Platform.LoadFrameBuffer(EnumFrameBuffer.Default);
            }
            catch (Exception e)
            {
                _capi.Logger.Error("[CANGuide atlas] Build() crashed: {0}", e);
                try { _game.Platform.GlScissorFlag(false); } catch { }
                try { _game.PerspectiveMode(); } catch { }
                try { _game.Platform.LoadFrameBuffer(EnumFrameBuffer.Default); } catch { }
            }
        }

        public void Draw(ItemStack stack, Vector2 size, string tooltip = null)
        {
            if (stack?.Collectible == null) return;
            string key = GetKey(stack);
            if (_itemIndexMap.TryGetValue(key, out int index))
            {
                GetUv(index, out var uv0, out var uv1);
                ImGui.Image((IntPtr)TextureId, size, uv0, uv1);
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(tooltip ?? stack.GetName());
                    ImGui.EndTooltip();
                }
            }
            else
            {
                Build(new[] { stack });
                ImGui.Text("?");
            }
        }

        public void DrawToList(ItemStack stack, Vector2 pos, Vector2 size, ImDrawListPtr dl, string tooltip = null)
        {
            if (stack?.Collectible == null) return;
            string key = GetKey(stack);
            if (!_itemIndexMap.TryGetValue(key, out int index))
            {
                _capi.Logger.VerboseDebug("[CANGuide atlas] DrawToList building '{0}' (cache miss)", key);
                Build(new[] { stack });
                if (!_itemIndexMap.TryGetValue(key, out index))
                {
                    _capi.Logger.Warning("[CANGuide atlas] '{0}' not in map after Build; skipping draw", key);
                    return;
                }
            }
            int tex = TextureId;
            if (tex <= 0)
            {
                _capi.Logger.Warning("[CANGuide atlas] invalid TextureId={0} when drawing '{1}'", tex, key);
                return;
            }
            GetUv(index, out var uv0, out var uv1);
            dl.AddImage((IntPtr)tex, pos, pos + size, uv0, uv1);
        }

        private string GetKey(ItemStack stack) => stack.Collectible.Code.ToString();

        private void GetUv(int index, out Vector2 uv0, out Vector2 uv1)
        {
            int col = index % ItemsPerRow;
            int row = index / ItemsPerRow;
            float uStep = (float)ItemSize / AtlasSize;
            float vStep = (float)ItemSize / AtlasSize;
            uv0 = new(col * uStep, row * vStep);
            uv1 = new(uv0.X + uStep, uv0.Y + vStep);
        }

        public void Dispose()
        {
            _game.Platform.DisposeFrameBuffer(_frameBuffer);
            _itemRenderer.Dispose();
        }
    }
}
