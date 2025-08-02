using System;
using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using Jakojaannos.HandsomeTweaks.Config;

using Vintagestory.API.Client;
using Vintagestory.GameContent;

using static Jakojaannos.HandsomeTweaks.Modules.KeepHandbookHistory.ModuleInfo;

namespace Jakojaannos.HandsomeTweaks.Modules.KeepHandbookHistory.Patches;

[HarmonyPatchCategory(PATCH_CATEGORY)]
[HarmonyPatch(typeof(GuiDialogHandbook))]
public static class GuiDialogHandbookPatch {
	public class GuiState {
		public Stack<BrowseHistoryElement> BrowseHistory { get; set; } = new();
		public string? SearchText { get; set; } = null;
	}

	private static HandsomeTweaksSettings.KeepHandbookHistorySettings Settings
		=> HandsomeTweaksSettings.Instance.KeepHandbookHistory;

	[HarmonyPrefix]
	[HarmonyPatch(nameof(GuiDialogHandbook.OnGuiClosed))]
	public static void OnGuiClosedPrefix(
		Stack<BrowseHistoryElement> ___browseHistory,
		GuiComposer ___overviewGui,
		ref GuiState? __state
	) {
		if (!Settings.IsHistoryPersistent) {
			return;
		}

		var isDetailsViewOpen = ___browseHistory.Count > 0 && ___browseHistory.Peek().Page != null;
		__state = new GuiState() {
			// Don't store search text if details view is currently open. This
			// avoids creating an erroneous search history entry for the
			// previous search text.
			SearchText = isDetailsViewOpen
				? null
				: ___overviewGui.GetTextInput("searchField").GetText()
		};
		foreach (var item in ___browseHistory) {
			__state.BrowseHistory.Push(item);

			if (__state.BrowseHistory.Count > Settings.HistoryMaxSize) {
				break;
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(nameof(GuiDialogHandbook.OnGuiClosed))]
	public static void OnGuiClosedPostfix(
		ref Stack<BrowseHistoryElement> ___browseHistory,
		ref GuiState? __state
	) {
		if (!Settings.IsHistoryPersistent) {
			return;
		}

		if (__state is null) {
			throw new InvalidOperationException("Cannot restore handbook history, state was not defined in prefix patch!");
		}

		foreach (var item in __state.BrowseHistory) {
			___browseHistory.Push(item);
		}

		// Only clicking navigation *after* search or searches performed
		// through clicking links create history entries. Create a search
		// history entry to keep the current search in history.
		var isSearching = __state.SearchText?.Length > 0;
		var isCurrentSearchInHistory = ___browseHistory.Count > 0 && ___browseHistory.Peek().SearchText == __state.SearchText;
		if (isSearching && !isCurrentSearchInHistory) {
			___browseHistory.Push(new() {
				Page = null,
				SearchText = __state.SearchText,
				PosY = 0
			});
		}
	}

	[HarmonyTranspiler]
	[HarmonyPatch(typeof(GuiDialogHandbook), nameof(GuiDialogHandbook.OnGuiOpened))]
	public static IEnumerable<CodeInstruction> TranspileOnGuiOpened(
		IEnumerable<CodeInstruction> instructions,
		ILGenerator generator
	) {
		var matcher = new CodeMatcher(instructions, generator);
		matcher
			.Start()
			// Remove the call to GuiDialogHandbook::initOverviewGui()
			.MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(GuiDialogHandbook), nameof(GuiDialogHandbook.initOverviewGui))))
			.RemoveInstruction()
			// Inject a call to init override
			.InsertAndAdvance(
				// Push arguments
				CodeInstruction.LoadArgument(0),
				CodeInstruction.LoadField(typeof(GuiDialogHandbook), "browseHistory"),
				// Call method
				CodeInstruction.Call(typeof(GuiDialogHandbookPatch), nameof(OnGuiOpenedInitOverride))
			)
			.End();

		var result = matcher.Instructions();
		return result;
	}

	public static void OnGuiOpenedInitOverride(GuiDialogHandbook @this, Stack<BrowseHistoryElement> browseHistory) {
		if (browseHistory.Count == 0) {
			@this.initOverviewGui();
			return;
		}

		var searchText = browseHistory.Peek().SearchText;
		if (searchText is not null) {
			@this.Search(searchText);
		} else {
			initDetailGui(@this);
		}
	}

	[HarmonyReversePatch]
	[HarmonyPatch("OnButtonBack")]
	public static bool OnButtonBack(object instance) => throw new NotImplementedException("Reverse patch stub called!");

	[HarmonyReversePatch]
	[HarmonyPatch("initDetailGui")]
	public static void initDetailGui(object instance) => throw new NotImplementedException("Reverse patch stub called!");
}
