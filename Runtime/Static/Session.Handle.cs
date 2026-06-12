using System;
using System.Runtime.CompilerServices;
using FFS.Libraries.StaticPack;
using static System.Runtime.CompilerServices.MethodImplOptions;

namespace Shenanicode.Rollback {
	public readonly struct InputHandle {
		private readonly unsafe delegate*<int, int, int> _clearPrediction;
		private readonly unsafe delegate*<int, void> _populateUpTo;
		private readonly unsafe delegate*<int, void> _discardUpTo;
		private readonly unsafe delegate*<void> _reevaluate;
		private readonly unsafe delegate*<int, void> _hardReset;
		private readonly unsafe delegate*<int, ushort, ref BinaryPackReader, ReadResult> _readApproved;
		private readonly unsafe delegate*<int, ushort, ref BinaryPackReader, ReadResult> _readFullInput;
		private readonly unsafe delegate*<int, ushort, ref BinaryPackWriter, void> _write;
		private readonly unsafe delegate*<int, ushort, ref BinaryPackWriter, void> _writeFullInput;
		private readonly unsafe delegate*<ref BinaryPackReader, ReadResult> _skip;
		private readonly unsafe delegate*<int, int> _getUsedChannels;
		private readonly unsafe delegate*<int, int> _getFreshInputsCount;
		private readonly unsafe delegate*<int, ushort, bool> _isFresh;
		private readonly unsafe delegate*<int, MaskEnumerator> _getFreshInputs;

		public readonly Type InputType;
		public readonly int MessageId;

		internal static unsafe InputHandle Create<TSessionType, TInput>(int messageId)
			where TSessionType : ISessionType
			where TInput : unmanaged, IInput {
			return new InputHandle(
				typeof(TInput),
				messageId,
				&Session<TSessionType>._InputClearPrediction<TInput>,
				&Session<TSessionType>._InputPopulateUpTo<TInput>,
				&Session<TSessionType>._InputDiscardUpTo<TInput>,
				&Session<TSessionType>._InputReevaluate<TInput>,
				&Session<TSessionType>._InputHardReset<TInput>,
				&Session<TSessionType>._InputReadApproved<TInput>,
				&Session<TSessionType>._InputReadFullInput<TInput>,
				&Session<TSessionType>._InputWrite<TInput>,
				&Session<TSessionType>._InputWriteFullInput<TInput>,
				&Session<TSessionType>._InputSkip<TInput>,
				&Session<TSessionType>._InputGetUsedChannels<TInput>,
				&Session<TSessionType>._InputGetFreshInputsCount<TInput>,
				&Session<TSessionType>._InputIsFresh<TInput>,
				&Session<TSessionType>._InputGetFreshInputs<TInput>);
		}

		internal unsafe InputHandle(
			Type inputType,
			int messageId,
			delegate*<int, int, int> clearPrediction,
			delegate*<int, void> populateUpTo,
			delegate*<int, void> discardUpTo,
			delegate*<void> reevaluate,
			delegate*<int, void> hardReset,
			delegate*<int, ushort, ref BinaryPackReader, ReadResult> readApproved,
			delegate*<int, ushort, ref BinaryPackReader, ReadResult> readFullInput,
			delegate*<int, ushort, ref BinaryPackWriter, void> write,
			delegate*<int, ushort, ref BinaryPackWriter, void> writeFullInput,
			delegate*<ref BinaryPackReader, ReadResult> skip,
			delegate*<int, int> getUsedChannels,
			delegate*<int, int> getFreshInputsCount,
			delegate*<int, ushort, bool> isFresh,
			delegate*<int, MaskEnumerator> getFreshInputs) {
			InputType = inputType;
			MessageId = messageId;
			_clearPrediction = clearPrediction;
			_populateUpTo = populateUpTo;
			_discardUpTo = discardUpTo;
			_reevaluate = reevaluate;
			_hardReset = hardReset;
			_readApproved = readApproved;
			_readFullInput = readFullInput;
			_write = write;
			_writeFullInput = writeFullInput;
			_skip = skip;
			_getUsedChannels = getUsedChannels;
			_getFreshInputsCount = getFreshInputsCount;
			_isFresh = isFresh;
			_getFreshInputs = getFreshInputs;
		}

		[MethodImpl(AggressiveInlining)]
		public int ClearPrediction(int startTick, int endTick) {
			unsafe { return _clearPrediction(startTick, endTick); }
		}

		[MethodImpl(AggressiveInlining)]
		public void PopulateUpTo(int tick) {
			unsafe { _populateUpTo(tick); }
		}

		[MethodImpl(AggressiveInlining)]
		public void DiscardUpTo(int tick) {
			unsafe { _discardUpTo(tick); }
		}

		[MethodImpl(AggressiveInlining)]
		public void Reevaluate() {
			unsafe { _reevaluate(); }
		}

		[MethodImpl(AggressiveInlining)]
		public void HardReset(int startTick) {
			unsafe { _hardReset(startTick); }
		}

		[MethodImpl(AggressiveInlining)]
		public ReadResult ReadApproved(int tick, ushort channel, ref BinaryPackReader reader) {
			unsafe { return _readApproved(tick, channel, ref reader); }
		}

		[MethodImpl(AggressiveInlining)]
		public ReadResult ReadFullInput(int tick, ushort channel, ref BinaryPackReader reader) {
			unsafe { return _readFullInput(tick, channel, ref reader); }
		}

		[MethodImpl(AggressiveInlining)]
		public void Write(int tick, ushort channel, ref BinaryPackWriter writer) {
			unsafe { _write(tick, channel, ref writer); }
		}

		[MethodImpl(AggressiveInlining)]
		public void WriteFullInput(int tick, ushort channel, ref BinaryPackWriter writer) {
			unsafe { _writeFullInput(tick, channel, ref writer); }
		}

		[MethodImpl(AggressiveInlining)]
		public ReadResult Skip(ref BinaryPackReader reader) {
			unsafe { return _skip(ref reader); }
		}

		[MethodImpl(AggressiveInlining)]
		public int GetUsedChannels(int tick) {
			unsafe { return _getUsedChannels(tick); }
		}

		[MethodImpl(AggressiveInlining)]
		public int GetFreshInputsCount(int tick) {
			unsafe { return _getFreshInputsCount(tick); }
		}

		[MethodImpl(AggressiveInlining)]
		public bool IsFresh(int tick, ushort channel) {
			unsafe { return _isFresh(tick, channel); }
		}

		[MethodImpl(AggressiveInlining)]
		public MaskEnumerator GetFreshInputs(int tick) {
			unsafe { return _getFreshInputs(tick); }
		}
	}

	public readonly struct SignalHandle {
		private readonly unsafe delegate*<int, int, int> _clearPrediction;
		private readonly unsafe delegate*<int, void> _clear;
		private readonly unsafe delegate*<int, void> _populateUpTo;
		private readonly unsafe delegate*<int, void> _discardUpTo;
		private readonly unsafe delegate*<int, void> _hardReset;
		private readonly unsafe delegate*<int, byte, ushort, ref BinaryPackReader, ReadResult> _readApproved;
		private readonly unsafe delegate*<ref BinaryPackReader, ReadResult> _skip;
		private readonly unsafe delegate*<int, int, ref BinaryPackWriter, void> _write;
		private readonly unsafe delegate*<int, int> _getSignalsCount;
		private readonly unsafe delegate*<int, int, ushort> _getSignalChannel;
		private readonly unsafe delegate*<int, int, byte> _getSignalLocalOrder;
		private readonly unsafe delegate*<int, ushort, byte, int> _getSignalIndex;
		public readonly Type SignalType;
		public readonly int MessageId;

		internal static unsafe SignalHandle Create<TSessionType, TSignal>(int messageId)
			where TSessionType : ISessionType
			where TSignal : struct, ISignal {
			return new SignalHandle(
				typeof(TSignal),
				messageId,
				&Session<TSessionType>._SignalClearPrediction<TSignal>,
				&Session<TSessionType>._SignalClear<TSignal>,
				&Session<TSessionType>._SignalPopulateUpTo<TSignal>,
				&Session<TSessionType>._SignalDiscardUpTo<TSignal>,
				&Session<TSessionType>._SignalHardReset<TSignal>,
				&Session<TSessionType>._SignalReadApproved<TSignal>,
				&Session<TSessionType>._SignalSkip<TSignal>,
				&Session<TSessionType>._SignalWrite<TSignal>,
				&Session<TSessionType>._SignalGetSignalsCount<TSignal>,
				&Session<TSessionType>._SignalGetSignalChannel<TSignal>,
				&Session<TSessionType>._SignalGetSignalLocalOrder<TSignal>,
				&Session<TSessionType>._SignalGetSignalIndex<TSignal>);
		}

		internal unsafe SignalHandle(
			Type signalType,
			int messageId,
			delegate*<int, int, int> clearPrediction,
			delegate*<int, void> clear,
			delegate*<int, void> populateUpTo,
			delegate*<int, void> discardUpTo,
			delegate*<int, void> hardReset,
			delegate*<int, byte, ushort, ref BinaryPackReader, ReadResult> readApproved,
			delegate*<ref BinaryPackReader, ReadResult> skip,
			delegate*<int, int, ref BinaryPackWriter, void> write,
			delegate*<int, int> getSignalsCount,
			delegate*<int, int, ushort> getSignalChannel,
			delegate*<int, int, byte> getSignalLocalOrder,
			delegate*<int, ushort, byte, int> getSignalIndex) {
			SignalType = signalType;
			MessageId = messageId;
			_clearPrediction = clearPrediction;
			_clear = clear;
			_populateUpTo = populateUpTo;
			_discardUpTo = discardUpTo;
			_hardReset = hardReset;
			_readApproved = readApproved;
			_skip = skip;
			_write = write;
			_getSignalsCount = getSignalsCount;
			_getSignalChannel = getSignalChannel;
			_getSignalLocalOrder = getSignalLocalOrder;
			_getSignalIndex = getSignalIndex;
		}

		[MethodImpl(AggressiveInlining)]
		public int ClearPrediction(int startTick, int endTick) {
			unsafe { return _clearPrediction(startTick, endTick); }
		}

		[MethodImpl(AggressiveInlining)]
		public void Clear(int tick) {
			unsafe { _clear(tick); }
		}

		[MethodImpl(AggressiveInlining)]
		public void PopulateUpTo(int tick) {
			unsafe { _populateUpTo(tick); }
		}

		[MethodImpl(AggressiveInlining)]
		public void DiscardUpTo(int tick) {
			unsafe { _discardUpTo(tick); }
		}

		[MethodImpl(AggressiveInlining)]
		public void HardReset(int startTick) {
			unsafe { _hardReset(startTick); }
		}

		[MethodImpl(AggressiveInlining)]
		public ReadResult ReadApproved(int tick, byte localOrder, ushort channel, ref BinaryPackReader reader) {
			unsafe { return _readApproved(tick, localOrder, channel, ref reader); }
		}

		[MethodImpl(AggressiveInlining)]
		public ReadResult Skip(ref BinaryPackReader reader) {
			unsafe { return _skip(ref reader); }
		}

		[MethodImpl(AggressiveInlining)]
		public void Write(int tick, int index, ref BinaryPackWriter writer) {
			unsafe { _write(tick, index, ref writer); }
		}

		[MethodImpl(AggressiveInlining)]
		public int GetSignalsCount(int tick) {
			unsafe { return _getSignalsCount(tick); }
		}

		[MethodImpl(AggressiveInlining)]
		public ushort GetSignalChannel(int tick, int index) {
			unsafe { return _getSignalChannel(tick, index); }
		}

		[MethodImpl(AggressiveInlining)]
		public byte GetSignalLocalOrder(int tick, int index) {
			unsafe { return _getSignalLocalOrder(tick, index); }
		}

		[MethodImpl(AggressiveInlining)]
		public int GetSignalIndex(int tick, ushort channel, byte localOrder) {
			unsafe { return _getSignalIndex(tick, channel, localOrder); }
		}
	}

	public abstract partial class Session<TSessionType> where TSessionType : ISessionType {
		[MethodImpl(AggressiveInlining)]
		internal static int _InputClearPrediction<TInput>(int startTick, int endTick) where TInput : unmanaged, IInput => Inputs<TInput>.Instance.ClearPrediction(startTick, endTick);

		[MethodImpl(AggressiveInlining)]
		internal static void _InputPopulateUpTo<TInput>(int tick) where TInput : unmanaged, IInput => Inputs<TInput>.Instance.PopulateUpTo(tick);

		[MethodImpl(AggressiveInlining)]
		internal static void _InputDiscardUpTo<TInput>(int tick) where TInput : unmanaged, IInput => Inputs<TInput>.Instance.DiscardUpTo(tick);

		[MethodImpl(AggressiveInlining)]
		internal static void _InputReevaluate<TInput>() where TInput : unmanaged, IInput => Inputs<TInput>.Instance.Reevaluate();

		[MethodImpl(AggressiveInlining)]
		internal static void _InputHardReset<TInput>(int startTick) where TInput : unmanaged, IInput => Inputs<TInput>.Instance.HardReset(startTick);

		[MethodImpl(AggressiveInlining)]
		internal static ReadResult _InputReadApproved<TInput>(int tick, ushort channel, ref BinaryPackReader reader) where TInput : unmanaged, IInput => Inputs<TInput>.Instance.ReadApproved(tick, channel, ref reader);

		[MethodImpl(AggressiveInlining)]
		internal static ReadResult _InputReadFullInput<TInput>(int tick, ushort channel, ref BinaryPackReader reader) where TInput : unmanaged, IInput => Inputs<TInput>.Instance.ReadFullInput(tick, channel, ref reader);

		[MethodImpl(AggressiveInlining)]
		internal static void _InputWrite<TInput>(int tick, ushort channel, ref BinaryPackWriter writer) where TInput : unmanaged, IInput => Inputs<TInput>.Instance.Write(tick, channel, ref writer);

		[MethodImpl(AggressiveInlining)]
		internal static void _InputWriteFullInput<TInput>(int tick, ushort channel, ref BinaryPackWriter writer) where TInput : unmanaged, IInput => Inputs<TInput>.Instance.WriteFullInput(tick, channel, ref writer);

		[MethodImpl(AggressiveInlining)]
		internal static ReadResult _InputSkip<TInput>(ref BinaryPackReader reader) where TInput : unmanaged, IInput => Inputs<TInput>.Instance.Skip(ref reader);

		[MethodImpl(AggressiveInlining)]
		internal static int _InputGetUsedChannels<TInput>(int tick) where TInput : unmanaged, IInput => Inputs<TInput>.Instance.GetUsedChannels(tick);

		[MethodImpl(AggressiveInlining)]
		internal static int _InputGetFreshInputsCount<TInput>(int tick) where TInput : unmanaged, IInput => Inputs<TInput>.Instance.GetFreshInputsCount(tick);

		[MethodImpl(AggressiveInlining)]
		internal static bool _InputIsFresh<TInput>(int tick, ushort channel) where TInput : unmanaged, IInput => Inputs<TInput>.Instance.IsFresh(tick, channel);

		[MethodImpl(AggressiveInlining)]
		internal static MaskEnumerator _InputGetFreshInputs<TInput>(int tick) where TInput : unmanaged, IInput => Inputs<TInput>.Instance.GetFreshInputs(tick);

		[MethodImpl(AggressiveInlining)]
		internal static int _SignalClearPrediction<TSignal>(int startTick, int endTick) where TSignal : struct, ISignal => Signals<TSignal>.Instance.ClearPrediction(startTick, endTick);

		[MethodImpl(AggressiveInlining)]
		internal static void _SignalClear<TSignal>(int tick) where TSignal : struct, ISignal => Signals<TSignal>.Instance.Clear(tick);

		[MethodImpl(AggressiveInlining)]
		internal static void _SignalPopulateUpTo<TSignal>(int tick) where TSignal : struct, ISignal => Signals<TSignal>.Instance.PopulateUpTo(tick);

		[MethodImpl(AggressiveInlining)]
		internal static void _SignalDiscardUpTo<TSignal>(int tick) where TSignal : struct, ISignal => Signals<TSignal>.Instance.DiscardUpTo(tick);

		[MethodImpl(AggressiveInlining)]
		internal static void _SignalHardReset<TSignal>(int startTick) where TSignal : struct, ISignal => Signals<TSignal>.Instance.HardReset(startTick);

		[MethodImpl(AggressiveInlining)]
		internal static ReadResult _SignalReadApproved<TSignal>(int tick, byte localOrder, ushort channel, ref BinaryPackReader reader) where TSignal : struct, ISignal =>
			Signals<TSignal>.Instance.ReadApproved(tick, localOrder, channel, ref reader);

		[MethodImpl(AggressiveInlining)]
		internal static ReadResult _SignalSkip<TSignal>(ref BinaryPackReader reader) where TSignal : struct, ISignal => Signals<TSignal>.Instance.Skip(ref reader);

		[MethodImpl(AggressiveInlining)]
		internal static void _SignalWrite<TSignal>(int tick, int index, ref BinaryPackWriter writer) where TSignal : struct, ISignal => Signals<TSignal>.Instance.Write(tick, index, ref writer);

		[MethodImpl(AggressiveInlining)]
		internal static int _SignalGetSignalsCount<TSignal>(int tick) where TSignal : struct, ISignal => Signals<TSignal>.Instance.GetSignalsCount(tick);

		[MethodImpl(AggressiveInlining)]
		internal static ushort _SignalGetSignalChannel<TSignal>(int tick, int index) where TSignal : struct, ISignal => Signals<TSignal>.Instance.GetSignalChannel(tick, index);

		[MethodImpl(AggressiveInlining)]
		internal static byte _SignalGetSignalLocalOrder<TSignal>(int tick, int index) where TSignal : struct, ISignal => Signals<TSignal>.Instance.GetSignalLocalOrder(tick, index);

		[MethodImpl(AggressiveInlining)]
		internal static int _SignalGetSignalIndex<TSignal>(int tick, ushort channel, byte localOrder) where TSignal : struct, ISignal => Signals<TSignal>.Instance.GetSignalIndex(tick, channel, localOrder);
	}
}
