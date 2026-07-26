using canjewelry.src.CB;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;

namespace canjewelry.src.jewelry
{
    public class GuiDialogJewelerSet : GuiDialogBlockEntity
    {
        GuiElementVerticalTabs groupOfInterests;
        public float Width { get; private set; }
        public float Height { get; private set; }
        int selectedDropSocket = 0;
        // Extraction can destroy the gem or the whole piece, so it is confirmed first. The dialog
        // is not immediate mode: setting these and rebuilding the composer is what shows the
        // confirmation, -1 meaning nothing is pending.
        int pendingExtractGem = -1;
        int pendingExtractSocket = -1;
        // Shared with the ImGui dialog: renders the piece into its own framebuffer, which
        // GuiElementItemPreview then paints. Created lazily so a dialog that never shows a piece
        // does not allocate a framebuffer.
        gui.JewelerItemPreview itemPreview;
        // Two columns: the preview sits alone on the left, everything else lives to the right of
        // WorkColumnX. Slot rows are then centred within their own column rather than the dialog.
        // 260 is about as large as the preview gets for free: JewelerItemPreview renders into a
        // 300px framebuffer, so beyond that it would start to soften.
        const int PreviewSize = 260;
        const int WorkColumnX = 290;
        public GuiDialogJewelerSet(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi) : base(dialogTitle, inventory, blockEntityPos, capi)
        {            
            if (IsDuplicate)
            {
                return;
            }
            this.Width = 640;
            this.Height = 500;
            capi.World.Player.InventoryManager.OpenInventory((IInventory)inventory);
            SetupDialog();
        }
        public void SetupDialog()
        {
            // Constant on purpose: the confirmation strip appears and disappears as buttons are
            // pressed, and sizing the dialog to its current contents made the whole window jump
            // around. The room it needs is simply always reserved.
            this.Height = 680;

            ElementBounds closeButton = ElementBounds.Fixed(1000, 30, 0, 0).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(10.0, 2.0);
            ElementBounds elementBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds backgroundBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithFixedSize(Width, Height - 90); ;
            backgroundBounds.BothSizing = ElementSizing.Fixed;
           
            backgroundBounds.WithChildren(new ElementBounds[]
            {
                    //closeButton
            });
            var jewelerComposer =  this.SingleComposer = this.Composers["jewelersetgui" + this.BlockEntityPosition?.ToString()] = this.capi.Gui.CreateCompo("jewelersetgui" + this.BlockEntityPosition?.ToString(), elementBounds).
                  AddShadedDialogBG(backgroundBounds).
                  AddDialogTitleBar(Lang.Get("canjewelry:jewelset_gui_name"), new Action(this.OnTitleBarClose))
                  .BeginChildElements(backgroundBounds);

            int chosenGroupTab = groupOfInterests == null ? 0 : groupOfInterests.ActiveElement;

            var scaledSlotSize = (48);
            var scaledOffset = (48 / 12);

            ElementBounds encrustetItemBounds = backgroundBounds.FlatCopy().WithFixedSize(this.Width - WorkColumnX, 60).WithFixedPosition(WorkColumnX, 30);
            ElementBounds slotB = ElementBounds.FixedSize(48, 48).WithAlignment(EnumDialogArea.CenterMiddle);
            encrustetItemBounds.WithChild(slotB);
            //ElementBounds.Fixed(-120.0 + backgroundBounds.fixedX, 45.0 + backgroundBounds.fixedY, 50, 100);

            int[] intArr1 = new int[1];
            intArr1[0] = 0;
            jewelerComposer.AddItemSlotGrid(this.Inventory, new Action<object>((this).DoSendPacket), intArr1.Length, intArr1, slotB, "socketsslots");

            AddItemPreview(jewelerComposer, encrustetItemBounds);

            backgroundBounds.WithChild(encrustetItemBounds);

            //jewelerComposer.AddInset(encrustetItemBounds);

            ElementBounds slotsEl = encrustetItemBounds.BelowCopy().WithFixedSize(encrustetItemBounds.fixedWidth, encrustetItemBounds.fixedHeight - 20);
            // Room for the slot plus two button rows ("+" and, for a filled socket, "-").
            slotsEl.fixedHeight += 68;
            //jewelerComposer.AddInset(slotsEl);
           // slotsEl.BothSizing = ElementSizing.FitToChildren;
            ItemStack encrustable = this.Inventory[0].Itemstack;
            int maxSocketNumber = EncrustableCB.GetMaxAmountSockets(encrustable);
            if (encrustable != null && maxSocketNumber > 0)
            {               
                int possibleSockets = maxSocketNumber;
                ElementBounds tmpEl = slotsEl.FlatCopy().WithFixedSize(scaledSlotSize, scaledSlotSize);
                double center = slotsEl.fixedX + slotsEl.fixedWidth / 2;
                double centerSlot = center - (possibleSockets % 2 == 1 ? scaledSlotSize / 2: 0);
                double startSlot = centerSlot - (((int)(possibleSockets / 2)) * scaledSlotSize);
                tmpEl.fixedX = startSlot;

                var tree = encrustable.Attributes.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);

                for (int i = 0; i < possibleSockets; i++)
                {
                    if (tree != null && tree.HasAttribute("slot" + i))
                    {
                        string gemType = tree.GetTreeAttribute("slot" + i).GetString(CANJWConstants.GEM_TYPE_IN_SOCKET);
                        if(!(gemType == ""))
                        {
                            int gemSizeInt = tree.GetTreeAttribute("slot" + i).GetInt(CANJWConstants.ENCRUSTED_GEM_SIZE);
                            ITreeAttribute imaginaryGemTree = tree.GetTreeAttribute("slot" + i);
                            string gemSize = "normal";
                            if (gemSizeInt == 3)
                            {
                                gemSize = "exquisite";
                            }
                            else if(gemSizeInt == 2)
                            {
                                gemSize = "flawless";
                            }

                            var elGem = tmpEl.FlatCopy();
                            elGem.fixedY -= 48;
                            elGem.fixedX += 20;
                            elGem.fixedY += 20;
                            var bucketSatck = new ItemStack(capi.World.GetItem(new AssetLocation("canjewelry:gem-cut-" + gemSize + "-" + gemType)), 1);

                            if (imaginaryGemTree.HasAttribute(CANJWConstants.ENCRUSTABLE_BUFFS_NAMES) && imaginaryGemTree.HasAttribute(CANJWConstants.ENCRUSTABLE_BUFFS_VALUES))
                            {
                                var imaginaryGemTreeNew = new TreeAttribute();
                                string[] currentBuffNames = (imaginaryGemTree[CANJWConstants.ENCRUSTABLE_BUFFS_NAMES] as StringArrayAttribute)?.value;
                                float[] currentBuffValues = (imaginaryGemTree[CANJWConstants.ENCRUSTABLE_BUFFS_VALUES] as FloatArrayAttribute)?.value;
                                imaginaryGemTreeNew[CANJWConstants.ENCRUSTABLE_BUFFS_NAMES] = new StringArrayAttribute(currentBuffNames);
                                imaginaryGemTreeNew[CANJWConstants.ENCRUSTABLE_BUFFS_VALUES] = new FloatArrayAttribute(currentBuffValues);
                                imaginaryGemTreeNew.SetString(CANJWConstants.CUTTING_TYPE, imaginaryGemTree.GetString(CANJWConstants.CUTTING_TYPE, CANJWConstants.CUTTING_ROUND));
                                bucketSatck.Attributes[CANJWConstants.CUT_GEM_TREE] = imaginaryGemTreeNew;
                             }
                            var sli = new SlideshowItemstackTextComponent(capi, new ItemStack[] { bucketSatck }, 48, EnumFloat.Inline);
                            var rc = new RichTextComponentBase[] { sli };
                            tmpEl.fixedX += 20;
                            tmpEl.fixedY += 20;
                            if (bucketSatck != null)
                            {
                                SingleComposer.AddRichtext(rc, elGem, "gem_slot" + i);
                            }
                            //jewelerComposer.AddInset(tmpEl);
                            tmpEl = tmpEl.FlatCopy();
                            tmpEl.fixedX -= 20;
                            tmpEl.fixedY -= 20;

                            // Pull the gem back out. Asks first - it can break the piece. Sits on
                            // its own line under the "+" so both stay full slot width.
                            ElementBounds removeGemEl = ElementBounds.FixedSize(48, 24);
                            removeGemEl.fixedX = tmpEl.fixedX;
                            removeGemEl.fixedY = tmpEl.fixedY + tmpEl.fixedHeight + 32;
                            int gemToRemove = i;
                            jewelerComposer.AddSmallButton(Lang.Get("-"),
                               new ActionConsumable(() =>
                               {
                                   pendingExtractGem = gemToRemove;
                                   pendingExtractSocket = -1;
                                   this.capi.Event.EnqueueMainThreadTask(new Action(this.SetupDialog), "setupjewelersetdlg");
                                   return true;
                               }),
                               removeGemEl, EnumButtonStyle.Normal, "removegem" + i);
                        }
                        //jewelerComposer.AddInset(tmpEl);
                        int[] intArr = new int[1];
                        intArr[0] = i + 1;
                        // Green or red backdrop tells at a glance whether the gem lying in the
                        // slot can actually go into this socket, before the "+" is pressed.
                        this.Inventory[i + 1].HexBackgroundColor =
                            IsGemCompatible(this.Inventory[i + 1].Itemstack, encrustable, SocketTierAt(encrustable, i))
                                ? "#2FE147" : (this.Inventory[i + 1].Itemstack == null ? null : "#F03330");
                        jewelerComposer.AddItemSlotGrid(this.Inventory, new Action<object>(this.SendInvPacket), intArr.Length, intArr, tmpEl, "gemslot" + i);

                        ElementBounds buttonEl = ElementBounds.FixedSize(48, 24);
                        buttonEl.fixedX = tmpEl.fixedX;
                        buttonEl.fixedY = tmpEl.fixedY + tmpEl.fixedHeight + 4;
                        var elll = ElementBounds.FixedSize(40, 40);

                        int tmpI = i;
                        jewelerComposer.AddSmallButton(Lang.Get("+"),
                           new ActionConsumable(() =>
                           {
                               OnClickButtonAddGem(tmpI, tmpI + 1);
                               return true;
                           }),
                           buttonEl);
                    }
                    



                    tmpEl = tmpEl.FlatCopy();
                    tmpEl.fixedX += scaledSlotSize + scaledOffset * 2;
                }
                
                
            }

            ElementBounds socketsEl = slotsEl.BelowCopy().WithFixedSize(encrustetItemBounds.fixedWidth, encrustetItemBounds.fixedHeight);
            socketsEl.fixedY -= 20;
            socketsEl.fixedHeight += 40;
            //this.Composers["jewelersetgui" + this.BlockEntityPosition?.ToString()].AddInset(socketsEl);

            if (encrustable != null && maxSocketNumber > 0)
            {
                int possibleSockets = maxSocketNumber;
                ElementBounds tmpEl = socketsEl.FlatCopy().WithFixedSize(scaledSlotSize, scaledSlotSize);
                double center = slotsEl.fixedX + slotsEl.fixedWidth / 2;
                double centerSlot = center - (possibleSockets % 2 == 1 ? scaledSlotSize / 2 : 0);
                double startSlot = centerSlot - (((int)(possibleSockets / 2)) * scaledSlotSize);
                tmpEl.fixedX = startSlot;

                var tree = encrustable.Attributes.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);

                int[] tiersList = tiersList = EncrustableCB.GetSocketsTiers(encrustable);
                   //encrustable.Collectible.Attributes[CANJWConstants.SOCKETS_TIERS_STRING].AsArray();
                string green = "#2FE147";
                string blue = "#2B3FF7";
                string purple = "#9214C9";
                for (int i = 0; i < possibleSockets; i++)
                {
                    if (tree != null && tree.HasAttribute("slot" + i))
                    {

                        int socketTier = tree.GetTreeAttribute("slot" + i).GetInt(CANJWConstants.ADDED_SOCKET_TYPE);

                        string socket_type_str = "canjewelry:cansocket-tinbronze";

                        foreach(var it in canjewelry.config.LevelOfSocketByType)
                        {
                            if(it.Value == socketTier)
                            {
                                socket_type_str = it.Key;
                            }
                        }
                        var bucketSatck = new ItemStack(capi.World.GetItem(new AssetLocation(socket_type_str)), 1);
                        var sli = new SlideshowItemstackTextComponent(capi, new ItemStack[] { bucketSatck }, 48, EnumFloat.Inline);
                        var rc = new RichTextComponentBase[] { sli };
                        tmpEl.fixedX += 20;
                        tmpEl.fixedY += 20;
                        if (bucketSatck != null)
                        {
                            SingleComposer.AddRichtext(rc, tmpEl, "socket_slot" + i);
                        }
                        //jewelerComposer.AddInset(tmpEl);
                        tmpEl = tmpEl.FlatCopy();
                        tmpEl.fixedX -= 20;
                        tmpEl.fixedY -= 20;

                        // Taking the socket out is optional and can be disabled server side.
                        if (canjewelry.config.canExtractSocket)
                        {
                            ElementBounds removeSocketEl = ElementBounds.FixedSize(48, 24);
                            removeSocketEl.fixedX = tmpEl.fixedX;
                            removeSocketEl.fixedY = tmpEl.fixedY + tmpEl.fixedHeight + 4;
                            int socketToRemove = i;
                            jewelerComposer.AddSmallButton(Lang.Get("-"),
                               new ActionConsumable(() =>
                               {
                                   pendingExtractSocket = socketToRemove;
                                   pendingExtractGem = -1;
                                   this.capi.Event.EnqueueMainThreadTask(new Action(this.SetupDialog), "setupjewelersetdlg");
                                   return true;
                               }),
                               removeSocketEl, EnumButtonStyle.Normal, "removesocket" + i);
                        }

                        tmpEl.fixedX += scaledSlotSize + scaledOffset * 2;
                        continue;
                    }
                    else
                    {
                        //this.Composers["jewelersetgui" + this.BlockEntityPosition?.ToString()].AddInset(tmpEl);
                        int[] intArr = new int[1];
                        intArr[0] = i + 5;
                        this.Composers["jewelersetgui" + this.BlockEntityPosition?.ToString()]
                        .AddItemSlotGrid((IInventory)this.Inventory, new Action<object>(((GuiDialogJewelerSet)this).SendInvPacket), intArr.Length, intArr, tmpEl, "socketsslot" + i);

                        ElementBounds buttonEl = ElementBounds.FixedSize(48, 24);
                        buttonEl.fixedX = tmpEl.fixedX;
                        buttonEl.fixedY = tmpEl.fixedY + tmpEl.fixedHeight + 4;
                        var elll = ElementBounds.FixedSize(40, 40);
                        if (tiersList != null)
                        {
                            int curTier = tiersList[i];
                            if (curTier == 1)
                            {
                                this.Inventory[i + 5].HexBackgroundColor = green;
                            }
                            else if (curTier == 2)
                            {
                                this.Inventory[i + 5].HexBackgroundColor = blue;
                            }
                            else if (curTier == 3)
                            {
                                this.Inventory[i + 5].HexBackgroundColor = purple;
                            }
                        }
                        else
                        {
                            this.Inventory[i + 5].HexBackgroundColor = green;
                        }
                        int tmpI = i;
                        this.Composers["jewelersetgui" + this.BlockEntityPosition?.ToString()].AddSmallButton(Lang.Get("+"),
                           new ActionConsumable(() =>
                           {
                               OnClickButtonAddSocket(tmpI, tmpI + 5);
                               return true;
                           }),
                           buttonEl);
                    }

                    tmpEl = tmpEl.FlatCopy();
                    tmpEl.fixedX += scaledSlotSize + scaledOffset * 2;
                }


            }

            AddInscriptionSection(jewelerComposer, encrustable);
            // The confirmation lives in the work column and no longer follows the inscription,
            // which moved under the preview on the left.
            AddExtractConfirm(jewelerComposer, InscriptionTop(socketsEl), encrustable);

            this.Composers["jewelersetgui" + this.BlockEntityPosition?.ToString()].Compose();
            return;
        }

        /// <summary>
        /// Rotatable preview of the piece being worked on, to the left of its slot. Skipped when
        /// the slot is empty - there would be nothing to render.
        /// </summary>
        private void AddItemPreview(GuiComposer composer, ElementBounds anchor)
        {
            if (this.Inventory[0].Itemstack == null) return;

            itemPreview ??= new gui.JewelerItemPreview(this.capi);

            ElementBounds previewEl = ElementBounds.FixedSize(PreviewSize, PreviewSize);
            previewEl.fixedX = (WorkColumnX - PreviewSize) / 2;
            previewEl.fixedY = anchor.fixedY;
            composer.AddInteractiveElement(new gui.GuiElementItemPreview(this.capi, previewEl, itemPreview), "itempreview");
        }

        // The preview owns a framebuffer, so it is rendered before the gui pass rather than from
        // inside the element - switching framebuffers mid-pass would disturb the dialog itself.
        public override void OnRenderGUI(float deltaTime)
        {
            itemPreview?.Render(this.Inventory[0].Itemstack);
            base.OnRenderGUI(deltaTime);
        }

        public override void Dispose()
        {
            base.Dispose();
            itemPreview?.Dispose();
            itemPreview = null;
        }

        /// <summary>
        /// Where the sections below the sockets start. The preview is not taken into account: it
        /// lives in its own column, so it can be taller than this without colliding. The 20px
        /// come off the padding the sockets block carries but does not use.
        /// </summary>
        private double InscriptionTop(ElementBounds socketsEl)
        {
            return socketsEl.fixedY + socketsEl.fixedHeight - 20;
        }

        /// <summary>Tier of the socket sitting in slot <paramref name="socketNum"/>, 0 when there is none.</summary>
        private static int SocketTierAt(ItemStack encrustable, int socketNum)
        {
            var tree = encrustable?.Attributes?.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);
            return tree?.GetTreeAttribute("slot" + socketNum)?.GetInt(CANJWConstants.ADDED_SOCKET_TYPE) ?? 0;
        }

        /// <summary>
        /// Whether the gem may be set into that socket: the socket has to be at least the gem's
        /// tier, the gem has to be cut, and the piece has to accept that gem type at all.
        /// </summary>
        private static bool IsGemCompatible(ItemStack gemStack, ItemStack jewelry, int socketTier)
        {
            if (gemStack?.Collectible == null || jewelry == null) return false;
            if (!(gemStack.Collectible.Attributes?["canGemType"].Exists ?? false)) return false;
            if (socketTier < gemStack.Collectible.Attributes["canGemType"].AsInt()) return false;
            if (gemStack.Attributes?.GetTreeAttribute(CANJWConstants.CUT_GEM_TREE) == null) return false;

            string gemType = gemStack.Collectible.Code.Path.Split('-').Last();
            return EncrustableCB.canItemContainThisGem(gemType, jewelry);
        }

        /// <summary>
        /// Engraving is permanent and one-off: once a piece carries an inscription the field is
        /// replaced by the text itself. A companion mod can veto engraving through CanInscribe,
        /// which is why the event is asked rather than assumed.
        /// </summary>
        private void AddInscriptionSection(GuiComposer composer, ItemStack encrustable)
        {
            if (encrustable == null) return;

            // Left column, right under the preview: the work column is busy with slots, and the
            // engraving belongs to the piece being shown rather than to any single socket.
            ElementBounds labelEl = ElementBounds.FixedSize(WorkColumnX - 20, 20);
            labelEl.fixedX = 10;
            labelEl.fixedY = 30 + PreviewSize + 16;

            string existing = encrustable.Attributes?.GetString(CANJWConstants.INSCRIPTION);
            if (!string.IsNullOrEmpty(existing))
            {
                // Header stays put, so an engraved piece does not just show a floating quote.
                composer.AddStaticText(Lang.Get("canjewelry:jewelerset-inscription"), CairoFont.WhiteSmallText(), labelEl, "inscriptionlabel");

                ElementBounds existingEl = labelEl.FlatCopy();
                existingEl.fixedY = labelEl.fixedY + labelEl.fixedHeight + 4;
                composer.AddStaticText("\"" + existing + "\"", CairoFont.WhiteDetailText(), existingEl, "inscriptiontext");
                return;
            }

            var canInscribeEvent = new src.integration.CanInscribeEvent { Jewelry = encrustable };
            canjewelry.Instance?.FireCanInscribe(canInscribeEvent);
            if (!canInscribeEvent.Allowed)
            {
                composer.AddStaticText(Lang.Get("canjewelry:jewelerset-inscribe-locked"), CairoFont.WhiteSmallText(), labelEl, "inscriptionlocked");
                return;
            }

            composer.AddStaticText(Lang.Get("canjewelry:jewelerset-inscription"), CairoFont.WhiteSmallText(), labelEl, "inscriptionlabel");

            // Read off before the inset is added: ForkBoundingParent re-parents the input and
            // makes its fixedX/fixedY relative to that new frame, so they cannot be used after.
            double rowY = labelEl.fixedY + labelEl.fixedHeight + 4;

            ElementBounds inputEl = ElementBounds.FixedSize(WorkColumnX - 24, 26);
            inputEl.fixedX = 10;
            inputEl.fixedY = rowY;
            // The text input draws no background of its own, so it is invisible without an inset.
            composer.AddInset(inputEl.ForkBoundingParent(2, 2, 2, 2), 2);
            composer.AddTextInput(inputEl, null, CairoFont.WhiteSmallText(), "inscriptioninput");

            // Under the field rather than beside it - the left column is too narrow for both.
            ElementBounds inscribeEl = ElementBounds.FixedSize(130, 26);
            inscribeEl.fixedX = 10;
            inscribeEl.fixedY = rowY + 34;
            composer.AddSmallButton(Lang.Get("canjewelry:jewelerset-inscribe-btn"),
                new ActionConsumable(() =>
                {
                    string text = SingleComposer?.GetTextInput("inscriptioninput")?.GetText();
                    if (string.IsNullOrWhiteSpace(text)) return true;
                    SendInscribe(text);
                    return true;
                }), inscribeEl, EnumButtonStyle.Normal, "inscribebtn");
        }

        /// <summary>
        /// Confirmation strip for a pending extraction, with the odds taken from the config so the
        /// player sees what is being risked. Drawn below the sockets and only while something is
        /// pending - the composer is rebuilt whenever that changes.
        /// </summary>
        private void AddExtractConfirm(GuiComposer composer, double top, ItemStack encrustable)
        {
            if (pendingExtractGem < 0 && pendingExtractSocket < 0) return;

            bool removingGem = pendingExtractGem >= 0;
            string name = encrustable?.GetName() ?? "";
            string question = removingGem
                ? Lang.Get("canjewelry:jewelerset-extract-confirm-body", name,
                    (int)(canjewelry.config.gemExtractionReturnChance * 100f),
                    (int)(canjewelry.config.jewelryBreakOnExtractionChance * 100f))
                : Lang.Get("canjewelry:jewelerset-remove-socket-confirm-body", name,
                    (int)(canjewelry.config.socketExtractionReturnChance * 100f));

            // The warning wraps to a different number of lines depending on the item's name and
            // the translation, so its height is measured rather than guessed - guessing is what
            // made the buttons land on top of the last line.
            CairoFont font = CairoFont.WhiteSmallText();
            double textWidth = this.Width - WorkColumnX - 20;
            double textHeight = this.capi.Gui.Text.GetMultilineTextHeight(font, question, textWidth, EnumLinebreakBehavior.Default)
                                / RuntimeEnv.GUIScale;

            ElementBounds textEl = ElementBounds.FixedSize(textWidth, textHeight + 6);
            textEl.fixedX = WorkColumnX;
            textEl.fixedY = top;
            composer.AddStaticText(question, font, textEl, "extractconfirmtext");

            ElementBounds yesEl = ElementBounds.FixedSize(150, 26);
            yesEl.fixedX = WorkColumnX;
            yesEl.fixedY = textEl.fixedY + textEl.fixedHeight + 8;
            composer.AddSmallButton(Lang.Get(removingGem
                    ? "canjewelry:jewelerset-extract-confirm-yes"
                    : "canjewelry:jewelerset-remove-socket"),
                new ActionConsumable(() =>
                {
                    if (pendingExtractGem >= 0) SendExtractGem(pendingExtractGem);
                    else SendExtractSocket(pendingExtractSocket);
                    ClearPendingExtract();
                    return true;
                }), yesEl, EnumButtonStyle.Normal, "extractconfirmyes");

            ElementBounds noEl = yesEl.FlatCopy();
            noEl.fixedX = WorkColumnX + 162;
            noEl.fixedWidth = 110;
            composer.AddSmallButton(Lang.Get("canjewelry:jewelerset-extract-confirm-no"),
                new ActionConsumable(() =>
                {
                    ClearPendingExtract();
                    return true;
                }), noEl, EnumButtonStyle.Normal, "extractconfirmno");
        }

        private void ClearPendingExtract()
        {
            pendingExtractGem = -1;
            pendingExtractSocket = -1;
            this.capi.Event.EnqueueMainThreadTask(new Action(this.SetupDialog), "setupjewelersetdlg");
        }
        private void didSelectEntity(string code, bool selected)
        {
            if (selected)
            {
                if(int.TryParse(code, out int res))
                {
                    selectedDropSocket = res;
                    SetupDialog();
                }
                else
                {
                    selectedDropSocket = 0;
                    SetupDialog();
                }
            }
            else
            {
                selectedDropSocket = 0;
            }

        }
        private string[] GetAvailableGemTypes(ItemStack itemStack)
        {
            string itemCode = itemStack.Collectible.Code.Path;
            List<string> res = new List<string>();
            foreach(var gemTypeSetPair in canjewelry.config.buffNameToPossibleItem)
            {
                foreach(var it in gemTypeSetPair.Value)
                {                 
                    if (WildcardUtil.Match("*" + it + "*", itemCode))
                    {
                        res.Add(gemTypeSetPair.Key);
                    }
                }
            }
            return res.ToArray();
        }
        public static int getStringLength(string name)
        {
            using (var skFont = new SKFont(SKTypeface.FromFamilyName("Times New Roman"), 24))
            {
                var skBounds = SKRect.Empty;
                return (int)skFont.MeasureText(name);
            }
        }
        public void ComposeAvailableGemTypesGui()
        {
            int maxSocketNumber = EncrustableCB.GetMaxAmountSockets(this.Inventory[0].Itemstack);
            if(this.Inventory[0].Itemstack == null || maxSocketNumber < 1)
            {
                this.Composers.Remove("jewelersetgui-types");
                return;
            }
            string[] availableGemTypes = GetAvailableGemTypes(this.Inventory[0].Itemstack);
            if(availableGemTypes.Length < 1)
            {
                return;
            }
            ElementBounds leftDlgBounds = this.Composers["jewelersetgui" + this.BlockEntityPosition?.ToString()].Bounds;
            double b = leftDlgBounds.InnerHeight / (double)RuntimeEnv.GUIScale + 10.0;

            ElementBounds bgBounds = ElementBounds.Fixed(0.0, 0.0,
                235, leftDlgBounds.InnerHeight / (double)RuntimeEnv.GUIScale - GuiStyle.ElementToDialogPadding - 20.0 + b).WithFixedPadding(GuiStyle.ElementToDialogPadding);
            ElementBounds dialogBounds = bgBounds.ForkBoundingParent(0.0, 0.0, 0.0, 0.0)
                .WithAlignment(EnumDialogArea.LeftMiddle)
                .WithFixedAlignmentOffset((leftDlgBounds.renderX + leftDlgBounds.OuterWidth + 10.0) / (double)RuntimeEnv.GUIScale,  0);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            dialogBounds.BothSizing = ElementSizing.FitToChildren;
            dialogBounds.WithChild(bgBounds);
            ElementBounds textBounds = ElementBounds.FixedPos(EnumDialogArea.LeftTop,
                                                               0,
                                                                0);
            bgBounds.WithChildren(textBounds);

            this.Composers["jewelersetgui-types"] = this.capi.Gui.CreateCompo("jewelersetgui-types", dialogBounds).AddShadedDialogBG(bgBounds, false, 5.0, 0.75f);
            int maxWidth = availableGemTypes.OrderByDescending(s => s.Length).FirstOrDefault()?.Length ?? 20;
            for(int i = 0; i < availableGemTypes.Length; i++)
            {
                ElementBounds el = textBounds.CopyOffsetedSibling().WithFixedHeight(20)
                    .WithFixedWidth(maxWidth * 8)
                    .WithFixedPosition(0, i * 20);
                bgBounds.WithChildren(el);

                this.Composers["jewelersetgui-types"].AddStaticText(availableGemTypes[i], CairoFont.WhiteDetailText(), el);
            }
            this.Composers["jewelersetgui-types"].Compose();
        }
        private void OnTitleBarClose() => this.TryClose();
        public override void OnGuiClosed()
        {
            this.capi.Network.SendPacketClient(this.capi.World.Player.InventoryManager.CloseInventory((IInventory)this.Inventory));
            base.Inventory.SlotModified -= this.OnInventorySlotModified;
            base.OnGuiClosed();
        }
        private void OnGroupTabClicked(int clicked)
        {
            this.groupOfInterests.ActiveElement = clicked;
            this.SetupDialog();
        }
        private void OnGroupTabClicked2(int arg1, GuiTab tab)
        {
            //this.
            this.groupOfInterests.ActiveElement = arg1;
            this.SetupDialog();
            /*string layerGroupCode = this.tabnames[arg1];
            if (tab.Active)
            {
                this.renderLayerGroups.Remove(layerGroupCode);
            }
            else
            {
                this.renderLayerGroups.Add(layerGroupCode);
            }
            foreach (MapLayer ml in this.MapLayers)
            {
                if (ml.LayerGroupCode == layerGroupCode)
                {
                    ml.Active = tab.Active;
                }
            }
            this.updateMaplayerExtrasState();*/
        }
        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            base.Inventory.SlotModified += this.OnInventorySlotModified;

        }
        private void OnInventorySlotModified(int slotid)
        {
            if (slotid == 0)
            {
                this.capi.Event.EnqueueMainThreadTask(new Action(this.SetupDialog), "setupjewelersetdlg");
                this.capi.Event.EnqueueMainThreadTask(new Action(this.ComposeAvailableGemTypesGui), "setupavailabletypesdlg");
                
            }
        }
        public bool onClickBackButtonPutSocket()
        {
            byte[] array;
            using (MemoryStream output = new MemoryStream())
            {
                BinaryWriter stream = new BinaryWriter((Stream)output);
                //stream.Write("BlockEntityCANMarket");
                // stream.Write("123");
                // stream.Write((byte)4);
                TreeAttribute tree = new TreeAttribute();
                tree.SetInt("selectedSocketSlot", selectedDropSocket);
                tree.ToBytes(stream);
                array = output.ToArray();
            }

            this.capi.Network.SendBlockEntityPacket(this.BlockEntityPosition, 1004, array);
            //this.chosenCommand = enumChosenCommand.NO_CHOSEN_COMMAND;
            // this.buildWindow();
            return true;
        }
        public bool onClickBackButtonPutGem()
        {
            this.capi.Network.SendBlockEntityPacket(this.BlockEntityPosition, 1005);
            //this.chosenCommand = enumChosenCommand.NO_CHOSEN_COMMAND;
            // this.buildWindow();
            return true;
        }

        public void OnClickButtonAddGem(int socketNum, int slotNum)
        {
            byte[] array;
            using (MemoryStream output = new MemoryStream())
            {
                BinaryWriter stream = new BinaryWriter((Stream)output);
                TreeAttribute tree = new TreeAttribute();
                //in which slot in item we want socket to be added
                tree.SetInt("selectedSocketSlot", socketNum);
                //which slot of inventory contains socket item to be added
                tree.SetInt("selectedSlotNum", slotNum);
                tree.ToBytes(stream);
                array = output.ToArray();
            }
            this.capi.Network.SendBlockEntityPacket(this.BlockEntityPosition, 1005, array);
        }

        public void OnClickButtonAddSocket(int socketNum, int slotNum)
        {
            byte[] array;
            using (MemoryStream output = new MemoryStream())
            {
                BinaryWriter stream = new BinaryWriter((Stream)output);
                TreeAttribute tree = new TreeAttribute();
                //in which slot in item we want socket to be added
                tree.SetInt("selectedSocketSlot", socketNum);
                //which slot of inventory contains socket item to be added
                tree.SetInt("selectedSlotNum", slotNum);
                tree.ToBytes(stream);
                array = output.ToArray();
            }
            this.capi.Network.SendBlockEntityPacket(this.BlockEntityPosition, 1004, array);
        }
        private void SendInvPacket(object packet)
        {
            this.capi.Network.SendBlockEntityPacket(base.BlockEntityPosition.X, base.BlockEntityPosition.Y, base.BlockEntityPosition.Z, packet);
        }

        /// <summary>Pulls the gem out of socket <paramref name="socketNum"/>, may destroy it or the piece.</summary>
        private void SendExtractGem(int socketNum)
        {
            SendSocketAction(1006, socketNum, 1 + socketNum);
        }

        /// <summary>Pulls the socket itself back out of the piece.</summary>
        private void SendExtractSocket(int socketNum)
        {
            SendSocketAction(1008, socketNum, 5 + socketNum);
        }

        // Same payload shape the ImGui dialog uses, so the server side needs no changes.
        private void SendSocketAction(int packetId, int socketNum, int slotNum)
        {
            byte[] array;
            using (MemoryStream output = new MemoryStream())
            {
                BinaryWriter stream = new BinaryWriter(output);
                TreeAttribute tree = new TreeAttribute();
                tree.SetInt("selectedSocketSlot", socketNum);
                tree.SetInt("selectedSlotNum", slotNum);
                tree.ToBytes(stream);
                array = output.ToArray();
            }
            this.capi.Network.SendBlockEntityPacket(this.BlockEntityPosition, packetId, array);
        }

        private void SendInscribe(string text)
        {
            byte[] array;
            using (MemoryStream output = new MemoryStream())
            {
                BinaryWriter stream = new BinaryWriter(output);
                stream.Write(text ?? "");
                array = output.ToArray();
            }
            this.capi.Network.SendBlockEntityPacket(this.BlockEntityPosition, 1007, array);
        }
    }
}
