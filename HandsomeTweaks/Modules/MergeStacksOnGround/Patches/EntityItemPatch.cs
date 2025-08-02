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
[HarmonyPatchCategory(PATCH_CATEGORY)]
public static class EntityItemPatch {
	public const string RENDER_PATCH = "renderpatch";

	internal static readonly string ATTRIBUTE_LISTENER_ID = Attributes.Id(MODULE_ID, "listener");

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

		if (__instance.Attributes.HasAttribute(ATTRIBUTE_LISTENER_ID)) {
			var listenerId = __instance.Attributes.GetLong(ATTRIBUTE_LISTENER_ID);
			__instance.Api.Event.UnregisterGameTickListener(listenerId);
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
		larger.WatchedAttributes.SetInt(EntityItemRendererPatch.ATTRIBUTE_RENDER_STACK_COUNT, larger.Slot.StackSize);

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