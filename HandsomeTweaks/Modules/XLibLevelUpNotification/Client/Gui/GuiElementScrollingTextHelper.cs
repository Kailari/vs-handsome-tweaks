using Vintagestory.API.Client;

namespace Jakojaannos.HandsomeTweaks.Modules.XLibLevelUpNotification.Client.Gui;

public static class GuiElementScrollingTextHelper {
	public static GuiComposer AddScrollingText(this GuiComposer composer, string text, CairoFont font, ElementBounds bounds, string? key = null) {
		if (!composer.Composed) {
			var element = new GuiElementScrollingText(composer.Api, text, font, bounds);
			composer.AddInteractiveElement(element, key);
		}

		return composer;
	}

	public static GuiElementScrollingText GetScrollingText(this GuiComposer composer, string key) {
		return (GuiElementScrollingText)composer.GetElement(key);
	}
}