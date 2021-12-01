﻿using Cairo;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;


namespace Vintagestory.GameContent
{
    public class LiquidItemStackRenderer : ModSystem
    {
        ICoreClientAPI capi;

        Dictionary<string, LoadedTexture> litreTextTextures;
        CairoFont stackSizeFont;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            stackSizeFont = CairoFont.WhiteSmallText().WithFontSize((float)GuiStyle.DetailFontSize);
            stackSizeFont.FontWeight = FontWeight.Bold;
            stackSizeFont.Color = new double[] { 1, 1, 1, 1 };
            stackSizeFont.StrokeColor = new double[] { 0, 0, 0, 1 };
            stackSizeFont.StrokeWidth = RuntimeEnv.GUIScale + 0.25;

            litreTextTextures = new Dictionary<string, LoadedTexture>();

            api.Settings.AddWatcher<float>("guiScale", (newvalue) =>
            {
                stackSizeFont.StrokeWidth = newvalue + 0.25;

                foreach (var val in litreTextTextures)
                {
                    val.Value.Dispose();
                }

                litreTextTextures.Clear();
            });

            api.Event.LeaveWorld += Event_LeaveWorld;
            api.Event.LevelFinalize += Event_LevelFinalize;
        }

        private void Event_LevelFinalize()
        {
            foreach (var obj in capi.World.Collectibles)
            {
                if (obj.Attributes?["waterTightContainerProps"].Exists == true)
                {
                    RegisterLiquidStackRenderer(obj);
                }
            }
        }


        private void Event_LeaveWorld()
        {
            foreach (var val in litreTextTextures)
            {
                val.Value.Dispose();
            }

            litreTextTextures.Clear();
        }


        public void RegisterLiquidStackRenderer(CollectibleObject obj)
        {
            if (obj == null) throw new ArgumentNullException("obj cannot be null");
            if (obj.Attributes?["waterTightContainerProps"].Exists == null) throw new ArgumentException("This collectible object has no waterTightContainerProps");

            capi.Event.RegisterItemstackRenderer(obj, (inSlot, renderInfo, modelMat, posX, posY, posZ, size, color, rotate, showStackSize) => RenderLiquidItemStackGui(inSlot, renderInfo, modelMat, posX, posY, posZ, size, color, rotate, showStackSize), EnumItemRenderTarget.Gui);
        }


        public void RenderLiquidItemStackGui(ItemSlot inSlot, ItemRenderInfo renderInfo, Matrixf modelMat, double posX, double posY, double posZ, float size, int color, bool rotate = false, bool showStackSize = true)
        {
            ItemStack itemstack = inSlot.Itemstack;

            WaterTightContainableProps props = BlockLiquidContainerBase.GetInContainerProps(itemstack);

            capi.Render.RenderMesh(renderInfo.ModelRef);


            if (showStackSize)
            {
                float litreFloat = (float)itemstack.StackSize / props.ItemsPerLitre;
                string litres;
                if (litreFloat < 0.1)
                {
                    litres = Lang.Get("{0} mL", (int)(litreFloat * 1000));
                } else
                {
                    litres = Lang.Get("{0:0.##} L", litreFloat);
                }
                
                float mul = size / (float)GuiElement.scaled(32 * 0.8f);

                var texttex = GetOrCreateLitreTexture(litres, mul);

                capi.Render.GlToggleBlend(true, EnumBlendMode.PremultipliedAlpha);

                capi.Render.Render2DLoadedTexture(texttex,
                    (int)(posX + size + 1 - texttex.Width - GuiElement.scaled(1)),
                    (int)(posY + mul * GuiElement.scaled(3)),
                    (int)posZ + 60
                );
                capi.Render.GlToggleBlend(true, EnumBlendMode.Standard);
            }

        }



        public LoadedTexture GetOrCreateLitreTexture(string litres, float fontSizeMultiplier = 1f)
        {
            string key = litres + "-" + fontSizeMultiplier;
            if (!litreTextTextures.TryGetValue(key, out var texture))
            {
                CairoFont font = stackSizeFont.Clone();
                font.UnscaledFontsize *= fontSizeMultiplier;
                return litreTextTextures[key] = capi.Gui.TextTexture.GenTextTexture(litres, font);
            }

            return texture;
        }
    }
}