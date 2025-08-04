using System;

using Cairo;

using Vintagestory.API.Client;

namespace Jakojaannos.HandsomeTweaks.Modules.XLibLevelUpNotification.Client.Gui;

public class GuiElementScrollingText : GuiElementTextBase {
	private double LastLetterOpacity {
		get => _lastLetterOpacity;
		set {
			_lastLetterOpacity = Math.Clamp(value, 0.0, 1.0);
			_transparentFont.Color[3] = _lastLetterOpacity;
		}
	}
	private double _lastLetterOpacity = 1.0;

	private Range VisibleRange {
		get => _range;
		set => _range = value;
	}
	private Range _range = ..;

	public double VisibleFactor {
		set {
			var letters = Math.Clamp(Text.Length * value, 0.0, Text.Length);
			var overflow = letters - (int)letters;

			VisibleRange = ..(int)letters;
			LastLetterOpacity = letters < Text.Length
				? overflow
				: 1.0;

			RecomposeText();
		}
	}

	private LoadedTexture _textTexture;
	private readonly CairoFont _transparentFont;

	public GuiElementScrollingText(ICoreClientAPI capi, string text, CairoFont font, ElementBounds bounds) : base(capi, text, font, bounds) {
		_textTexture = new LoadedTexture(capi);
		_transparentFont = font.Clone();
		_transparentFont.Color[3] = _lastLetterOpacity;
	}

	public override void ComposeTextElements(Context ctx, ImageSurface surface) {
		Bounds.CalcWorldBounds();
		RecomposeText();
	}

	public void RecomposeText() {
		var imageSurface = new ImageSurface(Format.Argb32, (int)Bounds.InnerWidth, (int)Bounds.InnerHeight);

		var isLastLetterFullyOpaque = LastLetterOpacity > 0.999f;
		var context = genContext(imageSurface);

		var visible = Text[VisibleRange];
		if (visible.Length == 0 || isLastLetterFullyOpaque) {
			Font.SetupContext(context);
			textUtil.DrawTextLine(context, Font, visible, 0.0, 0.0, textPathMode);
		} else {
			var currText = visible[..^1];
			var nextText = visible;

			_transparentFont.SetupContext(context);
			textUtil.DrawTextLine(context, _transparentFont, nextText, 0.0, 0.0, textPathMode);
			Font.SetupContext(context);
			textUtil.DrawTextLine(context, Font, currText, 0.0, 0.0, textPathMode);
		}

		generateTexture(imageSurface, ref _textTexture);
		context.Dispose();
		imageSurface.Dispose();
	}

	public override void RenderInteractiveElements(float deltaTime) {
		api.Render.Render2DTexturePremultipliedAlpha(
			_textTexture.TextureId,
			(int)Bounds.renderX,
			(int)Bounds.renderY,
			(int)Bounds.InnerWidth,
			(int)Bounds.InnerHeight
		);
	}

	public override void Dispose() {
		_textTexture.Dispose();
	}
}