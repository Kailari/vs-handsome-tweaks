using System;
using System.Linq;

using Vintagestory.API.Common;

using ConfigLib;

using Jakojaannos.HandsomeTweaks.Config;

using static Jakojaannos.HandsomeTweaks.ModInfo;

namespace Jakojaannos.HandsomeTweaks.Compatibility.ConfigLib;

internal class ConfigLibCompat {
	private readonly ConfigLibModSystem _configLib;
	private readonly ILogger _logger;

	private static HandsomeTweaksSettings Settings => HandsomeTweaksSettings.Instance;

	private ConfigLibCompat(ILogger logger, ICoreAPI api) {
		_configLib = api.ModLoader.GetModSystem<ConfigLibModSystem>();
		_logger = logger;
	}

	internal static ConfigLibCompat? TryInitialize(ILogger logger, ICoreAPI api) {
		if (!api.ModLoader.IsModEnabled("configlib")) {
			return null;
		}

		return new ConfigLibCompat(logger, api);
	}

	internal void SubscribeToConfigChange() {
		_configLib.ConfigsLoaded += () => {
			var config = _configLib.GetConfig(MOD_ID);
			if (config is null) {
				return;
			}

			AssignSettingValues(config, Settings);
		};
	}

	private readonly struct SettingFieldOrProperty {
		public required string Name { get; init; }
		public required bool IsNestedSettings { get; init; }
		public required Action<object?> Setter { get; init; }
		public required Func<object?> Getter { get; init; }
	}

	private void AssignSettingValues(IConfig config, object settings, string prefix = "") {
		var settingsType = settings.GetType();
		var fields = settingsType
			.GetFields()
			.Where(field => field.IsPublic)
			.Where(field => !field.IsLiteral)
			.Select(field => new SettingFieldOrProperty() {
				Name = field.Name,
				Setter = (value) => field.SetValue(settings, value),
				Getter = () => field.GetValue(settings),
				IsNestedSettings = field.FieldType.Name.EndsWith("Settings")
			});
		var properties = settingsType
			.GetProperties()
			.Where(prop => prop.GetMethod != null && prop.GetMethod.IsPublic)
			.Select(prop => new SettingFieldOrProperty() {
				Name = prop.Name,
				Setter = (value) => prop.SetValue(settings, value),
				Getter = () => prop.GetValue(settings),
				IsNestedSettings = prop.PropertyType.Name.EndsWith("Settings")
			});

		foreach (var field in fields) {
			var code = prefix.Length > 0
				? $"{prefix}/{field.Name}"
				: field.Name;

			// FIXME: this is a really bad heuristic
			if (field.IsNestedSettings) {
				var nested = field.Getter();
				if (nested is null) {
					_logger.Warning($"Encountered null nested setting instance \"{code}\" on {settingsType.Name}");
				} else {
					AssignSettingValues(config, nested, prefix: code);
				}
				continue;
			}

			var setting = config.GetSetting(code);
			if (setting is not null) {
				object value = setting.SettingType switch {
					ConfigSettingType.Boolean => setting.Value.AsBool(),
					ConfigSettingType.Float => setting.Value.AsFloat(),
					ConfigSettingType.Integer => setting.Value.AsInt(),
					ConfigSettingType.String => setting.Value.AsString(),
					ConfigSettingType.Other => setting.Value.ToAttribute(),
					var t => throw new NotImplementedException($"Unhandled setting type: {t}"),
				};
				field.Setter(value ?? setting.DefaultValue);
			} else {
				_logger.Warning($"Encountered non-setting public field \"{code}\" on {settingsType.Name}");
			}
		}
	}
}
