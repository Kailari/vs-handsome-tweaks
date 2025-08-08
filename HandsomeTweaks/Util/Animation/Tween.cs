using System;
using System.Collections.Generic;
using System.Linq;

using Vintagestory.API.MathTools;

namespace Jakojaannos.HandsomeTweaks.Util.Animation;

public class Tween {
	public event Action? Finished;
	public float TotalDuration => _propertyTweeners.Select(tweener => tweener.FinishesAt).Max();

	private float _elapsed = 0.0f;
	private float _delay = 0.0f;

	private List<IPropertyTweener> _propertyTweeners = new();
	private bool _finished = false;


	public Tween TweenProperty(Action<double> setter, double from, double to, float duration) {
		var tweener = new PropertyTweener<double>() {
			Setter = setter,
			Lerp = (a, b, t) => GameMath.Lerp(a, b, t),
			From = from,
			To = to,
			Duration = duration,
			Delay = _delay,
		};
		_propertyTweeners.Add(tweener);

		return this;
	}

	public Tween WaitAllFinished() {
		_delay = _propertyTweeners.Count == 0
			? 0.0f
			 : _propertyTweeners.Select(tweener => tweener.FinishesAt).Max();
		return this;
	}

	public Tween Wait(float delay) {
		_delay += delay;
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
		_propertyTweeners.ForEach(tween => tween.Update(_elapsed));

		_finished = seconds >= TotalDuration;
	}
}
