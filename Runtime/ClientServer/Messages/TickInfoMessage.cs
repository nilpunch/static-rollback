using FFS.Libraries.StaticPack;

namespace Shenanicode.Rollback {
	public struct TickInfoMessage {
		public int Tick;
		public ushort MessagesCount;

		public static BinaryPackWriter Create(int tick, ushort messagesCount) {
			var writer = BinaryPackWriter.CreateFromPool();
			writer.WriteMessageId(MessageType.TickInfo);
			Write(new TickInfoMessage() {
					Tick = tick,
					MessagesCount = messagesCount
				},
				ref writer);
			return writer;
		}

		public static ReadResult Read(ref BinaryPackReader reader, out TickInfoMessage tickInfoMessage) {
			tickInfoMessage = default;

			if (!reader.TryReadInt(out var tick) ||
				!reader.TryReadUshort(out var messagesCount)) {
				return ReadResult.NotEnoughData;
			}

			tickInfoMessage = new TickInfoMessage {
				Tick = tick,
				MessagesCount = messagesCount,
			};

			return ReadResult.Success;
		}

		public static void Write(TickInfoMessage message, ref BinaryPackWriter writer) {
			writer.WriteInt(message.Tick);
			writer.WriteUshort(message.MessagesCount);
		}
	}
}
