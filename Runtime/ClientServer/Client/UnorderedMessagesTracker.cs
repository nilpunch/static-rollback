namespace Shenanicode.Rollback {
	public struct UnorderedMessagesTracker {
		private struct TickReceiveState {
			public ushort ExpectedMessages;
			public ushort ReceivedMessages;
			public bool HasTickInfo;

			public readonly bool IsComplete => HasTickInfo && ReceivedMessages == ExpectedMessages;
		}

		private RingBuffer<TickReceiveState> _ticks;

		public static UnorderedMessagesTracker Create(int startTick = 0) {
			return new UnorderedMessagesTracker {
				_ticks = RingBuffer<TickReceiveState>.Create(startTick)
			};
		}

		public void HardReset(int startTick) {
			_ticks.Reset(startTick);
		}

		public bool TrySetExpectedMessages(int tick, ushort messagesCount) {
			if (tick < _ticks.HeadIndex) {
				return true;
			}

			ref var state = ref EnsureTick(tick);
			if (state.HasTickInfo) {
				return state.ExpectedMessages == messagesCount;
			}

			if (state.ReceivedMessages > messagesCount) {
				return false;
			}

			state.ExpectedMessages = messagesCount;
			state.HasTickInfo = true;
			return true;
		}

		public bool TryMarkMessageReceived(int tick) {
			if (tick < _ticks.HeadIndex) {
				return true;
			}

			ref var state = ref EnsureTick(tick);
			if (state.ReceivedMessages == ushort.MaxValue) {
				return false;
			}

			if (state.HasTickInfo && state.ReceivedMessages == state.ExpectedMessages) {
				return false;
			}

			state.ReceivedMessages += 1;
			return true;
		}

		public bool TryMoveToNextTick(out int nextTick) {
			if (_ticks.CycledCount == 0) {
				nextTick = default;
				return false;
			}

			nextTick = _ticks.HeadIndex;
			while (nextTick < _ticks.TailIndex && _ticks[nextTick].IsComplete) {
				nextTick += 1;
			}

			if (nextTick == _ticks.HeadIndex) {
				return false;
			}

			_ticks.RemoveUpTo(nextTick);

			return true;
		}

		private ref TickReceiveState EnsureTick(int tick) {
			while (_ticks.TailIndex <= tick) {
				_ticks.Append(default);
			}

			return ref _ticks[tick];
		}
	}
}
