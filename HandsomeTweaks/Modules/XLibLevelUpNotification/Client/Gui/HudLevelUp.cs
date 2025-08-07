using System;
using System.Collections.Generic;

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

	public class PropertyTweener<T> : IPropertyTween {
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

	public interface IPropertyTween {
		public void Update(float t);
	}

	public class Tween {
		public event Action? Finished;
		public float TotalDuration => _delayFromWaitAll + _delayFromWait + _currentBatchMaxDuration;

		private float _elapsed = 0.0f;
		private float _delayFromWait = 0.0f;
		private float _delayFromWaitAll = 0.0f;

		private float _currentBatchMaxDuration = 0.0f;

		private List<IPropertyTween> _propertyTweeners = new();
		private bool _finished = false;


		public Tween TweenProperty(Action<double> setter, double from, double to, float duration) {
			var tweener = new PropertyTweener<double>() {
				Setter = setter,
				Lerp = (a, b, t) => GameMath.Lerp(a, b, t),
				From = from,
				To = to,
				Duration = duration,
				Delay = _delayFromWaitAll + _delayFromWait,
			};
			_propertyTweeners.Add(tweener);

			_currentBatchMaxDuration = Math.Max(_currentBatchMaxDuration, duration);

			return this;
		}

		public Tween WaitAll() {
			_delayFromWaitAll += _currentBatchMaxDuration;
			_delayFromWait = 0.0f;

			_currentBatchMaxDuration = 0.0f;

			return this;
		}

		public Tween Wait(float delay) {
			_delayFromWait += delay;
			return this;
		}

		public void Update(float deltaTime) {
			_elapsed += deltaTime;
			if (_finished) {
				return;
			}

			_propertyTweeners.ForEach(tween => tween.Update(_elapsed));

			if (_elapsed > TotalDuration) {
				Finished?.Invoke();
				_finished = true;
			}
		}

		public void SeekTo(float seconds) {
			_elapsed = seconds;
			_finished = seconds >= TotalDuration;
		}
	}

	public const string ID = "levelupnotification";

	private readonly AssetLocation _levelUpSfx;
	private readonly string _labelText;
	private readonly string _labelHint;
	private Tween? _animation;

	public HudLevelUp(ICoreClientAPI capi, Skill skill, int level)
		: this(capi, skill.DisplayName, level) {
	}

	public HudLevelUp(ICoreClientAPI capi, string skill, int level)
		: this(
			  capi,
			  message: FormatSkillLevelUpMessage(skill, level),
			  hint: FormatOpenSkillUIHint(capi)
	) {
	}

	private static string FormatSkillLevelUpMessage(string skill, int level) {
		return Lang.Get(Guis.TranslationKey("xskills/level-up/label"), skill, level);
	}

	private static string FormatOpenSkillUIHint(ICoreClientAPI capi) {
		var hotkey = capi.Input.GetHotKeyByCode("skilldialoghotkey");
		var hotkeyString = hotkey.CurrentMapping.ToString();
		return Lang.Get(Guis.TranslationKey("xskills/level-up/hint"), hotkeyString);
	}

	public HudLevelUp(ICoreClientAPI capi, string message, string hint) : base(capi) {
		_levelUpSfx = Assets.Path("sounds/level-up");
		_labelText = message;
		_labelHint = hint;
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

		var separatorFont = CairoFont.WhiteMediumText();
		separatorFont.FontWeight = Cairo.FontWeight.Bold;

		var separatorText = "________";
		var separatorBounds = labelBounds.BelowCopy();
		separatorFont.AutoBoxSize(separatorText, separatorBounds);

		var hintBounds = separatorBounds.BelowCopy();
		var hintFont = CairoFont.WhiteDetailText();
		hintFont.Slant = Cairo.FontSlant.Italic;
		hintFont.Orientation = EnumTextOrientation.Left;
		hintFont.AutoBoxSize(_labelHint, hintBounds);

		while (separatorBounds.OuterWidth < hintBounds.OuterWidth || separatorBounds.OuterWidth < labelBounds.OuterWidth) {
			separatorText += "_";
			separatorFont.AutoBoxSize(separatorText, separatorBounds);
		}

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
			.AddScrollingText(separatorText, font, labelBounds, "levelup-notification-animation-separator")
			.AddScrollingText(_labelHint, font, hintBounds, "levelup-notification-animation-hint")
			.EndChildElements()
			.Compose();

		var label = SingleComposer.GetScrollingText("levelup-notification-animation-text");
		var separator = SingleComposer.GetScrollingText("levelup-notification-animation-separator");
		var hint = SingleComposer.GetScrollingText("levelup-notification-animation-hint");
		_animation = new Tween()
			.TweenProperty(v => separator.VisibleFactor = v, 0.0, 1.0, (float)FADE_IN_DURATION)
			.Wait(0.1f)
			.TweenProperty(v => label.VisibleFactor = v, 0.0, 1.0, (float)FADE_IN_DURATION)
			.Wait(0.1f)
			.TweenProperty(v => hint.VisibleFactor = v, 0.0, 1.0, (float)FADE_IN_DURATION)
			.WaitAll()
			.Wait((float)FADE_DELAY)
			.TweenProperty(v => label.Font.Color[3] = v, 1.0, 0.0, (float)FADE_OUT_DURATION)
			.Wait(0.2f)
			.TweenProperty(v => hint.Font.Color[3] = v, 1.0, 0.0, (float)FADE_OUT_DURATION - 0.2f)
			.Wait(0.2f)
			.TweenProperty(v => separator.Font.Color[3] = v, 1.0, 0.0, (float)FADE_OUT_DURATION - 0.4f);

		_animation.Finished += () => TryClose();
		_animation.SeekTo(0.0f);

		capi.World.PlaySoundAt(_levelUpSfx, capi.World.Player, randomizePitch: false);
	}

	public override void OnBeforeRenderFrame3D(float deltaTime) {
		base.OnBeforeRenderFrame3D(deltaTime);

		if (IsOpened()) {
			_animation?.Update(deltaTime);
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
