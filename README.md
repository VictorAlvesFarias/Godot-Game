# Godot Multiplayer RPC Architecture Guide

This document explains the RPC (Remote Procedure Call) architecture used in this Godot 4.x multiplayer project with server authority.

---

## 1. RPC Communication in Godot: Declaration, Usage, and Architecture

### What is RPC?

RPC (Remote Procedure Call) allows you to call a method on another peer in the network. In Godot, when you call an RPC method, it executes on the specified peer(s) rather than locally.

### RPC Declaration

In Godot 4.x with C#, you declare RPC methods using the `[Rpc]` attribute:

```csharp
[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
public void MyRpcMethod(int param1, string param2)
{
    // This code executes on remote peers
}
```

**Key Parameters:**

- **RpcMode**: Determines who can call this RPC
  - `AnyPeer`: Any connected peer can call it
  - `Authority`: Only the multiplayer authority of the node can call it
  
- **CallLocal**: Whether the method executes locally when called
  - `true`: Executes on the caller AND remote peers
  - `false`: Only executes on remote peers

- **TransferMode**: How data is sent (optional)
  - `Reliable`: Guaranteed delivery (default)
  - `Unreliable`: May be lost, but faster

### RPC Usage

To call an RPC method:

```csharp
// Call on all peers (including self if CallLocal = true)
Rpc(nameof(MyRpcMethod), 42, "hello");

// Call on specific peer
RpcId(peerId, nameof(MyRpcMethod), 42, "hello");
```

### Architecture Overview

```
┌─────────────┐                  ┌─────────────┐
│   Client 1  │◄────────────────►│   Server    │
│  (Player 1) │      RPC         │ (Authority) │
└─────────────┘                  └─────────────┘
                                        ▲
                                        │ RPC
                                        ▼
                                 ┌─────────────┐
                                 │   Client 2  │
                                 │  (Player 2) │
                                 └─────────────┘
```

**Server Authority Model**: The server is the source of truth. Clients send input to the server, and the server validates and broadcasts state changes.

---

## 2. Property Synchronization Between Client and Server

### The Challenge

Regular properties are NOT automatically synchronized in Godot multiplayer. Each peer maintains its own copy of the scene tree, and property changes on one peer don't affect others unless explicitly synchronized.

### Solution: RPC-Based Synchronization

This project uses **explicit RPC calls** to synchronize properties across the network.

#### Example: Health Property Synchronization

**Property Declaration:**
```csharp
public partial class Player : CharacterBody2D
{
    [Export] public int MaxHealth { get; set; } = 5;
    public int CurrentHealth { get; set; }  // NOT automatically synchronized
    
    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
    }
}
```

**Synchronization Method:**
```csharp
[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
public void SyncHealth(int health)
{
    CurrentHealth = health;  // Update local property
}
```

**Usage in Damage System:**
```csharp
[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
public void TakeDamage(int damage)
{
    // Only server processes damage calculation
    if (!Multiplayer.IsServer())
        return;

    if (CurrentHealth <= 0)
        return;

    CurrentHealth -= damage;  // Server updates its local value
    
    // Synchronize to all clients (including server with CallLocal = true)
    Rpc(nameof(SyncHealth), CurrentHealth);
    
    // Synchronize visual effects too
    Rpc(nameof(ShowDamageEffect));

    if (CurrentHealth <= 0)
        HandleResetPosition();
}
```

**Key Points:**
1. **Server Authority**: Only the server modifies `CurrentHealth`
2. **Broadcast Update**: Server calls `SyncHealth` RPC to update all peers
3. **Single Source of Truth**: Server's value is authoritative
4. **CallLocal = true**: Ensures server also runs the sync method

---

## 3. Building Actions and Methods for Server Authority

### Architecture Pattern

For server authority, follow this pattern:

```
Client Input → Client Prediction → Server Validation → Server Execution → Broadcast to All Clients
```

### Step-by-Step Method Construction

#### Step 1: Input Collection (Client-Side)

```csharp
private float inputX;
private float inputY;
private bool inputDash;

public void HandleInput()
{
    inputX = Input.GetAxis("move_left", "move_right");
    inputY = Input.GetAxis("move_up", "move_down");
    inputDash = Input.IsActionJustPressed("dash");
}
```

#### Step 2: Send Input to Server

```csharp
public override void _PhysicsProcess(double delta)
{
    bool isOwner = GetMultiplayerAuthority() == Multiplayer.GetUniqueId();

    if (isOwner)
    {
        HandleInput();
        // Send input to server for validation
        Rpc(nameof(ServerReceiveInput), inputX, inputY, inputJump, inputDash);
    }
    
    // Both server and owning client simulate
    if (Multiplayer.IsServer() || isOwner)
    {
        HandleMovement((float)delta, inputX, inputJump, inputDash);
    }
}
```

#### Step 3: Server Receives and Validates Input

```csharp
[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
public void ServerReceiveInput(float x, float y, bool jump, bool dash)
{
    // Store input for server-side simulation
    inputX = x;
    inputY = y;
    inputJump = jump;
    inputDash = dash;
}
```

#### Step 4: Server Executes Authoritative Action

```csharp
private void HandleMovement(float delta, float x, bool jump, bool dash)
{
    Vector2 v = Velocity;

    // Dash example: only server authorizes dash start
    if (dash && canDash && !isDashing)
    {
        Vector2 direction = new Vector2(x, inputY);
        if (direction.Length() == 0)
            direction = Vector2.Right;
        dashDirection = direction.Normalized();
        isDashing = true;
        canDash = false;
        dashTimer = 0;
        
        // Broadcast visual effects to all clients
        Rpc(nameof(ActivateDashEffectsRpc));
    }

    // Physics simulation
    if (isDashing)
    {
        v = dashDirection * DashSpeed;
        // ... rest of logic
    }
    
    Velocity = v;
    MoveAndSlide();  // This is authoritative on server
}
```

#### Step 5: Broadcast Visual/Audio Effects

```csharp
[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
public void ActivateDashEffectsRpc()
{
    if (dashParticles != null)
        dashParticles.Emitting = true;

    if (sprite != null)
        sprite.DefaultColor = new Color(0.5f, 1f, 1f);
}
```

### Best Practices for Server Authority

1. **Input Only**: Clients only send input, never direct state changes
2. **Server Validates**: Server checks if action is allowed (cooldowns, resources, etc.)
3. **Server Executes**: Server performs the actual game logic
4. **Broadcast Effects**: Server tells clients to play effects/animations
5. **Client Prediction**: Clients can run the same logic locally for responsiveness, but server has final say

### Example: Shooting with Server Authority

```csharp
// Client collects input
private void HandleShooting(float delta)
{
    if (Input.IsActionPressed("shoot") && canShoot && bulletScene != null)
    {
        Vector2 dir = (GetGlobalMousePosition() - GlobalPosition).Normalized();
        // Tell everyone to spawn a bullet (server will be authoritative)
        Rpc(nameof(SpawnBullet), GlobalPosition, dir);
        canShoot = false;
    }
}

// Server spawns bullet authoritatively
[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
public void SpawnBullet(Vector2 spawnPosition, Vector2 direction)
{
    Bullet bullet = bulletScene.Instantiate<Bullet>();
    bullet.GlobalPosition = spawnPosition + (direction * 50);
    bullet.Direction = direction;
    bullet.Shooter = this;

    Node mainScene = GetTree().Root.GetNodeOrNull("Main");
    if (mainScene != null)
        mainScene.AddChild(bullet);  // Added to server, synced to clients
    else
        bullet.QueueFree();
}
```

---

## 4. Interface Construction with Complete Architecture Integration

### Example: Health HUD Interface

The Health HUD demonstrates how UI interfaces with the multiplayer system, combining all previous concepts.

#### Challenge: UI Must Show Local Player's State

Each client needs to:
1. Identify their own player among multiple players
2. Read that player's health property
3. Update the display when health changes

#### Solution Architecture

```csharp
public partial class HealthHUD : Label
{
    private Player localPlayer;

    public override void _Process(double delta)
    {
        // Continuously try to find local player
        if (localPlayer == null || !IsInstanceValid(localPlayer))
        {
            FindLocalPlayer();
        }

        // Update display if we have a player
        if (localPlayer != null && IsInstanceValid(localPlayer))
        {
            UpdateHealthDisplay();
        }
        else
        {
            Text = "";
        }
    }
}
```

#### Step 1: Finding the Local Player

```csharp
private void FindLocalPlayer()
{
    var players = GetTree().GetNodesInGroup("players");
    
    // Check if multiplayer is active and ready
    int localPeerId = 1;
    bool hasMultiplayer = false;
    
    // Verify peer is connected before accessing GetUniqueId()
    if (Multiplayer != null && 
        Multiplayer.MultiplayerPeer != null && 
        Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
    {
        try
        {
            localPeerId = Multiplayer.GetUniqueId();
            hasMultiplayer = true;
        }
        catch
        {
            hasMultiplayer = false;
        }
    }
    
    // Find player with matching authority
    foreach (Node node in players)
    {
        if (node is Player player)
        {
            // Single player: take first player
            // Multiplayer: take player with our authority
            if (!hasMultiplayer || player.GetMultiplayerAuthority() == localPeerId)
            {
                localPlayer = player;
                break;
            }
        }
    }
}
```

**Key Concepts:**
- **Multiplayer Authority**: Each player node has an authority ID matching their peer ID
- **Safe Access**: Check if multiplayer is ready before calling `GetUniqueId()` to avoid errors
- **Fallback**: Works in both single-player and multiplayer modes

#### Step 2: Reading Synchronized Properties

```csharp
private void UpdateHealthDisplay()
{
    // Read CurrentHealth property
    // This property is synchronized via SyncHealth RPC (see section 2)
    string hearts = "";
    for (int i = 0; i < localPlayer.CurrentHealth; i++)
    {
        hearts += "♥ ";
    }
    
    int lostHealth = localPlayer.MaxHealth - localPlayer.CurrentHealth;
    for (int i = 0; i < lostHealth; i++)
    {
        hearts += "♡ ";
    }
    
    Text = hearts.Trim();
}
```

**Integration with Synchronization:**

When the server changes health:
```csharp
// In Player.cs
[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
public void TakeDamage(int damage)
{
    if (!Multiplayer.IsServer())
        return;

    CurrentHealth -= damage;
    Rpc(nameof(SyncHealth), CurrentHealth);  // ← Broadcasts to all
}

[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
public void SyncHealth(int health)
{
    CurrentHealth = health;  // ← This updates for HUD to read
}
```

The HUD automatically reflects the change because:
1. Server modifies `CurrentHealth` and calls `SyncHealth`
2. `SyncHealth` RPC updates `CurrentHealth` on all clients
3. HUD's `_Process` reads the updated `CurrentHealth` every frame

### Complete Data Flow Diagram

```
┌────────────────────────────────────────────────────────────┐
│  Player A shoots Player B                                  │
└────────────────────────────────────────────────────────────┘
                         
1. Client A: Input.IsActionPressed("shoot")
   │
   ├─→ Client A: Rpc(SpawnBullet, position, direction)
   │                                                         
2. Server: SpawnBullet() creates bullet
   │
   ├─→ All Clients: See bullet spawn (CallLocal = true)
   │
3. Server: Bullet detects collision with Player B
   │
   ├─→ Server: bullet.OnBodyEntered(playerB)
   │    └─→ (Check: if !Multiplayer.IsServer() return)
   │    └─→ playerB.Rpc(TakeDamage, 1)
   │
4. Server: playerB.TakeDamage(1)
   │    └─→ (Check: if !Multiplayer.IsServer() return)
   │    └─→ CurrentHealth -= 1  (Server state updated)
   │    └─→ Rpc(SyncHealth, CurrentHealth)
   │    └─→ Rpc(ShowDamageEffect)
   │
5. All Clients: SyncHealth(newHealth)
   │    └─→ CurrentHealth = newHealth  (Client state synced)
   │
6. All Clients: ShowDamageEffect()
   │    └─→ sprite.DefaultColor = Red
   │    └─→ damageColorTimer = 0.3f
   │
7. Client B: HealthHUD._Process()
   │    └─→ UpdateHealthDisplay()
   │    └─→ Read localPlayer.CurrentHealth
   │    └─→ Update UI text
   │
8. All Clients: Player._PhysicsProcess()
   │    └─→ damageColorTimer -= delta
   │    └─→ When timer reaches 0: sprite.DefaultColor = White
```

### Interface Best Practices

1. **Poll, Don't Push**: Interfaces should read state every frame rather than using signals/events from network objects
2. **Identify Local Objects**: Use `GetMultiplayerAuthority()` to find which objects belong to this client
3. **Safe Multiplayer Checks**: Always verify multiplayer is ready before calling multiplayer API
4. **Null Checks**: Network objects can be destroyed, always check validity
5. **Graceful Degradation**: Support both single-player and multiplayer modes

---

## Summary: Complete Architecture Flow

```
INPUT LAYER (Client)
    ↓
INPUT TRANSMISSION (RPC)
    ↓
SERVER VALIDATION (Server Authority)
    ↓
SERVER EXECUTION (Game Logic)
    ↓
STATE SYNCHRONIZATION (RPC Broadcast)
    ↓
INTERFACE DISPLAY (UI Reads State)
```

**Key Takeaways:**

1. **RPC is the Communication Layer**: All network communication happens through RPC calls
2. **Properties Don't Auto-Sync**: You must explicitly sync properties via RPC
3. **Server is Authority**: Server validates, executes, then broadcasts results
4. **Clients Predict**: Clients can run the same logic for responsiveness, but server wins conflicts
5. **Interfaces Poll State**: UI reads synchronized properties every frame

This architecture provides:
- ✅ **Authoritative Server**: Prevents cheating
- ✅ **Responsive Clients**: Client prediction reduces perceived lag
- ✅ **Consistent State**: All clients see the same game state
- ✅ **Scalable**: Easy to add new actions following the same pattern

---

## Project-Specific Examples

### Player Movement with Dash
- Client sends input via `ServerReceiveInput` RPC
- Both server and owning client simulate movement (client prediction)
- Server's simulation is authoritative
- Dash effects broadcast via `ActivateDashEffectsRpc`

### Damage System
- Bullet collision only processed by server (`if (!Multiplayer.IsServer()) return`)
- Server calls `TakeDamage` RPC
- Damage calculation only on server
- Health synced via `SyncHealth` RPC
- Visual effect synced via `ShowDamageEffect` RPC
- HUD reads `CurrentHealth` property to display

### Network Connection
- Host creates server with `NetworkManager.CreateServer(port)`
- Client joins with `NetworkManager.JoinServer(address, port)`
- Server spawns player for each connected peer
- Existing players synced to new clients via `SpawnPlayerOnClient` RPC

---

## Debugging Tips

1. **Check Who's Running Code**: Add `GD.Print($"Running on: {(Multiplayer.IsServer() ? "Server" : "Client")}")` 
2. **Verify RPC Calls**: Log in RPC methods to confirm they're being called
3. **Authority Checks**: Ensure `GetMultiplayerAuthority()` is set correctly on spawned nodes
4. **CallLocal Flag**: Remember `CallLocal = false` means the caller doesn't execute the method
5. **Connection Status**: Verify `Multiplayer.MultiplayerPeer.GetConnectionStatus()` is `Connected`

---

*This architecture ensures a robust, cheat-resistant multiplayer game with responsive gameplay and synchronized state across all connected peers.*
