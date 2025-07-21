using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace canjewelry.src.gui
{
    public class GuiElementPlayerBuffCell : GuiElementTextBase, IGuiElementCell, IDisposable
    {
        public PlayerBuffCellElement cell;
        public static double unscaledRightBoxWidth = 40.0;
        internal double unscaledSwitchPadding = 4.0;
        internal double unscaledSwitchSize = 25.0;
        private List<GuiElementRichtext> texts;
        private LoadedTexture modcellTexture;
        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            base.ComposeElements(ctx, surface);
        }
        public GuiElementPlayerBuffCell(ICoreClientAPI capi, PlayerBuffCellElement cell, ElementBounds bounds) : base(capi, "", null, bounds)
        {
            this.cell = cell;
            this.texts = new();
            this.Font = CairoFont.WhiteSmallishText();
            modcellTexture = new LoadedTexture(capi);
            var tmpEB = ElementBounds.Fixed(0, 0, 125, 25).WithParent(Bounds);
            texts.Add(new GuiElementRichtext(capi,
                VtmlUtil.Richtextify(capi, cell.BuffName, CairoFont.WhiteMediumText().WithFontSize(20)), tmpEB));
        }
        ElementBounds IGuiElementCell.Bounds => Bounds;
        private void Compose()
        {
            ImageSurface imageSurface = new ImageSurface(Format.Argb32, Bounds.OuterWidthInt, Bounds.OuterHeightInt);
            Context context = new Context(imageSurface);
            double num = GuiElement.scaled(unscaledRightBoxWidth);
            Bounds.CalcWorldBounds();

            double num5 = GuiElement.scaled(unscaledSwitchSize);
            double num6 = GuiElement.scaled(unscaledSwitchPadding);
            EmbossRoundRectangleElement(context, 0.0, 0.0, Bounds.OuterWidth, Bounds.OuterHeight, inverse: false, (int)GuiElement.scaled(4.0), 0);
            textUtil.AutobreakAndDrawMultilineTextAt(context, Font, "he", Bounds.absPaddingX, Bounds.absPaddingY + GuiElement.scaled(10), 55, EnumTextOrientation.Left);
            if (texts != null)
            {
                foreach (var it in texts)
                {
                    it.BeforeCalcBounds();
                    it.Compose();
                }
            }
            generateTexture(imageSurface, ref modcellTexture);
            ComposeElements(context, imageSurface);
            context.Dispose();
            imageSurface.Dispose();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            base.RenderInteractiveElements(deltaTime);
            foreach (var it in texts)
            {
                it.RenderInteractiveElements(deltaTime);
            }
        }
        public void OnRenderInteractiveElements(ICoreClientAPI api, float deltaTime)
        {
            if (modcellTexture.TextureId == 0)
            {
                Compose();
            }
            api.Render.Render2DTexturePremultipliedAlpha(modcellTexture.TextureId, (int)Bounds.absX, (int)Bounds.absY, Bounds.OuterWidthInt, Bounds.OuterHeightInt);
            foreach (var it in texts)
            {
                it.RenderInteractiveElements(deltaTime);
            }
        }

        public override void Dispose()
        {
            base.Dispose();      
            modcellTexture?.Dispose();
        }

        public void OnMouseUpOnElement(MouseEvent args, int elementIndex)
        {         

        }

        public void OnMouseMoveOnElement(MouseEvent args, int elementIndex)
        {
        }

        public void OnMouseDownOnElement(MouseEvent args, int elementIndex)
        {

        }

        public void UpdateCellHeight()
        {
            Bounds.CalcWorldBounds();
            if (Bounds.fixedHeight < 50)
            {
                Bounds.fixedHeight = 50;
            }
        }
    }
}
