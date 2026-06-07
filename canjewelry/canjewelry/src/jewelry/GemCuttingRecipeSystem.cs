using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace canjewelry.src.jewelry
{
    public class GemCuttingRecipeSystem
    {
        public class GemCuttingRecipeRegistry<T> : RecipeRegistryBase where T : IByteSerializable, new()
        {

            public List<GemCuttingRecipe> Recipes;

            public GemCuttingRecipeRegistry()
            {
                Recipes = new List<GemCuttingRecipe>();
            }

            public GemCuttingRecipeRegistry(List<GemCuttingRecipe> recipes)
            {
                Recipes = recipes;
            }

            public override void FromBytes(IWorldAccessor resolver, int quantity, byte[] data)
            {
                using MemoryStream input = new MemoryStream(data);
                BinaryReader reader = new BinaryReader(input);
                for (int i = 0; i < quantity; i++)
                {
                    GemCuttingRecipe item = new GemCuttingRecipe();
                    item.FromBytes(reader, resolver);
                    Recipes.Add(item);
                }
            }

            public override void ToBytes(IWorldAccessor resolver, out byte[] data, out int quantity)
            {
                quantity = Recipes.Count;
                using MemoryStream memoryStream = new MemoryStream();
                BinaryWriter writer = new BinaryWriter(memoryStream);
                foreach (GemCuttingRecipe recipe in Recipes)
                {
                    recipe.ToBytes(writer);
                }

                data = memoryStream.ToArray();
            }
        }

        public class PotionCauldronRecipeLoader : ModSystem
        {
            public override double ExecuteOrder()
            {
                return 1.0;
            }
            /*public override bool ShouldLoad(EnumAppSide side)
            {
                return true;
            }*/
            /*public override bool ShouldLoad(EnumAppSide forSide)
            {
                return forSide == EnumAppSide.Server;
            }*/
            public override void Start(ICoreAPI api)
            {
                base.Start(api);
                canjewelry.gemCuttingRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<GemCuttingRecipe>>("gemcuttingrecipes").Recipes;
            }
            public override void AssetsLoaded(ICoreAPI api)
            {
                if (api.Side == EnumAppSide.Client) return;
                LoadPotionCauldronRecipes(api);
            }










            /// <summary>
            /// ///////////////////////////////
            /// </summary>
            /// <param name="api"></param>
            public override void StartServerSide(ICoreServerAPI api)
            {
                this.api = api;
            }

            public override void Dispose()
            {
                base.Dispose();
            }

            public void LoadFoodRecipes()
            {
                this.LoadPotionCauldronRecipes(api);
            }

            public void LoadFoodRecipesClient(IClientPlayer byPlayer)
            {
                capi.Event.RegisterCallback((dt =>
                {
                    this.LoadPotionCauldronRecipes(capi);
                }
                ), 30 * 1000);

            }

            public void LoadPotionCauldronRecipes(ICoreAPI api)
            {
                Dictionary<AssetLocation, JToken> many = null;
                if (api.Side == EnumAppSide.Server)
                {
                    many = api.Assets.GetMany<JToken>(api.Logger, "recipes/gemcutting", null);
                }
                else
                {
                    return;
                }
                foreach (KeyValuePair<AssetLocation, JToken> keyValuePair in many)
                {
                    if(keyValuePair.Value is JArray)
                    {
                        foreach(var it in keyValuePair.Value)
                        {
                            AddRecipe(it, api);                            
                        }
                    }
                    else
                    {
                        AddRecipe(keyValuePair.Value, api);
                    }
                }
            }

            private void AddRecipe(JToken readToken, ICoreAPI api)
            {
                GemCuttingRecipe recipe = readToken.ToObject<GemCuttingRecipe>();
                if (!recipe.Enabled) return;

                Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(api.World);
                if (nameToCodeMapping.Count == 0)
                {
                    if (recipe.Resolve(api.World, "gem cutting"))
                    {
                        recipe.RecipeId = canjewelry.gemCuttingRecipes.Count() + 1;
                        canjewelry.gemCuttingRecipes.Add(recipe);
                    }
                    return;
                }

                // Expand wildcard variants (mirrors GridRecipeLoader pattern)
                int variantsCombinations = 1;
                foreach (var mapping in nameToCodeMapping)
                    variantsCombinations *= mapping.Value.Length;

                List<GemCuttingRecipe> subRecipes = new List<GemCuttingRecipe>();
                bool first = true;
                int variantCodeIndexDivider = 1;

                foreach (var kvp in nameToCodeMapping)
                {
                    string variantCode = kvp.Key;
                    string[] variants = kvp.Value;
                    if (variants.Length == 0) continue;

                    for (int i = 0; i < variantsCombinations; i++)
                    {
                        string currentVariant = variants[i / variantCodeIndexDivider % variants.Length];
                        GemCuttingRecipe currentRecipe;
                        if (first)
                        {
                            currentRecipe = recipe.Clone();
                            subRecipes.Add(currentRecipe);
                        }
                        else
                        {
                            currentRecipe = subRecipes[i];
                        }
                        CraftingRecipeIngredient ingred = currentRecipe.Ingredient;
                        if (ingred != null && ingred.Name == variantCode)
                        {
                            ingred.FillPlaceHolder(variantCode, currentVariant);
                            ingred.Code.Path = ingred.Code.Path.Replace("*", currentVariant);
                            ingred.IsBasicWildCard = false;
                        }
                        currentRecipe.Output.FillPlaceHolder(variantCode, currentVariant);
                    }
                    variantCodeIndexDivider *= variants.Length;
                    first = false;
                }

                foreach (GemCuttingRecipe subRecipe in subRecipes)
                {
                    if (subRecipe.Resolve(api.World, "gem cutting"))
                    {
                        subRecipe.RecipeId = canjewelry.gemCuttingRecipes.Count() + 1;
                        canjewelry.gemCuttingRecipes.Add(subRecipe);
                    }
                }
            }
            public ICoreServerAPI api;
            public ICoreClientAPI capi;
        }
    }
}
