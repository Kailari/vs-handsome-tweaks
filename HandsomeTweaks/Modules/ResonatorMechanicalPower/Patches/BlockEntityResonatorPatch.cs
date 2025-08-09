using System;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

using static Jakojaannos.HandsomeTweaks.Modules.ResonatorMechanicalPower.ModuleInfo;

namespace Jakojaannos.HandsomeTweaks.Modules.ResonatorMechanicalPower.Patches;

[HarmonyPatchCategory(PATCH_CATEGORY)]
[HarmonyPatch(typeof(BlockEntityResonator))]
public static class BlockEntityResonatorPatch {
	[HarmonyPostfix]
	[HarmonyPatch(nameof(BlockEntityResonator.Initialize))]
	public static void InitializePostfix(BlockEntityResonator __instance) {
		if (__instance.Api.Side != EnumAppSide.Client) {
			return;
		}

		// Autostart music if receiving MP
		if (__instance.HasDisc) {
			var mpConsumer = __instance.GetBehavior<BEBehaviorMPConsumer>();
			if (mpConsumer.TrueSpeed > 0) {
				StartMusic(__instance);
			}
		}
	}

	[HarmonyReversePatch]
	[HarmonyPatch("StartMusic")]
	public static void StartMusic(object @this) => throw new NotImplementedException("Reverse patch stub");

	[HarmonyPostfix]
	[HarmonyPatch("OnClientTick")]
	public static void OnClientTickPostfix(
		BlockEntityResonator __instance,
		MusicTrack ___track
	) {
		// If track is loaded and not playing, we have very likely reached the
		// end of the track, and should check if we can loop. Looping is
		// enabled iff the resonator receives MP.
		if (___track?.Sound is ILoadedSound sound && !sound.IsPlaying) {
			var mpConsumer = __instance.GetBehavior<BEBehaviorMPConsumer>();
			if (mpConsumer.TrueSpeed > 0) {
				__instance.Api.World.PlaySoundAt(new AssetLocation("sounds/block/vinyl"), __instance.Pos, 0.0, null, randomizePitch: false);
				___track.Sound.Start();
			}
		}
	}
}
