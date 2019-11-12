﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AnvilWorkItemRenderer : IRenderer
    {
        private ICoreClientAPI api;
        private BlockPos pos;

        MeshRef workItemMeshRef;
        MeshRef recipeOutlineMeshRef;

        ItemStack ingot;
        int texId;

        Vec4f outLineColorMul = new Vec4f(1, 1, 1, 1);
        protected Matrixf ModelMat = new Matrixf();

        public AnvilWorkItemRenderer(BlockPos pos, ICoreClientAPI capi)
        {
            this.pos = pos;
            this.api = capi;
        }

        public double RenderOrder
        {
            get { return 0.5; }
        }

        public int RenderRange
        {
            get { return 24; }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (workItemMeshRef == null) return;
            if (stage == EnumRenderStage.AfterFinalComposition)
            {
                RenderRecipeOutLine();
                return;
            }

            IRenderAPI rpi = api.Render;
            IClientWorldAccessor worldAccess = api.World;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;
            EntityPos plrPos = worldAccess.Player.Entity.Pos;
            int temp = (int)ingot.Collectible.GetTemperature(api.World, ingot);

            Vec4f lightrgbs = worldAccess.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
            float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
            lightrgbs[0] += glowColor[0];
            lightrgbs[1] += glowColor[1];
            lightrgbs[2] += glowColor[2];



            rpi.GlDisableCullFace();

            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();
            rpi.BindTexture2d(texId);
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.RgbaLightIn = lightrgbs;
            prog.RgbaBlockIn = ColorUtil.WhiteArgbVec;
            prog.ExtraGlow = GameMath.Clamp((temp - 700) / 2, 0, 255);
            prog.ExtraGodray = 0;
            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                .Values
            ;
            

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            rpi.RenderMesh(workItemMeshRef);
            prog.Stop();
        }



        private void RenderRecipeOutLine()
        {
            if (recipeOutlineMeshRef == null || api.HideGuis) return;
            IRenderAPI rpi = api.Render;
            IClientWorldAccessor worldAccess = api.World;
            EntityPos plrPos = worldAccess.Player.Entity.Pos;
            Vec3d camPos = worldAccess.Player.Entity.CameraPos;
            ModelMat.Set(rpi.CameraMatrixOriginf).Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);
            outLineColorMul.A = 1 - GameMath.Clamp((float)Math.Sqrt(plrPos.SquareDistanceTo(pos.X, pos.Y, pos.Z)) / 5 - 1f, 0, 1);

            rpi.GLEnableDepthTest();
            rpi.GlToggleBlend(true);

            IShaderProgram prog = rpi.GetEngineShader(EnumShaderProgram.Wireframe);

            prog.Use();
            prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);
            prog.UniformMatrix("modelViewMatrix", ModelMat.Values);
            prog.Uniform("colorIn", outLineColorMul);
            rpi.RenderMesh(recipeOutlineMeshRef);
            prog.Stop();
        }



        public void RegenMesh(ItemStack ingot, bool[,,] Voxels, SmithingRecipe recipeToOutline)
        {
            workItemMeshRef?.Dispose();
            workItemMeshRef = null;
            
            if (ingot == null) return;

            if (recipeToOutline != null)
            {
                RegenOutlineMesh(recipeToOutline, Voxels);
            }

            this.ingot = ingot;
            MeshData workItemMesh = new MeshData(24, 36, false);

            TextureAtlasPosition tpos = api.BlockTextureAtlas.GetPosition(api.World.GetBlock(new AssetLocation("ingotpile")), ingot.Collectible.LastCodePart());
            MeshData voxelMesh = CubeMeshUtil.GetCubeOnlyScaleXyz(1 / 32f, 1 / 32f, new Vec3f(1 / 32f, 1 / 32f, 1 / 32f));
            texId = tpos.atlasTextureId;

            for (int i = 0; i < voxelMesh.Uv.Length; i++)
            {
                if (i % 2 > 0)
                {
                    voxelMesh.Uv[i] = tpos.y1 + voxelMesh.Uv[i] * 2f / api.BlockTextureAtlas.Size.Height;
                } else
                {
                    voxelMesh.Uv[i] = tpos.x1 + voxelMesh.Uv[i] * 2f / api.BlockTextureAtlas.Size.Width;
                }
                
            }

            voxelMesh.XyzFaces = (int[])CubeMeshUtil.CubeFaceIndices.Clone();
            voxelMesh.XyzFacesCount = 6;
            voxelMesh.Tints = new int[6];
            voxelMesh.Flags = new int[24];
            voxelMesh.TintsCount = 6;
            for (int i = 0; i < voxelMesh.Rgba.Length; i++) voxelMesh.Rgba[i] = 255;
            voxelMesh.Rgba2 = null;// voxelMesh.Rgba;


            MeshData voxelMeshOffset = voxelMesh.Clone();

            for (int x = 0; x < 16; x++)
            {
                for (int y = 10; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (!Voxels[x, y, z]) continue;

                        float px = x / 16f;
                        float py = y / 16f;
                        float pz = z / 16f;

                        for (int i = 0; i < voxelMesh.xyz.Length; i += 3)
                        {
                            voxelMeshOffset.xyz[i] = px + voxelMesh.xyz[i];
                            voxelMeshOffset.xyz[i + 1] = py + voxelMesh.xyz[i + 1];
                            voxelMeshOffset.xyz[i + 2] = pz + voxelMesh.xyz[i + 2];
                        }

                        float offsetX = (px * 32f) / api.BlockTextureAtlas.Size.Width;
                        float offsetZ = (pz * 32f) / api.BlockTextureAtlas.Size.Height;

                        for (int i = 0; i < voxelMesh.Uv.Length; i += 2)
                        {
                            voxelMeshOffset.Uv[i] = voxelMesh.Uv[i] + offsetX;
                            voxelMeshOffset.Uv[i + 1] = voxelMesh.Uv[i + 1] + offsetZ;
                        }

                        workItemMesh.AddMeshData(voxelMeshOffset);
                    }
                }
            }

            workItemMeshRef = api.Render.UploadMesh(workItemMesh);
        }


        private void RegenOutlineMesh(SmithingRecipe recipeToOutline, bool[,,] voxels)
        {
            recipeOutlineMeshRef?.Dispose();

            MeshData recipeOutlineMesh = new MeshData(24, 36, false, false, true, false, false);
            recipeOutlineMesh.SetMode(EnumDrawMode.Lines);

            int greenCol = (156 << 24) | (100 << 16) | (200 << 8) | (100);
            int orangeCol = (156 << 24) | (219 << 16) | (92 << 8) | (92);
            MeshData greenVoxelMesh = LineMeshUtil.GetCube(greenCol);
            MeshData orangeVoxelMesh = LineMeshUtil.GetCube(orangeCol);
            for (int i = 0; i < greenVoxelMesh.xyz.Length; i++)
            {
                greenVoxelMesh.xyz[i] = greenVoxelMesh.xyz[i] / 32f + 1 / 32f;
                orangeVoxelMesh.xyz[i] = orangeVoxelMesh.xyz[i] / 32f + 1 / 32f;
            }
            MeshData voxelMeshOffset = greenVoxelMesh.Clone();


            for (int x = 0; x < 16; x++)
            {
                int y = 10;
                for (int z = 0; z < 16; z++)
                {
                    bool shouldFill = recipeToOutline.Voxels[x, z];
                    bool didFill = voxels[x, y, z];
                    if (shouldFill == didFill) continue;

                    float px = x / 16f;
                    float py = y / 16f;
                    float pz = z / 16f;

                    for (int i = 0; i < greenVoxelMesh.xyz.Length; i += 3)
                    {
                        voxelMeshOffset.xyz[i] = px + greenVoxelMesh.xyz[i];
                        voxelMeshOffset.xyz[i + 1] = py + greenVoxelMesh.xyz[i + 1];
                        voxelMeshOffset.xyz[i + 2] = pz + greenVoxelMesh.xyz[i + 2];
                    }

                    voxelMeshOffset.Rgba = (shouldFill && !didFill) ? greenVoxelMesh.Rgba : orangeVoxelMesh.Rgba;

                    recipeOutlineMesh.AddMeshData(voxelMeshOffset);
                }
            }

            recipeOutlineMeshRef = api.Render.UploadMesh(recipeOutlineMesh);
        }

        public void Unregister()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            api.Event.UnregisterRenderer(this, EnumRenderStage.AfterFinalComposition);
        }

        // Called by UnregisterRenderer
        public void Dispose()
        {
            recipeOutlineMeshRef?.Dispose();
            workItemMeshRef?.Dispose();
        }
    }
}
