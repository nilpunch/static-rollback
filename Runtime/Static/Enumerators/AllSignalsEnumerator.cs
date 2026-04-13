using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace Shenanicode.Rollback {
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public struct AllSignalsEnumerator<T> where T : ISignal {
		private readonly AllSignals<T> _allSignals;
		private int _index;

		public AllSignalsEnumerator(AllSignals<T> allSignals) {
			_allSignals = allSignals;
			_index = -1;
		}

		public Signal<T> Current {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _allSignals.Signals[_index];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool MoveNext() {
			return ++_index < _allSignals.Count;
		}
	}
}
