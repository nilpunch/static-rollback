using System;
using System.Collections.Generic;
using FFS.Libraries.StaticPack;

// ReSharper disable StaticMemberInGenericType

namespace Shenanicode.Rollback {
	public abstract class Server<TSessionType> where TSessionType : ISessionType {
		private static int TicksAcceptWindow;
		private static int MaxMessagesPerClient;
		private static double ClientSilenceTimeoutSeconds;
		private static ILogger Logger;
		private static IFullSyncHandler FullSyncHandler;
		private static double LastInputLogTime;
		private static List<BinaryPackWriter> BroadcastMessages;
		private static IRemoteClientListener ConnectionListener;
		private static List<RemoteClientConnection> AllConnections;
		private static List<RemoteClientConnection> NewConnections;
		private static Stack<ushort> ChannelsPool;
		private static int UsedChannels;

		public static IReadOnlyList<RemoteClientConnection> Connections => AllConnections;

		public static SessionStatus Status { get; private set; }
		public static bool IsCreated => Status is SessionStatus.Created or SessionStatus.Initialized;
		public static bool IsInitialized => Status == SessionStatus.Initialized;

		public static void Create(
			SessionConfig sessionConfig,
			IRemoteClientListener connectionListener,
			IFullSyncHandler fullSyncHandler = null,
			ServerConfig serverConfig = default,
			ILogger logger = null) {
			AssertServerIsNotCreated();
			AssertSessionStatus(SessionStatus.NotCreated);

			sessionConfig = ApplyClientServerDefaults(sessionConfig);
			serverConfig = serverConfig.MergeWith(ServerConfig.Default);

			Session<TSessionType>.Create(SimulationType.ForwardOnly, sessionConfig);
			Session<TSessionType>.Types()
								.Signal<PlayerConnectedSignal>()
								.Signal<PlayerDisconnectedSignal>();

			TicksAcceptWindow = (int)(Session<TSessionType>.TickRate * serverConfig.TicksAcceptWindowSeconds!.Value);
			MaxMessagesPerClient = serverConfig.MaxMessagesPerClient!.Value;
			ClientSilenceTimeoutSeconds = serverConfig.ClientSilenceTimeoutSeconds!.Value;
			Logger = logger ?? NullLogger.Instance;
			FullSyncHandler = fullSyncHandler ?? NullFullSyncHandler.Instance;
			ConnectionListener = connectionListener;
			ConnectionListener.Start();
			AllConnections = new List<RemoteClientConnection>();
			NewConnections = new List<RemoteClientConnection>();
			ChannelsPool = new Stack<ushort>();
			UsedChannels = 0;
			LastInputLogTime = -1;
			BroadcastMessages = new List<BinaryPackWriter>();
			Status = SessionStatus.Created;

			Logger.Log($"Server created. TickRate={Session<TSessionType>.TickRate}, AcceptWindow={TicksAcceptWindow} ticks, ClientSilenceTimeout={ClientSilenceTimeoutSeconds}s, MaxMessagesPerClient={MaxMessagesPerClient}");
		}

		public static void Initialize() {
			AssertServerIsCreated();
			AssertSessionStatus(SessionStatus.Created);

			Session<TSessionType>.Initialize();
			Status = SessionStatus.Initialized;
			Logger.Log("Server initialized.");
		}

		public static void Destroy() {
			AssertServerIsCreatedOrInitialized();
			AssertSessionStatus(Status);

			if (AllConnections != null) {
				foreach (var connection in AllConnections) {
					connection.Close();
					ConnectionListener?.Release(connection);
				}
			}

			ConnectionListener?.Stop();
			Session<TSessionType>.Destroy();

			TicksAcceptWindow = default;
			MaxMessagesPerClient = default;
			Logger = default;
			FullSyncHandler = default;
			ClientSilenceTimeoutSeconds = default;
			ConnectionListener = default;
			AllConnections = default;
			NewConnections = default;
			ChannelsPool = default;
			UsedChannels = default;
			LastInputLogTime = default;
			BroadcastMessages = default;
			Status = SessionStatus.NotCreated;
		}

		public static void Update(double serverTime) {
			AssertServerIsInitialized();
			AssertSessionStatus(SessionStatus.Initialized);

			Session<TSessionType>.PopulateUpTo(Session<TSessionType>.CurrentTick);
			PollTransport();
			RemoveTransportDisconnectedClients();
			AcceptNewConnections(serverTime);
			ReadMessages(serverTime);
			RemoveTimedOutClients(serverTime);

			var lastTick = Session<TSessionType>.CurrentTick;
			var targetTick = (int)Math.Floor(serverTime * Session<TSessionType>.TickRate);
			var simulatingNewTick = targetTick > lastTick;

			SendFullSyncToNewConnections(simulatingNewTick);
			AdvanceSimulation(targetTick);
			BroadcastFreshInputsAndSignals(lastTick, targetTick, simulatingNewTick);
			FlushConnections();
		}

		private static void PollTransport() {
			ConnectionListener.Poll();
		}

		private static void RemoveTransportDisconnectedClients() {
			for (var i = AllConnections.Count - 1; i >= 0; i--) {
				var connection = AllConnections[i];
				if (connection.IsConnected) {
					continue;
				}

				Logger.Warn($"Client on channel {connection.Channel} disconnected. Active connections: {AllConnections.Count - 1}");
				DisconnectClient(i, connection);
			}
		}

		private static void AcceptNewConnections(double serverTime) {
			while (ConnectionListener.TryAccept(out var connection)) {
				var isRecycled = ChannelsPool.Count > 0;
				if (!isRecycled && UsedChannels > ushort.MaxValue) {
					Logger.Warn($"Server exhausted channel pool ({ushort.MaxValue} max). Rejecting new connection.");
					connection.Close();
					ConnectionListener.Release(connection);
					continue;
				}

				connection.Channel = isRecycled ? ChannelsPool.Pop() : (ushort)UsedChannels++;
				connection.MessageReadCount = 0;
				connection.LastIncomingTime = serverTime;
				AllConnections.Add(connection);
				Session<TSessionType>.AppendApprovedSignalAt(Session<TSessionType>.CurrentTick, connection.Channel, new PlayerConnectedSignal());
				NewConnections.Add(connection);

				Logger.Log($"Client accepted on channel {connection.Channel} ({(isRecycled ? "recycled" : "new")}). Active connections: {AllConnections.Count}");
			}
		}

		private static void DisconnectClient(int index, RemoteClientConnection connection) {
			ChannelsPool.Push(connection.Channel);
			AllConnections.RemoveAt(index);
			ConnectionListener.Release(connection);
			Session<TSessionType>.AppendApprovedSignalAt(Session<TSessionType>.CurrentTick, connection.Channel, new PlayerDisconnectedSignal());
		}

		private static void ReadMessages(double serverTime) {
			foreach (var connection in AllConnections) {
				connection.Poll();

				connection.MessageReadCount = 0;

				connection.ReadIncomingOrderedMessages(connection,
					static (ref BinaryPackReader message, RemoteClientConnection connection) => {
						DisconnectWith(connection, $"Channel {connection.Channel} sent invalid ordered message.");
						return MessageIteration.Break;
					});

				connection.ReadIncomingUnorderedMessages((connection, serverTime),
					static (ref BinaryPackReader message, (RemoteClientConnection Connection, double ServerTime) args) => {
						if (args.Connection.MessageReadCount >= MaxMessagesPerClient) {
							DisconnectWith(args.Connection, $"Channel {args.Connection.Channel} hit message flood limit ({MaxMessagesPerClient}/update).");
							return MessageIteration.Break;
						}

						if (message.ReadMessageId(out var messageId) != ReadResult.Success) {
							DisconnectWith(args.Connection, $"Channel {args.Connection.Channel} sent malformed message.");
							return MessageIteration.Break;
						}

						ReadResult readResult;

						switch (messageId) {
							case (int)MessageType.Ping:
								readResult = HandlePing(args.Connection, args.ServerTime, ref message);
								break;

							case >= (int)MessageType.MessageTypeCount when IsAppropriateClientMessage(messageId):
								readResult = HandleClientInput(args.Connection, messageId, args.ServerTime, ref message);
								break;

							default:
								DisconnectWith(args.Connection, $"Channel {args.Connection.Channel} sent invalid message id={messageId}.");
								return MessageIteration.Break;
						}

						if (readResult != ReadResult.Success) {
							DisconnectWith(args.Connection, $"Channel {args.Connection.Channel} sent malformed message id={messageId}.");
							return MessageIteration.Break;
						}

						args.Connection.LastIncomingTime = args.ServerTime;
						args.Connection.MessageReadCount += 1;

						return MessageIteration.Continue;
					});
			}
		}

		private static ReadResult HandlePing(RemoteClientConnection connection, double serverTime, ref BinaryPackReader reader) {
			var readResult = PingMessage.Read(ref reader, out var pingMessage);
			if (readResult != ReadResult.Success) {
				return readResult;
			}

			connection.WriteOutgoingUnorderedMessage(PongMessage.Create(pingMessage, serverTime));
			return ReadResult.Success;
		}

		private static ReadResult HandleClientInput(RemoteClientConnection connection, int messageId, double serverTime, ref BinaryPackReader reader) {
			if (!reader.TryReadInt(out var tick)) {
				return ReadResult.NotEnoughData;
			}

			if (CanAcceptTick(tick)) {
				if (serverTime > LastInputLogTime + 5f) {
					LastInputLogTime = serverTime;
					Logger.Log($"Channel {connection.Channel} sent successful input. It is ahead of server tick: {tick - Session<TSessionType>.CurrentTick}");
				}

				return MessageSerializer<TSessionType>.ReadClientInput(messageId, tick, connection.Channel, ref reader);
			}

			Logger.Warn(
				$"Channel {connection.Channel} sent out-of-window tick {tick} (current={Session<TSessionType>.CurrentTick}, window=[{Session<TSessionType>.CurrentTick}, {Session<TSessionType>.CurrentTick + TicksAcceptWindow}]). Input dropped.");
			return MessageSerializer<TSessionType>.SkipClientInput(messageId, ref reader);
		}

		private static bool IsAppropriateClientMessage(int messageId) {
			return Session<TSessionType>.IsMessageIdRegistered(messageId) && !Session<TSessionType>.IsAuthoritiveMessage(messageId);
		}

		private static bool CanAcceptTick(int tick) {
			return tick >= Session<TSessionType>.CurrentTick && tick <= Session<TSessionType>.CurrentTick + TicksAcceptWindow;
		}

		private static void DisconnectWith(RemoteClientConnection connection, string reason) {
			Logger.Error($"{reason} Disconnecting.");
			connection.Close();
		}

		private static void RemoveTimedOutClients(double serverTime) {
			for (var i = AllConnections.Count - 1; i >= 0; i--) {
				var connection = AllConnections[i];
				if (!connection.IsConnected || !HasTimedOut(connection, serverTime)) {
					continue;
				}

				Logger.Warn($"Client on channel {connection.Channel} timed out after {serverTime - connection.LastIncomingTime:F3}s without incoming messages. Active connections: {AllConnections.Count - 1}");
				DisconnectClient(i, connection);
			}
		}

		private static bool HasTimedOut(RemoteClientConnection connection, double serverTime) {
			return ClientSilenceTimeoutSeconds > 0d && serverTime - connection.LastIncomingTime >= ClientSilenceTimeoutSeconds;
		}

		private static void SendFullSyncToNewConnections(bool simulatingNewTick) {
			if (!simulatingNewTick || NewConnections.Count == 0) {
				return;
			}

			var fullSyncMessage = BinaryPackWriter.CreateFromPool();
			var channelPosition = PrepareFullSync(ref fullSyncMessage);
			foreach (var newConnection in NewConnections) {
				if (!newConnection.IsConnected) {
					continue;
				}

				Logger.Log($"Sending full sync to channel {newConnection.Channel} at tick {Session<TSessionType>.CurrentTick}");

				var messageCopy = fullSyncMessage.CloneFromPool();
				messageCopy.WriteUshortAt(channelPosition, newConnection.Channel);
				newConnection.WriteOutgoingOrderedMessage(messageCopy);
			}

			fullSyncMessage.Dispose();
			NewConnections.Clear();
		}

		private static uint PrepareFullSync(ref BinaryPackWriter writer) {
			MessageSerializer.WriteMessageId(MessageType.FullSync, ref writer);
			var channelPosition = writer.MakePoint(sizeof(ushort));

			writer.WriteInt(Session<TSessionType>.CurrentTick);
			FullSyncHandler.WriteFullSync(ref writer);
			MessageSerializer<TSessionType>.WriteFullSyncInputsAndSignals(Session<TSessionType>.CurrentTick, ref writer);

			return channelPosition;
		}

		private static void AdvanceSimulation(int targetTick) {
			Session<TSessionType>.FastForwardToTick(targetTick);
		}

		private static void BroadcastFreshInputsAndSignals(int lastTick, int targetTick, bool simulatingNewTick) {
			if (!simulatingNewTick) {
				return;
			}

			var messages = BroadcastMessages;

			for (var tick = lastTick; tick < targetTick; tick++) {
				var inputsSignalsCount = MessageSerializer<TSessionType>.AppendAllFreshInputsAndSignals(tick, messages);
				messages.Add(TickInfoMessage.Create(tick, (ushort)inputsSignalsCount));
			}

			foreach (var connection in AllConnections) {
				if (!connection.IsConnected) {
					continue;
				}

				foreach (var message in messages) {
					connection.WriteOutgoingUnorderedMessage(message.CloneFromPool());
				}
			}

			foreach (var message in messages) {
				message.Dispose();
			}

			messages.Clear();

			Session<TSessionType>.DiscardUpTo(targetTick);
		}

		private static void FlushConnections() {
			foreach (var connection in AllConnections) {
				connection.Flush();
			}
		}

		private static SessionConfig ApplyClientServerDefaults(SessionConfig config) {
			config = config.MergeWith(new SessionConfig(messageIdOffset: SessionConfig.ReservedMessageIdCount));
			if (config.MessageIdOffset!.Value < (int)MessageType.MessageTypeCount) {
				throw new ArgumentOutOfRangeException(nameof(config), config.MessageIdOffset.Value, $"ClientServer message id offset should be >= {(int)MessageType.MessageTypeCount}.");
			}

			return config;
		}

		private static void AssertServerIsNotCreated() {
			if (Status != SessionStatus.NotCreated) {
				throw new InvalidOperationException($"Server {typeof(TSessionType).Name} already created.");
			}
		}

		private static void AssertServerIsCreated() {
			if (Status != SessionStatus.Created) {
				throw new InvalidOperationException($"Server {typeof(TSessionType).Name} should be created.");
			}
		}

		private static void AssertServerIsInitialized() {
			if (Status != SessionStatus.Initialized) {
				throw new InvalidOperationException($"Server {typeof(TSessionType).Name} should be initialized.");
			}
		}

		private static void AssertServerIsCreatedOrInitialized() {
			if (Status != SessionStatus.Created && Status != SessionStatus.Initialized) {
				throw new InvalidOperationException($"Server {typeof(TSessionType).Name} should be created or initialized.");
			}
		}

		private static void AssertSessionStatus(SessionStatus expected) {
			if (Session<TSessionType>.Status != expected) {
				throw new InvalidOperationException($"Session {typeof(TSessionType).Name} status is {Session<TSessionType>.Status}, but Server expected {expected}. Session lifecycle should be managed only through Server.");
			}
		}
	}
}
