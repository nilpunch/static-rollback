using System;

namespace Shenanicode.Rollback {
	public class ConsoleLogger : ILogger {
		private readonly string _prefix;

		public ConsoleLogger(string prefix = "") {
			_prefix = string.IsNullOrEmpty(prefix) ? "" : $"[{prefix}] ";
		}

		public void Log(string message) => Console.WriteLine($"[INFO]  {_prefix}{message}");

		public void Warn(string message) => Console.WriteLine($"[WARN]  {_prefix}{message}");

		public void Error(string message) => Console.WriteLine($"[ERROR] {_prefix}{message}");
	}
}
