using System.Collections.Generic;

namespace Shenanicode.Rollback {
	public class RollbackGroup : IRollback {
		private readonly List<IRollback> _rollbacks = new();
		private readonly CyclicFrameCounter _cyclicFrameCounter;

		public RollbackGroup(int framesCapacity) {
			_cyclicFrameCounter = new CyclicFrameCounter(framesCapacity);
		}

		public int CanRollbackFrames => _cyclicFrameCounter.CanRollbackFrames;

		public void Add(IRollback rollback) {
			_rollbacks.Add(rollback);
		}

		public void Remove(IRollback rollback) {
			_rollbacks.Remove(rollback);
		}

		public void SaveFrame() {
			_cyclicFrameCounter.SaveFrame();

			foreach (var rollback in _rollbacks) {
				rollback.SaveFrame();
			}
		}

		public void Rollback(int frames) {
			_cyclicFrameCounter.Rollback(frames);

			foreach (var rollback in _rollbacks) {
				rollback.Rollback(frames);
			}
		}
	}
}
