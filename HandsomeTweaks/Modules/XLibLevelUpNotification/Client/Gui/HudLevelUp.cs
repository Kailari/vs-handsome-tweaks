using System;

using Jakojaannos.HandsomeTweaks.Util;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Jakojaannos.HandsomeTweaks.Modules.XLibLevelUpNotification.Client.Gui;

public class HudLevelUp : HudElement {

	const double LETTERS_PER_SECOND = 12.5;
	const int TICKS_PER_LETTER = 5;
	const double FADE_DELAY = 3.0;
	const double FADE_DURATION = 2.75;

	public const string ID = "levelupnotification";

	private readonly AssetLocation _levelUpSfx;
	private readonly string _labelText;
	private long? _tickListener = null;

	public HudLevelUp(ICoreClientAPI capi, string message = "SNEAK INCREASED TO 100") : base(capi) {
		capi
			.ChatCommands
			.Create("jj.debug.levelup")
			.WithDescription("Show the level up notification")
			.HandleWith(OnDebugLevelUp);

		_levelUpSfx = Assets.Path("sounds/level-up");
		_labelText = message;
	}

	private TextCommandResult OnDebugLevelUp(TextCommandCallingArgs args) {
		if (IsOpened()) {
			TryClose();
		} else {
			TryOpen();
		}

		return TextCommandResult.Success();
	}

	public override void OnGuiOpened() {
		var font = CairoFont.WhiteMediumText();
		font.Orientation = EnumTextOrientation.Left;

		var ghostFont = font.Clone();
		ghostFont.Color[3] = 0.0;

		var invisibleFont = CairoFont.WhiteMediumText();
		invisibleFont.Color = new[] { 0.0, 0.0, 0.0, 0.0 };

		var invisibleBounds = ElementBounds.Fixed(0, 0);
		invisibleFont.AutoBoxSize(_labelText, invisibleBounds);
		invisibleBounds.WithFixedAlignmentOffset(-10.0, 0.0);
		invisibleBounds.fixedWidth += 20.0;

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
			.WithChild(invisibleBounds)
			.WithChild(ghostBounds)
			.WithChild(labelBounds);

		SingleComposer = capi
			.Gui
			.CreateCompo(Guis.Id(ID), panelBounds)
			.AddContainer(ElementBounds.Fill)
			.BeginChildElements()
			.AddStaticText(_labelText, invisibleFont, invisibleBounds)
			.AddDynamicText("", ghostFont, ghostBounds, "levelup-notification-animation-text-ghost")
			.AddDynamicText("", font, labelBounds, "levelup-notification-animation-text")
			.EndChildElements()
			.Compose();

		capi.World.PlaySoundAt(_levelUpSfx, capi.World.Player, randomizePitch: false);

		var ticks = 0;
		var tickDuration = 1.0 / (LETTERS_PER_SECOND * TICKS_PER_LETTER);
		_tickListener = capi.Event.RegisterGameTickListener(_ => {
			var label = SingleComposer.GetDynamicText("levelup-notification-animation-text");
			var ghostLabel = SingleComposer.GetDynamicText("levelup-notification-animation-text-ghost");

			ticks++;
			var letters = ticks / TICKS_PER_LETTER;
			var end = Math.Clamp(letters, 0, _labelText.Length);
			if (end < _labelText.Length && _labelText[end] == ' ') {
				ticks += TICKS_PER_LETTER;
				letters = ticks / TICKS_PER_LETTER;
				end = Math.Clamp(letters, 0, _labelText.Length);
			}

			var overflowTicks = ticks % TICKS_PER_LETTER;
			var ghostEnd = Math.Clamp(end + (overflowTicks > 0 ? 1 : 0), 0, _labelText.Length);
			var ghostAlpha = overflowTicks / (double)TICKS_PER_LETTER;

			var textPortion = _labelText[..end] ?? "";
			var ghostTextPortion = _labelText[..ghostEnd] ?? "";

			var totalTicks = _labelText.Length * TICKS_PER_LETTER;
			var overshoot = Math.Max(0, ticks - totalTicks);
			var inSeconds = overshoot * tickDuration;
			var adjusted = Math.Max(0.0, inSeconds - FADE_DELAY);
			var rescaled = adjusted / FADE_DURATION;
			var clamped = Math.Clamp(rescaled, 0.0, 1.0);
			var alpha = 1.0 - clamped;
			label.Font.Color[3] = alpha;
			if (overshoot > 0) {
				ghostLabel.Font.Color[3] = 0.0;
			} else {
				ghostLabel.Font.Color[3] = Math.Clamp(ghostAlpha, 0.0, 1.0);
			}

			ghostLabel.SetNewText(ghostTextPortion, forceRedraw: true);
			label.SetNewText(textPortion, forceRedraw: true);

			if (alpha < 0.001) {
				TryClose();
			}
		}, (int)(tickDuration * 1000));
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
