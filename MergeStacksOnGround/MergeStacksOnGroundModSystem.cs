using HarmonyLib;

using Vintagestory.API.Common;

namespace Jakojaannos.MergeStacksOnGround;

public class MergeStacksOnGroundModSystem : ModSystem {
	private Harmony _harmony;

	private static bool patched;

	public override void Start(ICoreAPI api) {
		if (!patched) {
			patched = true;
			Harmony.DEBUG = true;
			_harmony = new(Mod.Info.ModID);
			_harmony.PatchAll();
		}
	}

	public override void Dispose() {
		if (patched) {
			_harmony?.UnpatchAll(Mod.Info.ModID);
			patched = false;
		}
	}
}
