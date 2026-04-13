using System.Collections.Generic;

namespace Shenanicode.Rollback {
	public class UpdateRootGroup : IUpdateRoot {
		private readonly List<IUpdateRoot> _updateRoots = new();

		public void Add(IUpdateRoot updateRoot) {
			_updateRoots.Add(updateRoot);
		}

		public void Remove(IUpdateRoot updateRoot) {
			_updateRoots.Remove(updateRoot);
		}

		public void Update(int tick) {
			foreach (var updateRoot in _updateRoots) {
				updateRoot.Update(tick);
			}
		}
	}
}
