using System;

using Jakojaannos.HandsomeTweaks.Util;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

using XLib.XLeveling;

namespace Jakojaannos.HandsomeTweaks.Modules.XLibLevelUpNotification.Client.Gui;

public class HudLevelUp : HudElement {
	private const double FADE_IN_DURATION = 0.5;
	private const double FADE_DELAY = 3.0;
	private const double FADE_OUT_DURATION = 2.75;

	/*
	Animation sequence:
	  A Scroll main label letters into view
	  B Fade in the separator graphic
	  C Fade in the skill-point hint
	  D Fade out the main label
	  E Fade out the separator
	  F Fade out the hint label

	  AAAAA------DDDDDD
	  ---BBB-------EEEE
	  ----CCC-------FFF

	As seen on the "graphic", the animation can be split nicely to three
	distinct sections:
	  1. fade in
	  2. pause
	  3. fade out

	These can be implemented as staggered tweens over a period of time. The A
	is just animating the number of letters visible on the string.

	B/C should be doable as just fading in alpha. And D, E and F should be just
	alpha fade out.

	The common denominator: These are all just linear property transitions
	between two known states over time. That's also called a "tween" (as in
	in-betweening), in some contexts.
	*/

	public class PropertyTweener<T> {
		public required Action<T> Setter { get; init; }
		public required System.Func<T, T, float, T> Lerp { get; init; }
		public required float Duration { get; init; }
		public EnumTransformFunction Transform { get; init; } = EnumTransformFunction.LINEAR;
		public float Delay { get; init; } = 0.0f;

		public required T From { get; init; }
		public required T To { get; init; }

		private EvolvingNatFloat? _generator;

		public void Update(float t) {
			_generator ??= new(Transform, 1.0f / Duration);

			var adjustedTime = Math.Max(0.0f, t - Delay);
			var d = _generator.nextFloat(0.0f, adjustedTime);
			var value = Lerp(From, To, d);
			Setter(value);
		}
	}

	public const string ID = "levelupnotification";

	private readonly AssetLocation _levelUpSfx;
	private readonly string _labelText;

	private double _elapsed = 0.0;

	private PropertyTweener<double>? _mainLabelScroll;
	private PropertyTweener<double>? _mainLabelAlpha;

	public HudLevelUp(ICoreClientAPI capi, Skill skill, int level)
		: this(capi, skill.DisplayName, level) {
	}

	public HudLevelUp(ICoreClientAPI capi, string skill, int level)
		: this(capi, message: FormatSkillLevelUpMessage(skill, level)) {
	}

	private static string FormatSkillLevelUpMessage(string skill, int level) {
		return Lang.Get(Guis.TranslationKey("xskills/level-up/label"), skill, level);
	}

	public HudLevelUp(ICoreClientAPI capi, string message) : base(capi) {
		_levelUpSfx = Assets.Path("sounds/level-up");
		_labelText = message;
	}

	internal TextCommandResult OnDebugLevelUp() {
		if (!IsOpened()) {
			TryOpen();
		}

		return TextCommandResult.Success();
	}

	public override void OnGuiOpened() {
		var font = CairoFont.WhiteMediumText();
		font.Orientation = EnumTextOrientation.Left;

		var labelBounds = ElementBounds.Fixed(0, 0);
		font.AutoBoxSize(_labelText, labelBounds);

		// Position horizontally centered and vertically at 25% from the top of the screen
		var screenHeight = capi.Gui.WindowBounds.InnerHeight / RuntimeEnv.GUIScale;
		var offsetY = screenHeight / 4.0f;

		var panelBounds = ElementBounds
			.FixedPos(EnumDialogArea.CenterTop, 0.0, 0.0)
			.WithFixedAlignmentOffset(0.0, offsetY)
			.WithSizing(ElementSizing.FitToChildren)
			.WithChild(labelBounds);

		SingleComposer = capi
			.Gui
			.CreateCompo(Guis.Id(ID), panelBounds)
			.AddContainer(ElementBounds.Fill)
			.BeginChildElements()
			.AddScrollingText(_labelText, font, labelBounds, "levelup-notification-animation-text")
			.EndChildElements()
			.Compose();

		var label = SingleComposer.GetScrollingText("levelup-notification-animation-text");
		_mainLabelScroll = new PropertyTweener<double>() {
			Setter = (value) => label.VisibleFactor = value,
			Lerp = (x, y, t) => GameMath.Lerp(x, y, t),
			From = 0.0,
			To = 1.0,
			Duration = (float)FADE_IN_DURATION,
		};
		_mainLabelAlpha = new PropertyTweener<double>() {
			Setter = (value) => label.Font.Color[3] = value,
			Lerp = (x, y, t) => GameMath.Lerp(x, y, t),
			From = 1.0f,
			To = 0.0f,
			Delay = (float)(FADE_IN_DURATION + FADE_DELAY),
			Duration = (float)FADE_OUT_DURATION,
		};
		_mainLabelScroll.Update(0.0f);

		capi.World.PlaySoundAt(_levelUpSfx, capi.World.Player, randomizePitch: false);
	}

	public override void OnBeforeRenderFrame3D(float deltaTime) {
		base.OnBeforeRenderFrame3D(deltaTime);

		if (IsOpened()) {
			_elapsed += deltaTime;

			_mainLabelAlpha?.Update((float)_elapsed);
			_mainLabelScroll?.Update((float)_elapsed);

			var totalDuration = FADE_IN_DURATION + FADE_DELAY + FADE_OUT_DURATION;
			if (_elapsed > totalDuration) {
				TryClose();
			}
		}
	}

	public override bool CaptureAllInputs() {
		return false;
	}

	public override bool ShouldReceiveKeyboardEvents() {
		return false;
	}

	public override bool ShouldReceiveMouseEvents() {
		return false;
	}

	public override bool ShouldReceiveRenderEvents() {
		return true;
	}
}
