using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Jakojaannos.MergeStacksOnGround.Patches;

[HarmonyPatch]
public static class EntityItemPatch {
	private const string ATTRIBUTE_LISTENER_ID = "jakojaannos.mergestacksonground.listener";
	private const string ATTRIBUTE_RENDER_STACK_COUNT = "jakojaannos.mergestacksonground.stacks";

	private static readonly Vec3f[] s_offsets = new Vec3f[11] {
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
	};

	[HarmonyPostfix]
	[HarmonyPatch(typeof(EntityItem), nameof(EntityItem.Initialize))]
	public static void Initialize(EntityItem __instance) {
		if (__instance.Api.Side == EnumAppSide.Client) {
			return;
		}

		// Attempt merging this item stack every 5 seconds (= all item stacks will try to merge themselves)
		var listenerId = __instance.Api.Event.RegisterGameTickListener(_ => TryMergeWithNearbyStacks(__instance), 5000, 0);
		__instance.Attributes.SetLong(ATTRIBUTE_LISTENER_ID, listenerId);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(EntityItem), nameof(EntityItem.OnEntityDespawn))]
	private static void OnEntityDespawn(EntityItem __instance) {
		if (__instance.Api.Side == EnumAppSide.Client) {
			return;
		}

		if (!__instance.Attributes.HasAttribute(ATTRIBUTE_LISTENER_ID)) {
			return;
		}

		var listenerId = __instance.Attributes.GetLong(ATTRIBUTE_LISTENER_ID);
		__instance.Api.Event.UnregisterGameTickListener(listenerId);
	}

	private static readonly MethodInfo s_renderMethod = AccessTools.Method(typeof(IRenderAPI), nameof(IRenderAPI.RenderMultiTextureMesh));

	[HarmonyTranspiler]
	[HarmonyPatch(typeof(EntityItemRenderer), nameof(EntityItemRenderer.DoRender3DOpaque))]
	public static IEnumerable<CodeInstruction> TranspileDoRender3DOpaque(IEnumerable<CodeInstruction> instructions) {
		var success = false;
		foreach (var instruction in instructions) {
			// If the instruction is a call to the render method, hijack it.
			if (instruction.Calls(s_renderMethod)) {
				/*
				The stack already contains the this-arg and all other original
				arguments. Any arguments loaded to the stack here are appended
				after those. Ordering is FIFO, with the implicit this arg as
				the 0th arg.

				That is, the code assumes the original IL is something like:
				   1	ldloc <idx of render (this arg)>
				   2	ldloc <idx of itemStackRenderInfo>
				   3	call instance MultiTextureMeshRef ItemRenderInfo::get_ModelRef()
				   4	ldloc <idx of textureSampleName>
				   5	ldc.i4.0
				   6	call instance void IRenderAPI::RenderMultiTextureMesh(int32, string, MultiTextureMeshRef)

				The current instruction being processed is the (6). That is,
				all of instructions 1-5 are already yielded on the stack.

				The call to get_ModelRef() pops the value pushed at 2 off the
				stack (that is the itemStackRenderInfo). This leaves the top of
				the stack as (from top to bottom):
				   1. constant zero, the texture number (1)
				   2. texture sample name (2)
				   3. reference to IRenderAPI (5)
				   4. reference to the MultiTextureMeshRef (4)


				In order to make the wrapper work, a few extra arguments are
				needed. As the parameters are already on the stack, any
				parameters we append now, will appear after those.

				We need to insert four extra arguments:
				   1	itemRenderer: EntityItemRenderer
				   2	item: EntityItem
				   3	isShadowPass: bool.

				The item renderer is the instance itself, thus it is just the
				argument zero (implicit this-argument in the local scope).

				The item argument is a private field of the instance of the
				class being patched. This requires additional ldfld, with
				corresponding instance on the stack.

				The isShadowPass (3) is just an argument of the patched method.
				Therefore, its index can be easily identified from the method
				signature. The 0th parameter is the this-argument, 'dt' is at
				index 1 and thus 'isShadowPass' is the index two.


				The final IL is something akin to:
				   5	ldloc <idx of render>
				   3	ldloc <idx of itemStackRenderInfo>
				   4	call instance MultiTextureMeshRef ItemRenderInfo::get_ModelRef()
				   2	ldloc <idx of textureSampleName>
				   1	ldc.i4.0
				   7	ldarg.0
				   9	ldarg.0
				   8	ldfld EntityItem EntityItemRenderer::entityitem
				   6	ldarg.2
				   10	call instance void <The wrapper in EntityItemPatch>

				Note that the original call instruction is dropped and only
				the call to the wrapper remains (10).
				*/

				///////////////////////////////////////////////////////////////
				// PUSH extra args
				const int SHADOW_PASS_ARG_INDEX = 2;

				/* itemRenderer: this */
				yield return CodeInstruction.LoadArgument(0);

				/* item: this.entityitem */
				yield return CodeInstruction.LoadArgument(0);
				yield return CodeInstruction.LoadField(typeof(EntityItemRenderer), "entityitem");

				/* isShadowPass: isShadowPass */
				yield return CodeInstruction.LoadArgument(SHADOW_PASS_ARG_INDEX);

				///////////////////////////////////////////////////////////////
				// CALL
				var replacement = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EntityItemPatch), nameof(RenderMultiTextureMeshOverride)));
				yield return replacement;
				success = true;
			}
			// Emit all other opcodes as-is.
			else {
				yield return instruction;
			}
		}

		if (!success) {
			throw new InvalidOperationException("Cannot find/wrap <CALL RenderMultiTextureMesh> in original DoRender3DOpaque");
		}
	}

	// Render call wrapper to render merged stacks as multiple copies.
	//
	// NOTE:
	// The signature MUST match exactly the signature of IRenderAPI.RenderMultiTextureMesh. The
	// "hidden" this-argument MUST be included and MUST be the first argument. All extra arguments
	// MUST be manually pushed to the stack prior to the inserted call-opcode.
	public static void RenderMultiTextureMeshOverride(
		// Original args:
		// Implicit this arg ??
		IRenderAPI @this,
		MultiTextureMeshRef mmr,
		// Value is passed, but always "tex" (or "tex2d" if isShadowPass is true)
		string textureSampleName,
		// Default arg, not supplied in original code, value is always zero
		int textureNumber,

		// Extra args:
		EntityItemRenderer itemRenderer,
		EntityItem entityitem,
		bool isShadowPass
	) {
		if (isShadowPass) {
			@this.RenderMultiTextureMesh(mmr, textureSampleName, textureNumber);
		} else {
			// HACK: assume standard shader is used
			var shader = @this.StandardShader;

			var stackCount = entityitem.WatchedAttributes.GetInt(ATTRIBUTE_RENDER_STACK_COUNT, 0) / 6;
			var renderInstanceCount = Math.Clamp(stackCount, 1, 10);

			var adjustedModelMat = Mat4f.Create();
			var originalModelMat = itemRenderer.ModelMat;
			for (var i = 0; i < renderInstanceCount; ++i) {
				var offset = s_offsets[i];

				Mat4f.Translate(adjustedModelMat, originalModelMat, offset.X, offset.Y, offset.Z);
				shader.ModelMatrix = adjustedModelMat;

				@this.RenderMultiTextureMesh(mmr, textureSampleName, textureNumber);
			}
		}
	}

	private static void TryMergeWithNearbyStacks(EntityItem item) {
		var isJustSpawned = !item.Collided;
		if (item.IsStackEmpty() || isJustSpawned || item.IsStackFull()) {
			return;
		}

		var nearbyItemEntities = item.Api.World.GetEntitiesAround(item.ServerPos.XYZ, 5.0f, 5.0f, entity => entity is EntityItem);
		foreach (var other in nearbyItemEntities.Cast<EntityItem>()) {
			if (other.EntityId == item.EntityId || !other.Collided) {
				continue;
			}

			TryMerge(item, other);

			var wasMergedToOther = item.IsStackEmpty() || !item.Alive;
			if (wasMergedToOther) {
				return;
			}
		}
	}

	private static void TryMerge(EntityItem a, EntityItem b) {
		if (a.Slot.Itemstack == null || b.Slot.Itemstack == null) {
			return;
		}

		var isALargerThanB = a.Slot.Itemstack.StackSize >= b.Slot.Itemstack.StackSize;
		var larger = isALargerThanB ? a : b;
		var smaller = isALargerThanB ? b : a;

		var spaceLeft = larger.GetRemainingStackSpace();
		smaller.Slot.TryPutInto(a.Api.World, larger.Slot, spaceLeft);

		// NOTE: this is essentially just a flag that determines whether or not to do special rendering
		larger.WatchedAttributes.SetInt(ATTRIBUTE_RENDER_STACK_COUNT, larger.Slot.StackSize);

		if (smaller.IsStackEmpty()) {
			smaller.Die(EnumDespawnReason.Removed);
		}
	}

	/// <summary>
	/// Amount of space left for items in the stack
	/// </summary>
	private static int GetRemainingStackSpace(this EntityItem item) {
		if (item.Slot.Itemstack == null) {
			return 0;
		}

		var maxStackSize = item.Itemstack.Collectible.MaxStackSize;
		var itemCount = Math.Max(0, item.Slot.Itemstack.StackSize);
		return Math.Max(0, maxStackSize - itemCount);
	}

	private static bool IsStackFull(this EntityItem item) {
		return item.GetRemainingStackSpace() <= 0;
	}

	private static bool IsStackEmpty(this EntityItem item) {
		if (item.Slot.Itemstack == null) {
			return true;
		}

		return item.Slot.StackSize <= 0;
	}
}