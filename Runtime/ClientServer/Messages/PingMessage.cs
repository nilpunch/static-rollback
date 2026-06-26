using FFS.Libraries.StaticPack;

namespace Shenanicode.Rollback {
	public struct PingMessage {
		public double ClientPingSendTime;

		public static BinaryPackWriter Create(double clientTime) {
			var writer = BinaryPackWriter.CreateFromPool();
			writer.WriteMessageId(MessageType.Ping);
			Write(new PingMessage { ClientPingSendTime = clientTime }, ref writer);
			return writer;
		}

		public static ReadResult Read(ref BinaryPackReader reader, out PingMessage pingMessage) {
			pingMessage = default;

			if (!reader.TryReadDouble(out var clientSendTime)) {
				return ReadResult.NotEnoughData;
			}

			pingMessage = new PingMessage() {
				ClientPingSendTime = clientSendTime,
			};

			return ReadResult.Success;
		}

		public static void Write(PingMessage message, ref BinaryPackWriter writer) {
			writer.WriteDouble(message.ClientPingSendTime);
		}
	}
}
