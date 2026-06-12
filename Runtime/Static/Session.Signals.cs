using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FFS.Libraries.StaticPack;
using Unity.IL2CPP.CompilerServices;

namespace Shenanicode.Rollback {
	public enum ReadResult : byte {
		Failure,
		NotEnoughData,
		Success,
	}

	public enum SetResult : byte {
		Applied,
		Duplicate,
		Conflict,
	}

	public interface ISignal {
		void Write(ref BinaryPackWriter writer) { }

		/// <summary>
		/// You don't need to undo reader state in case of <see cref="ReadResult.Failure"/> or <see cref="ReadResult.NotEnoughData"/>.
		/// </summary>
		ReadResult Read(ref BinaryPackReader reader) {
			return ReadResult.Failure;
		}

		/// <summary>
		/// Will not be called after unsuccessful read.
		/// Will not be called when there is no Write or Read implementation.
		/// </summary>
		void Dispose() { }

		bool IsEquivalentTo(ISignal other) {
			return true;
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Signal<T> where T : ISignal {
		public ushort Channel;
		public byte LocalOrder;
		public bool IsApproved;
		public T Data;

		public Signal(ushort channel, byte localOrder, T data, bool isApproved) {
			Data = data;
			Channel = channel;
			LocalOrder = localOrder;
			IsApproved = isApproved;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out ushort channel, out T data) {
			data = Data;
			channel = Channel;
		}
	}

	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public struct AllSignals<T> where T : ISignal {
		public int Count;
		public bool HasDispose;

		public Signal<T>[] Signals;
		public int SignalsCapacity;

		public readonly bool HasAny => Count != 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureInitialized(bool hasDispose) {
			Signals ??= Array.Empty<Signal<T>>();
			HasDispose = hasDispose;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte AppendPrediction(ushort channel, T data) {
			GetAppendPosition(channel, out var index, out var localOrder);
			InsertSignal(index, channel, localOrder, data, isPrediction: true);
			return localOrder;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte AppendApproved(ushort channel, T data) {
			GetAppendPosition(channel, out var index, out var localOrder);
			InsertSignal(index, channel, localOrder, data, isPrediction: false);
			return localOrder;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public SetResult SetApproved(ushort channel, byte localOrder, T data) {
			var index = BinarySearch(channel, localOrder);
			if (index >= 0) {
				ref var existingSignal = ref Signals[index];
				if (!existingSignal.Data.IsEquivalentTo(data)) {
					return SetResult.Conflict;
				}

				var result = existingSignal.IsApproved ? SetResult.Duplicate : SetResult.Applied;
				if (HasDispose) {
					existingSignal.Data.Dispose();
				}

				existingSignal = new Signal<T>(channel, localOrder, data, isApproved: true);
				return result;
			}

			InsertSignal(~index, channel, localOrder, data, isPrediction: false);
			return SetResult.Applied;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Clear() {
			if (HasDispose) {
				for (var i = 0; i < Count; i++) {
					Signals[i].Data.Dispose();
					Signals[i] = default;
				}
			}
			Count = 0;
		}

		/// <summary>
		/// Returns the number of cleared signals.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int ClearPrediction() {
			var clearedCount = 0;
			var writeIndex = 0;
			for (var readIndex = 0; readIndex < Count; readIndex++) {
				if (!Signals[readIndex].IsApproved) {
					if (HasDispose) {
						Signals[readIndex].Data.Dispose();
					}
					clearedCount++;
				}
				else {
					if (writeIndex != readIndex) {
						Signals[writeIndex] = Signals[readIndex];
					}
					writeIndex++;
				}
			}

			for (var i = writeIndex; i < Count; i++) {
				Signals[i] = default;
			}

			Count = writeIndex;
			return clearedCount;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly int DenseCount() {
			return Count;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureSignalAt(int index) {
			if (index >= SignalsCapacity) {
				ResizeSignals(MathUtils.RoundUpToPowerOfTwo(index + 1));
			}
		}

		public void ResizeSignals(int capacity) {
			Signals = Signals.Resize(capacity);
			SignalsCapacity = capacity;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool HasAnyAtIndex(int index) {
			return (uint)index < (uint)Count;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void InsertSignal(int index, ushort channel, byte localOrder, T data, bool isPrediction) {
			EnsureSignalAt(Count);

			for (var i = Count; i > index; i--) {
				Signals[i] = Signals[i - 1];
			}

			Signals[index] = new Signal<T>(channel, localOrder, data, isApproved: !isPrediction);
			Count++;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int GetMedian(int low, int high) {
			return low + ((high - low) >> 1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int Compare(ushort leftChannel, int leftLocalOrder, ushort rightChannel, int rightLocalOrder) {
			var channelCompare = leftChannel.CompareTo(rightChannel);
			if (channelCompare != 0) {
				return channelCompare;
			}

			return leftLocalOrder.CompareTo(rightLocalOrder);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int BinarySearch(ushort channel, int localOrder) {
			var low = 0;
			var high = Count - 1;
			while (low <= high) {
				var median = GetMedian(low, high);
				ref var signal = ref Signals[median];
				var comparison = Compare(signal.Channel, signal.LocalOrder, channel, localOrder);
				if (comparison == 0) {
					return median;
				}

				if (comparison < 0) {
					low = median + 1;
				}
				else {
					high = median - 1;
				}
			}

			return ~low;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal int GetIndex(ushort channel, byte localOrder) {
			var index = BinarySearch(channel, localOrder);
			if (index < 0) {
				throw new InvalidOperationException($"Signal at channel {channel} with local order {localOrder} was not found.");
			}

			return index;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void GetAppendPosition(ushort channel, out int insertIndex, out byte localOrder) {
			var index = BinarySearch(channel, byte.MaxValue);
			insertIndex = index >= 0 ? index + 1 : ~index;
			if (insertIndex > 0 && Signals[insertIndex - 1].Channel == channel) {
				var previousLocalOrder = Signals[insertIndex - 1].LocalOrder;
				if (previousLocalOrder == byte.MaxValue) {
					throw new InvalidOperationException($"Channel {channel} exceeded the maximum local order of {byte.MaxValue} signals per tick.");
				}

				localOrder = (byte)(previousLocalOrder + 1);
				return;
			}

			localOrder = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly AllSignalsEnumerator<T> GetEnumerator() {
			return new AllSignalsEnumerator<T>(this);
		}
	}

	public abstract partial class Session<TSessionType> where TSessionType : ISessionType {
		public struct Signals<T> where T : struct, ISignal {
			public static Signals<T> Instance;
			public static SignalHandle Handle;

			private readonly IPredictionReceiver _predictionReceiver;
			private CyclicBuffer<AllSignals<T>> _signalsBuffer;

			private readonly bool _isUnmanagedPack;
			private readonly uint _sizeOfUnmanaged;
			private readonly bool _hasWrite;
			private readonly bool _hasRead;
			private readonly bool _hasDispose;

			[MethodImpl(MethodImplOptions.NoInlining)]
			internal static void AutoRegister() {
				RegisterSignalType<T>();
			}

			public Signals(int startTick, IPredictionReceiver predictionReceiver = null) {
				_predictionReceiver = predictionReceiver;
				_signalsBuffer = CyclicBuffer<AllSignals<T>>.Create(startTick);

				_isUnmanagedPack = false;
				_sizeOfUnmanaged = 0;
				_hasWrite = SignalType<T>.HasWrite();
				_hasRead = SignalType<T>.HasRead();
				_hasDispose = SignalType<T>.HasDispose();

				if (!_hasWrite && !_hasRead) {
					_isUnmanagedPack = TypeUtils.TryRegisterUnmanagedPacking<T>();
					if (_isUnmanagedPack) {
						_sizeOfUnmanaged = (uint)TypeUtils.SizeOfUnmanaged(typeof(T));
					}
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ref AllSignals<T> GetAllSignals(int tick) {
				return ref _signalsBuffer[tick];
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public SetResult SetApproved(int tick, byte localOrder, ushort channel, T data) {
				PopulateUpTo(tick);

				var result = _signalsBuffer[tick].SetApproved(channel, localOrder, data);
				switch (result) {
					case SetResult.Applied:
						NotifyGlobalChange(tick);
						return SetResult.Applied;

					case SetResult.Duplicate:
						return SetResult.Duplicate;

					case SetResult.Conflict:
						return SetResult.Conflict;

					default:
						throw new ArgumentOutOfRangeException();
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public byte AppendApproved(int tick, ushort channel, T data) {
				PopulateUpTo(tick);

				var localOrder = _signalsBuffer[tick].AppendApproved(channel, data);

				NotifyGlobalChange(tick);
				return localOrder;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public byte AppendPrediction(int tick, ushort channel, T data) {
				PopulateUpTo(tick);

				var localOrder = _signalsBuffer[tick].AppendPrediction(channel, data);

				NotifyGlobalChange(tick);
				_predictionReceiver?.OnSignalPredicted<T>(tick, channel, localOrder);
				return localOrder;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Clear(int tick) {
				PopulateUpTo(tick);

				_signalsBuffer[tick].Clear();

				NotifyGlobalChange(tick);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int ClearPrediction(int startTick, int endTick) {
				if (startTick < _signalsBuffer.HeadIndex) {
					startTick = _signalsBuffer.HeadIndex;
				}

				if (endTick > _signalsBuffer.TailIndex - 1) {
					endTick = _signalsBuffer.TailIndex - 1;
				}

				var clearedTicksCount = 0;
				for (var tick = startTick; tick <= endTick; tick++) {
					var clearedCount = _signalsBuffer[tick].ClearPrediction();
					if (clearedCount > 0) {
						clearedTicksCount++;
					}
				}

				if (clearedTicksCount > 0) {
					NotifyGlobalChange(startTick);
				}

				return clearedTicksCount;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void PopulateUpTo(int tick) {
				for (var i = _signalsBuffer.TailIndex; i <= tick; i++) {
					ref var signals = ref _signalsBuffer.Append();
					signals.EnsureInitialized(_hasDispose);
					signals.Clear();
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void DiscardUpTo(int tick) {
				if (_hasDispose) {
					var endTick = MathUtils.Min(tick, _signalsBuffer.TailIndex);
					for (var i = _signalsBuffer.HeadIndex; i < endTick; i++) {
						_signalsBuffer[i].Clear();
					}
				}

				_signalsBuffer.RemoveUpTo(tick);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void HardReset(int startTick) {
				if (_hasDispose) {
					for (var i = _signalsBuffer.HeadIndex; i < _signalsBuffer.TailIndex; i++) {
						_signalsBuffer[i].Clear();
					}
				}

				_signalsBuffer.Reset(startTick);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ReadResult ReadApproved(int tick, byte localOrder, ushort channel, ref BinaryPackReader reader) {
				PopulateUpTo(tick);

				if (_isUnmanagedPack) {
					if (!reader.HasNext(_sizeOfUnmanaged)) {
						return ReadResult.NotEnoughData;
					}

					var input = reader.Read<T>();
					var result = _signalsBuffer[tick].SetApproved(channel, localOrder, input);
					switch (result) {
						case SetResult.Applied:
							NotifyGlobalChange(tick);
							return ReadResult.Success;

						case SetResult.Duplicate:
							return ReadResult.Success;

						case SetResult.Conflict:
							if (_hasDispose) {
								input.Dispose();
							}
							return ReadResult.Failure;

						default:
							throw new ArgumentOutOfRangeException();
					}
				}
				else {
					if (!_hasRead) {
						throw new Exception($"Method Read not implemented for input type {typeof(T)}");
					}

					var input = default(T);
					var lastPosition = reader.Position;
					var result = input.Read(ref reader);
					if (result != ReadResult.Success) {
						reader.Position = lastPosition;
						return result;
					}

					var applyResult = _signalsBuffer[tick].SetApproved(channel, localOrder, input);
					switch (applyResult) {
						case SetResult.Applied:
							NotifyGlobalChange(tick);
							return ReadResult.Success;

						case SetResult.Duplicate:
							return ReadResult.Success;

						case SetResult.Conflict:
							if (_hasDispose) {
								input.Dispose();
							}
							return ReadResult.Failure;

						default:
							throw new ArgumentOutOfRangeException();
					}
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Write(int tick, int index, ref BinaryPackWriter writer) {
				if (_isUnmanagedPack) {
					writer.Write(_signalsBuffer[tick].Signals[index].Data);
				}
				else {
					if (!_hasWrite) {
						throw new Exception($"Method Write not implemented for input type {typeof(T)}");
					}

					_signalsBuffer[tick].Signals[index].Data.Write(ref writer);
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ReadResult Skip(ref BinaryPackReader reader) {
				if (_isUnmanagedPack) {
					if (!reader.HasNext(_sizeOfUnmanaged)) {
						return ReadResult.NotEnoughData;
					}

					reader.SkipNext(_sizeOfUnmanaged);
				}
				else {
					if (!_hasWrite) {
						throw new Exception($"Method Write not implemented for input type {typeof(T)}");
					}

					var input = default(T);
					var previousReader = reader;
					var result = input.Read(ref reader);
					if (result != ReadResult.Success) {
						reader = previousReader;
						return result;
					}

					if (_hasDispose) {
						input.Dispose();
					}
				}

				return ReadResult.Success;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int GetSignalsCount(int tick) {
				return _signalsBuffer[tick].DenseCount();
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ushort GetSignalChannel(int tick, int index) {
				return _signalsBuffer[tick].Signals[index].Channel;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public byte GetSignalLocalOrder(int tick, int index) {
				return _signalsBuffer[tick].Signals[index].LocalOrder;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int GetSignalIndex(int tick, ushort channel, byte localOrder) {
				return _signalsBuffer[tick].GetIndex(channel, localOrder);
			}
		}
	}

	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppEagerStaticClassConstruction]
	internal static class SignalType<
		#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
		#endif
		T> where T : struct, ISignal {
		private static readonly Type[] WriteParams = {
			typeof(BinaryPackWriter).MakeByRefType()
		};

		private static readonly Type[] ReadParams = {
			typeof(BinaryPackReader).MakeByRefType(), typeof(ReadResult).MakeByRefType()
		};

		private static readonly Type[] DisposeParams =
			{ };

		internal static bool HasWrite() {
			return HasMethod(typeof(T), nameof(ISignal.Write), WriteParams);
		}

		internal static bool HasRead() {
			return HasMethod(typeof(T), nameof(ISignal.Read), ReadParams);
		}

		internal static bool HasDispose() {
			return HasMethod(typeof(T), nameof(ISignal.Dispose), DisposeParams);
		}

		private static bool HasMethod(
			#if NET5_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
			#endif
			Type structType,
			string methodName,
			Type[] parameterTypes) {
			var methods = structType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
			foreach (var methodInfo in methods) {
				if (methodInfo.Name == methodName && methodInfo.IsGenericMethodDefinition) {
					var parameters = methodInfo.GetParameters();
					if (parameters.Length == parameterTypes.Length) {
						var match = true;
						for (var i = 0; i < parameters.Length; i++) {
							if (parameterTypes[i].Name != parameters[i].ParameterType.Name) {
								match = false;
								break;
							}
						}
						if (match)
							return true;
					}
				}
			}
			return false;
		}
	}
}
