# Godot-Game

A 2.5D game built with **Godot 4.6** and **C# (.NET 8)**.

The project follows a feature-based folder architecture with a clear separation between **logic (`Features/`)**, **scenes (`Scenes/`)**, **assets (`Assets/`)**, **constants (`Constants/`)** and **shared helpers (`Utils/`)**. Each feature is self-contained in its own folder with standardized sublayers, making it easy to add new features by following the same pattern.

---

## Folder structure

The Godot project lives in `Game/`; the solution file sits at the repository root.

```
root/
├── Game/
│   ├── Assets/
│   │   ├── Audio/
│   │   ├── Fonts/
│   │   ├── Shaders/
│   │   └── Textures/
│   ├── Constants/
│   │   └── <Name>Constants.cs
│   ├── Docs/
│   │   └── <DocName>.md
│   ├── Features/
│   │   ├── UI/
│   │   │   └── <UIName>/
│   │   │       ├── Abstractions/
│   │   │       │   └── <BaseName>.cs
│   │   │       ├── Managers/
│   │   │       │   └── <ManagerName>Manager.cs
│   │   │       ├── Objects/
│   │   │       │   └── <ObjectName>.cs
│   │   │       └── View/
│   │   │           └── <UIName>UI.cs
│   │   └── World/
│   │       └── <DomainName>/
│   │           ├── Abstractions/
│   │           │   └── <BaseName>.cs
│   │           ├── Database/
│   │           │   └── <DomainName>DB.cs
│   │           ├── Definitions/
│   │           │   └── <ConcreteName>.cs
│   │           ├── Entities/
│   │           │   └── <EntityName>.cs
│   │           ├── Managers/
│   │           │   └── <DomainName>Manager.cs
│   │           ├── Resources/
│   │           │   └── <ResourceName>Data.cs
│   │           ├── Singletons/
│   │           │   └── <SingletonName>.cs
│   │           ├── Structures/
│   │           │   └── <StructName>.cs
│   │           ├── Systems/
│   │           │   └── <SystemName>.cs
│   │           ├── Types/
│   │           │   └── <EnumName>.cs
│   │           └── View/
│   │               └── <OverlayName>.cs
│   ├── Scenes/
│   │   ├── Main.tscn
│   │   ├── Managers/
│   │   │   └── Managers.tscn
│   │   ├── Ui/
│   │   │   ├── Ui.tscn
│   │   │   └── <UIName>/
│   │   │       └── <UIName>.tscn
│   │   └── World/
│   │       ├── World.tscn
│   │       └── <SceneName>/
│   │           └── <SceneName>.tscn
│   ├── Utils/
│   │   └── <UtilName>/
│   ├── project.godot
│   ├── DefaultTheme.tres
│   └── Game.csproj
└── Game.sln
```

> No sublayer is mandatory. Only use the ones that make sense for the feature.

### Features/UI/

User interface features. Each screen/UI component lives in its own folder inside `UI/`.

**Sublayers:**

- **Abstractions:** Abstract classes and base interfaces. Defines the immutable contract/blueprint that concrete implementations must follow (lifecycle methods, required properties).
- **Managers:** Global UI services that are not a screen themselves (`RouterManager`, `WindowManager`).
- **Objects:** DTOs, data models used as arguments or to facilitate data transport.
- **View:** Visual script that extends `CanvasLayer`. This is the script attached directly to the corresponding `.tscn` scene in `Scenes/Ui/`.

### Features/World/

Game world features (gameplay). Each game domain lives in its own folder inside `World/`. There is no top-level `Managers/` feature: a manager belongs to the domain it serves, in that domain's `Managers/` folder (`Core/Managers/WorldManager.cs`, `Save/Managers/SaveManager.cs`, `Chunks/Managers/TileStreamingManager.cs`). `Core` holds the central manager of the World feature; the other domains sit as siblings of `Core`.

**Sublayers:**

- **Abstractions:** Abstract classes and base interfaces. Defines the immutable contract/blueprint that concrete implementations must follow (lifecycle methods, required properties).
- **Database:** Static registries (in-memory databases). Classes with `Dictionary<string, Definition>` that catalog all available definitions and provide factory methods to create instances.
- **Definitions:** Concrete implementations of abstractions. Each class inherits from an abstraction and implements the actual, specific behavior for that type.
- **Entities:** Main game objects. Scripts attached to the root Node of an entity (character, camera, etc.) — the "owner" of the entity in the scene tree.
- **Managers:** Global service for that domain. The manager script sits directly inside `Managers/`.
- **Resources:** Godot `Resource` subclasses owned by that domain (save data models, definition data, etc.).
- **Singletons:** Single shared instances that back the domain at runtime (`TerrainLayer`, `WorldRandom`).
- **Structures:** Structs, simple and immutable data passed between systems (damage info, inputs, etc.).
- **Systems:** Behavior scripts attached to nodes. Logic that runs as a child component of an entity or scene, processing behavior every frame or event.
- **Types:** Enums. Categorize variants within the feature's domain.
- **View:** Debug/visual overlays owned by the domain.

---

## Features

### Definitions and instances

Almost every gameplay domain here is split in two, and the split is the same everywhere. Understanding it once explains items, actions, effects and skill nodes at the same time.

**Definition versus Data**

| | **Definition** | **Data** |
|---|---|---|
| where | `Abstractions/` + `Definitions/` | `Resources/` |
| what it is | a plain C# class | a Godot `Resource` |
| how many | one per *type*, shared | one per *instance* |
| built by | the `Database/` factory (`ItemFactory`, `ActionFactory`, `EffectDB`, `SkillTreeDB`) | `CreateInstance(id)` |
| serialized | **never** | **always** — every field is `[Export, GodotDictionaryField]` |
| holds | behaviour and the baseline numbers | the mutable state of this one copy |

The same pair repeats in every domain, with the same naming:

**The pairs, domain by domain**
```
ItemDefinition    (class)      ItemData          (Resource)  →  save + RPC
ActionDefinition  (class)      ActionDefinitionData (Resource)
EffectDefinition  (class)      EffectDefinitionData (Resource)
SkillTreeNode     (class)      SkillTreeNodeData    (Resource)
```

The `Data` never carries behaviour and the `Definition` never carries per-copy state. A method always receives both:

**Every method receives both**
```csharp
public abstract void Use(Player player, ItemData data);
public abstract void OnStartAction(Player player, ActionDefinitionData instance, float delta);
public virtual void Apply(Player player, EffectDefinitionData data, float delta);
```

**Runtime properties versus persisted properties.** Both the `Definition` and the `Data` carry `Properties` and `Modifiers`. That duplication is deliberate and it is the hook for randomness:

- the **definition's** lists are the baseline shared by every copy of that id — every `iron_sword` has them;
- the **instance's** lists live on the `Resource`, are written to the save and travel by RPC — so **this** `iron_sword` can carry numbers no other copy has.

That is where an RNG mechanic plugs in: roll the modifiers when the instance is created, write them onto the `Data`, and they persist with that particular item, action or effect. Nothing else in the pipeline needs to change, because everything downstream reads the merged result rather than the definition.

**The merge is the `Resolver`.** It takes several lists of the same property type and folds them into one, with rules per type — damage of the same `DamageType` sums the amount and multiplies the multiplier, resistances sum and clamp to `[0, 1]`:

**Merging two lists of the same property**
```csharp
var damages = Resolver.Resolve(definition.Properties.OfType<DamagePropertyData>().ToList(),
                               data.Properties.OfType<DamagePropertyData>().ToList());
```

**On the player the same split appears again**, and this one is easy to confuse:

**Persisted versus derived, on the player**

| persisted (`[GodotDictionaryField]`) | derived (recomputed) |
|---|---|
| `Properties`, `CurrentEffects`, `UnlockedAbilities`, `SkillTree`, `Inventory`, `CurrentHealth` | `ActiveProperties`, `ActiveEffects`, `ActiveAbilities` |
| what the character *has* | the effect of that *right now* |

`ApplySkillTree()` clears the `Active*` lists and rebuilds them from the base values plus everything the skill tree grants. Equipping an item pushes its modifiers into `ActiveProperties`; unequipping removes them. Nothing in the `Active*` lists is ever saved — they are recomputed on load.

---

### The grant hierarchy

Four sources can grant things to a player, and the currency is always the same three: **properties**, **effects** and **abilities**.

**What each source grants**
```
SkillTreeNode ──┬─ Properties      (stacked once per level)
                ├─ Effects         → ActiveEffects
                └─ UnlockedAbilities → ActiveAbilities

ItemDefinition ─┬─ Modifiers       → ActiveProperties on equip, removed on unequip
                └─ Effects         → instantiated into ItemData.Effects on creation
                                     ├─ OnUse  → applied when the item is used
                                     └─ OnHit  → handed to the hitbox

ActionDefinition ┬─ Properties/Modifiers
                 └─ Effects        → instantiated into ActionDefinitionData.Effects
                                     filtered by CreateEffects(EffectTriggerType)

EffectDefinition ─── Properties/Modifiers
```

**An effect chooses who it lands on.** `EffectApply` is declared on the definition and honoured by the hitbox at the moment of impact:

**Where the target is decided**
```csharp
foreach (var effect in Effects)
{
    if (effect.ApplyTo == EffectApply.ToTarget || effect.ApplyTo == EffectApply.ToAll) target.GiveEffect(effect.Id);
    if (effect.ApplyTo == EffectApply.ToOwner  || effect.ApplyTo == EffectApply.ToAll) Owner?.GiveEffect(effect.Id);
}
```

So a weapon that poisons the enemy and heals the wielder is one weapon with two effects, `ToTarget` and `ToOwner` — no special case in the weapon.

**`EffectTriggerType` decides *when*.** `OnUse` fires when the item or action is used; `OnHit` is copied into the hitbox and fires only if it actually connects. The filtering happens where the hitbox is built:

**Filtering the effects handed to a hitbox**
```csharp
var hitEffects = instance.Effects.Where(e => e.Type == EffectTriggerType.OnHit);
hitbox.Initialize(damages, hitEffects, player, weapon.KnockbackForce);
```

**Two links in this diagram are not wired yet**, and it is worth being explicit about them:

- **An effect cannot grant an ability.** `EffectDefinition` has `Properties` and `Modifiers` and nothing else — there is no `UnlockedAbilities` on it. An effect that unlocks an action would need that field plus a place to consume it in `ApplySkillTree`.
- **An item cannot grant an ability either.** `ItemDefinitionData.UnlockedAbilities` exists as a field, but nothing reads it. Today only the skill tree reaches `ActiveAbilities`.

---

### Entities

An entity is the script attached to the **root node of a scene** — `Player`, `Prop`, `Portal`, `WorldItem`. It lives in the `Entities/` sublayer of its domain and it is the owner of that scene in the tree.

**The rule for child nodes is that they are not autonomous.** A child never registers itself, never saves itself, and is never addressed from the outside. The root resolves its children once and keeps the reference:

**Resolving the children once**
```csharp
public override void _Ready()
{
    Sprite = GetNodeOrNull<Sprite2D>("Sprite");
    Collision = GetNodeOrNull<CollisionShape2D>("Collision");
    PickupArea = GetNodeOrNull<Area2D>("PickupArea");
}
```

The references are declared under a `#region Node children references` and resolved in `_Ready` — never looked up again at the point of use.

This is what makes the flat save work. The persistence contract says that **only the scene root with a marked script is asked anything**; a child either reads what it needs from the parent, or the parent fills it in when restoring. So the dimension holds the terrain mutations and hands them to its `TileMapLayer` children, and `WorldItem` holds an `ItemData` and pushes the icon into its own `Sprite2D`. Neither `TileMapLayer` nor `Sprite2D` appears in the file.

The same idea decides what streaming sees: the entity is the unit that is loaded, unloaded and replicated. Its children come with the scene, so they never need to be described anywhere.

---

### Items

An item is an `ItemDefinition` subclass plus the `ItemData` instance carried in an inventory. `ItemType` classifies it — `WeaponMelee`, `WeaponRanged`, `Consumable`, `Material`, `Tool`, `Block`, `Misc` — but the behaviour comes from the subclass, not from the enum.

**The item definitions**

| definition | what it does |
|---|---|
| `WeaponDefinition` | spawns a hitbox from `HitboxScene`, feeds it the resolved damages and the `OnHit` effects |
| `ToolDefinition` | breaks blocks and props at the aimed cell; has a range indicator |
| `BlockItemDefinition` | places a block on the tilemap; the indicator shows the target cell |
| `PortalItemDefinition` | places a prop in the world through `SpawnPropAuthoritative` |
| `ConsumableDefinition` | applies its effects to the user and consumes a unit |

**`Use` versus `UseAt`.** `Use` runs on the owning client: it validates, aims, starts the cooldown and then asks the server. `UseAt` runs on the authoritative side, already with a world position — it validates again, places the thing and consumes the item. Placeables are exactly the items that implement `UseAt`:

**Placing something in the world**
```csharp
public override void UseAt(Player player, ItemData data, Vector2 position)
{
    if (dimensions == null || !dimensions.SpawnPropAuthoritative("portal", position, player.GetActiveDimensionId()))
    {
        return;
    }

    player.RemoveItemRequest(data.InstanceId, 1);
}
```

**The player does not know what an item does.** It forwards to the definition and the definition decides. Adding an item type means adding a subclass and registering it in `ItemFactory` — nothing in `Player` changes.

**Charges, cooldown and reload** live on the base class, driven by a `ChargesPropertyData` resolved from the properties: `CanUse` checks the cooldown, the reload and whether there are charges; `Update(delta, data)` ticks both timers down. An item with no `ChargesPropertyData` is treated as having infinite charges.

**The indicator** (`UpdateIndicator` / `HideIndicator` / `DestroyIndicator`) is the preview drawn while the item is equipped — the translucent quad over the target cell. It is owned by the definition, so each kind of item draws its own.

---

### Props

A prop is anything placed in the world as an object: the portal today, the rest later. `PropDefinition` is a small record with an `Id` and a `ScenePath`, catalogued in `PropDB`; `Prop` is the `Area2D` script on the scene root, and it holds the whole lifecycle — placing, breaking, replicating and persisting — so a subclass only implements what is specific to it. `Portal` is exactly that: it inherits `Prop` and adds nothing but the interaction.

**The two attributes a prop carries**
```csharp
[Unload(UnloadMode.Global)]
public partial class Prop : Area2D
{
    [Save, GodotDictionaryField]
    public string PropId { get; set; } = "";
```

Two attributes carry all of it. `[Unload]` is the streaming policy; `[Save]` puts `PropId` in the file, and `[SaveScene]` on the concrete subclass says which scene to rebuild it from. `Position` is written implicitly by the document.

**Placing goes through the server.** `SpawnPropAuthoritative` checks that the target cell is empty and that the cell below is solid, builds the record and calls `Spawn` locally plus `SpawnRequest` for the peers — so the prop appears with the same name, and therefore the same RPC path, on every peer.

---

### Actions

An action is an ability with charges, cooldown and duration: `DashDefinition`, `FireballDefinition`, `GroundStrikeDefinition`. The definition holds the behaviour, `ActionDefinitionData` holds the state of this player's copy.

The whole cycle is one method, `Update`, called every frame:

**The cycle, in call order**
```
OnPassiveUpdate            always, even when idle
OnStartActionValidation    should it start now?
OnStartAction              consumes a charge, resets the duration
OnUpdateWhileActive        every frame while DurationTimer < Duration
OnFinishedAction           when the duration runs out
OnEnableAction             when a charge comes back
```

Charges recharge one at a time: while `InCooldown`, `CooldownTimer` climbs to `Cooldown`, gives one charge back and resets. `CanUse` is true while there is at least one charge, so an action with `MaxCharges = 3` can be fired three times in a row and then recovers gradually.

`CreateEffects(EffectTriggerType)` is what the definition uses to hand the right slice of its effects to a hitbox or apply them directly.

---

### Effects

An effect is a temporary (or infinite) modification applied to a player. `EffectDefinition` declares the duration, the trigger, the target and the properties it contributes; `EffectDefinitionData` counts the elapsed time on that player.

**What an effect declares**
```csharp
public float Duration { get; init; } = 0f;
public bool Infinite { get; init; } = false;
public EffectTriggerType Type { get; init; } = EffectTriggerType.OnUse;
public EffectApply ApplyTo { get; init; } = EffectApply.ToOwner;
```

`Tick` runs the clock and calls `Apply` every frame until `Elapsed >= Duration`, then marks `Expired` and calls `OnFinished` once. An `Infinite` effect never expires and never gets the closing call.

The three concrete ones show the range: `InstantHealEffectDefinition` acts once, `DamageEffectDefinition` acts per tick (poison), and `StatBoostEffectDefinition` acts through its `Properties`, which the resolver merges into the player's while the effect lasts.

---

### Hitboxes

A hitbox is the physical volume that carries an impact. `BaseHitbox` is an `Area2D` that knows nothing about who created it — it receives the already resolved damages, the already filtered effects, the owner and the knockback:

**Handing a hitbox everything it needs**
```csharp
hitbox.Initialize(damages, hitEffects, player, weapon.KnockbackForce);
```

`ApplyImpact` is the only place where damage, knockback and effects land on a target, and it is the same code whether the hitbox came from a weapon or from an action.

**The hitbox shapes**

| definition | shape |
|---|---|
| `MeleeHitbox` | fixed volume in front of the player |
| `AngledMeleeHitbox` | melee following the aimed angle |
| `ProjectileHitbox` | travels at `Speed` until `Lifetime` runs out |
| `GroundHitbox` | area on the ground, for a strike |

`Perfuracao` and `HitCount` control how many bodies it goes through before disappearing; `DestroyInAllBodies` decides whether hitting terrain also destroys it. **The hitbox manages its own lifetime** — it frees itself, and nobody keeps a reference to it.

---

### Skill tree

`SkillTreeNode` is a node in the tree, catalogued in `SkillTreeDB`. It has a level (`MaxLevel`), dependencies on other nodes with a minimum level, and the three things it grants:

**What a node grants**
```csharp
public Godot.Collections.Array<BasePropertyData> Properties { get; set; }
public Godot.Collections.Array<string> Effects { get; set; }
public Godot.Collections.Array<string> UnlockedAbilities { get; set; }
```

**What is saved is only the progress** — `SkillTreeNodeData` with the node id and the current level. Everything else is recomputed by `ApplySkillTree`, which clears `ActiveProperties`, puts the base values back, and then walks the invested nodes:

**Stacking a node once per level**
```csharp
for (int level = 0; level < progress.CurrentLevel; level++)
{
    foreach (var property in node.Properties)
    {
        ActiveProperties.Add(property);
    }
}
```

Properties are stacked **once per level**, so level 3 of a node contributes three times. Abilities and effects are not stacked — they are granted once, and revoked when the node stops granting them.

---

### Inventory

`InventoryData` is a `Resource` on the player with an `Array<ItemData>` and a `Size`; `InventorySystem` is a static class holding all the operations. There is no instance state in the system — every method receives the inventory it should act on.

**The two operations that matter**
```csharp
InventorySystem.GetSlot(inventory, index)
InventorySystem.MoveItem(inventory, instanceId, toIndex)
```

**The slot is the position in the array**, and `Size` is the authority on how many exist — `GetSlot` returns `null` for any index at or beyond it. The UI reads `Size` to decide how many slots to build, so changing the inventory size is a one-line change in `InventoryData`.

**Items are identified by `InstanceId`, not by slot.** Moving an item names the instance and the destination, which is what makes the operation safe over RPC: the client asks, the server resolves, and there is no index that could have drifted in between.

The first 8 slots are the hotbar, rendered in their own row; the rest go to the grid. That is a UI decision, not a data one — the inventory is a flat list.

---

### Tile streaming

`TileStreamingManager` — a node in `Scenes/Managers/Managers.tscn`, so it exists for the whole run of the application. It decides which chunks to paint and erase as players walk, and replicates that decision to the peers. **It only touches tilemap cells — it never instantiates anything.**

It is always in the tree and `_Process` is always called, but the first lines decide whether the tick does anything:

**The gate:**
```csharp
if (!Enabled || !IsServerAuthoritative() || Game.Managers.WorldManager.Node == null)
{
    return;
}
```

`Enabled` is set by `WorldManager.SetChunkStreamingEnabled` — `true` for a procedural world, `false` for a hand-drawn one, where the terrain is already in the level scene and nothing should be generated or erased. `IsServerAuthoritative()` is true for solo and for the host, never for a client. **So on a client the loop is a no-op: the client decides nothing.**

**The tick.** Every `EVALUATE_INTERVAL_SECONDS` (0.75s) the manager evaluates each dimension independently and asynchronously. A per-dimension flag (`_isEvaluatingOverworld` / `_isEvaluatingUpsidedown`) stops a second evaluation from starting while the previous one is still painting.

**The decision, per dimension:**

```
players   = WorldManager.GetPlayersInDimension(dimensionId)
needed    = union of the (2·LOAD_RADIUS_CHUNKS+1)² square around each player   (7×7 = 49 chunks)
missing   = needed - loaded, nearest player first, capped at MAX_CHUNK_LOADS_PER_TICK (6)
to unload = every loaded chunk farther than UNLOAD_RADIUS_CHUNKS from every player
```

The load radius (3) is smaller than the unload radius (6) on purpose: the gap is hysteresis, so walking back and forth across a chunk border does not thrash paint/erase.

**The tuning is bounded by throughput, not by radius.** A chunk is `CHUNK_SIZE` × the 16px tile = 512px, and the camera shows 960×540px, so even a radius of 2 already loads further than the screen shows. What made terrain pop in was the rate: crossing a chunk border makes a whole new line of chunks pending at once, and at 2 loads per 0.75s that line took 1.88s to fill while the player crossed the chunk in 1.71s (and in well under that when falling). The deficit accumulated every chunk until the load frontier caught up with the player. At 6 loads per 0.25s the line fills in 0.29s.

**Entity streaming has its own constants** (`ENTITY_RADIUS_CHUNKS`, `ENTITY_EVALUATE_INTERVAL_SECONDS`) so that tuning the tile loader does not silently change how far away entities materialize.

**Who notifies whom.** The server observes every player; there is no request coming from the client. `GetPlayersInDimension` runs on the authoritative side, the radius is computed around each player, and the server pushes the result with `RpcId`. And yes — this is a node that runs forever with a flag: it is never destroyed, it is disabled.

**Loading a chunk** paints from the seed and then replays the mutations:

**Painting a chunk**
```csharp
await _generator.PaintTilesAsync(layer, baseLayer, WorldSeed, dimensionId, chunkCoord, CHUNK_SIZE);
ApplyMutations(layer, chunkState);
_minimap.RecordChunk(dimensionId, layer, chunkCoord);
```

**What travels on the wire** is only the coordinate and the delta, never the painted terrain:

**What is sent to a peer**
```csharp
RpcId(peerId, nameof(LoadChunkReceive), dimensionId, chunkCoord, stateDict);
RpcId(peerId, nameof(UnloadChunkReceive), dimensionId, chunkCoord);
```

The client runs the same generator with the same `WorldSeed`, sent once by `SetWorldSeedReceive` when it joins, so the terrain is regenerated on both ends and only the mutation list is transmitted.

**Per-peer bookkeeping.** `_<dimension>LoadedPeers[chunkCoord]` holds which peers already have that chunk. Without it, a chunk the server had already painted because of *another* player would never reach the one who arrived later — the "is it loaded?" filter is global, so the chunk was skipped. `SendPendingChunksToPeers` closes that gap: there the decision is per peer, not global.

**Catch-up.** When a peer joins, `CatchUpPeer(peerId, spawnPosition)` sends the seed and then every already-loaded chunk within `UNLOAD_RADIUS_CHUNKS` of where that peer is about to spawn — not every loaded chunk, which with several players spread out meant painting regions the peer would never reach and unloading them right after.

**Events.** `ChunkLoaded` / `ChunkUnloaded` are emitted for whoever needs to react (the minimap today). The manager knows nothing about its subscribers.

---

### Entity streaming

`WorldStreaming` — the script attached to the **root of `World.tscn`**, not to `Managers.tscn`. It handles what exists *inside* the world: which entities are materialized, which leave the tree, and what goes into the file. Same shape as the tile streaming — a node that stays alive with an `Enabled` flag, a tick every `EVALUATE_INTERVAL_SECONDS`, and the same server-only guard — but a completely independent decision. **It does not subscribe to `ChunkLoaded`:** the two systems measure distance to the same players, not to each other.

**There is no registry: the tree is the index.** A recursive walk from the World root finds everything, and the criterion to participate is having a field marked with `[GodotDictionaryField]` and not being in the `players` group.

**Godot's own semantics already express the three operations:**

| operation | meaning | consequence in the save |
|---|---|---|
| `AddChild` | loaded | in the tree |
| `RemoveChild` | unloaded | still saved, comes back when a player gets close |
| `QueueFree` | forgotten | disappears from both sources — this is how it leaves the save |

The class observes `GetTree().NodeAdded` and `NodeRemoved`, so this holds no matter who did it — gameplay code, a reparent, the editor. `IsQueuedForDeletion()` inside `NodeRemoved` is what tells "unloaded" apart from "forgotten".

Whoever removed the node has to keep the reference or it leaks, because `Node` is not `RefCounted`. That is what `_unloaded` is for: a dictionary of what is out of the tree, owned by the streaming until it is given back or freed.

**A node out of the tree cannot be asked which dimension it is in.** `_dimensionOf` remembers it while the node is still in the tree, so a prop unloaded in the Overworld does not answer "upsidedown" and end up saved in the wrong dimension.

**The policy is declared on the class**, with the `[Unload]` attribute:

**Declaring the unload policy**
```csharp
[Unload(UnloadMode.Global)]
public partial class Prop : Area2D
```

Three modes cover every case, and `Global` is what a class with no attribute gets:

**What each unload mode does**

| mode | behavior |
|---|---|
| `Never` | never unloads — arena boss, quest structure |
| `Global` | nobody has it, not even the server; the simulation stops (prop, dropped item) |
| `PeerOnly` | the server keeps it and goes on simulating; only the distant peer loses the node — for something that has to run with nobody watching |

**Loading and unloading** hang the node under the dimension's `Entities` node and replicate the decision:

**Loading and unloading**
```csharp
Load   →  Dimensions.ResolveEntities(dimension).AddChild(node);   Dimensions.SpawnRequest(record)
Unload →  node.GetParent().RemoveChild(node);                     Dimensions.DespawnRequest(id)
```

`RemoveChild`, never `QueueFree`: the node leaves the tree but goes on being the data.

**Identity.** The node's name *is* the identity (`E<instanceId>`), because Godot resolves RPC by node path — the same entity has to carry the same name on every peer. `FindByInstanceId` looks that name up inside `Entities`.

---

### Save system

One file per world, `user://saves/worlds/<worldId>/world.json`, plus one file per character in `user://saves/characters/<characterId>.json`. The convention is **a flat list, with persistence declared only on the first layer**.

1. **Only a scene root with a marked script goes into the save.** `[SaveScene("type", "res://…")]` on the class is the declaration — there is no group, no central list, and no walk over internal nodes.
2. **Properties are declared by hand** with `[Save]`. Nothing enters the file by accident.
3. **An internal node gets by through its parent.** Either it reads what it needs from the parent, or the parent fills its children when restoring. The `Sprite2D`, the `CollisionShape2D` and the 54 `AtlasTexture` of a `SpriteFrames` never show up in the file.
4. **A flat list inside each dimension.** Nothing contains anything else in this game, so nothing nests in the file.

**The shape of the file:**

```json
{
	"$type": "world",
	"state": { "worldId": "d3af6ac8", "name": "Mundo", "seed": 123 },
	"dimensions": [
		{
			"$type": "upsidedown",
			"state": { "mutations": [ { "type": "break", "x": -7, "y": 42, "blockId": "" } ] },
			"nodes": [
				{ "$type": "player", "id": "abc-123", "$ref": "characters/abc-123.json" },
				{ "$type": "portal", "id": "E777",
				  "state": { "propId": "portal", "position": { "x": 864.0, "y": -128.0 } } }
			]
		}
	]
}
```

There is no `parent` field: the position in the file already says where the thing is, which makes it impossible to write an entity without a dimension, or a dimension inside a dimension. **There is no scene path either** — the class is stored in `$type`, and the scene comes from that class's `[SaveScene]` when loading, so moving a scene between folders never invalidates a save.

**Tiles are saved as mutations, not as chunks.** The dimension declares them as its own state; the chunk is a streaming detail and does not exist in the file. A chunk that was visited but never altered is not written — otherwise the save would grow with explored area instead of growing with what the player did.

**`$ref`: whoever has their own file.** A class that declares `Ref` writes only the pointer, never `state`:

**Declaring a class with its own file**
```csharp
[SaveScene("player", "res://Scenes/World/Characters/Player.tscn", Ref = "characters/{0}.json")]
```

The `{0}` is the external identity — for the player, its `CharacterId`. The NPC uses the same scene but has no `CharacterId`, and for that reason does not enter the file at all.

**Saving is a merge of two sources**, and this is the part that matters: what is hanging in the tree, plus what the streaming is holding outside of it. An unloaded entity still exists, it just is not materialized — without the merge, saving right after loading a world would write an empty world.

**Saving**
```
SaveManager.SaveAll()                      autosave timer, or leaving the world
  ├─ Saving?.Invoke()                      whoever holds state outside the Resource updates it
  ├─ SaveWorld(world)                      only host/solo writes the world
  │    └─ WorldManager.SalvarDocumento
  │         ├─ WorldDocument.Escrever(Streaming, dimensions, Streaming.Descarregados)
  │         └─ SaveStorage.SaveWorldDocument
  └─ the local character is written by everyone; the host also writes the peers'
```

**Loading** rebuilds two levels, without recursion:

**Loading**
```
WorldManager.CarregarDocumento(save)
  └─ for each dimension in the document:
       ├─ SaveSerializer.Ler(dimension, state)   the setter passes the mutations to the layers
       └─ for each node:
            ├─ is it a $ref?  skip — the session loads it when the owner joins
            └─ WorldDocument.Construir           instantiate the scene, name it, apply state
                 ├─ streaming on  → Streaming.Adotar   born UNLOADED
                 └─ streaming off → AddChild straight into Entities
```

**What `[Save]` accepts:** primitives, `Vector2`, `Dictionary`, `Array` and any `Resource` — the last one through `GodotDictionaryParser`, so an `ItemData` goes in whole, in a single field. Any other type raises `NotSupportedException` at the moment of writing, never in silence.

**`position` is implicit.** `Position` belongs to `Node2D` and cannot take an attribute, so instead of every class declaring a shim, `WorldDocument` writes and reads it for every entity — it is the one thing they all have and none of them can declare.

**The pieces:**

| class | role |
|---|---|
| `SaveSceneAttribute` | `$type` + scene + optional `Ref`. The mark of "I am a persistable root" |
| `SaveAttribute` | the property enters `state`; optional custom name, camelCase otherwise |
| `SaveSerializer` | `$type → class` map by reflection; reads and writes `state` |
| `WorldDocument` | the shape of `world.json`: keys, writing, reading, `Construir` |
| `SaveStorage` | pure IO: `LoadWorldDocument` / `SaveWorldDocument` |
| `SaveManager` | registry of what is in play, autosave timer, and the save policy |

---

## Pending

### 1. Effects applied on equip and unequip

An item can only apply effects at the moment it is *used*. Equipping it contributes properties but no effects, so there is no way to build a piece of gear whose effect lasts while it is worn.

`OnEquip` and `OnUnequip` move `Modifiers` into `ActiveProperties` and stop there — the item's `Effects` list is never read on either side.

Two gaps stand between here and that feature. `EffectTriggerType` only distinguishes `OnUse` from `OnHit`, so an effect cannot declare that it lasts while equipped. And the player exposes `GiveEffect(id)` with no counterpart — there is no way to take an effect away, so an effect granted on equip would survive unequipping.

---

### 2. Identification of the source and category of a property or effect

A property carries no record of where it came from: `BasePropertyData` holds nothing but an `InstanceId`, and `EffectDefinitionData` is in the same position.

Once a property reaches `ActiveProperties` there is no way to tell whether an item, a skill node or an effect put it there, and no way to tell a weapon property apart from an armour one or a global one. Removal works around this by keeping the same object reference and calling `ActiveProperties.Remove(modifier)` — which ties the item to the exact instance it inserted.

Recording a source and a category on `BasePropertyData` would reach the whole system at once, since every property inherits from it and every merge goes through the `Resolver`. It is what makes conditional mechanics expressible: a bonus that applies only to weapon properties, gear that cancels effects coming from other gear, or removing everything a given source contributed without holding on to references.

---

### 3. Lighting

Lighting is prototyped on two branches, `ilumination-feature` and `ilumination-hdr-feature`, present locally and on the remote. It is exploratory work: it still has to be developed and merged back.

---

### 4. Terrain connection between different biomes

`TerrainLayer` intercepts Godot's own tile connection so the project can control what happens at the border between two terrains — `ConnectAsync`, `ReconnectForeignBorderAsync`, `ConnectDependentAsync` and `ReconnectForeignBorderDependentAsync`, plus `excludedForeignTerrainSets` for keeping specific sets out of the connection.

At 1378 lines it is the largest file after `Player`, and the least exercised part of the project. The border between biomes is where the interception actually matters and where it has been verified the least.

---

### 5. The gap between textures that do not connect

Where two terrains meet and cannot be connected, a hole is left in the tiling. The intended answer is already wired: `BiomeDefinition.BorderCapTerrainSet` is filled in for both biomes and painted onto the `Base` layer through `ConnectDependent` — a second terrain set, underneath the main one, covering what the first cannot resolve.

The approach depends on textures larger than a single tile. Those do not exist yet, which is why the gap is still visible.

---

### 6. Dependency hierarchy between features

Splitting the code into separate C# projects — one assembly per layer, with the Godot project referencing them through a central one — would make the dependencies between features explicit and enforced by the compiler instead of by convention.

Four domains would not survive the split as they are. `Items`, `Actions`, `Effects` and `Hitboxes` all know `Player` and are known by it, so each of them would become a circular reference the moment it became its own assembly. `SkillTree` and `Properties` only point one way and could be separated without rework.

The coupling is deliberate. It is what lets an item, an action or an effect act directly on the player, and it is where the flexibility of the mechanics comes from. Doing the split means first choosing what replaces it — an interface for the player, an event bus, or inverting who calls whom — and that decision is the expensive part, not the split.

---

### 7. Review of the existing code

Not all of the code was reviewed before development paused, and parts of it are likely to improve under a second look. `Player`, `TerrainLayer` and `Game` are the largest files by a wide margin, followed by the inventory and HUD screens — the reasonable place to start.

---

### 8. Reorganizing the project structure

The folder structure separates code by technical sublayer, and it predates the save convention. That convention settled on a flat layer where the unit of everything — persistence, streaming and replication — is a scene together with the script attached to its root.

Since the scene and its script are now what the whole system revolves around, the structure may be clearer if it is organised around them rather than around the sublayer split it uses today.
