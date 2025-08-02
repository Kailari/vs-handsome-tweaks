using Vintagestory.API.Common;

using static Jakojaannos.HandsomeTweaks.ModInfo;

namespace Jakojaannos.HandsomeTweaks.Config;

internal sealed class HandsomeTweaksSettings {
	public const string FILENAME = $"{AUTHOR_DOMAIN}/{MOD_ID}.json";

	public readonly StartupSettings Startup = new();
	public readonly MergeStacksOnGroundSettings MergeStacksOnGround = new();

	internal sealed class StartupSettings {
		public bool IsMergeStacksOnGroundEnabled { get; set; } = true;
		public bool IsStructuredTranslationEnabled { get; set; } = true;
	}

	internal sealed class MergeStacksOnGroundSettings {
		public bool IsRenderPatchEnabled { get; set; } = true;
		public int MaxRenderedStacks { get; set; } = 10;
	}

	internal static HandsomeTweaksSettings Instance { get; set; } = new();

	internal static void SyncWithModConfig(ICoreAPI api) {
		try {
			var existingConfig = api.LoadModConfig<HandsomeTweaksSettings>(FILENAME);
			if (existingConfig is not null) {
				Instance = existingConfig;
			} else {
				api.StoreModConfig(Instance, FILENAME);
			}
		} catch {
			api.StoreModConfig(Instance, FILENAME);
		}
	}
}
