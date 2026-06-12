using System.Runtime.CompilerServices;

namespace Shenanicode.Rollback {
	public static class InputExtensions {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TInput LastFresh<TInput>(this Input<TInput> input) where TInput : unmanaged, IInput {
			return input.Data;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TInput FreshOrDefault<TInput>(this Input<TInput> input) where TInput : unmanaged, IInput {
			return input.IsFresh ? input.Data : StaticTypeConfig<TInput>.DefaultValue;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TInput FreshOrDefault<TInput>(this Input<TInput> input, TInput fallback) where TInput : unmanaged, IInput {
			return input.IsFresh ? input.Data : fallback;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TInput FadeOut<TInput>(this Input<TInput> input, in FadeOutConfig fadeOutConfig)
			where TInput : unmanaged, IInput, IFadeOutInput<TInput> {
			return input.Data.FadeOut(input.TicksPassed, fadeOutConfig);
		}
	}
}
