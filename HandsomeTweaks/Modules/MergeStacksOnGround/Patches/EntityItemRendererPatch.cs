using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

using HarmonyLib;

using Jakojaannos.HandsomeTweaks.Config;
using Jakojaannos.HandsomeTweaks.Util;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using static Jakojaannos.HandsomeTweaks.Modules.MergeStacksOnGround.ModuleInfo;

namespace Jakojaannos.HandsomeTweaks.Modules.MergeStacksOnGround.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(PATCH_CATEGORY + RENDER_PATCH)]
public static class EntityItemRendererPatch {
	public const string RENDER_PATCH = "renderpatch";

	internal static readonly string ATTRIBUTE_RENDER_STACK_COUNT = Attributes.Id(MODULE_ID, "stacks");

	private static readonly Vec3f[] OFFSETS = [
		new(0.0f, 0.0f, 0.0f),
		new(-0.37f, 0.03f, 0.37f),
		new(-0.34f, 0.06f, -0.34f),
		new(0.31f, 0.09f, 0.31f),
		new(-0.28f, 0.12f, -0.28f),
		new(-0.25f, 0.15f, 0.25f),
		new(0.22f, 0.18f, -0.22f),
		new(-0.19f, 0.21f, -0.19f),
		new(0.16f, 0.24f, 0.16f),
		new(-0.13f, 0.27f, -0.13f),
		new(0.1f, 0.3f, -0.1f),
	];


	[HarmonyTranspiler]
	[HarmonyPatch(typeof(EntityItemRenderer), nameof(EntityItemRenderer.DoRender3DOpaque))]
	public static IEnumerable<CodeInstruction> TranspileDoRender3DOpaque(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
		var matcher = new CodeMatcher(instructions, generator);
		const int SHADOW_PASS_ARG_INDEX = 2;

		matcher
			.Start()
			// Remove the main drawcall
			.MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(IRenderAPI), nameof(IRenderAPI.RenderMultiTextureMesh))))
			.RemoveInstruction()
			// Inject a wrapper with extra arguments
			.InsertAndAdvance(
				/* == Extra arguments == */

				/* itemRenderer: this */
				CodeInstruction.LoadArgument(0),
				/* item: this.entityitem */
				CodeInstruction.LoadArgument(0),
				CodeInstruction.LoadField(typeof(EntityItemRenderer), "entityitem"),
				/* isShadowPass (forwarded from patched method args) */
				CodeInstruction.LoadArgument(SHADOW_PASS_ARG_INDEX),

				/* == Call the wrapper == */
				// Args includes all the arguments of the original (removed)
				// call-instruction, with the extra ones above appended.
				// The method signature must match this combination of original
				// method args and extra args.
				CodeInstruction.Call(typeof(EntityItemRendererPatch), nameof(RenderMultiTextureMeshOverride))
			)
			.End();

		var result = matcher.Instructions();
		return result;
	}

	// Render call wrapper to render merged stacks as multiple copies.
	//
	// NOTE:
	// The signature MUST match exactly the signature of IRenderAPI.RenderMultiTextureMesh. The
	// "hidden" this-argument MUST be included and MUST be the first argument. All extra arguments
	// MUST be manually pushed to the stack prior to the inserted call-opcode.
	public static void RenderMultiTextureMeshOverride(
		/* Original args */
		IRenderAPI @this,
		MultiTextureMeshRef mmr,
		string textureSampleName,
		int textureNumber,
		/* Extra args */
		EntityItemRenderer itemRenderer,
		EntityItem entityitem,
		bool isShadowPass
	) {
		var settings = HandsomeTweaksSettings.Instance;
		var maxRenderedStacks = Math.Clamp(settings.MergeStacksOnGround.MaxRenderedStacks + 1, 1, OFFSETS.Length);

		var maxStackSize = entityitem.Itemstack.Collectible.MaxStackSize;
		var itemsPerRenderedStackSizeTier = maxStackSize / maxRenderedStacks;
		var itemsInMergedStack = entityitem.WatchedAttributes.GetInt(ATTRIBUTE_RENDER_STACK_COUNT, 0);
		var stackCount = itemsInMergedStack / itemsPerRenderedStackSizeTier;
		var renderInstanceCount = Math.Clamp(stackCount, 1, maxRenderedStacks);
		if (isShadowPass) {
			renderInstanceCount = 1;
		}

		// HACK: assume standard shader is used
		var shader = @this.StandardShader;

		var adjustedModelMat = Mat4f.Create();
		var originalModelMat = itemRenderer.ModelMat;
		for (var i = 0; i < renderInstanceCount; ++i) {
			var offset = OFFSETS[i];

			Mat4f.Translate(adjustedModelMat, originalModelMat, offset.X, offset.Y, offset.Z);
			shader.ModelMatrix = adjustedModelMat;

			@this.RenderMultiTextureMesh(mmr, textureSampleName, textureNumber);
		}
	}
}
