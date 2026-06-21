using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Shenanicode.Rollback {
	public interface ISessionType { }

	public interface IPredictionReceiver {
		void OnInputPredicted<T>(int tick, ushort channel);

		void OnSignalPredicted<T>(int tick, ushort channel, byte localOrder);
	}

	public interface IUpdateRoot {
		void Update(int tick);

		class Empty : IUpdateRoot {
			public void Update(int tick) { }
		}
	}

	public interface IInterpolationReceiver {
		void SaveInterpolationState();

		class Empty : IInterpolationReceiver {
			public void SaveInterpolationState() { }
		}
	}

	/// <summary>
	/// Defines a contract for objects that support saving and restoring snapshots of state.<br/>
	/// The term "frames" refers to snapshots of states.
	/// </summary>
	public interface IRollback {
		/// <summary>
		/// The number of frames that can be rolled back.<br/>
		/// Can be negative if there are no saved frames to restore data.
		/// </summary>
		int CanRollbackFrames { get; }

		/// <summary>
		/// Saves the current frame data.
		/// </summary>
		void SaveFrame();

		/// <summary>
		/// Restores data from the specified number of frames ago.<br/>
		/// A value of 0 restores data to the state of the last <see cref="IRollback.SaveFrame"/> call.
		/// </summary>
		/// <param name="frames">
		/// The number of frames to roll back. Must be non-negative and not exceed <see cref="IRollback.CanRollbackFrames"/>.
		/// </param>
		void Rollback(int frames);
	}

	public struct SessionConfig {
		public const int ReservedMessageIdCount = 10;

		public readonly int? TickRate;

		public readonly int? SaveEachNthTick;

		public readonly int? FramesCapacity;

		public readonly int? MessageIdOffset;

		public SessionConfig(int? tickRate = null, int? framesCapacity = null, int? saveEachNthTick = null, int? messageIdOffset = null) {
			TickRate = tickRate;
			SaveEachNthTick = saveEachNthTick;
			FramesCapacity = framesCapacity;
			MessageIdOffset = messageIdOffset;
		}

		public static SessionConfig Default => new(
			tickRate: 60,
			saveEachNthTick: 5,
			framesCapacity: 26,
			messageIdOffset: ReservedMessageIdCount);

		public SessionConfig MergeWith(SessionConfig other) {
			return new SessionConfig(
				tickRate: TickRate ?? other.TickRate,
				saveEachNthTick: SaveEachNthTick ?? other.SaveEachNthTick,
				framesCapacity: FramesCapacity ?? other.FramesCapacity,
				messageIdOffset: MessageIdOffset ?? other.MessageIdOffset);
		}
	}

	public enum SimulationType {
		ForwardOnly,
		AutomaticRollbacks,
	}

	public enum SessionStatus {
		NotCreated,
		Created,
		Initialized,
	}

	public abstract partial class Session<TSessionType> where TSessionType : ISessionType {
		public static SessionStatus Status {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Data.Instance.Status;
		}

		public static bool IsSessionCreated {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Data.Instance.Status is SessionStatus.Created or SessionStatus.Initialized;
		}

		public static bool IsSessionInitialized {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Data.Instance.Status == SessionStatus.Initialized;
		}

		public static int CurrentTick {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Data.Instance.CurrentTick;
		}

		public static int TickRate {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Data.Instance.TickRate;
		}

		public static int FramesCapacity {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Data.Instance.FramesCapacity;
		}

		public static int RollbackTicksCapacity {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (Data.Instance.FramesCapacity - 1) * Data.Instance.SaveEachNthTick;
		}

		public static void Create(SimulationType simulationType, SessionConfig config = default, IPredictionReceiver predictionReceiver = null) {
			AssertSessionIsNotCreated();
			Data.Instance = new Data(simulationType, config, predictionReceiver);
		}

		public static void Initialize() {
			AssertSessionIsCreated();
			Data.Instance.InitializeInternal();
		}

		public static void Destroy() {
			AssertSessionIsCreatedOrInitialized();
			Data.Instance.DestroyInternal();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TypeRegistrar Types() => default;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetUpdateRoot(IUpdateRoot updateRoot) {
			AssertSessionIsCreatedOrInitialized();
			Data.Instance.UpdateRoot = updateRoot;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetRollback(IRollback rollback) {
			AssertSessionIsCreatedOrInitialized();
			Data.Instance.Rollback = rollback;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetInterpolationReceiver(IInterpolationReceiver interpolationReceiver) {
			AssertSessionIsCreatedOrInitialized();
			Data.Instance.InterpolationReceiver = interpolationReceiver;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SaveFrame() {
			AssertSessionIsInitialized();
			Data.Instance.Rollback.SaveFrame();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void PopulateUpTo(int tick) {
			AssertSessionIsInitialized();
			Data.Instance.PopulateUpTo(tick);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DiscardUpTo(int tick) {
			AssertSessionIsInitialized();
			Data.Instance.DiscardUpTo(tick);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Reevaluate() {
			AssertSessionIsInitialized();
			Data.Instance.Reevaluate();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void HardReset(int startTick) {
			AssertSessionIsInitialized();
			Data.Instance.HardReset(startTick);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void FastForwardToTick(int tick) {
			AssertSessionIsInitialized();
			Data.Instance.FastForwardToTick(tick);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetInputHandle(Type inputType, out InputHandle handle) {
			return Data.Instance.TryGetInputHandle(inputType, out handle);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static InputHandle GetInputHandle(Type inputType) {
			return Data.Instance.GetInputHandle(inputType);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetSignalHandle(Type signalType, out SignalHandle handle) {
			return Data.Instance.TryGetSignalHandle(signalType, out handle);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SignalHandle GetSignalHandle(Type signalType) {
			return Data.Instance.GetSignalHandle(signalType);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsMessageIdRegistered(int messageId) {
			return Data.Instance.IsMessageIdRegistered(messageId);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsSignalMessage(int messageId) {
			return Data.Instance.IsSignalMessage(messageId);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsAuthoritiveMessage(int messageId) {
			return Data.Instance.IsAuthoritiveMessage(messageId);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Type GetMessageType(int messageId) {
			return Data.Instance.GetMessageType(messageId);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetInputMessageId(Type inputType) {
			return Data.Instance.GetInputMessageId(inputType);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetSignalMessageId(Type signalType) {
			return Data.Instance.GetSignalMessageId(signalType);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadOnlySpan<InputHandle> GetAllInputHandles() {
			return Data.Instance.GetAllInputHandles();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadOnlySpan<SignalHandle> GetAllSignalHandles() {
			return Data.Instance.GetAllSignalHandles();
		}

		public readonly struct TypeRegistrar {
			[MethodImpl(MethodImplOptions.NoInlining)]
			public TypeRegistrar RegisterAll() {
				AutoRegistration.RegisterAll<TSessionType>(typeof(TSessionType).Assembly);
				return this;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			public TypeRegistrar RegisterAll(Assembly first, params Assembly[] rest) {
				if (rest == null || rest.Length == 0) {
					AutoRegistration.RegisterAll<TSessionType>(first);
				}
				else {
					var assemblies = new Assembly[rest.Length + 1];
					assemblies[0] = first;
					Array.Copy(rest, 0, assemblies, 1, rest.Length);
					AutoRegistration.RegisterAll<TSessionType>(assemblies);
				}
				return this;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public TypeRegistrar Input<T>() where T : unmanaged, IInput {
				RegisterInputType<T>();
				return this;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public TypeRegistrar Signal<T>() where T : struct, ISignal {
				RegisterSignalType<T>();
				return this;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SetResult SetApprovedInput<T>(ushort channel, T input) where T : unmanaged, IInput {
			return SetApprovedInputAt(CurrentTick, channel, input);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SetResult SetApprovedInputAt<T>(int tick, ushort channel, T input) where T : unmanaged, IInput {
			return GetInputSet<T>().SetApproved(tick, channel, input);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetPredictionInput<T>(ushort channel, T input) where T : unmanaged, IInput {
			SetPredictionInputAt(CurrentTick, channel, input);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetPredictionInputAt<T>(int tick, ushort channel, T input) where T : unmanaged, IInput {
			GetInputSet<T>().SetPrediction(tick, channel, input);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Input<T> GetInput<T>(ushort channel) where T : unmanaged, IInput {
			return GetInputAt<T>(CurrentTick, channel);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Input<T> GetInputAt<T>(int tick, ushort channel) where T : unmanaged, IInput {
			return GetInputSet<T>().GetInputs(tick).Get(channel);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref AllInputs<T> GetAllInputs<T>() where T : unmanaged, IInput {
			return ref GetAllInputsAt<T>(CurrentTick);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref AllInputs<T> GetAllInputsAt<T>(int tick) where T : unmanaged, IInput {
			return ref GetInputSet<T>().GetInputs(tick);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FreshInputsEnumerable<T> GetFreshInputs<T>() where T : unmanaged, IInput {
			return GetFreshInputsAt<T>(CurrentTick);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FreshInputsEnumerable<T> GetFreshInputsAt<T>(int tick) where T : unmanaged, IInput {
			return new FreshInputsEnumerable<T>(GetInputSet<T>().GetInputs(tick));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SetResult SetApprovedSignal<T>(byte localOrder, ushort channel, T data) where T : struct, ISignal {
			return SetApprovedSignalAt(CurrentTick, localOrder, channel, data);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SetResult SetApprovedSignalAt<T>(int tick, byte localOrder, ushort channel, T data) where T : struct, ISignal {
			return GetSignalSet<T>().SetApproved(tick, localOrder, channel, data);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte AppendApprovedSignal<T>(ushort channel, T data) where T : struct, ISignal {
			return AppendApprovedSignalAt(CurrentTick, channel, data);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte AppendApprovedSignalAt<T>(int tick, ushort channel, T data) where T : struct, ISignal {
			return GetSignalSet<T>().AppendApproved(tick, channel, data);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte AppendPredictionSignal<T>(ushort channel, T data) where T : struct, ISignal {
			return AppendPredictionSignalAt(CurrentTick, channel, data);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte AppendPredictionSignalAt<T>(int tick, ushort channel, T data) where T : struct, ISignal {
			return GetSignalSet<T>().AppendPrediction(tick, channel, data);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref AllSignals<T> GetAllSignals<T>() where T : struct, ISignal {
			return ref GetAllSignalsAt<T>(CurrentTick);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref AllSignals<T> GetAllSignalsAt<T>(int tick) where T : struct, ISignal {
			return ref GetSignalSet<T>().GetAllSignals(tick);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NotifyGlobalChange(int tick) {
			Data.Instance.NotifyChange(tick);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ConfirmGlobalChangesUpTo(int tick) {
			Data.Instance.ConfirmChangesUpTo(tick);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void RegisterInputType<T>() where T : unmanaged, IInput {
			AssertSessionIsCreated();
			if (IsInputTypeRegistered<T>()) {
				throw new InvalidOperationException($"Input type {typeof(T).GetFullGenericName()} already registered.");
			}

			Inputs<T>.Instance = new Inputs<T>(CurrentTick, Data.Instance.PredictionReceiver);
			Inputs<T>.Handle = InputHandle.Create<TSessionType, T>(Data.Instance.NextMessageId);
			Data.Instance.RegisterInputHandle(ref Inputs<T>.Handle);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void RegisterSignalType<T>() where T : struct, ISignal {
			AssertSessionIsCreated();
			if (IsSignalTypeRegistered<T>()) {
				throw new InvalidOperationException($"Signal type {typeof(T).GetFullGenericName()} already registered.");
			}

			Signals<T>.Instance = new Signals<T>(CurrentTick, Data.Instance.PredictionReceiver);
			Signals<T>.Handle = SignalHandle.Create<TSessionType, T>(Data.Instance.NextMessageId);
			Data.Instance.RegisterSignalHandle(ref Signals<T>.Handle);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static ref Inputs<T> GetInputSet<T>() where T : unmanaged, IInput {
			AssertSessionIsInitialized();
			EnsureInputRegistered<T>();
			return ref Inputs<T>.Instance;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static ref Signals<T> GetSignalSet<T>() where T : struct, ISignal {
			AssertSessionIsInitialized();
			EnsureSignalRegistered<T>();
			return ref Signals<T>.Instance;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void EnsureInputRegistered<T>() where T : unmanaged, IInput {
			if (!IsInputTypeRegistered<T>()) {
				throw new InvalidOperationException($"Input type {typeof(T)} is not registered. Call Session<{typeof(TSessionType).Name}>.Types().Input<{typeof(T).Name}>().");
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void EnsureSignalRegistered<T>() where T : struct, ISignal {
			if (!IsSignalTypeRegistered<T>()) {
				throw new InvalidOperationException($"Signal type {typeof(T)} is not registered. Call Session<{typeof(TSessionType).Name}>.Types().Signal<{typeof(T).Name}>().");
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool IsInputTypeRegistered<T>() where T : unmanaged, IInput {
			return Data.Instance.InputHandles.ContainsKey(typeof(T));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool IsSignalTypeRegistered<T>() where T : struct, ISignal {
			return Data.Instance.SignalHandles.ContainsKey(typeof(T));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void AssertSessionIsNotCreated() {
			if (Data.Instance.Status != SessionStatus.NotCreated) {
				throw new InvalidOperationException($"Session {typeof(TSessionType).Name} already created.");
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void AssertSessionIsCreated() {
			if (Data.Instance.Status != SessionStatus.Created) {
				throw new InvalidOperationException($"Session {typeof(TSessionType).Name} should be created.");
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void AssertSessionIsInitialized() {
			if (Data.Instance.Status != SessionStatus.Initialized) {
				throw new InvalidOperationException($"Session {typeof(TSessionType).Name} should be initialized.");
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void AssertSessionIsCreatedOrInitialized() {
			if (Data.Instance.Status != SessionStatus.Created && Data.Instance.Status != SessionStatus.Initialized) {
				throw new InvalidOperationException($"Session {typeof(TSessionType).Name} should be created or initialized.");
			}
		}
	}
}
