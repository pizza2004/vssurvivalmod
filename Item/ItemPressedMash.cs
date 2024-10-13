using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ItemPressedMash : Item
    {
        public override string GetHeldItemName(ItemStack itemStack)
        {
            float availableLitres = (float)Math.Round(itemStack.Attributes.GetDecimal("juiceableLitresLeft"), 2);
            string ap = availableLitres > 0 ? "wet" : "dry";
            string type = ItemClass.Name();

            return Lang.GetMatching(Code?.Domain + AssetLocation.LocationSeparator + type + "-" + Code?.Path + "-" + ap);
        }

        public override ItemStack OnTransitionNow(ItemSlot slot, TransitionableProperties props)
        {
            if (props.Type == EnumTransitionType.Perish)
            {
                var juiceProps = getJuiceableProps(slot.ItemStack);
                float juiceableLitresLeft = slot.ItemStack.Attributes.TryGetFloat("juiceableLitresLeft");
                
                if (juiceableLitresLeft != null)
                {
                    int stacksize = GameMath.RoundRandom(Api.World.Rand, juiceableLitresLeft);
                    slot.ItemStack.Attributes.RemoveAttribute("juiceableLitresTransfered");
                    slot.ItemStack.Attributes.RemoveAttribute("juiceableLitresLeft");
                    slot.ItemStack.Attributes.RemoveAttribute("squeezeRel");
                    props.TransitionRatio = (int)(stacksize * juiceProps.PressedDryRatio);
                }
            }

            return base.OnTransitionNow(slot, props);
        }
    }
}
