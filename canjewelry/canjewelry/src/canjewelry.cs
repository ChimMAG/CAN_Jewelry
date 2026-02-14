using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using canjewelry.src.bb;
using canjewelry.src.be;
using canjewelry.src.blocks;
using canjewelry.src.cb;
using canjewelry.src.CB;
using canjewelry.src.eb;
using canjewelry.src.harmony;
using canjewelry.src.inventories;
using canjewelry.src.items;
using canjewelry.src.items.resource;
using canjewelry.src.jewelry;
using canjewelry.src.utils;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.Server;

namespace canjewelry.src
{
    /// <summary>
    /// Main ModSystem class for the CAN Jewelry mod.
    /// Responsible for registering items, blocks, behaviors,
    /// loading configuration, handling networking,
    /// and applying Harmony patches.
    /// </summary>
    public class canjewelry: ModSystem
    {
        /// <summary>
        /// Harmony instance used for runtime patching.
        /// </summary>
        public Harmony harmonyInstance;

        /// <summary>
        /// Unique Harmony ID for this mod.
        /// </summary>
        public const string harmonyID = "canjewelry.Patches";

        /// <summary>
        /// Client-side API reference.
        /// </summary>
        public static ICoreClientAPI capi;

        /// <summary>
        /// Server-side API reference.
        /// </summary>
        public static ICoreServerAPI sapi;

        /// <summary>
        /// Server network channel for config synchronization.
        /// </summary>
        internal static IServerNetworkChannel serverChannel;

        /// <summary>
        /// Client network channel for config synchronization.
        /// </summary>
        internal static IClientNetworkChannel clientChannel;

        /// <summary>
        /// Global mod configuration.
        /// </summary>
        public static Config config;

        /// <summary>
        /// Map of gem type → texture path.
        /// </summary>
        public static Dictionary<string, string> gems_textures = new();

        /// <summary>
        /// Map of gem type → PNG texture path.
        /// </summary>
        public static Dictionary<string, string> gems_textures_pngs;

        /// <summary>
        /// Loaded gem cutting recipes.
        /// </summary>
        public static List<GemCuttingRecipe> gemCuttingRecipes;
        /// <summary>
        /// Loaded wearable restrictions by name.
        /// </summary>
        private readonly Dictionary<string, RestrictionData> restrictions = [];
        /// <summary>
        /// Model transformations associated with restrictions.
        /// </summary>
        private readonly Dictionary<string, Dictionary<string, ModelTransform>> transformations = [];

        /// <summary>
        /// Executed before all mods are started.
        /// Adds an additional jewelry inventory to player defaults.
        /// </summary>
        public override void StartPre(ICoreAPI api)
        {
            PlayerInventoryManager.defaultInventories = PlayerInventoryManager.defaultInventories.Append("additionaljewelrycharacter");
            base.StartPre(api);
        }

        /// <summary>
        /// Called on both client and server.
        /// Registers blocks, items, behaviors, entities,
        /// and applies core Harmony patches.
        /// </summary>
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            harmonyInstance = new Harmony(harmonyID);
            var p = harmonyInstance.GetPatchedMethods();
            if(p.All(it => it.Name != "GetMaxDurability"))
            {
                harmonyInstance.Patch(typeof(Vintagestory.API.Common.CollectibleObject).GetMethod("GetMaxDurability"), postfix: new HarmonyMethod(typeof(harmPatch).GetMethod("Postfix_CollectibleObject_GetMaxDurability")));
            }
            
            api.RegisterBlockClass("JewelerSetBlock", typeof(JewelerSetBlock));
            api.RegisterBlockEntityClass("JewelerSetBE", typeof(JewelerSetBE));

            api.RegisterCollectibleBehaviorClass("Encrustable", typeof(EncrustableCB));
            api.RegisterCollectibleBehaviorClass("GemCuttableCB", typeof(CANGemCuttableCB));

            api.RegisterBlockBehaviorClass("CANTemporalGraspBB", typeof(CANTemporalGraspBB));

            api.RegisterBlockClass("BlockJewelGrinder", typeof(BlockJewelGrinder));
            api.RegisterBlockClass("BlockCANWireDrawingBench", typeof(CANWireDrawingBench));
            api.RegisterBlockEntityClass("BEJewelGrinder", typeof(BEJewelGrinder));
            api.RegisterBlockEntityClass("BEWireDrawingBench", typeof(CANBEWireDrawingBench));
            api.RegisterBlockEntityClass("BEGemCuttingTable", typeof(BlockEntityGemCuttingTable));

            api.RegisterItemClass("GrindLayerBlock", typeof(GrindLayerBlock));

            api.RegisterItemClass("ProcessedGem", typeof(ProcessedGem));
            api.RegisterItemClass("CANCutGemItem", typeof(CANCutGemItem));
            api.RegisterItemClass("CANRoughGemItem", typeof(CANRoughGemItem));
            
            api.RegisterItemClass("CANItemSimpleNecklace", typeof(CANItemSimpleNecklace));
            api.RegisterItemClass("CANItemTiara", typeof(CANItemTiara));
            api.RegisterItemClass("CANItemRottenKingMask", typeof(CANItemRottenKingMask));
            api.RegisterItemClass("CANItemCoronet", typeof(CANItemCoronet));
            api.RegisterItemClass("CANItemMonocle", typeof(CANItemMonocle));
            api.RegisterItemClass("CANItemWireHank", typeof(CANItemWireHank));
            api.RegisterItemClass("CANItemArmBand", typeof(CANItemArmBand));
            api.RegisterItemClass("CANItemStrap", typeof(CANItemStrap));
            api.RegisterItemClass("CANItemSocket", typeof(CANItemSocket));
            api.RegisterItemClass("CANItemGemCuttingWorkItem", typeof(CANItemGemCuttingWorkItem));
            api.RegisterItemClass("CANItemGemChisel", typeof(CANItemGemChisel));
            api.RegisterItemClass("CANItemHorusEye", typeof(CANItemHorusEye));
            api.RegisterItemClass("CANItemNoseRing", typeof(CANItemNoseRing));
            api.RegisterItemClass("CANItemEarrings", typeof(CANItemEarrings));
            api.RegisterItemClass("CANItemNadiyanNecklace", typeof(CANItemNadiyanNecklace));
            api.RegisterItemClass("CANItemGlasses", typeof(CANItemGlasses));
            api.RegisterItemClass("CANItemRing", typeof(CANItemRing));

            api.RegisterBlockClass("CANBlockPan", typeof(CANBlockPan));
            api.RegisterBlockClass("BlockGemCuttingTable", typeof(BlockGemCuttingTable));
            api.RegisterEntityBehaviorClass("playeradditionaljewelryinventory", typeof(EntityBehaviorAdditionalJewelryPlayerInventory));

            api.RegisterBlockClass("CANBlockPSContainer", typeof(CANBasePSContainer));
            api.RegisterBlockEntityClass("CANBENecklaceStand", typeof(CANBENecklaceStand));
            api.RegisterBlockEntityClass("CANBEHeadStand", typeof(CANBEHeadStand));
        }
        /// <summary>
        /// Client-side initialization.
        /// Handles GUI icons, client patches, config sync,
        /// and visual gem data.
        /// </summary>
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;
            loadConfig(capi);
            AddCustomIcons();
            ClientPatcher.ApplyPatches(capi, harmonyID, ref harmonyInstance);

            clientChannel = api.Network.RegisterChannel("canjewelry");
            clientChannel.RegisterMessageType(typeof(SyncCANJewelryPacket));
            clientChannel.SetMessageHandler<SyncCANJewelryPacket>((packet) =>
            {
                config = JsonConvert.DeserializeObject<Config>(packet.CompressedConfig);
                AddBehaviorAndSocketNumber(false);
            });

            //Set colors of processed gems on jewel grinder
            Item[] arrayResult = api.World.SearchItems(new AssetLocation("canjewelry:gem-cut-*"));
            foreach(var gem in arrayResult)
            {
                string gemType = gem.Code.Path.Split('-').Last();
                if(!BEJewelGrinder.gemTypeToColor.TryGetValue(gemType, out _))
                {
                    int color = gem.GetRandomColor(capi, null);
                    BEJewelGrinder.gemTypeToColor[gemType] = color;
                }
            }
            ClientMain.ClassRegistry.RegisterInventoryClass("additionaljewelrycharacter", typeof(InventoryCharacterAdditionalJewelry));
            api.Event.PlayerJoin += (IClientPlayer byPlayer) =>
            {
                if (byPlayer != null && capi.World.Player != null && byPlayer == capi.World.Player)
                {
                    if (clientChannel.Connected)
                    {
                        clientChannel.SendPacket(new SyncCANJewelryPacket()
                        {
                            CompressedConfig = ""
                        });
                    }
                    else
                    {
                        canjewelry.capi.Event.RegisterCallback((dt =>
                        {
                            if (clientChannel.Connected)
                            {
                                clientChannel.SendPacket(new SyncCANJewelryPacket()
                                {
                                    CompressedConfig = ""
                                });
                            }
                        }
                        ), 60 * 1000);
                    }
                }
            };
            
        }
        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);

            if (api.Side == EnumAppSide.Server)
            {
                var restrictionGroupsServer = DiscoverRestrictionGroups(api);
                LoadData(api, restrictionGroupsServer);
            }
            CANItemWearable.NotVisTexture = new AssetLocation("canjewelry:item/gem/notvis.png");
        }
        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);
            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                foreach (var restriction in restrictions)
                {
                    transformations.TryGetValue(restriction.Key, out var transformation);
                    RestrictionsPatches.PatchCollectibleWhitelist(obj, restriction, transformation);
                }
            }
            Item[] cut_gems_items = api.World.SearchItems(new AssetLocation("canjewelry:gem-cut-*"));
            gems_textures = new();
            gems_textures_pngs = new();
            foreach (var gem in cut_gems_items)
            {
                //catch if not present?
                gems_textures.TryAdd(gem.Code.Path.Split('-').Last(), gem.Textures["gem"].Base.Domain + ":textures/" + gem.Textures["gem"].Base.Path);
                gems_textures_pngs.TryAdd(gem.Code.Path.Split('-').Last(), gem.Textures["gem"].Base.Domain + ":" + gem.Textures["gem"].Base.Path + ".png");
            }
            
        }
        private Dictionary<string, string[]> DiscoverRestrictionGroups(ICoreAPI api)
        {
            var restrictionGroups = new Dictionary<string, string[]>();
            string basePath = "config/restrictions/";

            var restrictionAssets = api.Assets.GetMany("config/restrictions", "canjewelry", false);

            foreach (var asset in restrictionAssets)
            {
                string fullPath = asset.Location.Path;

                string relativePath = fullPath[basePath.Length..];
                string[] pathParts = relativePath.Split('/');

                if (pathParts.Length >= 2)
                {
                    string folderName = pathParts[0];
                    string fileName = pathParts[1];

                    if (fileName.EndsWith(".json"))
                    {
                        fileName = fileName[..^5];
                    }

                    if (!restrictionGroups.TryGetValue(folderName, out string[] value))
                    {
                        value = [];
                        restrictionGroups[folderName] = value;
                    }

                    var currentFiles = value.ToList();
                    if (!currentFiles.Contains(fileName))
                    {
                        currentFiles.Add(fileName);
                        restrictionGroups[folderName] = [.. currentFiles];
                    }
                }
            }

            // Remove folders that have no files
            var foldersToRemove = restrictionGroups.Where(kvp => kvp.Value.Length == 0).Select(kvp => kvp.Key).ToList();
            foreach (var folder in foldersToRemove)
            {
                restrictionGroups.Remove(folder);
            }

            return restrictionGroups;
        }
        private void LoadData(ICoreAPI api, Dictionary<string, string[]> restrictionGroups)
        {
            foreach (var (category, names) in restrictionGroups)
            {
                foreach (var name in names)
                {
                    string restrictionPath = $"canjewelry:config/restrictions/{category}/{name}.json".Replace("//", "/");
                    string transformationPath = $"canjewelry:config/transformations/{category}/{name}.json".Replace("//", "/");

                    restrictions[name] = api.LoadAsset<RestrictionData>(restrictionPath);

                    if (api.Assets.Exists(transformationPath))
                    {
                        transformations[name] = api.LoadAsset<Dictionary<string, ModelTransform>>(transformationPath);
                    }
                }
            }
        }
        public void AddCustomIcons()
        {
            List<string> iconList = new List<string> { "nose-side", "drop-earrings", "eye-left", "eye-right", "nose",
            "earrings-right", "earrings-left", "palm-right", "palm-left"};
            foreach (var icon in iconList)
            {
                capi.Gui.Icons.CustomIcons["canjewelry:" + icon] = delegate (Context ctx, int x, int y, float w, float h, double[] rgba)
                {
                    AssetLocation location = new AssetLocation("canjewelry:textures/icons/" + icon + ".svg");
                    IAsset svgAsset = capi.Assets.TryGet(location, true);
                    int value = ColorUtil.ColorFromRgba(44, 44, 44, 204);
                    capi.Gui.DrawSvg(svgAsset, ctx.GetTarget() as ImageSurface, x, y, (int)w, (int)h, new int?(value));
                };
            }
        }
        /// <summary>
        /// Server-side initialization.
        /// Loads config, applies server patches,
        /// registers commands and networking,
        /// and injects custom drops.
        /// </summary>
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;
            loadConfig(sapi);
            ServerPatcher.ApplyPatches(api, harmonyID, ref harmonyInstance);
           
            
            config.InitColors();
            api.RegisterEntityBehaviorClass("cangembuffaffected", typeof(CANGemBuffAffected));
            
            serverChannel = sapi.Network.RegisterChannel("canjewelry");
            serverChannel.RegisterMessageType(typeof(SyncCANJewelryPacket));
            api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, () => AddBehaviorAndSocketNumber());
            api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
            commands.RegisterCommands.registerServerCommands(sapi);

            serverChannel.SetMessageHandler<SyncCANJewelryPacket>((player, packet) =>
            {
                sendNewValues(player);
            });
            foreach (var it in config.gems_drops_table)
            {
                Block[] found_blocks = api.World.SearchBlocks(new AssetLocation(it.Key));
                foreach (var block in found_blocks)
                {
                    List<BlockDropItemStack> blockDropsToAdd = new List<BlockDropItemStack>();
                    foreach (var dropInfo in it.Value)
                    {
                        ItemStack itemStack;
                        if (dropInfo.TypeCollectable == EnumItemClass.Item)
                        {
                            Item item = sapi.World.GetItem(new AssetLocation(dropInfo.NameCollectable));
                            if (item == null)
                            {
                                sapi.Logger.VerboseDebug(dropInfo.NameCollectable + " not found.");
                                continue;
                            }
                            itemStack = new ItemStack(item);
                        }
                        else
                        {
                            itemStack = new ItemStack(sapi.World.GetBlock(new AssetLocation(dropInfo.NameCollectable)));
                        }
                        BlockDropItemStack additionalDrop = new BlockDropItemStack();
                        additionalDrop.Type = dropInfo.TypeCollectable;
                        additionalDrop.Code = itemStack.Collectible.Code;
                        additionalDrop.ResolvedItemstack = itemStack;
                        additionalDrop.Quantity.avg = dropInfo.avg;
                        additionalDrop.Quantity.var = dropInfo.var;
                        additionalDrop.LastDrop = dropInfo.LastDrop;
                        additionalDrop.DropModbyStat = null;
                        blockDropsToAdd.Add(additionalDrop);
                    }
                    block.Drops = block.Drops.Append(blockDropsToAdd.ToArray());
                }
            }
            ServerMain.ClassRegistry.RegisterInventoryClass("additionaljewelrycharacter", typeof(InventoryCharacterAdditionalJewelry));
        }
        public void OnPlayerNowPlaying(IServerPlayer byPlayer)
        {
            if (!canjewelry.config.TurnOffBuffs)
            {
                var plBeh = byPlayer.Entity.GetBehavior<CANGemBuffAffected>();
                if (plBeh != null)
                {
                    if (!plBeh.initialized)
                    {
                        plBeh.TryToAddSlotModified();
                    }
                }
            }
        }
        public override void Dispose()
        {
            base.Dispose();
            if (harmonyInstance != null)
            {
                harmonyInstance.UnpatchAll(harmonyID + "_client");
                harmonyInstance.UnpatchAll(harmonyID + "_server");
            }
            capi = null;
            sapi = null;
            serverChannel = null;
            clientChannel = null;
            config = null;
            gems_textures?.Clear();
            gems_textures = null;
            gems_textures_pngs?.Clear();
            gems_textures_pngs = null;
            gemCuttingRecipes = null;
            CANItemWearable.NotVisTexture = null;
        }
        /// <summary>
        /// Adds socket behavior, gem attributes, and custom variants
        /// to items based on configuration.
        /// Can be executed on both client and server.
        /// </summary>
        /// <param name="serverSide">
        /// True if executed on server, false if on client.
        /// </param>
        public void AddBehaviorAndSocketNumber(bool serverSide = true)
        {
            ICoreAPI api = capi;
            if (serverSide)
            {
                api = sapi;
            }

            foreach (var it in config.items_codes_with_socket_count_and_tiers)
            {
                if(it.Value == null || it.Value.Length == 0)
                {
                    continue;
                }
                Item[] arrayResult = api.World.SearchItems(new AssetLocation(it.Key));
                if(arrayResult.Length > 0) 
                {
                    
                    foreach (Item item in arrayResult)
                    {
                        if (!item.HasBehavior<EncrustableCB>())
                        {
                            item.CollectibleBehaviors = item.CollectibleBehaviors.Append(new EncrustableCB(item));
                        }
                        if(item.Attributes == null)
                        {
                            JToken jt = JToken.Parse("{}");
                            jt[CANJWConstants.SOCKETS_NUMBER_STRING] = it.Value.Length;
                            item.Attributes = new JsonObject(jt);
                            continue;
                        }

                        string s = JsonConvert.SerializeObject(it.Value);

                        //parse it for jarray
                        JToken k = JToken.Parse(s);
                        //set to item general attributes, accessible across all
                        item.Attributes.Token[CANJWConstants.SOCKETS_TIERS_STRING] = k;

                        item.Attributes.Token[CANJWConstants.SOCKETS_NUMBER_STRING] = it.Value.Length;

                        item.Attributes = new JsonObject(item.Attributes.Token);
                    }
                }
                else
                {
                    if (config.debugMode)
                    {
                        api.Logger.VerboseDebug(String.Format("[canjewelry] Item with \"{0}\" code not found", it.Key));
                    }
                }
            }         
            
            Item[] rough_gems_items = api.World.SearchItems(new AssetLocation("canjewelry:gem-rough-*"));
            Item[] rough_gems_other = api.World.SearchItems(new AssetLocation("game:gem-*-rough"));

            rough_gems_items = rough_gems_items.Append(rough_gems_other);
            foreach (var gem in rough_gems_items)
            {
                string code_item = gem.Code.Path.Split('-').Last();
                if(config.gem_type_to_buff.ContainsKey(code_item) )
                {
                    gem.Attributes.Token["canGemTypeToAttribute"] = config.gem_type_to_buff[code_item];
                }
            }

            foreach (var gem in rough_gems_items)
            {
                if (!gem.HasBehavior<CANGemCuttableCB>())
                {
                    gem.CollectibleBehaviors = gem.CollectibleBehaviors.Append(new CANGemCuttableCB(gem));
                }
            }


            Item[] cut_gems_items = api.World.SearchItems(new AssetLocation("canjewelry:gem-cut-*"));

            if (!serverSide)
            {
                foreach (var gem in cut_gems_items)
                {
                    //catch if not present?
                    gems_textures.TryAdd(gem.Code.Path.Split('-').Last(), gem.Textures["gem"].Base.Domain + ":textures/" + gem.Textures["gem"].Base.Path);
                    gems_textures_pngs.TryAdd(gem.Code.Path.Split('-').Last(), gem.Textures["gem"].Base.Domain + ":" + gem.Textures["gem"].Base.Path + ".png");
                }
            }
          
            foreach (var gem in cut_gems_items)
            {
                string code_item = gem.Code.Path.Split('-').Last();
                if (config.gem_type_to_buff.ContainsKey(code_item))
                {
                    gem.Attributes.Token["canGemTypeToAttribute"] = config.gem_type_to_buff[code_item];
                }
            }

            foreach(var it in config.custom_variants_sockets_tiers)
            {
                Item[] arrayResult = api.World.SearchItems(new AssetLocation(it.ItemCode));
                if (arrayResult.Length > 0)
                {
                    foreach (Item item in arrayResult)
                    {
                        if (!item.HasBehavior<EncrustableCB>())
                        {
                            item.CollectibleBehaviors = item.CollectibleBehaviors.Append(new EncrustableCB(item));
                        }
                        if (item.Attributes == null)
                        {
                            JToken jt = JToken.Parse("{}");
                            item.Attributes = new JsonObject(jt);
                        }


                        string s = JsonConvert.SerializeObject(it.SocketTiers);

                        //parse it for jarray
                        JToken k = JToken.Parse(s);

                        //set to item general attributes, accessible across all
                        item.Attributes.Token[CANJWConstants.CAN_CUSTOM_VARIANTS] = k;
                        item.Attributes.Token[CANJWConstants.CAN_CUSTOM_VARIANTS_COMPARE_KEY] = it.AttributeKey;
                    }
                }
                else
                {
                    if (config.debugMode)
                    {
                       // api.Logger.VerboseDebug(String.Format("[canjewelry] Item with \"{0}\" code not found", it.Key));
                    }
                }
            }

            if (config.TemporalGraspEnabled)
            {
                foreach (var it in config.TemporalGraspBlockList)
                {
                    Block[] arrayResult = api.World.SearchBlocks(new AssetLocation(it));
                    foreach (var itB in arrayResult)
                    {
                        if (!itB.HasBehavior<CANTemporalGraspBB>())
                        {
                            itB.BlockBehaviors = itB.BlockBehaviors.Append(new CANTemporalGraspBB(itB));
                        }
                    }
                }
            }
            foreach (var it in config.LevelOfSocketByType)
            {
                Item[] arrayResult = api.World.SearchItems(new AssetLocation(it.Key));
                if (arrayResult.Length > 0)
                {
                    foreach (Item item in arrayResult)
                    {
                        if (item.Attributes == null)
                        {
                            JToken jt = JToken.Parse("{}");
                            item.Attributes = new JsonObject(jt);
                        }


                        string s = JsonConvert.SerializeObject(it.Value);

                        //parse it for jarray
                        JToken k = JToken.Parse(s);

                        //set to item general attributes, accessible across all
                        item.Attributes.Token[CANJWConstants.LEVEL_OF_SOSCKET_STRING] = k;
                    }
                }
                else
                {
                    if (config.debugMode)
                    {
                        // api.Logger.VerboseDebug(String.Format("[canjewelry] Item with \"{0}\" code not found", it.Key));
                    }
                }
            }
        }
        /// <summary>
        /// Sends the current configuration to a client.
        /// </summary>
        /// <param name="byPlayer">Target player</param>
        public void sendNewValues(IServerPlayer byPlayer)
        {
            if (byPlayer.ConnectionState != EnumClientState.Offline)
            {
                serverChannel.SendPacket(new SyncCANJewelryPacket()
                {
                    CompressedConfig = JsonConvert.SerializeObject(config)
                },
                byPlayer);
            }
        }
        private void loadConfig(ICoreAPI api)
        {
            //Try to read old config
            OldConfig oldConfig = null;
            try
            {
                oldConfig = api.LoadModConfig<OldConfig>(this.Mod.Info.ModID + ".json");
            }
            catch (Exception)
            {

            }
            //old config was found and we just copy values from it
            if (oldConfig != null)
            {
                config = new Config();
                config.grindTimeOneTick = oldConfig.grindTimeOneTick.Val;
                config.buffNameToPossibleItem = oldConfig.buffNameToPossibleItem.Val;
                config.gems_buffs = oldConfig.gems_buffs.Val;
                config.items_codes_with_socket_count = oldConfig.items_codes_with_socket_count.Val;
                //make copy of the old config and new to old file
                try
                {
                    api.StoreModConfig<OldConfig>(oldConfig, this.Mod.Info.ModID + "_old.json");
                    api.StoreModConfig<Config>(config, this.Mod.Info.ModID + ".json");
                }
                catch(Exception)
                {

                }
                return;
            }
            //no old config, try to load new format
            else
            {
                //config = new Config();
                config = api.LoadModConfig<Config>(this.Mod.Info.ModID + ".json");
                if (config != null && config.items_codes_with_socket_count.Count != 1)
                {
                    foreach (var itemIter in config.items_codes_with_socket_count)
                    {
                        int[] tmp = new int[itemIter.Value].Fill(3);
                        config.items_codes_with_socket_count_and_tiers[itemIter.Key] = tmp;
                    }
                    config.items_codes_with_socket_count.Clear();
                    config.items_codes_with_socket_count["moved to items_codes_with_socket_count_and_tiers"] = 42;
                }
                if (config == null)
                { 
                    config = new Config();
                    config.FillDefaultValues();
                    config.config_version = api.ModLoader.Mods.FirstOrDefault(mod => mod.Info.ModID == "canjewelry")?.Info.Version ?? "0.0.0";
                    api.StoreModConfig<Config>(config, this.Mod.Info.ModID + ".json");
                    if (config.debugMode)
                    {
                        api.Logger.VerboseDebug("[canjewelry] " + this.Mod.Info.ModID + ".json" + " new config created and stored.");
                    }
                    return;
                }
                var currVersion = api.ModLoader.Mods.FirstOrDefault(mod => mod.Info.ModID == "canjewelry")?.Info.Version ?? "0.0.0";
                if(currVersion != null) 
                {
                    /*if(currVersion != config.config_version)
                    {
                        config.FillDefaultValues(true);
                        config.config_version = currVersion;
                    }*/
                    config.config_version = currVersion;
                }


                api.StoreModConfig<Config>(config, this.Mod.Info.ModID + ".json");
                if (config.debugMode)
                {
                    api.Logger.VerboseDebug("[canjewelry] " + this.Mod.Info.ModID + ".json" + "config read and stored back.");
                }
                return;
            }
        }      
    }
}
