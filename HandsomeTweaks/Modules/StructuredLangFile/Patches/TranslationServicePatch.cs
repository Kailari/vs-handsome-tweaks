using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using HarmonyLib;

using Newtonsoft.Json.Linq;

using Vintagestory.API.Common;
using Vintagestory.API.Config;

using static Jakojaannos.HandsomeTweaks.Modules.StructuredLangFile.ModuleInfo;

namespace Jakojaannos.HandsomeTweaks.Modules.StructuredLangFile.Patches;

[HarmonyPatchCategory(PATCH_CATEGORY)]
[HarmonyPatch(typeof(TranslationService))]
public static class TranslationServicePatch {
	private static readonly JsonLoadSettings JSON_LOAD_SETTINGS = new() {
		CommentHandling = CommentHandling.Ignore,
		DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
	};

	// FIXME: this could be easier implemented as a transpiler
	[HarmonyPrefix]
	[HarmonyPatch(nameof(TranslationService.Load))]
	public static bool StructuredLoad(
		TranslationService __instance,
		ref string? ___preLoadAssetsPath,
		ref bool ___loaded,
		ILogger ___logger,
		IAssetManager ___assetManager,
		Dictionary<string, string> ___entryCache,
		Dictionary<string, KeyValuePair<Regex, string>> ___regexCache,
		Dictionary<string, string> ___wildcardCache,
		bool lazyLoad = false
	) {
		Load(__instance, ref ___preLoadAssetsPath, ref ___loaded, ___logger, ___assetManager, __instance.LanguageCode, ref ___entryCache, ref ___regexCache, ref ___wildcardCache, lazyLoad);
		// Prevent the original code from running
		return false;
	}

	/* Method mostly copy-pasted from the original TranslationService. */
	// FIXME: just PR these changes to VSApi
	private static void Load(
		TranslationService __instance,
		ref string? preLoadAssetsPath,
		ref bool loaded,
		ILogger logger,
		IAssetManager assetManager,
		string LanguageCode,
		ref Dictionary<string, string> entryCache,
		ref Dictionary<string, KeyValuePair<Regex, string>> regexCache,
		ref Dictionary<string, string> wildcardCache,
		bool lazyLoad = false
	) {
		preLoadAssetsPath = null;
		if (lazyLoad) return;
		loaded = true;

		// Don't work on dicts directly for thread safety (client and local server access the same dict)
		var localEntryCache = new Dictionary<string, string>();
		var localRegexCache = new Dictionary<string, KeyValuePair<Regex, string>>();
		var localWildcardCache = new Dictionary<string, string>();

		var origins = assetManager.Origins;

		foreach (var asset in origins.SelectMany(p => p.GetAssets(AssetCategory.lang).Where(a => a.Name.Equals($"{LanguageCode}.json") || a.Name.Equals($"worldconfig-{LanguageCode}.json")))) {

			try {
				var json = asset.ToText();

				/* === MODIFIED CODE STARTS === */
				LoadEntries(__instance, logger, entryCache, regexCache, wildcardCache, JToken.Parse(json), asset.Location.Domain);
				/* === MODIFIED CODE ENDS === */
			} catch (Exception ex) {
				logger.Error($"Failed to load language file: {asset.Name}");
				logger.Error(ex);
			}
		}

		entryCache = localEntryCache;
		regexCache = localRegexCache;
		wildcardCache = localWildcardCache;
	}

	private static void LoadEntries(
		TranslationService __instance,
		ILogger logger,
		Dictionary<string, string> entryCache,
		Dictionary<string, KeyValuePair<Regex, string>> regexCache,
		Dictionary<string, string> wildcardCache,
		JToken json,
		string domain = GlobalConstants.DefaultDomain,
		string key = ""
	) {
		switch (json) {
			case JObject jsonObject:
				foreach (var property in jsonObject.Properties()) {
					var newKey = key.Length == 0
						? property.Name
						: $"{key}-{property.Name}";
					LoadEntries(__instance, logger, entryCache, regexCache, wildcardCache, property.Value, domain, newKey);
				}
				break;
			case JValue jsonValue when jsonValue.Type == JTokenType.String && key.Length > 0:
				var value = jsonValue.ToString();
				if (domain != "game") {
					logger.Debug($"Loading translation entry: Domain={domain}, Key={key}, Value={value}");
				}
				LoadEntry(__instance, entryCache, regexCache, wildcardCache, new(key, value), domain);
				break;
			default:
				throw new InvalidOperationException($"Unexpected token while parsing language file: {json.Type}");
		}
	}

	[HarmonyReversePatch]
	[HarmonyPatch(typeof(TranslationService), "LoadEntry")]
	public static void LoadEntry(
		object instance,
		Dictionary<string, string> entryCache,
		Dictionary<string, KeyValuePair<Regex, string>> regexCache,
		Dictionary<string, string> wildcardCache,
		KeyValuePair<string, string> entry,
		string domain
	) => throw new NotImplementedException("Reverse patch stub called!");

}
