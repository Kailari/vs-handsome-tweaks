using HarmonyLib;

using Jakojaannos.HandsomeTweaks.Modules.XLibLevelUpNotification.Client.Gui;

using Vintagestory.API.Client;

using XLib.XLeveling;

using static Jakojaannos.HandsomeTweaks.Modules.XLibLevelUpNotification.ModuleInfo;

namespace Jakojaannos.HandsomeTweaks.Modules.XLibLevelUpNotification.Patches;

[HarmonyPatch(typeof(PlayerSkill))]
[HarmonyPatchCategory(PATCH_CATEGORY)]
public static class PlayerSkillPatch {
	public readonly struct SetExperiencePatchState {
		public required int LevelBefore { get; init; }
	}

	[HarmonyPrefix]
	[HarmonyPatch(nameof(PlayerSkill.Experience), MethodType.Setter)]
	public static void SetExperiencePrefix(PlayerSkill __instance, ref SetExperiencePatchState __state) {
		__state = new() {
			LevelBefore = __instance.Level,
		};
	}

	[HarmonyPostfix]
	[HarmonyPatch(nameof(PlayerSkill.Experience), MethodType.Setter)]
	public static void SetExperiencePostfix(PlayerSkill __instance, SetExperiencePatchState __state) {
		if (__instance.Level <= __state.LevelBefore) {
			return;
		}

		var api = __instance.Skill.XLeveling.Api;
		if (api is ICoreClientAPI capi) {
			// FIXME: queue if already open
			capi.Event.EnqueueMainThreadTask(() =>
				new HudLevelUp(capi, __instance.Skill, __instance.Level).TryOpen(), "OnLevelUpOpenNotification"
			);
		}
	}
}
