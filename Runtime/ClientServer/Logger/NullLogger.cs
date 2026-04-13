namespace Shenanicode.Rollback {
	public class NullLogger : ILogger {
		public static readonly NullLogger Instance = new();

		public void Log(string message) { }

		public void Warn(string message) { }

		public void Error(string message) { }
	}
}
