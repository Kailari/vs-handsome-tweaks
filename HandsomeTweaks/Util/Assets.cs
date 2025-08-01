using Vintagestory.API.Common;

using static Jakojaannos.HandsomeTweaks.ModInfo;

namespace Jakojaannos.HandsomeTweaks.Util;

internal static class Assets {
	internal static AssetLocation Path(string asset) => Path(MOD_ID, asset);
	internal static AssetLocation Path(string modId, string asset) => new(modId.ToLower(), asset.ToLower());
}
