using static Jakojaannos.HandsomeTweaks.ModInfo;

namespace Jakojaannos.HandsomeTweaks.Util;

public static class Guis {
	public static string Id(string id) => $"{MOD_ID}:{id}";

	public static string TranslationKey(string key) => $"{MOD_ID}:{key}"
		.Replace('/', '-');
}
