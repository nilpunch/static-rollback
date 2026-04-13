namespace Shenanicode.Rollback {
	public interface IRemoteClientListener {
		bool IsListening { get; }

		void Start();

		void Stop();

		void Poll();

		bool TryAccept(out RemoteClientConnection connection);

		void Release(RemoteClientConnection connection);
	}
}
