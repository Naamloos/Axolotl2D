# Save Games

`SaveGameManager` stores typed game data in versioned JSON slots. It writes a temporary file in the save directory and replaces the slot after serialization succeeds.

## Define save data

Keep save records independent from runtime objects:

```csharp
public sealed record PlayerSave(
    Vector2 Position,
    int Health,
    IReadOnlyList<string> Inventory);
```

Save a slot from a controlled game transition:

```csharp
await saves.SaveAsync(
    "slot-1",
    new PlayerSave(player.Transform.Position, health.Value, inventory.ItemIds),
    dataVersion: 2,
    cancellationToken);
```

Axolotl2D does not serialize scenes or GameObjects. Load the record, instantiate the required objects, add their components, and apply the stored state in game code.

## Load and migrate

```csharp
var save = await saves.LoadAsync<PlayerSave>(
    "slot-1",
    currentDataVersion: 2,
    migrate: (storedVersion, json) => storedVersion switch
    {
        1 => MigrateVersion1(json),
        _ => throw new InvalidDataException()
    },
    cancellationToken);
```

`LoadAsync` returns `null` when the slot does not exist. It rejects newer data versions. Older versions require the migration callback, which receives the stored version and raw `JsonElement`.

## Manage slots

```csharp
IReadOnlyList<SaveSlotInfo> slots = await saves.ListAsync(cancellationToken);
bool exists = saves.Exists("slot-1");
bool deleted = saves.Delete("slot-1");
```

Each `SaveSlotInfo` contains the slot name, data version, UTC save time, and file size.

The default directory is `%LocalAppData%/Axolotl2D/<entry-assembly>/Saves` on Windows and the platform-equivalent local application-data directory elsewhere. Replace `SaveGameOptions` during service registration to set `GameId`, `DirectoryPath`, or `JsonSerializerOptions`.

Save files provide persistence, not tamper resistance. Validate loaded values before applying them to game state. Add authentication or encryption at the game layer when the threat model requires it.
