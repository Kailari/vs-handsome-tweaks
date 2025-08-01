using Jakojaannos.HandsomeTweaks.Util;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Jakojaannos.HandsomeTweaks.Config;

internal sealed class HandsomeTweaksSettings {
	public int TestInteger { get; set; } = 42;

	internal static HandsomeTweaksSettings FromAsset(IAssetManager assets) {
		var settingsAsset = assets.Get(Assets.Path("config/settings-main.json"));
		var settingsJson = JsonObject.FromJson(settingsAsset.ToText());
		return settingsJson.AsObject<HandsomeTweaksSettings>();
	}
}
