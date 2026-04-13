using System;
using UnityEngine.Scripting;

namespace Shenanicode.Rollback {
	[Preserve]
	[Authoritive]
	public struct PlayerDisconnectedSignal : ISignal {
		public Guid PlayerGuid;
	}
}
