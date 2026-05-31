using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace canjewelry.src.jewelry
{
    public class GemCuttingRecipe : LayeredVoxelRecipe, IConcreteCloneable<GemCuttingRecipe>, ICloneable//
                                                                                                        //IRecipeBase, IByteSerializable, ICloneable, IConcreteCloneable<RecipeBase>
                                                                                                        //, RecipeBase, IByteSerializable, IConcreteCloneable<GridRecipe>, ICloneable
    {
        public override int QuantityLayers
        {
            get
            {
                return 14;
            }
        }
        public override string RecipeCategoryCode
        {
            get
            {
                return "gem_cutting";
            }
        }
        protected override bool RotateRecipe
        {
            get
            {
                return true;
            }
        }
        public override GemCuttingRecipe Clone()
        {
            return new GemCuttingRecipe
            {
                Pattern = (string[][])this.Pattern.Clone(),
                Ingredient = base.Ingredient.Clone(),
                Output = this.Output.Clone(),
                Name = base.Name,
                RecipeId = this.RecipeId
            };
        }
        public override void ToBytes(BinaryWriter writer)
        {
           
            if (this.Ingredient == null || base.Name == null)
            {
                throw new InvalidOperationException("Trying to write voxel recipe into bytes, but recipe ");
            }
            writer.Write(base.RecipeId);
            writer.Write(this.Ingredients.Length);
            CraftingRecipeIngredient[] ingredients = this.Ingredients;
            for (int j = 0; j < ingredients.Length; j++)
            {
                ingredients[j].ToBytes(writer);
            }
            writer.Write(this.Pattern.Length);
            for (int i = 0; i < this.Pattern.Length; i++)
            {
                writer.WriteArray(this.Pattern[i]);
            }
            writer.Write(base.Name.ToShortString());
            this.RecipeOutput.ToBytes(writer);
            writer.Write((bool)(Output.ResolvedItemstack != null));
            if (Output.ResolvedItemstack != null)
            {
                Output.ResolvedItemstack.ToBytes(writer);
            }



            return;
            writer.Write((bool)(Output.ResolvedItemstack != null));
            if (Output.ResolvedItemstack != null)
            {
                Output.ResolvedItemstack.ToBytes(writer);
            }
            return;


            ////
            writer.Write(RecipeId);
            base.Ingredient.ToBytes(writer);
            
            writer.Write(Pattern.Length);
            for (int i = 0; i < Pattern.Length; i++)
            {
                writer.WriteArray(Pattern[i]);
            }
            ////
            writer.Write(base.Name.ToShortString());
            writer.Write((short)Output.Type);
            writer.Write(Output.Code.ToShortString());
            writer.Write(Output.StackSize);
            writer.Write(Output.ResolvedItemstack != null);

            if (Output.ResolvedItemstack != null)
            {
                Output.ResolvedItemstack.ToBytes(writer);
            }
            //Output.ToBytes(writer);
            //base.ToBytes(writer);
        }
        public override void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            
            base.RecipeId = reader.ReadInt32();
            int ingredientsCount = reader.ReadInt32();
            this.Ingredients = new CraftingRecipeIngredient[ingredientsCount];
            for (int i = 0; i < ingredientsCount; i++)
            {
                CraftingRecipeIngredient ingredient = new CraftingRecipeIngredient();
                ingredient.FromBytes(reader, resolver);
                ingredient.Resolve(resolver, "Voxel recipes FromBytes", this);
                this.Ingredients[i] = ingredient;
            }
            int len = reader.ReadInt32();
            this.Pattern = new string[len][];
            for (int j = 0; j < this.Pattern.Length; j++)
            {
                this.Pattern[j] = reader.ReadStringArray();
            }
            base.Name = new AssetLocation(reader.ReadString());
            this.Output = new JsonItemStack();
            this.RecipeOutput.FromBytes(reader, resolver);
            this.RecipeOutput.Resolve(resolver, "[Voxel recipe FromBytes] " + this.Ingredient.Code);
            if (reader.ReadBoolean())
            {
                
                Output.ResolvedItemstack.FromBytes(reader);
                var c1 = Output.ResolvedItemstack.Attributes.Clone();
                Output.Resolve(resolver, "[Voxel recipe FromBytes]", base.Ingredient.Code);
                Output.ResolvedItemstack.Attributes = c1;
            }
            /*if(reader.ReadBoolean())
            {
                var c1 = Output.ResolvedItemstack.Attributes.Clone();
                Output.Resolve(resolver, "[Voxel recipe FromBytes]", base.Ingredient.Code);

                Output.ResolvedItemstack.Attributes = c1;
            }*/


            this.GenVoxels();
            return;

            base.Ingredient = new CraftingRecipeIngredient();
            RecipeId = reader.ReadInt32();
            base.Ingredient.FromBytes(reader, resolver);
            int num = reader.ReadInt32();
            Pattern = new string[num][];
            for (int i = 0; i < Pattern.Length; i++)
            {
                Pattern[i] = reader.ReadStringArray();
            }

            base.Name = new AssetLocation(reader.ReadString());
            Output = new JsonItemStack();
            Output.FromBytes(reader, resolver.ClassRegistry);
            //Output.Attributes = new JsonObject(Output.ResolvedItemstack.Attributes.Clone().ToJsonToken());
            var c = Output.ResolvedItemstack.Attributes.Clone();
            Output.Resolve(resolver, "[Voxel recipe FromBytes]", base.Ingredient.Code);
            
            Output.ResolvedItemstack.Attributes = c;
           /* if (Output.Attributes. CANJWConstants.CUTTING_TYPE))
            {
                Output.ResolvedItemstack.Attributes.SetString(CANJWConstants.CUTTING_TYPE, Output.Attributes[CANJWConstants.CUTTING_TYPE].ToString());
            }*/
            GenVoxels();
            //base.FromBytes(reader, resolver);
        }
        public override bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
        {
            var c = base.Resolve(world, sourceForErrorLogging);
            return c;
            if(this.Ingredient.ResolvedItemStack == null)
            {
                this.Ingredient.Resolve(world, sourceForErrorLogging);
            }
            if (this.Output == null)
            {
                ILogger logger = world.Logger;
                DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(39, 1);
                defaultInterpolatedStringHandler.AppendLiteral("Grid Recipe '");
                defaultInterpolatedStringHandler.AppendFormatted<AssetLocation>(base.Name);
                defaultInterpolatedStringHandler.AppendLiteral("' has no output specified.");
                logger.Error(defaultInterpolatedStringHandler.ToStringAndClear());
                return false;
            }

            if (this.Ingredients == null)
            {
                ILogger logger3 = world.Logger;
                DefaultInterpolatedStringHandler defaultInterpolatedStringHandler3 = new DefaultInterpolatedStringHandler(46, 1);
                defaultInterpolatedStringHandler3.AppendLiteral("Grid Recipe with output '");
                defaultInterpolatedStringHandler3.AppendFormatted<AssetLocation>(this.Output.Code);
                defaultInterpolatedStringHandler3.AppendLiteral("' has no ingredients.");
                logger3.Error(defaultInterpolatedStringHandler3.ToStringAndClear());
                return false;
            }
          
            if (!this.Output.Resolve(world, "Grid recipe"))
            {
                ILogger logger7 = world.Logger;
                DefaultInterpolatedStringHandler defaultInterpolatedStringHandler7 = new DefaultInterpolatedStringHandler(43, 2);
                defaultInterpolatedStringHandler7.AppendLiteral("Grid Recipe '");
                defaultInterpolatedStringHandler7.AppendFormatted<AssetLocation>(base.Name);
                defaultInterpolatedStringHandler7.AppendLiteral("': Output ");
                defaultInterpolatedStringHandler7.AppendFormatted<AssetLocation>(this.Output.Code);
                defaultInterpolatedStringHandler7.AppendLiteral(" cannot be resolved.");
                logger7.Error(defaultInterpolatedStringHandler7.ToStringAndClear());
                return false;
            }
            return true;
        }
    }
}
