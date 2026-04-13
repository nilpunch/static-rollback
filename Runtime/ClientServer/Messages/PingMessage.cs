using FFS.Libraries.StaticPack;

namespace Shenanicode.Rollback {
	public struct PingMessage {
		public double ClientPingSendTime;

		public static BinaryPackWriter Create(double clientTime) {
			var writer = BinaryPackWriter.CreateFromPool();
			MessageSerializer.WriteMessageId(MessageType.Ping, ref writer);
			Write(new PingMessage { ClientPingSendTime = clientTime }, ref writer);
			return writer;
		}

		public static ReadResult Read(ref BinaryPackReader reader, out PingMessage pingMessage) {
			if (!reader.HasNext(TypeUtils.SizeOf<PingMessage>())) {
				pingMessage = default;
				return ReadResult.NotEnoughData;
			}

			var clientSendTime = reader.ReadDouble();
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
