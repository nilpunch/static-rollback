using Shenanicode.Rollback;

// Define session type.
public struct SessionType : ISessionType { }

// Type alias for convenient access.
public abstract class S : Session<SessionType> { }

// Define inputs.
public struct PlayerInput : IInput {
	public bool Left;
	public bool Right;
	public bool Action;
}

// Define signals.
public struct UseItem : ISignal {
	public int ItemId;
}

public class SimulationRollback : IRollback {
	public int CanRollbackFrames { get; }

	public void SaveFrame() { }

	public void Rollback(int frames) { }
}

public class SimulationUpdateRoot : IUpdateRoot {
	public void Update(int tick) {
		// High-level simulation logic, systems, etc.
		// ...

		var input = S.GetInput<PlayerInput>(channel: 0);
		if (input.LastFreshInput.Left) {
			// Move character to left.
			// Scale movement based on input age.
			var moveModifier = 1f - input.TicksPassed / 10f;
		}

		foreach (var (channel, data) in S.GetAllSignals<UseItem>()) {
			// Find the character for this channel and apply the action.
			var item = data.ItemId;
		}
	}
}

public class Program {
	public static void Main() {
		// Create a session.
		S.Create(SimulationType.AutomaticRollbacks,
			new SessionConfig(
				tickRate: 60,
				saveEachNthTick: 5,
				framesCapacity: 26));

		// Register simulation entry points.
		S.AddUpdateRoot(new SimulationUpdateRoot());
		S.AddRollback(new SimulationRollback());

		// Auto-register all inputs and signals from the calling assembly.
		S.Types().RegisterAll();

		// Initialize the session.
		S.Initialize();

		// Apply local input and signals as prediction.
		S.AppendPredictionSignal(channel: 0, new UseItem() { ItemId = 1 });
		S.SetPredictionInput(channel: 0, new PlayerInput() { Left = true });

		// Apply server-approved input and signals.
		S.SetApprovedSignal(localOrder: 0, channel: 0, new UseItem() { ItemId = 2 });
		S.SetApprovedInput(channel: 0, new PlayerInput() { Left = true });

		// Simulate up to the target tick with automatic resimulation.
		S.FastForwardToTick(tick: 10);

		// Destroy the session.
		S.Destroy();
	}
}
