using HarmonyLib;

using Jakojaannos.HandsomeTweaks.Modules.XLibLevelUpNotification.Client.Gui;
using Jakojaannos.HandsomeTweaks.Util;

using Vintagestory.API.Client;

using XLib.XLeveling;

namespace Jakojaannos.HandsomeTweaks.Modules.XLibLevelUpNotification.Patches;

[HarmonyPatch(typeof(PlayerSkill))]
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

	[HarmonyPrefix]
	[HarmonyPatch(nameof(PlayerSkill.Experience), MethodType.Setter)]
	public static void SetExperiencePostfix(PlayerSkill __instance, SetExperiencePatchState __state) {
		if (__instance.Level <= __state.LevelBefore) {
			return;
		}

		// TODO: display a nice notification _somehow_
		var api = __instance.Skill.XLeveling.Api;
		if (api is ICoreClientAPI capi) {
			var _ = new HudLevelUp(capi).TryOpen();
		}
	}
}
