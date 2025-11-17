using Vintagestory.API.Client;

namespace canjewelry.src.items.GemChiselMode
{
    public abstract class GemChiselMode
    {
        /// <summary>
        /// Gives back the action needed to draw this mode's icon
        /// </summary>
        /// <param name="capi">Some icons are pulled from the client API so it is provided here.</param>
        /// <returns>An action that will draw the mode's icon wherever needed.</returns>
        public abstract DrawSkillIconDelegate DrawAction(ICoreClientAPI capi);
    }
}
