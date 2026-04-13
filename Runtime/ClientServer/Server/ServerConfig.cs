namespace Shenanicode.Rollback {
	public struct ServerConfig {
		public readonly double? TicksAcceptWindowSeconds;
		public readonly double? ClientSilenceTimeoutSeconds;
		public readonly int? MaxMessagesPerClient;

		public ServerConfig(
			double? ticksAcceptWindowSeconds = null,
			double? clientSilenceTimeoutSeconds = null,
			int? maxMessagesPerClient = null) {
			TicksAcceptWindowSeconds = ticksAcceptWindowSeconds;
			ClientSilenceTimeoutSeconds = clientSilenceTimeoutSeconds;
			MaxMessagesPerClient = maxMessagesPerClient;
		}

		public static ServerConfig Default => new(
			ticksAcceptWindowSeconds: 2.0,
			clientSilenceTimeoutSeconds: 5.0,
			maxMessagesPerClient: 50);

		public ServerConfig MergeWith(ServerConfig other) {
			return new ServerConfig(
				ticksAcceptWindowSeconds: TicksAcceptWindowSeconds ?? other.TicksAcceptWindowSeconds,
				clientSilenceTimeoutSeconds: ClientSilenceTimeoutSeconds ?? other.ClientSilenceTimeoutSeconds,
				maxMessagesPerClient: MaxMessagesPerClient ?? other.MaxMessagesPerClient);
		}
	}
}
