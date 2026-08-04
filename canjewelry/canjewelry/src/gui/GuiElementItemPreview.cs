using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace canjewelry.src.gui
{
    // Draws the rotatable 3D item preview inside a dialog. The rendering itself is done by
    // JewelerItemPreview, which renders the stack into its own framebuffer and hands out a
    // texture id - painting that texture is all this element does.
    //
    // The render pass is deliberately NOT started here: switching framebuffers in the middle of
    // the gui pass would disturb it. The owning dialog calls JewelerItemPreview.Render before
    // composing, and this element only shows the result of that.
    public class GuiElementItemPreview : GuiElement
    {
        private readonly JewelerItemPreview preview;
        private bool dragging;
        private int lastMouseX;
        private int lastMouseY;

        public GuiElementItemPreview(ICoreClientAPI capi, ElementBounds bounds, JewelerItemPreview preview)
            : base(capi, bounds)
        {
            this.preview = preview;
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (preview == null || preview.TextureId <= 0) return;

            api.Render.Render2DTexture(preview.TextureId,
                (float)Bounds.renderX, (float)Bounds.renderY,
                (float)Bounds.InnerWidth, (float)Bounds.InnerHeight);
        }

        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            base.OnMouseDownOnElement(api, args);
            dragging = true;
            lastMouseX = args.X;
            lastMouseY = args.Y;
        }

        public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
        {
            base.OnMouseMove(api, args);
            if (!dragging || preview == null) return;

            // Horizontal drag spins the piece, vertical tilts it.
            preview.RotationY -= (args.X - lastMouseX) * 0.5f;
            preview.RotationX -= (args.Y - lastMouseY) * 0.5f;
            lastMouseX = args.X;
            lastMouseY = args.Y;
        }

        public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
        {
            base.OnMouseUp(api, args);
            dragging = false;
        }
    }
}
