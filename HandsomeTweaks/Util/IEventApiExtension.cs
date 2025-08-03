using System;

using Vintagestory.API.Common;

namespace Jakojaannos.HandsomeTweaks.Util;

public static class IEventAPIExtension {
	public static long TickOnceAfterDelay(this IEventAPI @this, Action<float> onGameTick, int delayMilliseconds) {
		long? listenerId = null;
		listenerId = @this.RegisterGameTickListener((value) => {
			try {
				onGameTick(value);
			} finally {
				if (listenerId is long id) {
					@this.UnregisterGameTickListener(id);
				}
			}
		}, delayMilliseconds);

		return listenerId.Value;
	}
}
