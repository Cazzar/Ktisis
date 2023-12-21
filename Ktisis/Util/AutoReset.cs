using System;
using System.Collections.Generic;

namespace Ktisis.Util {
	public static class AutoReset {
		public enum ResetType {
			GPoseCamera,
		}
		
		private static Dictionary<ResetType, List<Action>> _resetActions = new();
		
		public static void Set<T>(ResetType type, T value, Action<T> set, T newValue) {
			if (!_resetActions.ContainsKey(type))
				_resetActions[type] = [];
			
			_resetActions[type].Add(() => set(value));
			set(newValue);
		}
		
		public static void Reset(ResetType type) {
			if (!_resetActions.TryGetValue(type, out List<Action>? value))
				return;
			
			foreach (var action in value) {
				action();
			}
			
			_resetActions.Clear();
		}

		public static void ResetAll() {
			foreach (var resetType in Enum.GetValues<ResetType>()) {
				Reset(resetType);
			}
		}
	}
}
