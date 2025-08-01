using System;
using HarmonyLib;
using Vintagestory.API.Common;

using Jakojaannos.HandsomeTweaks.Compatibility.ConfigLib;

using Jakojaannos.HandsomeTweaks.Config;

using static Jakojaannos.HandsomeTweaks.ModInfo;

using VSModSystem = Vintagestory.API.Common.ModSystem;
using MergeStacksOnGround = Jakojaannos.HandsomeTweaks.Modules.MergeStacksOnGround.ModuleInfo;
using StructuredLangFile = Jakojaannos.HandsomeTweaks.Modules.StructuredLangFile.ModuleInfo;


namespace Jakojaannos.HandsomeTweaks.ModSystem;

public class HandsomeTweaksModSystem : VSModSystem {
	private HandsomeTweaksSettings _settings = new();

	private ConfigLibCompat? _configLib;
	private Harmony? _harmony;

	internal event Action<HandsomeTweaksSettings>? SettingsLoaded;

	private static volatile bool s_isPatchApplied = false;
	private bool _didPatch = false;

	public override void Start(ICoreAPI api) {
		Mod.Logger.Debug("Handsome Tweaks Starting!");

		HandsomeTweaksSettings.SyncWithModConfig(api, ref _settings);

		_configLib = ConfigLibCompat.TryInitialize(Mod, api);
		if (_configLib is not null) {
			_configLib.Settings = _settings;
		}

		// Don't re-apply patches if they have already been applied
		if (_harmony is null && !s_isPatchApplied) {
			Mod.Logger.Debug("Applying patches!");
			s_isPatchApplied = true;
			_didPatch = true;

			_harmony = new(MOD_ID);
			_harmony.PatchCategory(StructuredLangFile.PATCH_CATEGORY);

			if (_settings.Startup.IsMergeStacksOnGroundEnabled) {
				_harmony.PatchCategory(MergeStacksOnGround.PATCH_CATEGORY);
			}
		} else {
			Mod.Logger.Debug("Patches already applied - OK!");
		}
	}

	public override void AssetsFinalize(ICoreAPI api) {
		/*
		_settings = HandsomeTweaksSettings.FromAsset(api.Assets);

		if (_configLib is ConfigLibCompat configLib) {
			configLib.Settings = _settings;
		}
		*/
	}

	public override void Dispose() {
		if (_didPatch && s_isPatchApplied) {
			_harmony?.UnpatchAll(MOD_ID);
			s_isPatchApplied = false;
			_didPatch = false;
		}
	}
}
