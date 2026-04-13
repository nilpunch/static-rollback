using System;
using FFS.Libraries.StaticPack;

// ReSharper disable StaticMemberInGenericType

namespace Shenanicode.Rollback {
	public abstract class Client<TSessionType> where TSessionType : ISessionType {
		private sealed class PredictionReceiver : IPredictionReceiver {
			private readonly ServerConnection _connection;

			public PredictionReceiver(ServerConnection connection) {
				_connection = connection;
			}

			public void OnInputPredicted<T>(int tick, ushort channel) {
				var writer = MessageSerializer<TSessionType>.CreateClientInput(Session<TSessionType>.GetInputHandle(typeof(T)), tick, channel);
				_connection.WriteOutgoingUnorderedMessage(writer);
			}

			public void OnSignalPredicted<T>(int tick, ushort channel, byte localOrder) {
				var writer = MessageSerializer<TSessionType>.CreateClientSignal(Session<TSessionType>.GetSignalHandle(typeof(T)), tick, channel, localOrder);
				_connection.WriteOutgoingUnorderedMessage(writer);
			}
		}

		private static AuthoritativeTicksTracker AuthoritativeTicks { get; set; }
		private static ILogger Logger { get; set; }
		private static IFullSyncHandler FullSyncHandler { get; set; }
		private static double PingIntervalSeconds { get; set; }
		private static double LastPingTime { get; set; }

		public static TickSync TickSync { get; private set; }
		public static ServerConnection Connection { get; private set; }
		public static ushort Channel { get; private set; }
		public static bool Synced { get; private set; }

		public static SessionStatus Status { get; private set; }
		public static bool IsCreated => Status is SessionStatus.Created or SessionStatus.Initialized;
		public static bool IsInitialized => Status == SessionStatus.Initialized;

		public static void Create(
			SessionConfig sessionConfig,
			ServerConnection connection,
			IFullSyncHandler fullSyncHandler = null,
			double pingIntervalSeconds = 0.5f,
			TickSync tickSync = null,
			ILogger logger = null) {
			AssertClientIsNotCreated();
			AssertSessionStatus(SessionStatus.NotCreated);

			sessionConfig = ApplyClientServerDefaults(sessionConfig);

			Session<TSessionType>.Create(SimulationType.AutomaticRollbacks, sessionConfig, new PredictionReceiver(connection));
			Session<TSessionType>.Types()
								.Signal<PlayerConnectedSignal>()
								.Signal<PlayerDisconnectedSignal>();

			Logger = logger ?? NullLogger.Instance;
			FullSyncHandler = fullSyncHandler ?? NullFullSyncHandler.Instance;
			TickSync = tickSync ?? new AdaptiveTickSync();
			Connection = connection;
			AuthoritativeTicks = AuthoritativeTicksTracker.Create();
			Channel = default;
			PingIntervalSeconds = pingIntervalSeconds;
			LastPingTime = -1;
			Synced = false;

			TickSync.TickRate = Session<TSessionType>.TickRate;
			TickSync.MaxRollbackTicks = Session<TSessionType>.RollbackTicksCapacity;
			Status = SessionStatus.Created;

			Logger.Log($"Client created. TickRate={Session<TSessionType>.TickRate}, PingInterval={pingIntervalSeconds}s");
		}

		public static void Initialize() {
			AssertClientIsCreated();
			AssertSessionStatus(SessionStatus.Created);

			Session<TSessionType>.Initialize();
			Status = SessionStatus.Initialized;
			Logger.Log("Client initialized.");
		}

		public static void Destroy() {
			AssertClientIsCreatedOrInitialized();
			AssertSessionStatus(Status);

			Connection?.Close();

			Session<TSessionType>.Destroy();

			Logger = default;
			FullSyncHandler = default;
			TickSync = default;
			Connection = default;
			AuthoritativeTicks = default;
			Channel = default;
			PingIntervalSeconds = default;
			LastPingTime = default;
			Synced = default;
			Status = SessionStatus.NotCreated;
		}

		public static int InputPredictionTick(double clientTime) {
			AssertClientIsInitialized();
			AssertSessionStatus(SessionStatus.Initialized);

			return TickSync.CalculateTargetTick(clientTime);
		}

		public static void Update(double clientTime) {
			AssertClientIsInitialized();
			AssertSessionStatus(SessionStatus.Initialized);

			if (!EnsureConnected()) {
				return;
			}

			ReadMessages(clientTime);
			AdvanceAuthoritativeTicks();
			SendPingIfNeeded(clientTime);
			FlushConnection();

			if (Synced) {
				AdvanceSimulation(clientTime);
			}
		}

		private static bool EnsureConnected() {
			if (Connection.IsConnected) {
				return true;
			}

			if (Synced) {
				Logger.Warn("Connection lost. Marking client as de-synced.");
			}

			Synced = false;
			return false;
		}

		private static void SendPingIfNeeded(double clientTime) {
			if (!Synced || clientTime - LastPingTime < PingIntervalSeconds) {
				return;
			}

			LastPingTime = clientTime;
			Connection.WriteOutgoingUnorderedMessage(PingMessage.Create(clientTime));
		}

		private static void FlushConnection() {
			Connection.Flush();
		}

		private static void AdvanceSimulation(double clientTime) {
			Session<TSessionType>.FastForwardToTick(TickSync.CalculateTargetTick(clientTime));
		}

		private static void ReadMessages(double clientTime) {
			Connection.Poll();

			Connection.ReadIncomingOrderedMessages(clientTime,
				static (ref BinaryPackReader message, double clientTime) => {
					if (message.ReadMessageId(out var messageId) != ReadResult.Success) {
						DisconnectWith(Connection, $"Received malformed message from server.");
						return MessageIteration.Break;
					}

					ReadResult readResult;

					switch (messageId) {
						case (int)MessageType.FullSync:
							readResult = HandleFullSync(ref message);
							break;

						default:
							DisconnectWith(Connection, $"Received invalid message id={messageId} from server.");
							return MessageIteration.Break;
					}

					if (readResult != ReadResult.Success) {
						DisconnectWith(Connection, $"Received malformed message id={messageId} from server.");
						return MessageIteration.Break;
					}

					return MessageIteration.Continue;
				});

			// Don't consume unordered messages arrived before the full sync.
			if (!Synced) {
				return;
			}

			Connection.ReadIncomingUnorderedMessages(clientTime,
				static (ref BinaryPackReader message, double clientTime) => {
					if (message.ReadMessageId(out var messageId) != ReadResult.Success) {
						DisconnectWith(Connection, $"Received malformed message from server.");
						return MessageIteration.Break;
					}

					ReadResult readResult;

					switch (messageId) {
						case (int)MessageType.Pong:
							readResult = HandlePong(clientTime, ref message);
							break;

						case (int)MessageType.TickInfo:
							readResult = HandleTickInfo(ref message);
							break;

						case >= (int)MessageType.MessageTypeCount when Session<TSessionType>.IsMessageIdRegistered(messageId):
							readResult = HandleServerInput(messageId, ref message);
							break;

						default:
							DisconnectWith(Connection, $"Received invalid message id={messageId} from server.");
							return MessageIteration.Break;
					}

					if (readResult != ReadResult.Success) {
						DisconnectWith(Connection, $"Received malformed message id={messageId} from server.");
						return MessageIteration.Break;
					}

					return MessageIteration.Continue;
				});
		}

		private static void DisconnectWith(ServerConnection connection, string reason) {
			Logger.Error($"{reason} Disconnecting.");
			connection.Close();
		}

		private static ReadResult HandlePong(double clientTime, ref BinaryPackReader reader) {
			var readResult = PongMessage.Read(ref reader, out var pongMessage);
			if (readResult != ReadResult.Success) {
				return readResult;
			}

			var rtt = clientTime - pongMessage.ClientPingSendTime;
			var serverTime = pongMessage.ServerReceiveTime + rtt * 0.5;
			TickSync.UpdateTimeSync(serverTime, clientTime);
			TickSync.UpdateRTT(rtt);
			Logger.Log($"Pong received. RTT={rtt * 1000:F1}ms, estimated server time={serverTime:F3}");
			return ReadResult.Success;
		}

		private static ReadResult HandleFullSync(ref BinaryPackReader reader) {
			var channel = reader.ReadUshort();
			var serverTick = reader.ReadInt();

			Channel = channel;
			Session<TSessionType>.HardReset(serverTick);
			FullSyncHandler.ReadFullSync(ref reader);
			MessageSerializer<TSessionType>.ReadFullSyncInputs(serverTick, ref reader);

			Session<TSessionType>.SaveFrame();
			TickSync.HardReset(serverTick + 1);
			AuthoritativeTicks.HardReset(serverTick + 1);
			LastPingTime = -1;
			Synced = true;

			Logger.Log($"Full sync received. Assigned channel={Channel}, serverTick={serverTick}");
			return ReadResult.Success;
		}

		private static ReadResult HandleTickInfo(ref BinaryPackReader reader) {
			var readResult = TickInfoMessage.Read(ref reader, out var tickInfoMessage);
			if (readResult != ReadResult.Success) {
				return readResult;
			}

			return AuthoritativeTicks.TrySetExpectedMessages(tickInfoMessage.Tick, tickInfoMessage.MessagesCount)
				? ReadResult.Success
				: ReadResult.Failure;
		}

		private static ReadResult HandleServerInput(int messageId, ref BinaryPackReader reader) {
			var tick = reader.ReadInt();
			var channel = reader.ReadUshort();

			if (CanAcceptTick(tick)) {
				var result = MessageSerializer<TSessionType>.ReadServerInput(messageId, tick, channel, ref reader);
				if (result == ReadResult.Success && !AuthoritativeTicks.TryMarkMessageReceived(tick)) {
					return ReadResult.Failure;
				}

				return result;
			}

			Logger.Warn($"Dropped stale input from channel {channel} for tick {tick} (minPrediction={TickSync.MinPredictionTick})");
			return MessageSerializer<TSessionType>.SkipServerInput(messageId, ref reader);
		}

		private static bool CanAcceptTick(int tick) {
			return tick >= TickSync.MinPredictionTick;
		}

		private static void AdvanceAuthoritativeTicks() {
			var nextPredictionTick = AuthoritativeTicks.ConsumeCompletedFrom(TickSync.MinPredictionTick);
			if (nextPredictionTick <= TickSync.MinPredictionTick) {
				return;
			}

			var confirmedServerTick = nextPredictionTick - 1;

			foreach (var inputHandle in Session<TSessionType>.GetAllInputHandles()) {
				inputHandle.ClearPrediction(TickSync.MinPredictionTick, confirmedServerTick);
			}

			foreach (var signalHandle in Session<TSessionType>.GetAllSignalHandles()) {
				signalHandle.ClearPrediction(TickSync.MinPredictionTick, confirmedServerTick);
			}

			TickSync.UpdateMinPredictionTick(nextPredictionTick);
		}

		private static SessionConfig ApplyClientServerDefaults(SessionConfig config) {
			config = config.MergeWith(new SessionConfig(messageIdOffset: SessionConfig.ReservedMessageIdCount));
			if (config.MessageIdOffset!.Value < (int)MessageType.MessageTypeCount) {
				throw new ArgumentOutOfRangeException(nameof(config), config.MessageIdOffset.Value, $"ClientServer message id offset should be >= {(int)MessageType.MessageTypeCount}.");
			}

			return config;
		}

		private static void AssertClientIsNotCreated() {
			if (Status != SessionStatus.NotCreated) {
				throw new InvalidOperationException($"Client {typeof(TSessionType).Name} already created.");
			}
		}

		private static void AssertClientIsCreated() {
			if (Status != SessionStatus.Created) {
				throw new InvalidOperationException($"Client {typeof(TSessionType).Name} should be created.");
			}
		}

		private static void AssertClientIsInitialized() {
			if (Status != SessionStatus.Initialized) {
				throw new InvalidOperationException($"Client {typeof(TSessionType).Name} should be initialized.");
			}
		}

		private static void AssertClientIsCreatedOrInitialized() {
			if (Status != SessionStatus.Created && Status != SessionStatus.Initialized) {
				throw new InvalidOperationException($"Client {typeof(TSessionType).Name} should be created or initialized.");
			}
		}

		private static void AssertSessionStatus(SessionStatus expected) {
			if (Session<TSessionType>.Status != expected) {
				throw new InvalidOperationException($"Session {typeof(TSessionType).Name} status is {Session<TSessionType>.Status}, but Client expected {expected}. Session lifecycle should be managed only through Client.");
			}
		}
	}
}
