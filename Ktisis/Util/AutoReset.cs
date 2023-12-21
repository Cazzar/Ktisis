using System;
using System.Collections.Concurrent;

namespace Ktisis.Util {
	public static class AutoReset {
		public enum ResetType {
			GPoseCamera,
		}
		
		private static ConcurrentDictionary<ResetType, ConcurrentQueue<Action>> _resetActions = new();
		
		public static void Set<T>(ResetType type, T value, Action<T> set, T newValue) {
			if (!_resetActions.ContainsKey(type))
				_resetActions[type] = [];
			
			_resetActions[type].Enqueue(() => set(value));
			set(newValue);
		}
		
		public static void Reset(ResetType type) {
			Services.Framework.RunOnFrameworkThread(() => {
				if (!_resetActions.TryGetValue(type, out var value))
					return;

				while (value.TryDequeue(out var action))
					action();
			});
		}

		public static void ResetAll() {
			foreach (var resetType in Enum.GetValues<ResetType>()) {
				Reset(resetType);
			}
		}
	}
}
