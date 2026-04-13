using FFS.Libraries.StaticPack;

namespace Shenanicode.Rollback {
	public interface IFullSyncHandler {
		void WriteFullSync(ref BinaryPackWriter writer);

		void ReadFullSync(ref BinaryPackReader reader);
	}
}
