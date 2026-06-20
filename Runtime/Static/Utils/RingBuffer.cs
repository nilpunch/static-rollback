using System;
using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace Shenanicode.Rollback {
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public struct RingBuffer<T> {
		private const int InitialCapacity = 128;

		internal T[] Data;

		internal int CycleCapacityMinusOne { get; private set; }

		internal int CycledCount { get; private set; }

		internal int TailIndex { get; private set; }

		public int HeadIndex {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TailIndex - CycledCount;
		}

		public static RingBuffer<T> Create(int startIndex = 0, int initialCapacity = InitialCapacity) {
			return new RingBuffer<T>() {
				Data = new T[initialCapacity],
				CycleCapacityMinusOne = initialCapacity - 1,
				TailIndex = startIndex,
			};
		}

		public ref T this[int index] {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				if (index < HeadIndex || index >= TailIndex) {
					throw new ArgumentOutOfRangeException(nameof(index), index, $"List works in range [{HeadIndex}, {TailIndex}).");
				}
				return ref Data[index & CycleCapacityMinusOne];
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Append(T data) {
			Append() = data;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref T Append() {
			if (CycleCapacityMinusOne < CycledCount) {
				Resize(MathUtils.RoundUpToPowerOfTwo(CycledCount + 1));
			}

			ref var data = ref Data[TailIndex & CycleCapacityMinusOne];
			TailIndex++;
			CycledCount++;

			return ref data;
		}

		/// <summary>
		/// Removes elements from the beginning (head) up to the specified absolute index.
		/// The index must be in the range [HeadIndex, TailIndex].
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void RemoveUpTo(int index) {
			if (index < HeadIndex) {
				index = HeadIndex;
			}
			if (index > TailIndex) {
				index = TailIndex;
			}

			var removeCount = index - HeadIndex;
			CycledCount -= removeCount;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Reset(int startIndex) {
			CycledCount = 0;
			TailIndex = startIndex;
		}

		private void Resize(int newCapacity) {
			var newCapacityMinusOne = newCapacity - 1;
			var newData = new T[newCapacity];
			for (var i = HeadIndex; i < TailIndex; i++) {
				newData[i & newCapacityMinusOne] = Data[i & CycleCapacityMinusOne];
			}
			Data = newData;
			CycleCapacityMinusOne = newCapacityMinusOne;
		}
	}
}
