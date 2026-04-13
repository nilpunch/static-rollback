using System.Runtime.CompilerServices;

namespace Shenanicode.Rollback {
	public static class InputExtensions {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TInput LastFresh<TInput>(this Input<TInput> input) where TInput : struct, IInput {
			return input.LastFreshInput;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TInput FreshOrDefault<TInput>(this Input<TInput> input) where TInput : struct, IInput {
			return input.IsFresh ? input.LastFreshInput : StaticTypeConfig<TInput>.DefaultValue;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TInput FreshOrDefault<TInput>(this Input<TInput> input, TInput fallback) where TInput : struct, IInput {
			return input.IsFresh ? input.LastFreshInput : fallback;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TInput FadeOut<TInput>(this Input<TInput> input, in FadeOutConfig fadeOutConfig)
			where TInput : struct, IInput, IFadeOutInput<TInput> {
			return input.LastFreshInput.FadeOut(input.TicksPassed, fadeOutConfig);
		}
	}
}
