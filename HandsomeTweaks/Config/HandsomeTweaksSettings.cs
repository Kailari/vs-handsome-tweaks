using Vintagestory.API.Common;

using static Jakojaannos.HandsomeTweaks.ModInfo;

namespace Jakojaannos.HandsomeTweaks.Config;

internal sealed class HandsomeTweaksSettings {
	public const string FILENAME = $"{AUTHOR_DOMAIN}/{MOD_ID}.json";

	public readonly StartupSettings Startup = new();

	internal sealed class StartupSettings {
		public bool IsMergeStacksOnGroundEnabled { get; set; } = true;
	}

	internal static void SyncWithModConfig(ICoreAPI api, ref HandsomeTweaksSettings settings) {
		try {
			var existingConfig = api.LoadModConfig<HandsomeTweaksSettings>(FILENAME);
			if (existingConfig is not null) {
				settings = existingConfig;
			} else {
				api.StoreModConfig(settings, FILENAME);
			}
		} catch {
			api.StoreModConfig(settings, FILENAME);
		}
	}
}
