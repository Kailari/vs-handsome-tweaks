using System;

using Jakojaannos.HandsomeTweaks.Util;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

using XLib.XLeveling;

namespace Jakojaannos.HandsomeTweaks.Modules.XLibLevelUpNotification.Client.Gui;

public class HudLevelUp : HudElement {

	private const double LETTERS_PER_SECOND = 2;
	private const double TARGET_FPS = 30;
	private const double FADE_DELAY = 3.0;
	private const double FADE_IN_DURATION = 2.0;
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

	public class Tween {
		private readonly Action<float> _setter;
		private readonly Func<float> _getter;
		private readonly EvolvingNatFloat _generator;
	}

	public const string ID = "levelupnotification";

	private readonly AssetLocation _levelUpSfx;
	private readonly string _labelText;
	private long? _tickListener = null;

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

		var ghostBounds = labelBounds.FlatCopy();

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
		label.VisibleFactor = 0.0;
		label.RecomposeText();

		capi.World.PlaySoundAt(_levelUpSfx, capi.World.Player, randomizePitch: false);

		//var mainLabelTween = new Tween();

		var visibleLetterCount = new EvolvingNatFloat(EnumTransformFunction.LINEAR, 1.0f / (float)FADE_IN_DURATION);

		var tickDuration = 1.0 / TARGET_FPS;
		var elapsed = 0.0;
		_tickListener = capi.Event.RegisterGameTickListener(deltaTime => {
			var label = SingleComposer.GetScrollingText("levelup-notification-animation-text");
			elapsed += deltaTime;

			var d = Math.Min(1.0, elapsed / FADE_IN_DURATION);
			var letters = _labelText.Length * d;
			var end = Math.Clamp((int)letters, 0, _labelText.Length);

			var fraction = letters - (int)letters;

			var overshoot = Math.Max(0.0, elapsed - FADE_IN_DURATION);
			var adjusted = Math.Max(0.0, overshoot - FADE_DELAY);
			var rescaled = adjusted / FADE_OUT_DURATION;
			var clamped = Math.Clamp(rescaled, 0.0, 1.0);
			var alpha = 1.0 - clamped;
			label.Font.Color[3] = alpha;
			label.VisibleFactor = d;

			if (alpha < 0.001) {
				TryClose();
			}
		}, (int)Math.Round(tickDuration * 1000));
	}

	public override void OnGuiClosed() {
		if (_tickListener is long id) {
			capi.Event.UnregisterGameTickListener(id);
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
