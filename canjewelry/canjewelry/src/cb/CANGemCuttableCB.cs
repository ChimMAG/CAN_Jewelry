using canjewelry.src.be;
using canjewelry.src.items;
using canjewelry.src.jewelry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace canjewelry.src.cb
{
    public class CANGemCuttableCB: CollectibleBehavior
    {
        public static ICoreAPI aapi;
        public CANGemCuttableCB(CollectibleObject collObj) : base(collObj)
        {
        }
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            aapi = api;
        }
        public int GetRequiredGemCuttingTableTier(ItemStack stack)
        { 
            return 0; 
        }

        public List<GemCuttingRecipe> GetMatchingRecipes(ItemStack stack)
        {
           /* foreach(var it in canjewelry.gemCuttingRecipes)
            {
                var c = it.Ingredient.SatisfiesAsIngredient(stack, true);
                if(c)
                {
                    if (it.Ingredient.RecipeAttributes != null)
                    {
                        var T = it.Ingredient.RecipeAttributes.ToAttribute();
                        var f = stack.Attributes == T;
                        //Console.WriteLine(T..);
                        Console.WriteLine(stack.Attributes.ToString());
                        if (f)
                        {
                            var p = 3;
                        }
                    }
                }
            }*/
        
            return (from r in canjewelry.gemCuttingRecipes
                    where r.Ingredient.SatisfiesAsIngredient(stack, true) && (r.Ingredient.RecipeAttributes == null || stack.Attributes.ToJsonToken().Equals(r.Ingredient.RecipeAttributes.ToAttribute().ToJsonToken()))
                    orderby r.Output.ResolvedItemstack.Collectible.Code
                    select r).ToList<GemCuttingRecipe>();
        }

        public bool CanWork(ItemStack stack)
        {
            return true;
        }

        public ItemStack TryPlaceOn(ItemStack stack, BlockEntityGemCuttingTable beGemCuttingTable)
        {
            if(stack.Item is CANItemGemCuttingWorkItem)
            {
                if (beGemCuttingTable.WorkItemStack != null)
                {
                    return null;
                }
                try
                {
                    beGemCuttingTable.Voxels = BlockEntityGemCuttingTable.deserializeVoxels(stack.Attributes.GetBytes("voxels", null));
                    beGemCuttingTable.SelectedRecipeId = stack.Attributes.GetInt("selectedRecipeId", 0);
                }
                catch (Exception)
                {
                }
                return stack.Clone();
            }
            if (!this.CanWork(stack))
            {
                return null;
            }
            Item item = beGemCuttingTable.Api.World.GetItem(new AssetLocation("canjewelry:gemcuttingworkitem"));  //this.Variant["metal"]
                                                                                                     // + this.Variant["gemtype"]

            if (item == null)
            {
                return null;
            }
            ItemStack workItemStack = new ItemStack(item, 1);
            ITreeAttribute gemItemAttribute = new TreeAttribute();
            string gemType = stack.Collectible.Variant[CANJWConstants.GEM_TYPE_IN_SOCKET];
            if(gemType == null)
            {
                gemType = stack.Collectible.Variant["ore"];
            }
            gemItemAttribute.SetString(CANJWConstants.GEM_TYPE_IN_SOCKET, gemType);
            string qualityName = stack.Collectible.Variant["quality"];
            if (stack.Attributes.HasAttribute("potential"))
            {
                string potName = stack.Attributes.GetString("potential");
                switch (potName)
                {
                    case "low":
                        qualityName = "chipped";
                        break;
                    case "medium":
                        qualityName = "flawed";
                        break;
                    case "high":
                        qualityName = "normal";
                        break;
                }
            }
            gemItemAttribute.SetString(CANJWConstants.ENCRUSTED_GEM_SIZE, qualityName);
            workItemStack.Attributes = gemItemAttribute;
            //workItemStack.Collectible.SetTemperature(beGemCuttingTable.Api.World, workItemStack, stack.Collectible.GetTemperature(beGemCuttingTable.Api.World, stack), true);
            if (beGemCuttingTable.WorkItemStack == null)
            {
                CANRoughGemItem.CreateVoxelsFromRoughGem(beGemCuttingTable.Api, ref beGemCuttingTable.Voxels, false);
            }
            else
            {
                return null;
                /*if (this.isBlisterSteel)
                {
                    return null;
                }*/
                if (!string.Equals(beGemCuttingTable.WorkItemStack.Collectible.Variant["metal"], stack.Collectible.Variant["metal"]))
                {
                    if (beGemCuttingTable.Api.Side == EnumAppSide.Client)
                    {
                        (beGemCuttingTable.Api as ICoreClientAPI).TriggerIngameError(this, "notequal", Lang.Get("Must be the same metal to add voxels", Array.Empty<object>()));
                    }
                    return null;
                }
                if (ItemIngot.AddVoxelsFromIngot(ref beGemCuttingTable.Voxels) == 0)
                {
                    if (beGemCuttingTable.Api.Side == EnumAppSide.Client)
                    {
                        (beGemCuttingTable.Api as ICoreClientAPI).TriggerIngameError(this, "requireshammering", Lang.Get("Try hammering down before adding additional voxels", Array.Empty<object>()));
                    }
                    return null;
                }
            }
            return workItemStack;
        }

        public ItemStack GetBaseMaterial(ItemStack stack)
        {
            if (stack.Item is CANItemGemCuttingWorkItem)
            {
                Item item = aapi.World.GetItem(AssetLocation.Create("canjewelry:gem-rough-" + stack.Attributes.GetString(CANJWConstants.ENCRUSTED_GEM_SIZE) + "-" + stack.Attributes.GetString(CANJWConstants.GEM_TYPE_IN_SOCKET)));
                //Item item = api.World.GetItem(AssetLocation.Create("ingot-" + Variant["metal"], Attributes?["baseMaterialDomain"].AsString("game")));
                if(item == null)
                {
                    item = aapi.World.GetItem(AssetLocation.Create("game:gem-" + stack.Attributes.GetString(CANJWConstants.GEM_TYPE_IN_SOCKET)) + "-rough");
                }
                if (item == null)
                {
                    throw new Exception(string.Format("Base material for not found, there is no item with code 'ingot'"));
                }
                return new ItemStack(item);                
            }
            return stack;
        }
    }
}
