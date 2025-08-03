using System;
using HarmonyLib;
using Vintagestory.API.Common;

using Jakojaannos.HandsomeTweaks.Compatibility.ConfigLib;
using Jakojaannos.HandsomeTweaks.Config;
using Jakojaannos.HandsomeTweaks.Modules.MergeStacksOnGround.Patches;

using static Jakojaannos.HandsomeTweaks.ModInfo;

using VSModSystem = Vintagestory.API.Common.ModSystem;
using MergeStacksOnGround = Jakojaannos.HandsomeTweaks.Modules.MergeStacksOnGround.ModuleInfo;
using StructuredLangFile = Jakojaannos.HandsomeTweaks.Modules.StructuredLangFile.ModuleInfo;
using KeepHandbookHistory = Jakojaannos.HandsomeTweaks.Modules.KeepHandbookHistory.ModuleInfo;
using XLibLevelUpNotification = Jakojaannos.HandsomeTweaks.Modules.XLibLevelUpNotification.ModuleInfo;
using Vintagestory.API.Client;
using Jakojaannos.HandsomeTweaks.Modules.XLibLevelUpNotification.Client.Gui;


namespace Jakojaannos.HandsomeTweaks.ModSystem;

public class HandsomeTweaksModSystem : VSModSystem {
	private static HandsomeTweaksSettings Settings {
		get => HandsomeTweaksSettings.Instance;
	}

	private ConfigLibCompat? _configLib;
	private Harmony? _harmony;

	internal event Action<HandsomeTweaksSettings>? SettingsLoaded;

	private static volatile bool s_isPatchApplied = false;
	private bool _didPatch = false;

	public override void Start(ICoreAPI api) {
		Mod.Logger.Debug("Handsome Tweaks Starting!");

		HandsomeTweaksSettings.SyncWithModConfig(api);

		_configLib = ConfigLibCompat.TryInitialize(Mod.Logger, api);
		_configLib?.SubscribeToConfigChange();

		ApplyPatches();
	}

	public override void StartClientSide(ICoreClientAPI api) {
		var _ = new HudLevelUp(api);
	}

	private void ApplyPatches() {
		// Don't re-apply patches if they have already been applied
		if (_harmony is not null || s_isPatchApplied) {
			Mod.Logger.Debug("Patches already applied - OK!");
			return;
		}

		Mod.Logger.Debug("Applying patches!");
		s_isPatchApplied = true;
		_didPatch = true;

		_harmony = new(MOD_ID);
		if (Settings.Startup.IsStructuredTranslationEnabled) {
			_harmony.PatchCategory(StructuredLangFile.PATCH_CATEGORY);
		}

		if (Settings.Startup.IsMergeStacksOnGroundEnabled) {
			_harmony.PatchCategory(MergeStacksOnGround.PATCH_CATEGORY);

			if (Settings.MergeStacksOnGround.IsRenderPatchEnabled) {
				_harmony.PatchCategory(MergeStacksOnGround.PATCH_CATEGORY + EntityItemRendererPatch.RENDER_PATCH);
			}
		}

		if (Settings.Startup.IsKeepHandbookHistoryEnabled) {
			_harmony.PatchCategory(KeepHandbookHistory.PATCH_CATEGORY);
		}

		if (Settings.Startup.IsXLibLevelUpNotificationEnabled) {
			_harmony.PatchCategory(XLibLevelUpNotification.PATCH_CATEGORY);
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
