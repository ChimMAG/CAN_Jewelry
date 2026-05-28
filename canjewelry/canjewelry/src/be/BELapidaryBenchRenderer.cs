using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace canjewelry.src.be
{
    // In-world display + dop visualization renderer for BELapidaryBench.
    //
    // Renders two things each frame:
    //   1. A text quad floating above the block — Cairo-baked, shows stage,
    //      progress, target / current angle and index.
    //   2. The dop assembly mesh (tesselated from the dop item) — anchored to
    //      the bench's dop arm zone, rotated around Y by SelectedIndex and
    //      tilted around X by CurrentAngle. Only drawn when an assembly is in
    //      the inventory so the player gets immediate visual feedback for
    //      angle/index buttons.
    public class BELapidaryBenchRenderer : IRenderer, IDisposable
    {
        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        private readonly ICoreClientAPI capi;
        private readonly BlockPos pos;
        private readonly BELapidaryBench be;
        private readonly Matrixf modelMat = new Matrixf();

        // ----- Text display -----
        private MeshRef quadRef;
        private LoadedTexture texture;
        private string currentText = "";
        private bool textDirty = true;

        private const float TextTranslateX = 0.5f;
        private const float TextTranslateY = 1.25f;
        private const float TextTranslateZ = 0.5f;
        private const float TextQuadW = 1.1f;
        private const float TextQuadH = 0.45f;

        // ----- Dop arm visualization — tunable knobs (static so the debug
        // GUI can adjust them at runtime; bake winning values back into code
        // afterwards). -----

        // Spindle = anchor where the arm rotates (block-local 0..1 coords).
        public static float SpindleX = 0.500f;
        public static float SpindleY = 0.550f;
        public static float SpindleZ = 0.150f;

        // Arm-end offset relative to spindle BEFORE yaw rotation.
        public static float ArmOffsetX = 0.000f;
        public static float ArmOffsetY = 0.000f;
        public static float ArmOffsetZ = 0.350f;

        // Dop mesh tuning (applied at arm-end after tilt rotation).
        public static float DopScale = 0.470f;
        public static float DopOriginX = -0.940f;
        public static float DopOriginY = 0.420f;
        public static float DopOriginZ = -0.710f;

        // Gem mesh tuning.
        public static float GemScale = 0.350f;
        public static float GemOriginX = -0.500f;
        public static float GemOriginY = -0.790f;
        public static float GemOriginZ = -0.500f;

        // Multiplier on yaw input (0 = freeze, negative = reverse).
        public static float YawMul = 0.010f;

        // Per-axis tilt multipliers. Each is INDEPENDENT — controls how much
        // the dop rotates around X/Y/Z per degree of input angle. Three
        // separate Euler rotations applied in order X → Y → Z.
        //   (1, 0, 0) = pure X-axis tilt (original)
        //   (0, 1, 0) = pure Y-axis tilt
        //   etc.
        public static float TiltMulX = 1f;
        public static float TiltMulY = 0f;
        public static float TiltMulZ = 0f;

        // Debug: draw a small marker cube at the tilt pivot point in world.
        public static bool ShowTiltPivot = false;
        public static float PivotMarkerSize = 0.06f;

        // Debug-only: when non-zero, OVERRIDES the gameplay angle/index so you
        // can preview rotations without changing actual bench state. Set back
        // to 0 / -1 to use live values.
        public static float DebugTiltDeg = 0f;       // override tilt in degrees
        public static int DebugIndexOverride = -1;   // override index; -1 = use live

        // Pivot point for the dynamic TILT rotation, in arm-end-local space.
        // Default (0,0,0) = pivot at the arm-end. If the dop is visually
        // offset from the arm-end via DopOrigin, set this to the location of
        // the arm-attachment point on the dop so tilt rotates the dop around
        // that point (the base of the dop stays put).
        // TiltPivot is now in MESH-LOCAL space — (0,0,0) = pivot at the
        // shape's own mesh origin (typical voxel-space (0,0,0)).
        public static float TiltPivotX = 0f;
        public static float TiltPivotY = 0f;
        public static float TiltPivotZ = 0f;

        // Static rotations applied AFTER the dynamic yaw+tilt, in the dop's
        // local frame. Use these to re-orient the chisel mesh (which may be
        // baked in an unexpected pose). Degrees.
        public static float DopRotX = 0f;
        public static float DopRotY = 0f;
        public static float DopRotZ = 0f;

        public static float GemRotX = 0f;
        public static float GemRotY = -25f;
        public static float GemRotZ = 0f;

        private MeshRef dopMeshRef;
        private MeshRef gemMeshRef;
        private MeshRef pivotMarkerRef;

        public BELapidaryBenchRenderer(ICoreClientAPI api, BlockPos pos, BELapidaryBench be)
        {
            this.capi = api;
            this.pos = pos;
            this.be = be;

            // Text quad
            MeshData md = QuadMeshUtil.GetQuad();
            md.Uv = new float[] { 1f, 1f, 0f, 1f, 0f, 0f, 1f, 0f };
            md.Rgba = new byte[16];
            md.Rgba.Fill(byte.MaxValue);
            quadRef = api.Render.UploadMesh(md);

            // Dop item mesh — tesselate the dop item once. UVs are baked
            // against the item texture atlas (so we bind that atlas at draw).
            // Tesselate from the gem-on-dop item itself so any custom shape
            // (e.g. item/candop with attached gem) is used directly.
            var dopItem = api.World.GetItem(new AssetLocation("canjewelry:gem-on-dop"));
            if (dopItem != null)
            {
                api.Tesselator.TesselateItem(dopItem, out MeshData dopMesh);
                if (dopMesh != null) dopMeshRef = api.Render.UploadMesh(dopMesh);

                // Auto-seed TiltPivot from the shape's first element's
                // rotationOrigin (voxel coords 0..16 → mesh coords 0..1).
                TryAutoSetTiltPivot(api, dopItem);
            }

            // Placeholder gem visual — rough diamond. Replaceable once a real
            // "preform / faceting blank" shape exists.
            var gemItem = api.World.GetItem(new AssetLocation("canjewelry:gem-rough-normal-diamond"));
            if (gemItem != null)
            {
                api.Tesselator.TesselateItem(gemItem, out MeshData gemMesh);
                if (gemMesh != null) gemMeshRef = api.Render.UploadMesh(gemMesh);
            }

            // Debug pivot marker — tiny bright-red cube. Uses block atlas UV
            // arbitrarily; only visible when ShowTiltPivot toggled.
            MeshData markerMesh = CubeMeshUtil.GetCube(0.025f, 0.025f, 0.025f, new Vec3f(0, 0, 0));
            for (int i = 0; i < markerMesh.Rgba.Length; i += 4)
            {
                markerMesh.Rgba[i]     = 255; // R
                markerMesh.Rgba[i + 1] = 30;  // G
                markerMesh.Rgba[i + 2] = 30;  // B
                markerMesh.Rgba[i + 3] = 255; // A
            }
            pivotMarkerRef = api.Render.UploadMesh(markerMesh);

            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "canlapidarydisplay");
        }

        public void SetText(string text)
        {
            if (text == currentText) return;
            currentText = text ?? "";
            textDirty = true;
        }

        private void RebuildTexture()
        {
            texture?.Dispose();
            texture = null;
            if (string.IsNullOrEmpty(currentText)) return;

            var font = new CairoFont(40, "Arial", new double[] { 1, 1, 1, 1 });
            font.WithStroke(new double[] { 0, 0, 0, 1 }, 2);
            texture = capi.Gui.TextTexture.GenTextTexture(
                currentText, font, 512, 192,
                new TextBackground { FillColor = new double[] { 0, 0, 0, 0.65 }, Padding = 6 },
                EnumTextOrientation.Center, false);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (textDirty)
            {
                RebuildTexture();
                textDirty = false;
            }

            if (!capi.Render.DefaultFrustumCuller.SphereInFrustum(
                pos.X + 0.5, pos.InternalY + 0.5, pos.Z + 0.5, 1.0)) return;

            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;
            Vec4f light = capi.World.BlockAccessor.GetLightRGBs(pos);

            // --- Text quad (transparent overlay) ---
            if (texture != null)
            {
                rpi.GlToggleBlend(true, EnumBlendMode.PremultipliedAlpha);
                rpi.GlDisableCullFace();

                var tprog = rpi.PreparedStandardShader(pos.X, pos.InternalY, pos.Z);
                tprog.ViewMatrix = rpi.CameraMatrixOriginf;
                tprog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                tprog.NormalShaded = 0;
                tprog.ExtraGodray = 0f;
                tprog.SsaoAttn = 0f;
                tprog.AlphaTest = 0.05f;
                tprog.OverlayOpacity = 0f;
                tprog.RgbaLightIn = light;
                tprog.Tex2D = texture.TextureId;

                tprog.ModelMatrix = modelMat.Identity()
                    .Translate(pos.X - camPos.X + TextTranslateX,
                               pos.InternalY - camPos.Y + TextTranslateY,
                               pos.Z - camPos.Z + TextTranslateZ)
                    .Scale(0.5f * TextQuadW, 0.5f * TextQuadH, 0.5f * TextQuadW)
                    .Values;

                rpi.RenderMesh(quadRef);
                tprog.Stop();

                rpi.GlEnableCullFace();
                rpi.GlToggleBlend(true, EnumBlendMode.Standard);
            }

            // --- Dop+gem visualization (only when assembly present) ---
            if (dopMeshRef != null && be?.AssemblyStack != null)
            {
                int res = be.GetRecipe()?.IndexResolution ?? 16;
                int idx = DebugIndexOverride >= 0 ? DebugIndexOverride : be.SelectedIndex;
                bool useDebugTilt = Math.Abs(DebugTiltDeg) > 0.001f;
                float tiltDeg = useDebugTilt ? DebugTiltDeg : be.CurrentAngle;
                float yawRad = YawMul * idx * (2f * (float)Math.PI / res);
                float tiltRadBase = tiltDeg * (float)Math.PI / 180f;

                rpi.GlToggleBlend(true, EnumBlendMode.Standard);
                var prog = rpi.PreparedStandardShader(pos.X, pos.InternalY, pos.Z);
                prog.Tex2D = capi.ItemTextureAtlas.AtlasTextures[0].TextureId;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                prog.NormalShaded = 1;
                prog.RgbaLightIn = light;

                // Real-lapidary arm: yaw orbits around the spindle (back of
                // bench), the arm extends forward, the dop hangs from the
                // arm's end and tilts toward the lap.
                //
                // Vertex flow (last-in-code applies first to vertex):
                //   T_origin → Scale → RotX(tilt) → T_armReach → RotY(yaw) → T_spindle
                const float deg2rad = (float)Math.PI / 180f;

                // Tilt pivots in arm-end-local space at TiltPivot. Applied
                // OUTSIDE the per-mesh positioning so dop and gem rotate as
                // a rigid pair (shared world pivot). Set TiltPivot to where
                // the dop's attachment-end appears in arm-end frame.

                // Tilt now pivots in MESH-LOCAL space (inside the dop's
                // positioning chain). TiltPivot=(0,0,0) → pivot at the shape's
                // own mesh origin. Set TiltPivot to a different mesh-space
                // point to shift the pivot elsewhere within the mesh.
                prog.ModelMatrix = modelMat.Identity()
                    .Translate(pos.X - camPos.X + SpindleX,
                               pos.InternalY - camPos.Y + SpindleY,
                               pos.Z - camPos.Z + SpindleZ)
                    .RotateY(yawRad)
                    .Translate(ArmOffsetX, ArmOffsetY, ArmOffsetZ)
                    .RotateY(DopRotY * deg2rad)
                    .RotateX(DopRotX * deg2rad)
                    .RotateZ(DopRotZ * deg2rad)
                    .Scale(DopScale, DopScale, DopScale)
                    .Translate(DopOriginX, DopOriginY, DopOriginZ)
                    .Translate(TiltPivotX, TiltPivotY, TiltPivotZ)
                    .RotateZ(-tiltRadBase * TiltMulX)
                    .Translate(-TiltPivotX, -TiltPivotY, -TiltPivotZ)
                    .Values;

                rpi.RenderMesh(dopMeshRef);

                if (gemMeshRef != null)
                {
                    prog.ModelMatrix = modelMat.Identity()
                        .Translate(pos.X - camPos.X + SpindleX,
                                   pos.InternalY - camPos.Y + SpindleY,
                                   pos.Z - camPos.Z + SpindleZ)
                        .RotateY(yawRad)
                        .Translate(ArmOffsetX, ArmOffsetY, ArmOffsetZ)
                        .RotateY(GemRotY * deg2rad)
                        .RotateX(GemRotX * deg2rad)
                        .RotateZ(GemRotZ * deg2rad)
                        .Scale(GemScale, GemScale, GemScale)
                        .Translate(GemOriginX, GemOriginY, GemOriginZ)
                        .Translate(TiltPivotX, TiltPivotY, TiltPivotZ)
                        .RotateZ(-tiltRadBase * TiltMulX)
                        .Translate(-TiltPivotX, -TiltPivotY, -TiltPivotZ)
                        .Values;
                    rpi.RenderMesh(gemMeshRef);
                }

                // Debug pivot marker — sits at the tilt-pivot world position
                // (arm-end + TiltPivot, after yaw). Renders only when toggled.
                if (ShowTiltPivot && pivotMarkerRef != null)
                {
                    // Marker follows the same positioning chain as the dop,
                    // landing at the mesh-space TiltPivot. Compensate marker
                    // size for the dop scale so the cube stays a stable size.
                    float s = PivotMarkerSize / 0.025f / DopScale;
                    prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;
                    prog.ModelMatrix = modelMat.Identity()
                        .Translate(pos.X - camPos.X + SpindleX,
                                   pos.InternalY - camPos.Y + SpindleY,
                                   pos.Z - camPos.Z + SpindleZ)
                        .RotateY(yawRad)
                        .Translate(ArmOffsetX, ArmOffsetY, ArmOffsetZ)
                        .RotateY(DopRotY * deg2rad)
                        .RotateX(DopRotX * deg2rad)
                        .RotateZ(DopRotZ * deg2rad)
                        .Scale(DopScale, DopScale, DopScale)
                        .Translate(DopOriginX, DopOriginY, DopOriginZ)
                        .Translate(TiltPivotX, TiltPivotY, TiltPivotZ)
                        .Scale(s, s, s)
                        .Values;
                    rpi.RenderMesh(pivotMarkerRef);
                }

                prog.Stop();
            }
        }

        // Build a 4x4 rotation matrix (column-major) for rotation by `angleRad`
        // around the axis (ax, ay, az). Falls back to identity if axis is the
        // zero vector. Uses the Rodrigues formula.
        private static readonly float[] _aaScratch = new float[16];
        private static float[] AxisAngleRotation(float ax, float ay, float az, float angleRad)
        {
            float len = (float)Math.Sqrt(ax * ax + ay * ay + az * az);
            // Identity if zero axis
            _aaScratch[0] = 1; _aaScratch[1] = 0; _aaScratch[2] = 0; _aaScratch[3] = 0;
            _aaScratch[4] = 0; _aaScratch[5] = 1; _aaScratch[6] = 0; _aaScratch[7] = 0;
            _aaScratch[8] = 0; _aaScratch[9] = 0; _aaScratch[10] = 1; _aaScratch[11] = 0;
            _aaScratch[12] = 0; _aaScratch[13] = 0; _aaScratch[14] = 0; _aaScratch[15] = 1;
            if (len < 1e-6f) return _aaScratch;

            float x = ax / len, y = ay / len, z = az / len;
            float c = (float)Math.Cos(angleRad);
            float s = (float)Math.Sin(angleRad);
            float t = 1f - c;

            // Column-major: m[col*4 + row]
            _aaScratch[0]  = t * x * x + c;
            _aaScratch[1]  = t * x * y + s * z;
            _aaScratch[2]  = t * x * z - s * y;
            _aaScratch[3]  = 0;
            _aaScratch[4]  = t * x * y - s * z;
            _aaScratch[5]  = t * y * y + c;
            _aaScratch[6]  = t * y * z + s * x;
            _aaScratch[7]  = 0;
            _aaScratch[8]  = t * x * z + s * y;
            _aaScratch[9]  = t * y * z - s * x;
            _aaScratch[10] = t * z * z + c;
            _aaScratch[11] = 0;
            _aaScratch[12] = 0;
            _aaScratch[13] = 0;
            _aaScratch[14] = 0;
            _aaScratch[15] = 1;
            return _aaScratch;
        }

        // Load the item's shape JSON and copy the relevant origin into
        // TiltPivot (mesh coords 0..1). Overwrites any previous TiltPivot
        // — so simply breaking + replacing the bench re-syncs from the
        // (possibly edited) shape file.
        private static void TryAutoSetTiltPivot(ICoreClientAPI api, Item item)
        {
            var shapeLoc = item.Shape?.Base;
            if (shapeLoc == null) return;
            try
            {
                var path = shapeLoc.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
                var shape = api.Assets.TryGet(path)?.ToObject<Vintagestory.API.Common.Shape>();
                if (shape?.Elements == null || shape.Elements.Length == 0) return;

                // Use the "origin" element's rotationOrigin field as the
                // intended pivot. Search by name first; fall back to the
                // first element if not found.
                var el = shape.Elements[0];
                foreach (var e in shape.Elements)
                {
                    if (e?.Name == "origin") { el = e; break; }
                }

                var src = el.RotationOrigin;
                if (src == null || src.Length < 3) return;
                TiltPivotX = (float)src[0] / 16f;
                TiltPivotY = (float)src[1] / 16f;
                TiltPivotZ = (float)src[2] / 16f;
                api.Logger.Notification("[lapidary] auto-set TiltPivot from {0} (element '{1}'.rotationOrigin) → ({2:F3}, {3:F3}, {4:F3})",
                    shapeLoc, el.Name, TiltPivotX, TiltPivotY, TiltPivotZ);
            }
            catch (Exception e)
            {
                api.Logger.Warning("[lapidary] failed to read shape rotationOrigin: {0}", e.Message);
            }
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            texture?.Dispose();
            quadRef?.Dispose();
            dopMeshRef?.Dispose();
            gemMeshRef?.Dispose();
            pivotMarkerRef?.Dispose();
            texture = null;
            quadRef = null;
            dopMeshRef = null;
            gemMeshRef = null;
            pivotMarkerRef = null;
        }
    }
}
