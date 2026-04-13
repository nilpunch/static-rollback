using FFS.Libraries.StaticPack;

namespace Shenanicode.Rollback {
	public struct TickInfoMessage {
		public int Tick;
		public ushort MessagesCount;

		public static BinaryPackWriter Create(int tick, ushort messagesCount) {
			var writer = BinaryPackWriter.CreateFromPool();
			MessageSerializer.WriteMessageId(MessageType.TickInfo, ref writer);
			Write(new TickInfoMessage() {
					Tick = tick,
					MessagesCount = messagesCount
				},
				ref writer);
			return writer;
		}

		public static ReadResult Read(ref BinaryPackReader reader, out TickInfoMessage tickInfoMessage) {
			if (!reader.HasNext(TypeUtils.SizeOf<TickInfoMessage>())) {
				tickInfoMessage = default;
				return ReadResult.NotEnoughData;
			}

			var tick = reader.ReadInt();
			var messagesCount = reader.ReadUshort();
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
