using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace Shenanicode.Rollback {
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppEagerStaticClassConstruction]
	public abstract partial class Session<TSessionType> where TSessionType : ISessionType {
		[Il2CppEagerStaticClassConstruction]
		public struct Data {
			public static Data Instance;

			public readonly int TickRate;
			public readonly int SaveEachNthTick;
			public readonly int FramesCapacity;
			public readonly int MessageIdOffset;
			public readonly SimulationType SimulationType;
			public readonly IPredictionReceiver PredictionReceiver;
			public SessionStatus Status;

			public int StartTick;
			public int CurrentTick;
			public int EarliestChangedTick;

			public IUpdateRoot UpdateRoot;
			public IRollback Rollback;
			public IInterpolationReceiver InterpolationReceiver;

			public Dictionary<Type, InputHandle> InputHandles;
			public Dictionary<Type, SignalHandle> SignalHandles;

			private Dictionary<Type, int> _messageIdsByInputs;
			private Dictionary<Type, int> _messageIdsBySignals;
			private Type[] _messageTypes;
			private bool[] _messageIsSignal;
			private bool[] _messageIsAuthoritive;
			private InputHandle[] _inputHandles;
			private SignalHandle[] _signalHandles;
			private int _inputHandlesCount;
			private int _signalHandlesCount;

			public int NextMessageId;

			public Data(SimulationType simulationType, SessionConfig config = default, IPredictionReceiver predictionReceiver = null) {
				config = config.MergeWith(SessionConfig.Default);
				TickRate = config.TickRate!.Value;
				SaveEachNthTick = config.SaveEachNthTick!.Value;
				FramesCapacity = config.FramesCapacity!.Value;
				MessageIdOffset = config.MessageIdOffset!.Value;
				if (MessageIdOffset < 0) {
					throw new ArgumentOutOfRangeException(nameof(config), MessageIdOffset, "MessageIdOffset should be >= 0.");
				}
				if (MessageIdOffset > short.MaxValue) {
					throw new ArgumentOutOfRangeException(nameof(config), MessageIdOffset, $"MessageIdOffset should be <= {short.MaxValue}.");
				}
				SimulationType = simulationType;
				PredictionReceiver = predictionReceiver;
				Status = SessionStatus.Created;
				StartTick = 0;
				CurrentTick = 0;
				EarliestChangedTick = 0;
				UpdateRoot = new IUpdateRoot.Empty();
				Rollback = new CyclicFrameCounter(FramesCapacity);
				InterpolationReceiver = new IInterpolationReceiver.Empty();
				InputHandles = new Dictionary<Type, InputHandle>();
				SignalHandles = new Dictionary<Type, SignalHandle>();
				_messageIdsByInputs = new Dictionary<Type, int>();
				_messageIdsBySignals = new Dictionary<Type, int>();
				_messageTypes = new Type[64];
				_messageIsSignal = new bool[64];
				_messageIsAuthoritive = new bool[64];
				_inputHandles = new InputHandle[16];
				_signalHandles = new SignalHandle[16];
				_inputHandlesCount = 0;
				_signalHandlesCount = 0;
				NextMessageId = MessageIdOffset;
			}

			public void InitializeInternal() {
				Status = SessionStatus.Initialized;
			}

			public void DestroyInternal() {
				foreach (var handle in SignalHandles.Values) {
					handle.HardReset(CurrentTick);
				}

				foreach (var handle in InputHandles.Values) {
					handle.HardReset(CurrentTick);
				}

				StartTick = 0;
				CurrentTick = 0;
				EarliestChangedTick = 0;
				Status = SessionStatus.NotCreated;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void RegisterInputHandle(ref InputHandle handle) {
				if (InputHandles.ContainsKey(handle.InputType)) {
					throw new InvalidOperationException($"Input type {handle.InputType.GetFullGenericName()} already registered.");
				}

				InputHandles.Add(handle.InputType, handle);
				if (_inputHandlesCount == _inputHandles.Length) {
					Array.Resize(ref _inputHandles, _inputHandles.Length << 1);
				}
				_inputHandles[_inputHandlesCount++] = handle;
				RegisterMessageInput(handle.InputType);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void RegisterSignalHandle(ref SignalHandle handle) {
				if (SignalHandles.ContainsKey(handle.SignalType)) {
					throw new InvalidOperationException($"Signal type {handle.SignalType.GetFullGenericName()} already registered.");
				}

				SignalHandles.Add(handle.SignalType, handle);
				if (_signalHandlesCount == _signalHandles.Length) {
					Array.Resize(ref _signalHandles, _signalHandles.Length << 1);
				}
				_signalHandles[_signalHandlesCount++] = handle;
				RegisterMessageSignal(handle.SignalType);
			}

			public void RegisterMessageInput(Type type) {
				if (_messageIdsByInputs.ContainsKey(type) || _messageIdsBySignals.ContainsKey(type)) {
					throw new InvalidOperationException($"Message type {type.GetFullGenericName()} already registered.");
				}

				EnsureMessageId(NextMessageId);
				if (NextMessageId > short.MaxValue) {
					throw new InvalidOperationException($"Message id overflow. Maximum supported id is {short.MaxValue}.");
				}
				_messageIdsByInputs.Add(type, NextMessageId);
				_messageTypes[NextMessageId] = type;
				_messageIsSignal[NextMessageId] = false;
				_messageIsAuthoritive[NextMessageId] = type.IsDefined(typeof(AuthoritiveAttribute), false);
				NextMessageId++;
			}

			public void RegisterMessageSignal(Type type) {
				if (_messageIdsByInputs.ContainsKey(type) || _messageIdsBySignals.ContainsKey(type)) {
					throw new InvalidOperationException($"Message type {type.GetFullGenericName()} already registered.");
				}

				EnsureMessageId(NextMessageId);
				if (NextMessageId > short.MaxValue) {
					throw new InvalidOperationException($"Message id overflow. Maximum supported id is {short.MaxValue}.");
				}
				_messageIdsBySignals.Add(type, NextMessageId);
				_messageTypes[NextMessageId] = type;
				_messageIsSignal[NextMessageId] = true;
				_messageIsAuthoritive[NextMessageId] = type.IsDefined(typeof(AuthoritiveAttribute), false);
				NextMessageId++;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly bool IsMessageIdRegistered(int messageId) {
				return messageId >= MessageIdOffset && messageId < NextMessageId;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly bool IsSignalMessage(int messageId) {
				AssertRegisteredMessageId(messageId);
				return _messageIsSignal[messageId];
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly bool IsAuthoritiveMessage(int messageId) {
				AssertRegisteredMessageId(messageId);
				return _messageIsAuthoritive[messageId];
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly Type GetMessageType(int messageId) {
				AssertRegisteredMessageId(messageId);
				return _messageTypes[messageId];
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly int GetInputMessageId(Type type) {
				if (_messageIdsByInputs.TryGetValue(type, out var id)) {
					return id;
				}

				throw new InvalidOperationException($"Input message type {type.GetFullGenericName()} is not registered.");
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly int GetSignalMessageId(Type type) {
				if (_messageIdsBySignals.TryGetValue(type, out var id)) {
					return id;
				}

				throw new InvalidOperationException($"Signal message type {type.GetFullGenericName()} is not registered.");
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private void EnsureMessageId(int messageId) {
				if (messageId < _messageTypes.Length) {
					return;
				}

				var newSize = MathUtils.RoundUpToPowerOfTwo(messageId);

				Array.Resize(ref _messageTypes, newSize);
				Array.Resize(ref _messageIsSignal, newSize);
				Array.Resize(ref _messageIsAuthoritive, newSize);
			}

			private readonly void AssertRegisteredMessageId(int messageId) {
				if (!IsMessageIdRegistered(messageId)) {
					throw new InvalidOperationException($"Message with id {messageId} is not registered.");
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly ReadOnlySpan<InputHandle> GetAllInputHandles() {
				return new ReadOnlySpan<InputHandle>(_inputHandles, 0, _inputHandlesCount);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly ReadOnlySpan<SignalHandle> GetAllSignalHandles() {
				return new ReadOnlySpan<SignalHandle>(_signalHandles, 0, _signalHandlesCount);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly bool TryGetInputHandle(Type inputType, out InputHandle handle) {
				return InputHandles.TryGetValue(inputType, out handle);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly InputHandle GetInputHandle(Type inputType) {
				if (InputHandles.TryGetValue(inputType, out var handle)) {
					return handle;
				}

				throw new InvalidOperationException($"Input type {inputType} is not registered.");
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly bool TryGetSignalHandle(Type signalType, out SignalHandle handle) {
				return SignalHandles.TryGetValue(signalType, out handle);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public readonly SignalHandle GetSignalHandle(Type signalType) {
				if (SignalHandles.TryGetValue(signalType, out var handle)) {
					return handle;
				}

				throw new InvalidOperationException($"Signal type {signalType} is not registered.");
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void PopulateUpTo(int tick) {
				foreach (var handle in InputHandles.Values) {
					handle.PopulateUpTo(tick);
				}

				foreach (var handle in SignalHandles.Values) {
					handle.PopulateUpTo(tick);
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void DiscardUpTo(int tick) {
				foreach (var handle in InputHandles.Values) {
					handle.DiscardUpTo(tick);
				}

				foreach (var handle in SignalHandles.Values) {
					handle.DiscardUpTo(tick);
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Reevaluate() {
				foreach (var handle in InputHandles.Values) {
					handle.Reevaluate();
				}
			}

			public void HardReset(int startTick) {
				foreach (var handle in InputHandles.Values) {
					handle.HardReset(startTick);
				}

				foreach (var handle in SignalHandles.Values) {
					handle.HardReset(startTick);
				}

				StartTick = startTick;
				CurrentTick = startTick;
				EarliestChangedTick = startTick;
			}

			public void FastForwardToTick(int targetTick) {
				if (SimulationType == SimulationType.AutomaticRollbacks) {
					if (targetTick < StartTick) {
						throw new ArgumentOutOfRangeException(nameof(targetTick), targetTick, $"Target tick should not be less than start tick {StartTick}.");
					}

					var saveEachNthTick = SaveEachNthTick;
					var earliestTick = Math.Min(targetTick, EarliestChangedTick);
					var ticksToRollback = Math.Max(CurrentTick - earliestTick, 0);

					var currentFrame = CurrentTick / saveEachNthTick;
					var targetFrame = Math.Max(StartTick, CurrentTick - ticksToRollback) / saveEachNthTick;
					var framesToRollback = currentFrame - targetFrame;

					if (framesToRollback > Rollback.CanRollbackFrames) {
						throw new InvalidOperationException($"Can't rollback this far. CanRollbackFrames: {Rollback.CanRollbackFrames}, Actual: {framesToRollback}");
					}

					Rollback.Rollback(framesToRollback);
					CurrentTick = Math.Max(StartTick, (currentFrame - framesToRollback) * saveEachNthTick);

					Reevaluate();
					PopulateUpTo(targetTick);

					while (CurrentTick < targetTick) {
						if (CurrentTick == targetTick - 1) {
							InterpolationReceiver.SaveInterpolationState();
						}

						UpdateRoot.Update(CurrentTick);
						CurrentTick += 1;

						if (CurrentTick % saveEachNthTick == 0) {
							Rollback.SaveFrame();
						}
					}

					DiscardUpTo(Math.Min(StartTick, targetTick - (Rollback.CanRollbackFrames + 1) * saveEachNthTick));
				}
				else {
					if (targetTick < CurrentTick) {
						throw new ArgumentOutOfRangeException(nameof(targetTick), targetTick, $"Target tick should not be less than current tick {CurrentTick}.");
					}

					Reevaluate();
					PopulateUpTo(targetTick);

					while (CurrentTick < targetTick) {
						if (CurrentTick == targetTick - 1) {
							InterpolationReceiver.SaveInterpolationState();
						}

						UpdateRoot.Update(CurrentTick);
						CurrentTick += 1;
					}
				}

				ConfirmChangesUpTo(targetTick);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void NotifyChange(int tick) {
				if (tick < 0) {
					throw new ArgumentOutOfRangeException(nameof(tick), tick, $"Provided argument is negative.");
				}

				if (EarliestChangedTick > tick) {
					EarliestChangedTick = tick;
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void ConfirmChangesUpTo(int tick) {
				if (tick < 0) {
					throw new ArgumentOutOfRangeException(nameof(tick), tick, $"Provided argument is negative.");
				}

				if (EarliestChangedTick < tick) {
					EarliestChangedTick = tick;
				}
			}
		}
	}
}
