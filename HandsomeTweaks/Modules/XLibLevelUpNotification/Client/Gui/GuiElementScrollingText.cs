using Cairo;

using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace Jakojaannos.HandsomeTweaks.Modules.XLibLevelUpNotification.Client.Gui;

public class GuiElementScrollingText : GuiElementTextBase {
	public double VisibleFactor {
		get => _visibleFactor;
		set {
			_visibleFactor = value;
			RecomposeText();
		}
	}
	private double _visibleFactor = 1.0;

	private LoadedTexture _textTexture;

	public GuiElementScrollingText(ICoreClientAPI capi, string text, CairoFont font, ElementBounds bounds) : base(capi, text, font, bounds) {
		_textTexture = new LoadedTexture(capi);
	}

	public override void ComposeTextElements(Context ctx, ImageSurface surface) {
		Bounds.CalcWorldBounds();
		RecomposeText();
	}

	public void RecomposeText() {
		var imageSurface = new ImageSurface(Format.Argb32, (int)Bounds.InnerWidth, (int)Bounds.InnerHeight);

		var context = genContext(imageSurface);
		Font.SetupContext(context);
		context.Operator = Operator.Source;
		context.Antialias = Antialias.None;

		var maxGradientLetters = Text.Length;
		var visible = VisibleFactor * (Text.Length + maxGradientLetters);

		var opaqueLetters = 0;
		var alpha = Font.Color[3];
		for (var letterIndex = Text.Length - 1; letterIndex >= 0; letterIndex--) {
			var gradientLow = visible - maxGradientLetters;
			var gradientHigh = gradientLow + maxGradientLetters;
			var letterOpacity = 1.0 - GameMath.Smootherstep(gradientLow, gradientHigh, letterIndex);
			if (letterOpacity < 0.01) {
				continue;
			}

			if (letterOpacity > 0.99) {
				opaqueLetters++;
				continue;
			}

			var text = Text[..(letterIndex + 1)];
			Font.Color[3] = alpha * letterOpacity;
			context.SetSourceRGBA(Font.Color);
			context.MoveTo(0, (int)(0 + context.FontExtents.Ascent));
			context.ShowText(text);
		}

		var opaqueText = Text[..opaqueLetters];
		Font.Color[3] = alpha;
		context.SetSourceRGBA(Font.Color);
		context.MoveTo(0, (int)(0 + context.FontExtents.Ascent));
		context.Antialias = Antialias.Default;
		context.ShowText(opaqueText);

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