using System;
using System.Collections.Generic;
using FFS.Libraries.StaticPack;

namespace Shenanicode.Rollback {
	public enum DeliveryOrder : byte {
		Ordered,
		Unordered,
	}

	public enum MessageIteration {
		Continue,
		Break,
	}

	public delegate MessageIteration MessageReadAction<TArgs>(ref BinaryPackReader message, TArgs args);

	public abstract class Connection : IDisposable {
		protected Queue<BinaryPackWriter> IncomingOrderedMessages { get; } = new();
		protected Queue<BinaryPackWriter> IncomingUnorderedMessages { get; } = new();
		protected Queue<BinaryPackWriter> OutgoingOrderedMessages { get; } = new();
		protected Queue<BinaryPackWriter> OutgoingUnorderedMessages { get; } = new();

		public abstract bool IsConnected { get; }

		public abstract void Poll();

		public abstract void Flush();

		public abstract void Close();

		public void WriteOutgoingOrderedMessage(BinaryPackWriter writer) {
			OutgoingOrderedMessages.Enqueue(writer);
		}

		public void WriteOutgoingUnorderedMessage(BinaryPackWriter writer) {
			OutgoingUnorderedMessages.Enqueue(writer);
		}

		public void ReadIncomingOrderedMessages<TArgs>(TArgs args, MessageReadAction<TArgs> action) {
			while (IsConnected && IncomingOrderedMessages.TryDequeue(out var message)) {
				var messageReader = message.AsReader();
				var iterationResult = action.Invoke(ref messageReader, args);
				message.Dispose();
				if (iterationResult == MessageIteration.Break) {
					break;
				}
			}
		}

		public void ReadIncomingUnorderedMessages<TArgs>(TArgs args, MessageReadAction<TArgs> action) {
			while (IsConnected && IncomingUnorderedMessages.TryDequeue(out var message)) {
				var messageReader = message.AsReader();
				var iterationResult = action.Invoke(ref messageReader, args);
				message.Dispose();
				if (iterationResult == MessageIteration.Break) {
					break;
				}
			}
		}

		public void AppendIncomingOrderedMessage(BinaryPackWriter writer) {
			IncomingOrderedMessages.Enqueue(writer);
		}

		public void AppendIncomingUnorderedMessage(BinaryPackWriter writer) {
			IncomingUnorderedMessages.Enqueue(writer);
		}

		public void ResetBuffers() {
			while (IncomingOrderedMessages.TryDequeue(out var writer)) {
				writer.Dispose();
			}
			while (IncomingUnorderedMessages.TryDequeue(out var writer)) {
				writer.Dispose();
			}
			while (OutgoingOrderedMessages.TryDequeue(out var writer)) {
				writer.Dispose();
			}
			while (OutgoingUnorderedMessages.TryDequeue(out var writer)) {
				writer.Dispose();
			}
		}

		public void Dispose() {
			ResetBuffers();
			Close();
		}
	}

	public abstract class ServerConnection : Connection { }

	public abstract class RemoteClientConnection : Connection {
		public ushort Channel { get; set; }
		public double LastIncomingTime { get; set; }
		public int MessageReadCount { get; set; }
	}
}
