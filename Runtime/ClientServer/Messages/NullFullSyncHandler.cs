using FFS.Libraries.StaticPack;

namespace Shenanicode.Rollback {
	public sealed class NullFullSyncHandler : IFullSyncHandler {
		public static readonly NullFullSyncHandler Instance = new();

		private NullFullSyncHandler() { }

		public void WriteFullSync(ref BinaryPackWriter writer) { }

		public void ReadFullSync(ref BinaryPackReader reader) { }
	}
}
