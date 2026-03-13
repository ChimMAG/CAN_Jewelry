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
                // canjewelry.gemCuttingRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<GemCuttingRecipe>>("gemcuttingrecipes").Recipes;
                 if(api.Side == EnumAppSide.Client)
                  {
                      return;
                  }
                //api.ModLoader.GetModSystem<RecipeLoader>().
                LoadRecipes<GemCuttingRecipe>(api as ICoreServerAPI, "gemcuttingrecipes", "recipes/gemcutting", false, delegate (IRecipeBase r)
                {
                    r.RecipeId = canjewelry.gemCuttingRecipes.Count() + 1;
                    canjewelry.gemCuttingRecipes.Add(r as GemCuttingRecipe);
                    //serverApi.RegisterSmithingRecipe(r as SmithingRecipe);
                });
               // LoadPotionCauldronRecipes(api);
            }

            private static void LoadRecipes<TRecipe>(ICoreServerAPI api, string name, string path, bool classExclusiveRecipes, Action<IRecipeBase> registerDelegate) where TRecipe : IRecipeBase
            {
                Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Logger, path);
                int recipeQuantity = 0;

                int recipesLoaded = 0;
                int failedResolveCount = 0;

                foreach ((AssetLocation location, JToken content) in files)
                {
                    if (content is JObject recipeObject)
                    {
                        TRecipe? parsedContent = recipeObject.ToObject<TRecipe>(location.Domain);
                        if (parsedContent == null)
                        {
                            api.Logger.Error($"Failed to parse {name} recipe: {location}");
                            continue;
                        }

                        LoadRecipe(api, location, parsedContent, classExclusiveRecipes, registerDelegate, loaded: ref recipesLoaded, failedResolveCount: ref failedResolveCount);
                        recipeQuantity++;
                    }
                    else if (content is JArray arrayOfRecipes)
                    {
                        foreach (JToken token in arrayOfRecipes)
                        {
                            TRecipe? parsedContent = token.ToObject<TRecipe>(location.Domain);
                            if (parsedContent == null)
                            {
                                api.Logger.Error($"Failed to parse {name} recipe: {location}");
                                continue;
                            }

                            LoadRecipe(api, location, parsedContent, classExclusiveRecipes, registerDelegate, loaded: ref recipesLoaded, failedResolveCount: ref failedResolveCount);
                            recipeQuantity++;
                        }
                    }
                }

                if (failedResolveCount > 0)
                {
                    api.Logger.Event($"{recipeQuantity} {name} recipes loaded from {files.Count} files, failed to resolve {failedResolveCount} recipes");
                }
                else
                {
                    api.Logger.Event($"{recipeQuantity} {name} recipes loaded from {files.Count} files");
                }


                RecipeBase.CollectiblePreSearchResultsCache.Clear();
            }

            private static void LoadRecipe(ICoreServerAPI api, AssetLocation assetLocation, IRecipeBase recipe, bool classExclusiveRecipes, Action<IRecipeBase> registerDelegate, ref int loaded, ref int failedResolveCount)
            {
                if (!recipe.Enabled) return;

                if (!classExclusiveRecipes)
                {
                    recipe.RequiresTrait = null;
                }

                if (recipe.Name == null)
                {
                    recipe.Name = assetLocation;
                }

                recipe.OnParsed(api.World);

                IEnumerable<IRecipeBase> recipes = recipe.GenerateRecipesForAllIngredientCombinations(api.World);

                foreach (IRecipeBase subRecipe in recipes)
                {
                    if (subRecipe.Resolve(api.World, "RecipeLoader"))
                    {
                        registerDelegate.Invoke(subRecipe);
                        loaded++;
                    }
                    else
                    {
                        failedResolveCount++;
                    }
                }
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
                GemCuttingRecipe potionCauldronRecipe = readToken.ToObject<GemCuttingRecipe>();
                bool flag2 = !potionCauldronRecipe.Enabled;
                if (flag2)
                {
                    return;
                }
                GemCuttingRecipe potionCauldronRecipe2 = potionCauldronRecipe;
                var c = potionCauldronRecipe.GenerateRecipesForAllIngredientCombinations(api.World);
                //Dictionary<string, string[]> nameToCodeMapping = //potionCauldronRecipe.GetNameToCodeMapping(api.World);
                //var nameToCodeMapping = potionCauldronRecipe.GenerateRecipesForAllIngredientCombinations(api.World);
                var subRecipes = potionCauldronRecipe.GenerateRecipesForAllIngredientCombinations(api.World);
                foreach (GemCuttingRecipe subRecipe in subRecipes)
                {
                    if (!subRecipe.Resolve(api.World, "gem cutting"))
                    {
                        //quantityIgnored++;
                        continue;
                    }
                    subRecipe.RecipeId = canjewelry.gemCuttingRecipes.Count() + 1;
                    canjewelry.gemCuttingRecipes.Add(subRecipe);
                    //RegisterMethod(subRecipe);
                    //quantityRegistered++;
                }

                //TODO
                /*if (nameToCodeMapping.Count > 0)
                {
                    List<GemCuttingRecipe> subRecipes = new List<GemCuttingRecipe>();
                    int qCombs = 0;
                    bool first = true;
                    foreach (KeyValuePair<string, string[]> val2 in nameToCodeMapping)
                    {
                        if (first)
                        {
                            qCombs = val2.Value.Length;
                        }
                        else
                        {
                            qCombs *= val2.Value.Length;
                        }
                        first = false;
                    }
                    first = true;
                    foreach (KeyValuePair<string, string[]> val3 in nameToCodeMapping)
                    {
                        string variantCode = val3.Key;
                        string[] variants = val3.Value;
                        for (int i = 0; i < qCombs; i++)
                        {
                            GemCuttingRecipe rec;
                            if (first)
                            {
                                subRecipes.Add(rec = potionCauldronRecipe2.Clone());
                            }
                            else
                            {
                                rec = subRecipes[i];
                            }
                            if (rec.Ingredients != null)
                            {
                                foreach (IRecipeIngredient ingred in rec.Ingredients)
                                {
                                    if (ingred.Name == variantCode)
                                    {
                                        ingred.Code = ingred.Code.CopyWithPath(ingred.Code.Path.Replace("*", variants[i % variants.Length]));
                                    }
                                }
                            }
                            rec.Output.FillPlaceHolder(val3.Key, variants[i % variants.Length]);
                        }
                        first = false;
                    }
                    if (subRecipes.Count == 0)
                    {
                        this.api.World.Logger.Warning("{1} file {0} make uses of wildcards, but no blocks or item matching those wildcards were found.", new object[]
                        {

                        });
                    }
                    foreach (GemCuttingRecipe subRecipe in subRecipes)
                    {
                        if (!subRecipe.Resolve(api.World, "gem cutting"))
                        {
                            //quantityIgnored++;
                            continue;
                        }
                        subRecipe.RecipeId = canjewelry.gemCuttingRecipes.Count() + 1;
                        canjewelry.gemCuttingRecipes.Add(subRecipe);
                        //RegisterMethod(subRecipe);
                        //quantityRegistered++;
                    }
                }
                */

            }
            public ICoreServerAPI api;
            public ICoreClientAPI capi;
        }
    }
}
