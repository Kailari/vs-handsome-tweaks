using Vintagestory.API.Common;
using ConfigLib;
using Jakojaannos.HandsomeTweaks.Config;

using static Jakojaannos.HandsomeTweaks.ModInfo;

namespace Jakojaannos.HandsomeTweaks.Compatibility.ConfigLib;

internal class ConfigLibCompat {
	private readonly ConfigLibModSystem _configLib;

	private ConfigLibCompat(ICoreAPI api) {
		_configLib = api.ModLoader.GetModSystem<ConfigLibModSystem>();
	}

	internal static ConfigLibCompat? TryInitialize(ICoreAPI api) {
		if (!api.ModLoader.IsModEnabled("configlib")) {
			return null;
		}

		var instance = new ConfigLibCompat(api);

		return instance;
	}

	internal void SubscribeToConfigChange() {
		_configLib.SettingChanged += (domain, config, setting) => {
			if (domain != MOD_ID) {
				return;
			}

			setting.AssignSettingValue(HandsomeTweaksSettings.Instance);
		};

		_configLib.ConfigsLoaded += () => {
			_configLib.GetConfig(MOD_ID)?.AssignSettingsValues(HandsomeTweaksSettings.Instance);
		};
	}
}
