using System;
using Vintagestory.API.Common;

namespace canjewelry.src.integration
{
    // Mutable context objects passed to event subscribers so a companion mod can
    // adjust outcomes (buff strength, rough-gem consumption, grind multiplier, XP).
    // The core mod runs vanilla math, then fires the event; subscribers tweak fields.

    public class EncrustEvent
    {
        public IPlayer Player;
        public ItemStack Jewelry;
        public ItemStack Gem;
        public int SocketTier;
        public int SocketIndex;   // 0-based slot index being filled (for cross-socket logic)
        public string BuffName;
        public float Value;       // mutable — final value written into the socket tree
    }

    public class CutEvent
    {
        public IPlayer Player;
        public ItemStack RoughGem;
        public string CuttingType;   // round / pear / baguette
        public string GemSize;       // chipped / flawed / normal
        public bool ConsumeRough = true;
    }

    public class GrindEvent
    {
        public IPlayer Player;
        public ItemStack Gem;
        public int Stage;             // 1..3
        public float Multiplier = 1f; // mutable — applied on top of base grinding bonus
    }

    // Fires when a grinding stage begins — both at the first interaction with a freshly cut
    // gem (stage=1, counter=10) and at the transition into the next stage (stage=N+1,
    // counter=20). Subscribers shorten the grind by reducing Counter.
    public class GrindStageStartEvent
    {
        public IPlayer Player;
        public ItemStack Gem;
        public int Stage;       // the stage that's about to begin
        public int Counter;     // mutable — number of doGrind iterations until the stage completes
    }

    public class CANXpEvent
    {
        public IPlayer Player;
        public string Action;   // "encrust", "addSocket", "cut", "grind", "panGem"
        public float Xp;        // suggested xp award (companion mod routes to its own skill)
    }

    public class PanEvent
    {
        public IPlayer Player;
        public ItemStack Drop;          // mutable — bump StackSize for yield bonus
        public string SourceMaterial;   // block code of the panned material
    }

    // Fires before the pan consumes input material from the player's hotbar. Subscribers
    // shorten the consumption (e.g. Lapidary "Light Touch" perk). Floored at 1 by core.
    public class PanTakeEvent
    {
        public IPlayer Player;
        public string MaterialCode;
        public int Amount;              // mutable — input units to consume
    }

    // Fires when a CANPanningDrop entry has its `requiresPerk` field set. Allowed defaults
    // to false — entries gated by a perk are excluded unless a subscriber permits them. The
    // perk code is opaque to core; companion interprets it (e.g. "lapidary.prospector").
    public class CanPanDropEvent
    {
        public IPlayer Player;
        public string PerkCode;
        public bool Allowed = false;
    }

    // Permission hook: core asks whether the player may extract a gem from a socket.
    // Allowed defaults to true — without a companion the button is always visible. A companion
    // mod subscribing to OnCanExtract should set Allowed based on the player's skill/perk.
    public class CanExtractEvent
    {
        public IPlayer Player;
        public ItemStack Jewelry;
        public int SocketIndex;
        public bool Allowed = true;
    }

    // Permission hook: core asks subscribers whether the player may perform the action.
    // Allowed defaults to true — if no companion subscribes, the action proceeds (core-only
    // installs keep their pre-companion behaviour). A subscriber denies by setting false.
    public class CanInscribeEvent
    {
        public IPlayer Player;
        public ItemStack Jewelry;
        public bool Allowed = true;
    }

    public class WireDrawEvent
    {
        public IPlayer Player;     // who started the squeeze; may be null if BE was unloaded mid-squeeze
        public ItemStack Input;    // the metal strap/ingot in the bench slot (read-only intent)
        public ItemStack Output;   // mutable — bump StackSize for yield bonus
    }

    // Two independent outcome axes — gem and jewelry are decided separately so subscribers
    // can mix freely (e.g. ReturnedDowngraded gem + Damaged jewelry) without enumerating
    // every (gem, jewelry) combination.
    public enum GemOutcome
    {
        Destroyed,           // default — gem is consumed
        Returned,            // gem handed back to player intact
        ReturnedDowngraded,  // gem handed back one size smaller (normal falls back to Returned)
    }

    public enum JewelryOutcome
    {
        Intact,    // default — no durability hit
        Damaged,   // apply JewelryDamageAmount; damage exhausting durability promotes to Destroyed
        Destroyed, // jewelry stack is removed from inventory
    }

    public class ExtractEvent
    {
        public IPlayer Player;
        public ItemStack Jewelry;       // the socketed item the gem is being pulled from
        public ItemStack Gem;           // cloned gem from socket — read-only intent
        public int SocketTier;
        public int SocketIndex;         // 0-based slot index the gem came from
        public string BuffName;

        public GemOutcome     GemOutcome     = GemOutcome.Returned;
        public JewelryOutcome JewelryOutcome = JewelryOutcome.Intact;
        public int            JewelryDamageAmount = 0;  // only used when JewelryOutcome == Damaged

        // Base extraction success chance from config. Companion mods can increase this
        // (e.g. +0.1 per skill level). Core rolls against the final value after the event fires.
        // Ignored if the companion mod sets GemOutcome directly to something other than Returned.
        public float ExtractionChance = 1f;

        // If the ExtractionChance roll fails (gem would be Destroyed), core rolls this as a
        // fallback: on success the outcome becomes ReturnedDowngraded instead. Companion mods
        // set this to give players a consolation prize (e.g. a DelicateTouch-style perk).
        public float DowngradeChance = 0f;

        // Built-in chance (from config) that the jewelry is destroyed when a gem is pulled.
        // Companion mods can raise or lower it. Core promotes JewelryOutcome.Intact → Destroyed
        // after the event fires if the roll fails (only when outcome is still Intact).
        public float JewelryBreakChance = 0f;
    }
}
