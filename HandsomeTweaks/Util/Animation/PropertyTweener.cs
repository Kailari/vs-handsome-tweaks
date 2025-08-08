using System;

using Vintagestory.API.MathTools;

namespace Jakojaannos.HandsomeTweaks.Util.Animation;

public interface IPropertyTweener {
	public float FinishesAt { get; }

	public void Update(float t);
}

public class PropertyTweener<T> : IPropertyTweener {
	public required Action<T> Setter { get; init; }
	public required Func<T, T, float, T> Lerp { get; init; }
	public required float Duration { get; init; }
	public EnumTransformFunction Transform { get; init; } = EnumTransformFunction.LINEAR;
	public float Delay { get; init; } = 0.0f;

	public required T From { get; init; }
	public required T To { get; init; }

	public float FinishesAt => Delay + Duration;

	private EvolvingNatFloat? _generator;

	public void Update(float t) {
		_generator ??= new(Transform, 1.0f / Duration);

		var adjustedTime = Math.Clamp(t - Delay, 0.0f, Duration);
		var d = _generator.nextFloat(0.0f, adjustedTime);
		var value = Lerp(From, To, d);
		Setter(value);
	}
}
