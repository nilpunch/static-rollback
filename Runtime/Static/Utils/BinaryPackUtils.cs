using System.Runtime.CompilerServices;
using FFS.Libraries.StaticPack;
using Unity.IL2CPP.CompilerServices;

namespace Shenanicode.Rollback {
	[Il2CppEagerStaticClassConstruction]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public static class BinaryPackUtils {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static BinaryPackWriter CloneFromPool(this in BinaryPackWriter writer) {
			var clone = BinaryPackWriter.CreateFromPool(writer.Position);
			clone.WriteBytes(writer.Buffer, 0, writer.Position);
			return clone;
		}
	}
}
