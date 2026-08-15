Readme

A 2.5D game built with **Godot 4.6** and **C# (.NET)**.

The project follows a feature-based folder architecture with a clear separation between **logic (Features/)**, **scenes (Scenes/)**, **assets (Assets/)**, and **constants (Constants/)**. Each feature is self-contained in its own folder with standardized sublayers, making it easy to add new features by following the same pattern.

---

## Folder Structure

```
root/
├── Assets/
│   ├── Audio/
│   ├── Fonts/
│   ├── Icons/
│   ├── Sprites/
│   ├── Textures/
│   └── Tiles/
├── Constants/
│   └── Assets.cs
├── Features/
│   ├── Managers/
│   │   └── <ManagerName>/
│   │       ├── Manager/
│   │       │   └── <ManagerName>.cs
│   │       └── Structures/
│   │           └── <StructureName>.cs
│   ├── UI/
│   │   └── <UIName>/
│   │       ├── Objects/
│   │       │   └── <ObjectName>.cs
│   │       └── View/
│   │           └── <UIName>UI.cs
│   └── Word/
│       └── <FeatureName>/
│           ├── Abstractions/
│           │   └── <BaseName>.cs
│           ├── Definitions/
│           │   └── <ConcreteName>.cs
│           ├── Entities/
│           │   └── <EntityName>.cs
│           ├── Instances/
│           │   └── <FeatureName>Instance.cs
│           ├── Singletons/
│           │   └── <FeatureName>DB.cs
│           ├── Structures/
│           │   └── <StructName>.cs
│           ├── Systems/
│           │   └── <SystemName>.cs
│           └── Types/
│               └── <EnumName>.cs
├── Scenes/
│   ├── Main.tscn
│   ├── Managers/
│   │   └── Managers.tscn
│   ├── Ui/
│   │   ├── Ui.tscn
│   │   └── <UIName>/
│   │       └── <UIName>.tscn
│   └── World/
│       ├── World.tscn
│       └── <SceneName>/
│           └── <SceneName>.tscn
├── project.godot
├── DefaultTheme.tres
└── Jogo25D.sln
```

> No sublayer is mandatory. Only use the ones that make sense for the feature.

---

## Feature Layers

### Features/World/&lt;Domain&gt;/Managers/

Global service for that domain. Lives in a `Managers/` folder directly inside its domain, all nested under `Features/World/` (e.g. `Features/World/Core/Managers/` for `WorldManager`, `Features/World/Save/Managers/` for `SaveManager`, `Features/World/Screen/Managers/` for `ScreenManager`) instead of a separate top-level feature. `Core` holds the central/main manager of the World feature; other domains (`Save`, `Screen`, ...) sit as siblings of `Core`, each with its own `Managers/` folder.

**Sublayers:**

- The manager script itself sits directly inside `Managers/` (e.g. `WorldManager.cs`).
- **Resources:** Godot `Resource` subclasses owned by that manager (save data models, etc.).
- **Types:** Enums. Categorize variants within the manager's domain.

---

### Features/UI/

User interface features. Each screen/UI component lives in its own folder inside `UI/`.

**Sublayers:**

- **Structures:** Structs, simple and immutable data passed between systems (damage info, inputs, etc.).
- **Objects:** DTOs, data models used as arguments or to facilitate data transport.
- **View:** Visual script that extends CanvasLayer. This is the script attached directly to the corresponding `.tscn` scene in `Scenes/Ui/`.

---

### Features/Word/

Game world features (gameplay). Each game domain lives in its own folder inside `Word/`.

**Sublayers:**

- **Abstractions:** Abstract classes and base interfaces. Defines the immutable contract/blueprint that concrete implementations must follow (lifecycle methods, required properties).
- **Definitions:** Concrete implementations of abstractions. Each class inherits from an abstraction and implements the actual, specific behavior for that type.
- **Entities:** Main game objects. Scripts attached to the root Node of an entity (character, camera, etc.) — the "owner" of the entity in the scene tree.
- **Instances:** Mutable runtime state. Objects that hold data that changes during gameplay (cooldown, charges, quantity) and keep a reference to their Definition.
- **Singletons:** Static registries (in-memory databases). Classes with `Dictionary<string, Definition>` that catalog all available definitions and provide factory methods to create instances.
- **Structures:** Structs, simple and immutable data passed between systems (damage info, inputs, etc.).
- **Objects:** DTOs, data models used as arguments or to facilitate data transport.
- **Systems:** Behavior scripts attached to nodes. Logic that runs as a child component of an entity or scene, processing behavior every frame or event.
- **Types:** Enums. Categorize variants within the feature's domain.

