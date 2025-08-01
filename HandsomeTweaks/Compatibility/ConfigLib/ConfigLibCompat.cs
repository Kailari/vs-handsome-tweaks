using Vintagestory.API.Common;
using ConfigLib;
using Jakojaannos.HandsomeTweaks.Config;

using static Jakojaannos.HandsomeTweaks.ModInfo;

namespace Jakojaannos.HandsomeTweaks.Compatibility.ConfigLib;

internal class ConfigLibCompat {
	private readonly ConfigLibModSystem _configLib;
	private readonly ILogger _logger;
	internal HandsomeTweaksSettings? Settings { get; set; } = null;

	private ConfigLibCompat(ICoreAPI api, ILogger logger) {
		_configLib = api.ModLoader.GetModSystem<ConfigLibModSystem>();
		_logger = logger;
	}

	internal static ConfigLibCompat? TryInitialize(Mod mod, ICoreAPI api) {
		if (!api.ModLoader.IsModEnabled("configlib")) {
			return null;
		}

		var instance = new ConfigLibCompat(api, mod.Logger);
		instance.SubscribeToConfigChange();

		return instance;
	}

	private void SubscribeToConfigChange() {
		_configLib.SettingChanged += (domain, config, setting) => {
			if (domain != MOD_ID) {
				return;
			}

			if (Settings is not HandsomeTweaksSettings settings) {
				_logger.Error("Settings instance is missing!");
				return;
			}

			setting.AssignSettingValue(settings);
		};

		_configLib.ConfigsLoaded += () => {
			if (Settings is not HandsomeTweaksSettings settings) {
				_logger.Error("Settings instance is missing!");
				return;
			}

			_configLib.GetConfig(MOD_ID)?.AssignSettingsValues(Settings);
		};
	}
}
