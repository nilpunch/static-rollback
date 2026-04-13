using System;
using UnityEngine.Scripting;

namespace Shenanicode.Rollback {
	[Preserve]
	[Authoritive]
	public struct PlayerConnectedSignal : ISignal {
		public Guid PlayerGuid;
	}
}
