using System;
using System.Runtime.CompilerServices;

namespace Shenanicode.Rollback {
	public class CyclicFrameCounter {
		private int _savedFrames;

		public CyclicFrameCounter(int framesCapacity) {
			FramesCapacity = framesCapacity;
		}

		/// <summary>
		/// The maximum number of frames that can be saved.
		/// </summary>
		public int FramesCapacity { get; }

		/// <summary>
		/// The index of the current frame.
		/// </summary>
		public int CurrentFrame { get; private set; }

		public int CanRollbackFrames {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _savedFrames - 1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SaveFrame() {
			CurrentFrame = Loop(CurrentFrame + 1, FramesCapacity);
			_savedFrames = MathUtils.Min(_savedFrames + 1, FramesCapacity);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Rollback(int frames) {
			if (frames < 0) {
				throw new ArgumentOutOfRangeException(nameof(frames), frames, $"Provided argument is negative.");
			}

			if (frames > CanRollbackFrames) {
				throw new ArgumentOutOfRangeException(nameof(frames), frames, $"Can't rollback this far. CanRollbackFrames: {CanRollbackFrames}.");
			}

			_savedFrames -= frames;
			CurrentFrame = LoopNegative(CurrentFrame - frames, FramesCapacity);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int Loop(int a, int b) {
			return a % b;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int LoopNegative(int a, int b) {
			var result = a % b;

			if (result < 0) {
				return result + b;
			}

			return result;
		}
	}
}
