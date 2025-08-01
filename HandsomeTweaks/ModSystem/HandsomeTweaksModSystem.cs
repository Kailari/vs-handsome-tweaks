using System;
using HarmonyLib;
using Vintagestory.API.Common;

using Jakojaannos.HandsomeTweaks.Compatibility.ConfigLib;

using Jakojaannos.HandsomeTweaks.Config;

using static Jakojaannos.HandsomeTweaks.ModInfo;

using VSModSystem = Vintagestory.API.Common.ModSystem;
using MergeStacksOnGround = Jakojaannos.HandsomeTweaks.Modules.MergeStacksOnGround.ModuleInfo;
using System.Threading;


namespace Jakojaannos.HandsomeTweaks.ModSystem;

public class HandsomeTweaksModSystem : VSModSystem {
	private ConfigLibCompat? _configLib;
	private HandsomeTweaksSettings? _settings;
	private Harmony? _harmony;

	internal event Action<HandsomeTweaksSettings>? SettingsLoaded;

	private static volatile bool s_isPatchApplied = false;
	private bool _didPatch = false;

	public override void Start(ICoreAPI api) {
		_configLib = ConfigLibCompat.TryInitialize(Mod, api);

		// Don't re-apply patches if they have already been applied
		if (_harmony is null && !s_isPatchApplied) {
			s_isPatchApplied = true;
			_didPatch = true;

			_harmony = new(MOD_ID);
			_harmony.PatchCategory(MergeStacksOnGround.PATCH_CATEGORY);
		}
	}

	public override void AssetsFinalize(ICoreAPI api) {
		_settings = HandsomeTweaksSettings.FromAsset(api.Assets);

		if (_configLib is ConfigLibCompat configLib) {
			configLib.Settings = _settings;
		}
	}

	public override void Dispose() {
		if (_didPatch && s_isPatchApplied) {
			_harmony?.UnpatchAll(MOD_ID);
			s_isPatchApplied = false;
			_didPatch = false;
		}
	}
}
