using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FFS.Libraries.StaticPack;
using Unity.IL2CPP.CompilerServices;

namespace Shenanicode.Rollback {
	public interface IInput { }

	[StructLayout(LayoutKind.Sequential)]
	public struct Input<T> where T : unmanaged, IInput {
		public ushort TicksPassed;
		public bool IsApproved;
		public T Data;

		public Input(T data, ushort ticksPassed, bool isApproved) {
			Data = data;
			TicksPassed = ticksPassed;
			IsApproved = isApproved;
		}

		public bool IsFresh {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TicksPassed == 0;
		}

		public static readonly Input<T> Stale = new(StaticTypeConfig<T>.DefaultValue, ushort.MaxValue, false);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Input<T> Aged() {
			unchecked {
				var clampedNextTick = (ushort)(TicksPassed + 1);
				if (clampedNextTick < TicksPassed)
				{
					clampedNextTick = ushort.MaxValue;
				}
				return new Input<T>(Data, clampedNextTick, IsApproved);
			}
		}
	}

	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public struct AllInputs<T> where T : unmanaged, IInput {
		public int UsedChannels { get; private set; }

		public Input<T>[] Inputs { get; private set; }

		public int InputsCapacity { get; private set; }

		public ulong[] FreshMask { get; private set; }

		public int FreshMaskLength => (UsedChannels + 63) >> 6;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureInitialized() {
			Inputs ??= Array.Empty<Input<T>>();
			FreshMask ??= Array.Empty<ulong>();
			InputsCapacity = Inputs.Length;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Input<T> Get(ushort channel) {
			if (channel >= UsedChannels) {
				return Input<T>.Stale;
			}

			return Inputs[channel];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public SetResult SetApproved(ushort channel, T input) {
			EnsureChannel(channel);

			ref var existingInput = ref Inputs[channel];
			if (existingInput.IsFresh) {
				if (!EqualityComparer<T>.Default.Equals(existingInput.Data, input)) {
					return SetResult.Conflict;
				}

				var result = existingInput.IsApproved ? SetResult.Duplicate : SetResult.Applied;
				existingInput = new Input<T>(input, 0, true);
				FreshMask[channel >> 6] |= 1UL << (channel & 63);
				return result;
			}

			existingInput = new Input<T>(input, 0, true);
			FreshMask[channel >> 6] |= 1UL << (channel & 63);
			return SetResult.Applied;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public SetResult SetPrediction(ushort channel, T input) {
			EnsureChannel(channel);

			if (Inputs[channel].IsFresh) {
				return SetResult.Duplicate;
			}

			Inputs[channel] = new Input<T>(input, 0, false);
			FreshMask[channel >> 6] |= 1UL << (channel & 63);
			return SetResult.Applied;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetFullInput(ushort channel, Input<T> input) {
			EnsureChannel(channel);

			Inputs[channel] = input;
			if (input.IsFresh) {
				FreshMask[channel >> 6] |= 1UL << (channel & 63);
			}
			else {
				FreshMask[channel >> 6] &= ~(1UL << (channel & 63));
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureChannel(ushort channel) {
			// If channel already there, nothing to be done.
			if (channel < UsedChannels) {
				return;
			}

			EnsureInputForChannel(channel);

			UsedChannels = channel + 1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Clear() {
			Array.Fill(Inputs, Input<T>.Stale);

			var freshMaskLength = FreshMaskLength;

			for (var i = 0; i < freshMaskLength; i++) {
				FreshMask[i] = 0;
			}

			UsedChannels = 0;
		}

		/// <summary>
		/// Returns the number of cleared inputs.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int ClearPrediction() {
			var staleInput = Input<T>.Stale;

			var clearedCount = 0;
			foreach (var channel in GetFreshInputs()) {
				if (!Inputs[channel].IsApproved) {
					Inputs[channel] = staleInput;
					FreshMask[channel >> 6] &= ~(1UL << (channel & 63));
					clearedCount++;
				}
			}

			return clearedCount;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureInputForChannel(ushort channel) {
			if (channel >= InputsCapacity) {
				ResizeInputs(MathUtils.RoundUpToPowerOfTwo(channel + 1));
			}
		}

		/// <summary>
		/// Resizes the packed array to the specified capacity.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ResizeInputs(int capacity) {
			Inputs = Inputs.Resize(capacity);
			if (capacity > InputsCapacity) {
				Array.Fill(Inputs, Input<T>.Stale, InputsCapacity, capacity - InputsCapacity);
			}
			InputsCapacity = capacity;

			var maskLength = (InputsCapacity + 63) >> 6;
			if (maskLength > FreshMask.Length) {
				FreshMask = FreshMask.Resize(maskLength);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void CopyFrom(AllInputs<T> other) {
			EnsureChannel((ushort)(other.UsedChannels - 1));
			Array.Copy(other.Inputs, Inputs, other.UsedChannels);
			for (var i = 0; i < other.FreshMaskLength; i++) {
				FreshMask[i] = other.FreshMask[i];
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void CopyAgedFrom(AllInputs<T> other) {
			EnsureChannel((ushort)(other.UsedChannels - 1));
			for (var i = 0; i < other.UsedChannels; i++) {
				Inputs[i] = other.Inputs[i].Aged();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void CopyAgedIfNotFreshFrom(AllInputs<T> other) {
			EnsureChannel((ushort)(other.UsedChannels - 1));
			for (var i = 0; i < other.UsedChannels; i++) {
				if (!Inputs[i].IsFresh) {
					Inputs[i] = other.Inputs[i].Aged();
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FreshInputsCount() {
			var count = 0;
			for (var i = 0; i < FreshMaskLength; i++) {
				count += MathUtils.PopCount(FreshMask[i]);
			}
			return count;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MaskEnumerator GetFreshInputs() {
			return new MaskEnumerator(FreshMask, FreshMaskLength);
		}
	}

	public abstract partial class Session<TSessionType> where TSessionType : ISessionType {
		public struct Inputs<T> where T : unmanaged, IInput {
			public static Inputs<T> Instance;
			public static InputHandle Handle;

			private readonly IPredictionReceiver _predictionReceiver;
			private int _localEarliestChangedTick;
			private CyclicBuffer<AllInputs<T>> _inputs;

			private readonly uint _sizeOfUnmanaged;
			private readonly uint _sizeOfUnmanagedFull;

			[MethodImpl(MethodImplOptions.NoInlining)]
			internal static void AutoRegister() {
				RegisterInputType<T>();
			}

			public Inputs(int startTick, IPredictionReceiver predictionReceiver = null) {
				_predictionReceiver = predictionReceiver;
				_inputs = CyclicBuffer<AllInputs<T>>.Create(startTick);
				_inputs.Append().EnsureInitialized();
				_localEarliestChangedTick = startTick;

				_sizeOfUnmanaged = TypeUtils.SizeOf<T>();
				_sizeOfUnmanagedFull = TypeUtils.SizeOf<Input<T>>();
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ref AllInputs<T> GetInputs(int tick) {
				return ref _inputs[tick];
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public SetResult SetApproved(int tick, ushort channel, T input) {
				PopulateUpTo(tick);

				var result = _inputs[tick].SetApproved(channel, input);
				switch (result) {
					case SetResult.Applied:
						NotifyLocalChange(tick);
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
			public void SetPrediction(int tick, ushort channel, T input) {
				PopulateUpTo(tick);

				if (_inputs[tick].SetPrediction(channel, input) != SetResult.Applied) {
					return;
				}

				NotifyLocalChange(tick);
				NotifyGlobalChange(tick);
				_predictionReceiver?.OnInputPredicted<T>(tick, channel);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void SetInputs(int tick, AllInputs<T> allInputs) {
				PopulateUpTo(tick);

				_inputs[tick].CopyFrom(allInputs);

				NotifyLocalChange(tick);
				NotifyGlobalChange(tick);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int ClearPrediction(int startTick, int endTick) {
				if (startTick < _inputs.HeadIndex) {
					startTick = _inputs.HeadIndex;
				}

				if (endTick > _inputs.TailIndex - 1) {
					endTick = _inputs.TailIndex - 1;
				}

				var clearedTicksCount = 0;
				for (var currentTick = startTick; currentTick <= endTick; currentTick++) {
					var clearedCount = _inputs[currentTick].ClearPrediction();
					if (clearedCount > 0) {
						clearedTicksCount++;
					}
				}

				if (clearedTicksCount > 0) {
					NotifyLocalChange(startTick);
					NotifyGlobalChange(startTick);
				}

				return clearedTicksCount;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void HardReset(int startTick) {
				_inputs.Reset(startTick);
				ref var inputs = ref _inputs.Append();
				inputs.EnsureInitialized();
				inputs.Clear();
				_localEarliestChangedTick = startTick;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void PopulateUpTo(int tick) {
				for (var currentTick = _inputs.TailIndex; currentTick <= tick; currentTick++) {
					ref var inputs = ref _inputs.Append();
					inputs.EnsureInitialized();
					inputs.CopyAgedFrom(_inputs[currentTick - 1]);
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void DiscardUpTo(int tick) {
				// Ensure there at least one input left in list, so we can populate from it.
				if (tick > _inputs.TailIndex - 1) {
					tick = _inputs.TailIndex - 1;
				}

				_inputs.RemoveUpTo(tick);
				ConfirmLocalChangesUpTo(tick);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Reevaluate() {
				for (var i = _localEarliestChangedTick + 1; i < _inputs.TailIndex; i++) {
					_inputs[i].CopyAgedIfNotFreshFrom(_inputs[i - 1]);
				}

				ConfirmLocalChangesUpTo(_inputs.TailIndex);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ReadResult ReadApproved(int tick, ushort channel, ref BinaryPackReader reader) {
				PopulateUpTo(tick);

				if (!reader.HasNext(_sizeOfUnmanaged)) {
					return ReadResult.NotEnoughData;
				}

				var value = TypeUtils.UnmanagedRead<T>(ref reader);

				var result = _inputs[tick].SetApproved(channel, value);
				if (result == SetResult.Conflict) {
					return ReadResult.Failure;
				}

				if (result == SetResult.Duplicate) {
					return ReadResult.Success;
				}

				NotifyLocalChange(tick);
				NotifyGlobalChange(tick);

				return ReadResult.Success;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ReadResult ReadFullInput(int tick, ushort channel, ref BinaryPackReader reader) {
				PopulateUpTo(tick);

				if (!reader.HasNext(_sizeOfUnmanagedFull)) {
					return ReadResult.NotEnoughData;
				}

				var value = TypeUtils.UnmanagedRead<Input<T>>(ref reader);

				_inputs[tick].SetFullInput(channel, value);

				NotifyLocalChange(tick);
				NotifyGlobalChange(tick);

				return ReadResult.Success;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Write(int tick, ushort channel, ref BinaryPackWriter writer) {
				TypeUtils.UnmanagedWrite(ref writer, _inputs[tick].Get(channel).Data);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void WriteFullInput(int tick, ushort channel, ref BinaryPackWriter writer) {
				TypeUtils.UnmanagedWrite(ref writer, _inputs[tick].Get(channel));
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ReadResult Skip(ref BinaryPackReader reader) {
				if (!reader.HasNext(_sizeOfUnmanaged)) {
					return ReadResult.NotEnoughData;
				}

				reader.SkipNext(_sizeOfUnmanaged);

				return ReadResult.Success;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int GetUsedChannels(int tick) {
				return _inputs[tick].UsedChannels;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int GetFreshInputsCount(int tick) {
				return _inputs[tick].FreshInputsCount();
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool IsFresh(int tick, ushort channel) {
				return _inputs[tick].Inputs[channel].IsFresh;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public MaskEnumerator GetFreshInputs(int tick) {
				return _inputs[tick].GetFreshInputs();
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private void NotifyLocalChange(int tick) {
				if (tick < 0) {
					throw new ArgumentOutOfRangeException(nameof(tick), tick, $"Provided argument is negative.");
				}

				if (_localEarliestChangedTick > tick) {
					_localEarliestChangedTick = tick;
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private void ConfirmLocalChangesUpTo(int tick) {
				if (tick < 0) {
					throw new ArgumentOutOfRangeException(nameof(tick), tick, $"Provided argument is negative.");
				}

				if (_localEarliestChangedTick < tick) {
					_localEarliestChangedTick = tick;
				}
			}
		}
	}
}
