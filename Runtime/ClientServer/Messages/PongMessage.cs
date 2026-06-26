using FFS.Libraries.StaticPack;

namespace Shenanicode.Rollback {
	public struct PongMessage {
		public double ClientPingSendTime;
		public double ServerReceiveTime;

		public static BinaryPackWriter Create(PingMessage pingMessage, double serverReceiveTime) {
			var writer = BinaryPackWriter.CreateFromPool();
			writer.WriteMessageId(MessageType.Pong);
			Write(new PongMessage() {
					ClientPingSendTime = pingMessage.ClientPingSendTime,
					ServerReceiveTime = serverReceiveTime
				},
				ref writer);
			return writer;
		}

		public static ReadResult Read(ref BinaryPackReader reader, out PongMessage pongMessage) {
			pongMessage = default;

			if (!reader.TryReadDouble(out var clientSendTime) ||
				!reader.TryReadDouble(out var serverReceiveTime)) {
				return ReadResult.NotEnoughData;
			}

			pongMessage = new PongMessage() {
				ClientPingSendTime = clientSendTime,
				ServerReceiveTime = serverReceiveTime,
			};

			return ReadResult.Success;
		}

		public static void Write(PongMessage message, ref BinaryPackWriter writer) {
			writer.WriteDouble(message.ClientPingSendTime);
			writer.WriteDouble(message.ServerReceiveTime);
		}
	}
}
