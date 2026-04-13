# Static Rollback

- Gameplay client and server
- Input and singnal timelines with prediction support
- Serialization and transport with cheat resistance in mind

# Installation
The library has a dependency on [StaticPack](https://github.com/Felid-Force-Studios/StaticPack) `1.2.6` for binary serialization, StaticPack must also be installed

* ### As source code
  From the release page or as an archive from the branch. In the `master` branch there is a stable tested version

# Code Examples

```cs
// Define session type.
public struct SessionType : ISessionType { }

// Define type-alias for convenient access.
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
		// High-level calls for systems, etc.
		// ...

		var input = S.GetInput<PlayerInput>(channel: 0);
		if (input.LastFreshInput.Left) {
			// Decrease movement based on how old input is.
			var moveModifier = 1f - input.TicksPassed / 10f;
		}

		foreach (var (channel, data) in S.GetAllSignals<UseItem>()) {
			// Find character with this input channel and apply action.
			var item = data.ItemId;
		}
	}
}

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
```
