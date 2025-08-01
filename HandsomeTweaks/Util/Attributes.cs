using static Jakojaannos.HandsomeTweaks.ModInfo;

namespace Jakojaannos.HandsomeTweaks.Util;

public static class Attributes {
	public static string Id(string moduleId, string attributeId) => $"{AUTHOR_DOMAIN}.{MOD_ID}.{moduleId}.{attributeId}";
}
