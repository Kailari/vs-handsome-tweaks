using System;
using System.Collections.Generic;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

using XLib.XLeveling;

using Jakojaannos.HandsomeTweaks.Util;
using Jakojaannos.HandsomeTweaks.Util.Animation;

namespace Jakojaannos.HandsomeTweaks.Modules.XLibLevelUpNotification.Client.Gui;

public class HudLevelUp : HudElement {
	private const float FADE_IN_DURATION = 1.75f;
	private const float FADE_DELAY = 2.5f;
	private const float FADE_OUT_DURATION = 2f;

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
		return Lang.Get(Guis.TranslationKey("xskills/level-up/label"), skill, level.ToString());
	}

	private static string FormatOpenSkillUIHint(ICoreClientAPI capi) {
		var hotkey = capi.Input.GetHotKeyByCode("skilldialoghotkey");

		var parts = new List<string>();
		if (hotkey.CurrentMapping.Ctrl) {
			parts.Add("ctrl");
		}
		if (hotkey.CurrentMapping.Shift) {
			parts.Add("shift");
		}
		if (hotkey.CurrentMapping.Alt) {
			parts.Add("alt");
		}
		parts.Add(hotkey.CurrentMapping.PrimaryAsString());
		var hotkeyString = string.Join(" + ", parts);
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
		var labelFont = CairoFont.WhiteMediumText();
		labelFont.Orientation = EnumTextOrientation.Left;

		var labelBounds = ElementBounds.Fixed(0, 0);
		labelFont.AutoBoxSize(_labelText, labelBounds);

		var separatorFont = CairoFont.WhiteMediumText();
		separatorFont.FontWeight = Cairo.FontWeight.Bold;

		var separatorText = "________";
		var separatorBounds = ElementBounds.Fixed(0, 0);
		separatorFont.AutoBoxSize(separatorText, separatorBounds);

		var hintBounds = ElementBounds.Fixed(0, 0);
		var hintFont = CairoFont.WhiteSmallText();
		hintFont.Slant = Cairo.FontSlant.Italic;
		hintFont.Orientation = EnumTextOrientation.Left;
		hintFont.AutoBoxSize(_labelHint, hintBounds);

		while (separatorBounds.fixedWidth < hintBounds.fixedWidth * 1.2f || separatorBounds.fixedWidth < labelBounds.fixedWidth * 1.2f) {
			separatorText += "_";
			separatorFont.AutoBoxSize(separatorText, separatorBounds);
		}

		var w = Math.Max(labelBounds.fixedWidth, Math.Max(separatorBounds.fixedWidth, hintBounds.fixedWidth));
		labelBounds.fixedX = (w - labelBounds.fixedWidth) / 2.0f;
		separatorBounds.fixedX = (w - separatorBounds.fixedWidth) / 2.0f;
		hintBounds.fixedX = (w - hintBounds.fixedWidth) / 2.0f;

		var separatorOffset = separatorBounds.fixedHeight / -2.0f;

		separatorBounds.fixedY = labelBounds.fixedHeight + separatorOffset;
		hintBounds.fixedY = labelBounds.fixedHeight + separatorBounds.fixedHeight + separatorOffset;

		// Position horizontally centered and vertically at 25% from the top of the screen
		var screenHeight = capi.Gui.WindowBounds.InnerHeight / RuntimeEnv.GUIScale;
		var offsetY = screenHeight / 4.0f;

		var panelBounds = ElementBounds
			.FixedPos(EnumDialogArea.CenterTop, 0.0, 0.0)
			.WithFixedAlignmentOffset(0.0, offsetY)
			.WithSizing(ElementSizing.FitToChildren)
			.WithChild(labelBounds)
			.WithChild(separatorBounds)
			.WithChild(hintBounds);

		SingleComposer = capi
			.Gui
			.CreateCompo(Guis.Id(ID), panelBounds)
			.AddContainer(ElementBounds.Fill)
			.BeginChildElements()
			.AddScrollingText(_labelText, labelFont, labelBounds, "levelup-notification-animation-text")
			.AddScrollingText(separatorText, separatorFont, separatorBounds, "levelup-notification-animation-separator")
			.AddScrollingText(_labelHint, hintFont, hintBounds, "levelup-notification-animation-hint")
			.EndChildElements()
			.Compose();

		var label = SingleComposer.GetScrollingText("levelup-notification-animation-text");
		var separator = SingleComposer.GetScrollingText("levelup-notification-animation-separator");
		var hint = SingleComposer.GetScrollingText("levelup-notification-animation-hint");
		_animation = new Tween()
			.TweenProperty(v => separator.VisibleFactor = v, 0.0, 1.0, FADE_IN_DURATION)
			.Wait(0.25f)
			.TweenProperty(v => label.VisibleFactor = v, 0.0, 1.0, FADE_IN_DURATION)
			.Wait(1.0f)
			.TweenProperty(v => hint.VisibleFactor = v, 0.0, 1.0, FADE_IN_DURATION)
			.WaitAllFinished()
			.Wait(FADE_DELAY)
			.TweenProperty(v => label.Font.Color[3] = v, 1.0, 0.0, FADE_OUT_DURATION + 0.5f)
			.Wait(0.5f)
			.TweenProperty(v => separator.Font.Color[3] = v, 1.0, 0.0, FADE_OUT_DURATION + 0.5f)
			.Wait(1.0f)
			.TweenProperty(v => hint.Font.Color[3] = v, 1.0, 0.0, FADE_OUT_DURATION);

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
