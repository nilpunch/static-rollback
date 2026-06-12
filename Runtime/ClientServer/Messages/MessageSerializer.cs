using System.Collections.Generic;
using FFS.Libraries.StaticPack;

namespace Shenanicode.Rollback {
	public static class MessageSerializer {
		public static void WriteMessageId(int messageId, ref BinaryPackWriter writer) {
			writer.WriteShort((short)messageId);
		}

		public static void WriteMessageId(MessageType messageType, ref BinaryPackWriter writer) {
			writer.WriteShort((short)messageType);
		}

		public static ReadResult ReadMessageId(this ref BinaryPackReader reader, out int messageId) {
			if (!reader.TryReadShort(out var value)) {
				messageId = default;
				return ReadResult.NotEnoughData;
			}

			messageId = value;

			return ReadResult.Success;
		}
	}

	public static class MessageSerializer<TSessionType> where TSessionType : ISessionType {
		public static ReadResult ReadClientInput(int messageId, int tick, ushort channel, ref BinaryPackReader reader)
			=> ReadInputOrSignal(messageId, tick, channel, ref reader);

		public static ReadResult ReadServerInput(int messageId, int tick, ushort channel, ref BinaryPackReader reader)
			=> ReadInputOrSignal(messageId, tick, channel, ref reader);

		public static ReadResult SkipClientInput(int messageId, ref BinaryPackReader reader)
			=> SkipInputOrSignal(messageId, ref reader);

		public static ReadResult SkipServerInput(int messageId, ref BinaryPackReader reader)
			=> SkipInputOrSignal(messageId, ref reader);

		private static ReadResult ReadInputOrSignal(int messageId, int tick, ushort channel, ref BinaryPackReader reader) {
			if (Session<TSessionType>.IsSignalMessage(messageId)) {
				if (!reader.TryReadByte(out var localOrder)) {
					return ReadResult.NotEnoughData;
				}
				return GetSignalHandle(messageId).ReadApproved(tick, localOrder, channel, ref reader);
			}

			return GetInputHandle(messageId).ReadApproved(tick, channel, ref reader);
		}

		private static ReadResult SkipInputOrSignal(int messageId, ref BinaryPackReader reader) {
			if (Session<TSessionType>.IsSignalMessage(messageId)) {
				if (!reader.TryReadByte(out _)) {
					return ReadResult.NotEnoughData;
				}
				return GetSignalHandle(messageId).Skip(ref reader);
			}

			return GetInputHandle(messageId).Skip(ref reader);
		}

		public static void WriteFullSyncInputsAndSignals(int tick, ref BinaryPackWriter writer) {
			var signalHandles = Session<TSessionType>.GetAllSignalHandles();
			writer.WriteShort((short)signalHandles.Length);

			foreach (var signalHandle in signalHandles) {
				MessageSerializer.WriteMessageId(signalHandle.MessageId, ref writer);

				var signalsCount = signalHandle.GetSignalsCount(tick);
				writer.WriteShort((short)signalsCount);

				for (var index = 0; index < signalsCount; index++) {
					writer.WriteByte(signalHandle.GetSignalLocalOrder(tick, index));
					writer.WriteUshort(signalHandle.GetSignalChannel(tick, index));
					signalHandle.Write(tick, index, ref writer);
				}
			}

			var inputHandles = Session<TSessionType>.GetAllInputHandles();
			writer.WriteShort((short)inputHandles.Length);

			foreach (var inputHandle in inputHandles) {
				MessageSerializer.WriteMessageId(inputHandle.MessageId, ref writer);

				var usedChannels = inputHandle.GetUsedChannels(tick);
				writer.WriteShort((short)usedChannels);

				for (ushort channel = 0; channel < usedChannels; channel++) {
					inputHandle.WriteFullInput(tick, channel, ref writer);
				}
			}
		}

		public static void ReadFullSyncInputs(int tick, ref BinaryPackReader reader) {
			var signalSetsCount = reader.ReadShort();

			for (var i = 0; i < signalSetsCount; i++) {
				var messageId = reader.ReadShort();
				var signalsCount = reader.ReadShort();
				var signalHandle = GetSignalHandle(messageId);

				for (var j = 0; j < signalsCount; j++) {
					var localOrder = reader.ReadByte();
					var channel = reader.ReadUshort();
					signalHandle.ReadApproved(tick, localOrder, channel, ref reader);
				}
			}

			var inputSetsCount = reader.ReadShort();

			for (var i = 0; i < inputSetsCount; i++) {
				var messageId = reader.ReadShort();
				var usedChannels = reader.ReadShort();
				var inputHandle = GetInputHandle(messageId);

				for (ushort channel = 0; channel < usedChannels; channel++) {
					inputHandle.ReadFullInput(tick, channel, ref reader);
				}
			}
		}

		public static int AppendAllFreshInputsAndSignals(int tick, List<BinaryPackWriter> messages) {
			var messagesCount = 0;

			foreach (var signalHandle in Session<TSessionType>.GetAllSignalHandles()) {
				var signalsCount = signalHandle.GetSignalsCount(tick);
				for (var index = 0; index < signalsCount; index++) {
					messages.Add(CreateServerSignal(signalHandle, tick, index));
					messagesCount++;
				}
			}

			foreach (var inputHandle in Session<TSessionType>.GetAllInputHandles()) {
				foreach (var channel in inputHandle.GetFreshInputs(tick)) {
					messages.Add(CreateServerInput(inputHandle, tick, channel));
					messagesCount++;
				}
			}

			return messagesCount;
		}

		public static BinaryPackWriter CreateClientInput(InputHandle inputHandle, int tick, ushort channel) {
			var writer = BinaryPackWriter.CreateFromPool();
			MessageSerializer.WriteMessageId(Session<TSessionType>.GetInputMessageId(inputHandle.InputType), ref writer);
			writer.WriteInt(tick);
			inputHandle.Write(tick, channel, ref writer);
			return writer;
		}

		public static BinaryPackWriter CreateClientSignal(SignalHandle signalHandle, int tick, ushort channel, byte localOrder) {
			var writer = BinaryPackWriter.CreateFromPool();
			MessageSerializer.WriteMessageId(Session<TSessionType>.GetSignalMessageId(signalHandle.SignalType), ref writer);
			writer.WriteInt(tick);
			writer.WriteByte(localOrder);
			var index = signalHandle.GetSignalIndex(tick, channel, localOrder);
			signalHandle.Write(tick, index, ref writer);
			return writer;
		}

		public static BinaryPackWriter CreateServerInput(InputHandle inputHandle, int tick, ushort channel) {
			var writer = BinaryPackWriter.CreateFromPool();
			WriteServerInput(inputHandle, tick, channel, ref writer);
			return writer;
		}

		public static BinaryPackWriter CreateServerSignal(SignalHandle signalHandle, int tick, int index) {
			var writer = BinaryPackWriter.CreateFromPool();
			WriteServerSignal(signalHandle, tick, index, ref writer);
			return writer;
		}

		public static void WriteServerInput(InputHandle inputHandle, int tick, ushort channel, ref BinaryPackWriter writer) {
			MessageSerializer.WriteMessageId(Session<TSessionType>.GetInputMessageId(inputHandle.InputType), ref writer);
			writer.WriteInt(tick);
			writer.WriteUshort(channel);
			inputHandle.Write(tick, channel, ref writer);
		}

		public static void WriteServerSignal(SignalHandle signalHandle, int tick, int index, ref BinaryPackWriter writer) {
			MessageSerializer.WriteMessageId(Session<TSessionType>.GetSignalMessageId(signalHandle.SignalType), ref writer);
			writer.WriteInt(tick);
			writer.WriteUshort(signalHandle.GetSignalChannel(tick, index));
			writer.WriteByte(signalHandle.GetSignalLocalOrder(tick, index));
			signalHandle.Write(tick, index, ref writer);
		}

		private static InputHandle GetInputHandle(int messageId) {
			return Session<TSessionType>.GetInputHandle(Session<TSessionType>.GetMessageType(messageId));
		}

		private static SignalHandle GetSignalHandle(int messageId) {
			return Session<TSessionType>.GetSignalHandle(Session<TSessionType>.GetMessageType(messageId));
		}
	}
}
