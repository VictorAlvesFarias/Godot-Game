using Godot;
using Jogo25D.Biomes;
using Jogo25D.Blocks;
using Jogo25D.Characters;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.Managers.Save.Types;
using Jogo25D.Features.World.Characters.Resources;
using Jogo25D.Features.World.Chunks.Resources;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Instances;
using Jogo25D.Items;
using Jogo25D.Portals;
using Jogo25D.Props;
using Jogo25D.Structures;
using Jogo25D.UI;
using Jogo25D.Utils.GodotDictionaryParser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Systems
{
	public partial class WorldManager : Node
	{
		#region Properties

		public ENetMultiplayerPeer Peer { get; set; }
		public Node2D OverworldParent { get; set; }
		public Node2D UpsidedownParent { get; set; }
		public SubViewportContainer OverContainer { get; set; }
		public SubViewportContainer UpContainer { get; set; }

		#endregion

		#region Save - Properties

		public WorldSaveData CurrentWorldSave { get; private set; }

		public CharacterSaveData PendingCharacter { get; set; }

		public WorldSaveData PendingWorld { get; set; }
		public bool PendingWorldIsDefault { get; set; }

		public event Action<string, Godot.Collections.Array> ServerCharacterListAvailable;

		public Timer AutosaveTimer { get; set; }

		private readonly Dictionary<long, CharacterSaveData> _peerCharacters = new();
		private readonly Dictionary<long, string> _pendingProfileByPeer = new();

		#endregion

		#region Systems

		private Inventory Inventory { get; set; } = new Inventory();

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			GD.Print("[WorldManager._Ready] _Ready()");

			Multiplayer.PeerConnected += OnPeerConnected;
			Multiplayer.PeerDisconnected += OnPeerDisconnected;
			Multiplayer.ConnectedToServer += OnConnectedToServer;
			Multiplayer.ConnectionFailed += OnConnectionFailed;
			Multiplayer.ServerDisconnected += OnServerDisconnected;

			GetTree().Root.CloseRequested += PersistBeforeLeaving;
		}

		#endregion

		#region Core - World spawning

		private void ResolveWorldReferences()
		{
			var overworldParentPath = "Main/World/Levels/OverworldViewportContainer/OverworldViewport/Overworld";

			OverworldParent = GetTree().Root.GetNodeOrNull<Node2D>(overworldParentPath);

			if (OverworldParent == null)
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] GetNodeOrNull: OverworldParent not found at path {overworldParentPath}");
			}
			else
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] OverworldParent found: {OverworldParent.Name}");
			}

			var upsidedownParentPath = "Main/World/Levels/UpsidedownViewportContainer/UpsidedownViewport/Upsidedown";

			UpsidedownParent = GetTree().Root.GetNodeOrNull<Node2D>(upsidedownParentPath);

			if (UpsidedownParent == null)
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] GetNodeOrNull: UpsidedownParent not found at path {upsidedownParentPath}");
			}
			else
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] UpsidedownParent found: {UpsidedownParent.Name}");
			}

			var overContainerPath = "Main/World/Levels/OverworldViewportContainer";

			OverContainer = GetTree().Root.GetNodeOrNull<SubViewportContainer>(overContainerPath);

			if (OverContainer == null)
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] GetNodeOrNull: OverContainer not found at path {overContainerPath}");
			}
			else
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] OverContainer found: {OverContainer.Name}");
			}

			var upContainerPath = "Main/World/Levels/UpsidedownViewportContainer";

			UpContainer = GetTree().Root.GetNodeOrNull<SubViewportContainer>(upContainerPath);

			if (UpContainer == null)
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] GetNodeOrNull: UpContainer not found at path {upContainerPath}");
			}
			else
			{
				GD.Print($"[WorldManager.ResolveWorldReferences] UpContainer found: {UpContainer.Name}");
			}
		}

		public void SpawnWorld()
		{
			if (OverworldParent != null)
			{
				return;
			}

			var main = GetTree().Root.GetNodeOrNull<Node>("Main");

			if (main == null || main.HasNode("World"))
			{
				ResolveWorldReferences();

				return;
			}

			var world = GD.Load<PackedScene>("res://Scenes/World/World.tscn").Instantiate<Node2D>();

			main.AddChild(world);

			ResolveWorldReferences();

			GD.Print("[WorldManager.SpawnWorld] world instantiated");
		}

		public void EnterPendingWorld()
		{
			if (PendingWorldIsDefault)
			{
				SpawnLocalWorldAndPlayer();
			}
			else
			{
				CreateProceduralWorldAndPlayer(PendingWorld);
			}

			PendingWorld = null;
		}

		public void SpawnLocalWorldAndPlayer()
		{
			CurrentWorldSave = null;

			StopAutosaveTimer();

			SpawnWorld();

			SetChunkStreamingEnabled(false);

			RespawnLocalSoloPlayer();
		}

		public async void CreateProceduralWorldAndPlayer(WorldSaveData save = null)
		{
			var saveManager = ResolveSaveManager();

			save ??= saveManager?.CreateWorld("Mundo sem nome", (long)GD.Randi(), WorldCharacterMode.LocalCharacters, "", SavesConstants.DEFAULT_AUTOSAVE_INTERVAL_MINUTES);

			CurrentWorldSave = save;

			SpawnWorld();

			ClearWorldLayers();

			var chunkStreamingManager = GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(StaticNodePathsConstants.ChunkStreamingManager);

			if (chunkStreamingManager != null && save != null)
			{
				chunkStreamingManager.SetWorldSeed(save.Seed);
				chunkStreamingManager.ImportState(ChunkStreamingConstants.OVERWORLD_ID, saveManager?.LoadDimensionState(save.WorldId, ChunkStreamingConstants.OVERWORLD_ID));
				chunkStreamingManager.ImportState(ChunkStreamingConstants.UPSIDEDOWN_ID, saveManager?.LoadDimensionState(save.WorldId, ChunkStreamingConstants.UPSIDEDOWN_ID));
			}

			SetChunkStreamingEnabled(true);

			var loadingUi = GetTree().Root.GetNodeOrNull<LoadingUI>("Main/Ui/LoadingUI");

			loadingUi?.Open();

			if (chunkStreamingManager != null)
			{
				await chunkStreamingManager.PreloadSpawnAreaAsync(ChunkStreamingConstants.UPSIDEDOWN_ID, UpsidedownParent, Vector2.Zero);
			}

			RestorePortals(save);

			RespawnLocalSoloPlayer();

			StartAutosaveTimer(save);

			loadingUi?.Close();
		}

		private void ClearWorldLayers()
		{
			foreach (var parent in new[] { OverworldParent, UpsidedownParent })
			{
				parent?.GetNodeOrNull<TileMapLayer>(ChunkStreamingConstants.PROCEDURAL_BASE_LAYER_NAME)?.Clear();
				parent?.GetNodeOrNull<TileMapLayer>(ChunkStreamingConstants.PROCEDURAL_LAYER_NAME)?.Clear();
			}
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void ClearWorldLayersReceive()
		{
			ClearWorldLayers();
		}

		private void SetChunkStreamingEnabled(bool enabled)
		{
			var chunkStreamingManager = GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(StaticNodePathsConstants.ChunkStreamingManager);

			if (chunkStreamingManager != null)
			{
				chunkStreamingManager.Enabled = enabled;
			}
		}

		public string SpawnWorldAndJoin(string textAddress)
		{
			SpawnWorld();

			return JoinServer(textAddress);
		}

		private void SpawnTestNPC()
		{
			if (UpsidedownParent == null || UpsidedownParent.GetNodeOrNull("NPC_Dummy") != null)
			{
				return;
			}

			var npc = GD.Load<PackedScene>("res://Scenes/World/Characters/NPC.tscn").Instantiate<Player>();

			npc.Name = "NPC_Dummy";
			npc.Position = FindGroundSpawnPosition(UpsidedownParent, 200f);

			npc.SetMultiplayerAuthority(1);

			UpsidedownParent.AddChild(npc);
		}

		private Vector2 FindGroundSpawnPosition(Node2D dimensionParent, float worldX, float halfBodyHeight = 15f)
		{
			var layer = dimensionParent?.GetNodeOrNull<TileMapLayer>(ChunkStreamingConstants.PROCEDURAL_LAYER_NAME);

			if (layer?.TileSet == null)
			{
				return new Vector2(worldX, 0f);
			}

			var tileSize = layer.TileSet.TileSize.X;
			var cellX = Mathf.FloorToInt(worldX / tileSize);

			for (int cellY = -8; cellY <= 8; cellY++)
			{
				if (layer.GetCellSourceId(new Vector2I(cellX, cellY)) != -1)
				{
					return new Vector2(worldX, cellY * tileSize - halfBodyHeight);
				}
			}

			return new Vector2(worldX, 0f);
		}

		#endregion

		#region Core - Connection

		public string CreateServer(string textPort)
		{
			var port = NetworkingConstants.DEFAULT_PORT;

			if (!string.IsNullOrEmpty(textPort))
			{
				if (!int.TryParse(textPort, out port))
				{
					port = NetworkingConstants.DEFAULT_PORT;
				}
			}

			GD.Print($"[WorldManager.CreateServer] CreateServer(port={port})");

			Peer = new ENetMultiplayerPeer();

			if (Peer.CreateServer(port, NetworkingConstants.MAX_PLAYER) != Error.Ok)
			{
				GD.Print("[WorldManager.CreateServer] failed to create server");

				return "";
			}

			Multiplayer.MultiplayerPeer = Peer;

			var player = UpsidedownParent?.GetNodeOrNull<Player>("Player");

			if (player == null)
			{
				GD.Print("[WorldManager.CreateServer] local player not found");
			}
			else
			{
				player.PeerId = 1;
				player.Name = $"Player{player.PeerId}";

				player.SetMultiplayerAuthority((int)player.PeerId);
				player.AddToGroup("players");

				GD.Print($"[WorldManager.CreateServer] set authority to {player.PeerId} and renamed to {player.Name}");
			}

			return port.ToString();
		}

		public string LastJoinError { get; private set; } = "";

		public string JoinServer(string textAddress)
		{
			LastJoinError = "";

			var ip = NetworkingConstants.DEFAULT_ADDRESS;
			var port = NetworkingConstants.DEFAULT_PORT;

			if (!string.IsNullOrWhiteSpace(textAddress))
			{
				var parts = textAddress.Split(':');

				if (parts.Length > 1)
				{
					if (string.IsNullOrWhiteSpace(parts[0]) || !int.TryParse(parts[1], out port))
					{
						LastJoinError = "Formato de endereço inválido (esperado IP:Porta ou apenas Porta).";

						return "";
					}

					ip = parts[0];
				}
				else if (!int.TryParse(parts[0], out port))
				{
					LastJoinError = "Formato de endereço inválido (esperado IP:Porta ou apenas Porta).";

					return "";
				}
			}

			GD.Print($"[WorldManager.JoinServer] JoinServer(address={ip}, port={port})");

			Peer = new ENetMultiplayerPeer();

			var createError = Peer.CreateClient(ip, port);

			if (createError != Error.Ok)
			{
				LastJoinError = $"ENetMultiplayerPeer.CreateClient retornou: {createError}";

				GD.Print($"[WorldManager.JoinServer] failed to create client: {createError}");

				return "";
			}

			Multiplayer.MultiplayerPeer = Peer;

			var localPlayer = UpsidedownParent?.GetNodeOrNull<Player>("Player");

			if (localPlayer != null)
			{
				localPlayer.QueueFree();

				GD.Print("[WorldManager.JoinServer] local player queued for free");
			}
			else
			{
				GD.Print("[WorldManager.JoinServer] no local player to remove");
			}

			var localNpc = UpsidedownParent?.GetNodeOrNull("NPC_Dummy");

			if (localNpc != null)
			{
				localNpc.QueueFree();

				GD.Print("[WorldManager.JoinServer] local NPC queued for free");
			}

			return $"{ip}:{port}";
		}

		public void Disconnect()
		{
			GD.Print("[WorldManager.Disconnect] Disconnect()");

			PersistBeforeLeaving();

			if (Peer != null)
			{
				Peer.Close();

				Peer = null;
				Multiplayer.MultiplayerPeer = null;

				GD.Print("[WorldManager.Disconnect] peer closed");
			}

			var players = GetTree().GetNodesInGroup("players");

			foreach (Node player in players)
			{
				player.QueueFree();
			}

			GD.Print($"[WorldManager.Disconnect] freed {players.Count} player nodes");

			CallDeferred(nameof(RespawnLocalSoloPlayer));
		}

		public void LeaveWorld()
		{
			GD.Print("[WorldManager.LeaveWorld] LeaveWorld()");

			PersistBeforeLeaving();

			StopAutosaveTimer();

			CurrentWorldSave = null;
			PendingCharacter = null;

			_peerCharacters.Clear();
			_pendingProfileByPeer.Clear();

			if (Peer != null)
			{
				Peer.Close();

				Peer = null;
				Multiplayer.MultiplayerPeer = null;

				GD.Print("[WorldManager.LeaveWorld] peer closed");
			}

			var main = GetTree().Root.GetNodeOrNull<Node>("Main");
			var world = main?.GetNodeOrNull("World");

			if (world != null)
			{
				world.QueueFree();

				GD.Print("[WorldManager.LeaveWorld] world queued for free");
			}

			OverworldParent = null;
			UpsidedownParent = null;
			OverContainer = null;
			UpContainer = null;

			GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(StaticNodePathsConstants.ChunkStreamingManager)?.ResetState();
		}

		public void ReturnToMainMenu()
		{
			GD.Print("[WorldManager.ReturnToMainMenu] ReturnToMainMenu()");

			GetTree().Paused = false;

			var pauseUi = GetTree().Root.GetNodeOrNull<CanvasLayer>("Main/Ui/PauseUI");

			if (pauseUi != null)
			{
				pauseUi.Visible = false;
			}

			LeaveWorld();

			var startUi = GetTree().Root.GetNodeOrNull<CanvasLayer>("Main/Ui/StartUI");

			if (startUi != null)
			{
				startUi.Visible = true;
			}
		}

		private void RespawnLocalSoloPlayer()
		{
			var localPlayer = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

			localPlayer.Name = "Player";
			localPlayer.PeerId = 1;
			localPlayer.Position = FindGroundSpawnPosition(UpsidedownParent, 0f);

			if (PendingCharacter != null)
			{
				localPlayer.Data = (PlayerData)PendingCharacter.Data.Duplicate(true);
				localPlayer.Loaded = true;
			}
			else
			{
				localPlayer.GiveItem(ItemFactory.CreateInstance("portal"));
			}

			SpawnPlayer(localPlayer);

			SpawnTestNPC();

			GD.Print("[WorldManager.Disconnect] respawned local solo player");
		}

		#endregion

		#region Core - Player lookup

		public Player GetLocalPlayer()
		{
			GD.Print("[WorldManager.GetLocalPlayer] GetLocalPlayer()");

			var localPeerId = 1;

			if (
				Multiplayer != null &&
				Multiplayer.MultiplayerPeer != null &&
				Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected
			)
			{
				localPeerId = Multiplayer.GetUniqueId();
				GD.Print($"[WorldManager.GetLocalPlayer] localPeerId={localPeerId}");
			}

			return FindPlayerByPeerId(localPeerId);
		}

		public Player FindPlayerByPeerId(long peerId)
		{
			var players = GetTree().GetNodesInGroup("players").OfType<Player>();
			var found = players.FirstOrDefault(p => p.PeerId == peerId);

			GD.Print($"[WorldManager.FindPlayerByPeerId] peerId={peerId} found={(found != null)}");

			return found;
		}

		#endregion

		#region Core - Peer events

		public void OnPeerConnected(long id)
		{
			GD.Print($"[WorldManager.OnPeerConnected] OnPeerConnected(id={id})");
		}

		private async void FinishPeerJoin(long id, CharacterSaveData character)
		{
			if (!Multiplayer.IsServer() || character == null)
			{
				return;
			}

			var player = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

			player.Name = $"Player{id}";
			player.Position = Godot.Vector2.Zero;
			player.PeerId = id;
			player.Data = (PlayerData)character.Data.Duplicate(true);
			player.Loaded = true;

			var chunkStreamingManager = GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(StaticNodePathsConstants.ChunkStreamingManager);

			if (chunkStreamingManager != null && chunkStreamingManager.Enabled)
			{
				await chunkStreamingManager.PreloadSpawnAreaAsync(ChunkStreamingConstants.UPSIDEDOWN_ID, UpsidedownParent, player.Position);

				player.Position = FindGroundSpawnPosition(UpsidedownParent, player.Position.X);

				RpcId(id, nameof(ClearWorldLayersReceive));
			}

			chunkStreamingManager?.CatchUpPeer(id);

			SpawnPlayer(player);

			SpawnPlayerRequest(player);

			var players = GetTree().GetNodesInGroup("players");

			foreach (Node node in players)
			{
				if (node is NPC)
				{
					continue;
				}

				if (node is Player existingPlayer && existingPlayer.PeerId != id)
				{
					GD.Print($"[WorldManager.FinishPeerJoin] informing {id} about {existingPlayer.Name}");

					SpawnPlayerRequest(existingPlayer, id);
				}
			}

			var npc = UpsidedownParent?.GetNodeOrNull<Player>("NPC_Dummy");

			if (npc != null)
			{
				GD.Print($"[WorldManager.FinishPeerJoin] informing {id} about NPC_Dummy");

				SpawnNpcRequest(npc.Position, id);
			}

			var worldItems = (OverworldParent?.GetChildren().OfType<WorldItem>() ?? Enumerable.Empty<WorldItem>())
				.Concat(UpsidedownParent?.GetChildren().OfType<WorldItem>() ?? Enumerable.Empty<WorldItem>());

			foreach (var worldItem in worldItems)
			{
				GD.Print($"[WorldManager.FinishPeerJoin] informing {id} about {worldItem.Name}");

				SpawnWorldItemRequest(worldItem, id);
			}
		}

		public void OnPeerDisconnected(long id)
		{
			GD.Print($"[WorldManager.OnPeerDisconnected] OnPeerDisconnected(id={id})");

			var playerNode = FindPlayerByPeerId(id);

			if (playerNode == null)
			{
				GD.Print($"[WorldManager.OnPeerDisconnected] Player{id} not found");
			}

			SavePeerCharacterOnDisconnect(id, playerNode);

			if (playerNode != null)
			{
				playerNode.QueueFree();

				GD.Print($"[WorldManager.OnPeerDisconnected] removed Player{id}");
			}

			_peerCharacters.Remove(id);
			_pendingProfileByPeer.Remove(id);

			GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(StaticNodePathsConstants.ChunkStreamingManager)?.RemovePeer(id);
		}

		private void SavePeerCharacterOnDisconnect(long id, Player playerNode)
		{
			if (playerNode == null || CurrentWorldSave == null || !_peerCharacters.TryGetValue(id, out var character))
			{
				return;
			}

			var saveManager = ResolveSaveManager();

			if (saveManager == null)
			{
				return;
			}

			character.Data = playerNode.Data;
			character.LastPlayedUtc = SaveManager.NowUtc();

			if (CurrentWorldSave.CharacterMode == WorldCharacterMode.ServerCharacters)
			{
				saveManager.SaveServerCharacter(CurrentWorldSave.MultiplayerKey, character);
			}
			else
			{
				saveManager.SaveBackup(character.OwnerProfileId, character);
			}
		}

		public event Action ConnectionSucceeded;
		public event Action ConnectionAttemptFailed;

		public void OnConnectedToServer()
		{
			GD.Print("[WorldManager.OnConnectedToServer] OnConnectedToServer()");

			RpcId(1, nameof(RequestJoinInfoServerReceive));

			ConnectionSucceeded?.Invoke();
		}

		public void OnConnectionFailed()
		{
			GD.Print("[WorldManager.OnConnectionFailed] OnConnectionFailed()");

			Peer = null;
			Multiplayer.MultiplayerPeer = null;

			GD.Print("[WorldManager.OnConnectionFailed] peer reset");

			ConnectionAttemptFailed?.Invoke();
		}

		public void OnServerDisconnected()
		{
			GD.Print("[WorldManager.OnServerDisconnected] OnServerDisconnected()");

			ReturnToMainMenu();
		}

		#endregion

		#region Core - Rpc - Player spawn

		public void SpawnPlayer(Player player)
		{
			GD.Print($"[WorldManager.SpawnPlayer] SpawnPlayer(peerId={player.PeerId}, position={player.Position}, equippedItemId={player.Data.EquippedItemId})");

			player.AddToGroup("players");
			player.SetMultiplayerAuthority(1);

			if (UpsidedownParent != null)
			{
				UpsidedownParent.AddChild(player);

				GD.Print($"[WorldManager.SpawnPlayer] spawned {player.Name}");
			}
			else
			{
				GD.Print($"[WorldManager.SpawnPlayer] WARNING: UpsidedownParent is null, cannot add {player.Name}");
			}
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void SpawnPlayerReceive(long peerId, Vector2 position, Godot.Collections.Dictionary data)
		{
			GD.Print($"[WorldManager.SpawnPlayerReceive] peerId={peerId} position={position}");

			var player = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

			player.Name = $"Player{peerId}";
			player.Position = position;
			player.PeerId = peerId;
			player.Data = GodotDictionaryParser.ToResource<PlayerData>(data);

			SpawnPlayer(player);
		}

		public void SpawnPlayerRequest(Player player)
		{
			var data = GodotDictionaryParser.ToDictionary(player.Data);

			Rpc(nameof(SpawnPlayerReceive), player.PeerId, player.Position, data);
		}

		public void SpawnPlayerRequest(Player player, long targetPeerId)
		{
			var data = GodotDictionaryParser.ToDictionary(player.Data);

			RpcId(targetPeerId, nameof(SpawnPlayerReceive), player.PeerId, player.Position, data);
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void SpawnNpcReceive(Vector2 position)
		{
			GD.Print($"[WorldManager.SpawnNpcReceive] position={position}");

			if (UpsidedownParent == null || UpsidedownParent.GetNodeOrNull("NPC_Dummy") != null)
			{
				return;
			}

			var npc = GD.Load<PackedScene>("res://Scenes/World/Characters/NPC.tscn").Instantiate<Player>();

			npc.Name = "NPC_Dummy";
			npc.Position = position;

			npc.AddToGroup("players");
			npc.SetMultiplayerAuthority(1);

			UpsidedownParent.AddChild(npc);
		}

		public void SpawnNpcRequest(Vector2 position, long targetPeerId)
		{
			RpcId(targetPeerId, nameof(SpawnNpcReceive), position);
		}

		#endregion

		#region Core - Rpc - World items

		private Node2D ResolveDimensionParent(string dimensionId)
		{
			return dimensionId == ChunkStreamingConstants.OVERWORLD_ID ? OverworldParent : UpsidedownParent;
		}

		public void SpawnWorldItem(WorldItem item, string dimensionId)
		{
			ResolveDimensionParent(dimensionId)?.AddChild(item);
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void SpawnWorldItemReceive(long worldItemId, Godot.Collections.Dictionary data, Vector2 position, string dimensionId)
		{
			if (ResolveDimensionParent(dimensionId) == null || FindWorldItem(worldItemId) != null)
			{
				return;
			}

			var worldItem = GD.Load<PackedScene>("res://Scenes/World/Items/WorldItem.tscn").Instantiate<WorldItem>();

			worldItem.Name = $"WorldItem{worldItemId}";
			worldItem.WorldItemId = worldItemId;
			worldItem.Data = GodotDictionaryParser.ToResource<ItemData>(data);
			worldItem.Position = position;

			SpawnWorldItem(worldItem, dimensionId);
		}

		public long SpawnWorldItemRequest(ItemData item, Vector2 position, string dimensionId)
		{
			var worldItemId = InstanceIdGenerator.NextInstanceId();

			var worldItem = GD.Load<PackedScene>("res://Scenes/World/Items/WorldItem.tscn").Instantiate<WorldItem>();

			worldItem.Name = $"WorldItem{worldItemId}";
			worldItem.WorldItemId = worldItemId;
			worldItem.Data = item;
			worldItem.Position = position;

			SpawnWorldItem(worldItem, dimensionId);

			var data = GodotDictionaryParser.ToDictionary(item);

			Rpc(nameof(SpawnWorldItemReceive), worldItemId, data, position, dimensionId);

			return worldItemId;
		}

		public void SpawnWorldItemRequest(WorldItem item, long targetPeerId)
		{
			var data = GodotDictionaryParser.ToDictionary(item.Data);
			var dimensionId = item.GetParent() == OverworldParent ? ChunkStreamingConstants.OVERWORLD_ID : ChunkStreamingConstants.UPSIDEDOWN_ID;

			RpcId(targetPeerId, nameof(SpawnWorldItemReceive), item.WorldItemId, data, item.Position, dimensionId);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void RemoveWorldItemReceive(long worldItemId)
		{
			FindWorldItem(worldItemId)?.QueueFree();
		}

		public void RemoveWorldItemRequest(long worldItemId)
		{
			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer())
			{
				RemoveWorldItemReceive(worldItemId);

				return;
			}

			Rpc(nameof(RemoveWorldItemReceive), worldItemId);
		}

		public WorldItem FindWorldItem(long worldItemId)
		{
			return OverworldParent?.GetNodeOrNull<WorldItem>($"WorldItem{worldItemId}")
				?? UpsidedownParent?.GetNodeOrNull<WorldItem>($"WorldItem{worldItemId}");
		}

		#endregion

		#region Core - Rpc - Blocks

		private TerrainLayer ResolveDimensionLayer(string dimensionId)
		{
			return ResolveDimensionParent(dimensionId)?.GetNodeOrNull<TerrainLayer>(ChunkStreamingConstants.PROCEDURAL_LAYER_NAME);
		}

		private TerrainLayer ResolveDimensionBaseLayer(string dimensionId)
		{
			return ResolveDimensionParent(dimensionId)?.GetNodeOrNull<TerrainLayer>(ChunkStreamingConstants.PROCEDURAL_BASE_LAYER_NAME);
		}

		public void BreakBlockClientRequest(Vector2I cell, string dimensionId)
		{
			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
			{
				BreakBlockReceive(cell, dimensionId);

				return;
			}

			RpcId(1, nameof(BreakBlockServerReceive), cell, dimensionId);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void BreakBlockServerReceive(Vector2I cell, string dimensionId)
		{
			if (!Multiplayer.IsServer())
			{
				return;
			}

			BreakBlockReceive(cell, dimensionId);
		}

		private void BreakBlockReceive(Vector2I cell, string dimensionId)
		{
			var layer = ResolveDimensionLayer(dimensionId);

			if (layer == null)
			{
				return;
			}

			if (layer.GetCellSourceId(cell) == -1)
			{
				var baseLayer = ResolveDimensionBaseLayer(dimensionId);

				if (baseLayer == null || baseLayer.GetCellSourceId(cell) == -1)
				{
					return;
				}

				EraseBlockAndReconnect(baseLayer, cell, dimensionId);
				GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(StaticNodePathsConstants.ChunkStreamingManager)?.RecordMutation(dimensionId, cell, "break", "");
				Rpc(nameof(BreakBlockBroadcast), cell, dimensionId);
				return;
			}

			EraseBlockAndReconnect(layer, cell, dimensionId);

			GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(StaticNodePathsConstants.ChunkStreamingManager)?.RecordMutation(dimensionId, cell, "break", "");

			var dropPosition = layer.ToGlobal(layer.MapToLocal(cell));

			if (BlockDB.TryGet("grass", out var grassBlock))
			{
				SpawnWorldItemRequest(ItemFactory.CreateInstance(grassBlock.DropItemId), dropPosition, dimensionId);
			}

			Rpc(nameof(BreakBlockBroadcast), cell, dimensionId);
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void BreakBlockBroadcast(Vector2I cell, string dimensionId)
		{
			var layer = ResolveDimensionLayer(dimensionId);

			if (layer == null)
			{
				return;
			}

			if (layer.GetCellSourceId(cell) == -1)
			{
				var baseLayer = ResolveDimensionBaseLayer(dimensionId);

				if (baseLayer == null || baseLayer.GetCellSourceId(cell) == -1)
				{
					return;
				}

				EraseBlockAndReconnect(baseLayer, cell, dimensionId);

				return;
			}

			EraseBlockAndReconnect(layer, cell, dimensionId);
		}

		private static void BreakDecorationOnly(TerrainLayer decorationLayer, Vector2I cell)
		{
			if (decorationLayer == null || decorationLayer.GetCellSourceId(cell) == -1)
			{
				return;
			}

			EraseCellWithTerrainConnect(decorationLayer, cell);
			ReconnectDecorationsNear(decorationLayer, cell);
			decorationLayer.RedrawDebugOverlay();
		}

		public bool PlaceBlockAuthoritative(Vector2I cell, string blockId, string dimensionId)
		{
			var layer = ResolveDimensionLayer(dimensionId);
			var baseLayer = ResolveDimensionBaseLayer(dimensionId);

			if (layer == null || !BlockDB.TryGet(blockId, out var block))
			{
				return false;
			}

			if (layer.GetCellSourceId(cell) != -1 || (baseLayer != null && baseLayer.GetCellSourceId(cell) != -1))
			{
				return false;
			}

			if (!PlaceBlockOnCorrectLayer(layer, baseLayer, cell, block, dimensionId))
			{
				return false;
			}

			GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(StaticNodePathsConstants.ChunkStreamingManager)?.RecordMutation(dimensionId, cell, "place", blockId);

			Rpc(nameof(PlaceBlockBroadcast), cell, blockId, dimensionId);

			return true;
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void PlaceBlockBroadcast(Vector2I cell, string blockId, string dimensionId)
		{
			var layer = ResolveDimensionLayer(dimensionId);
			var baseLayer = ResolveDimensionBaseLayer(dimensionId);

			if (layer == null || !BlockDB.TryGet(blockId, out var block))
			{
				return;
			}

			PlaceBlockOnCorrectLayer(layer, baseLayer, cell, block, dimensionId);
		}

		private bool PlaceBlockOnCorrectLayer(TerrainLayer layer, TerrainLayer baseLayer, Vector2I cell, BlockDefinition block, string dimensionId)
		{
			_ = baseLayer;

			PaintBlockAndReconnect(layer, cell, block, dimensionId);

			return true;
		}

		public long NextPortalId { get; set; }

		public bool PlacePortalAuthoritative(Vector2 position, string dimensionId)
		{
			var layer = ResolveDimensionLayer(dimensionId);
			var parent = ResolveDimensionParent(dimensionId);

			if (layer == null || parent == null)
			{
				return false;
			}

			var cell = layer.LocalToMap(layer.ToLocal(position));

			if (layer.GetCellSourceId(cell) != -1 || layer.GetCellSourceId(cell + Vector2I.Down) == -1)
			{
				return false;
			}

			SpawnPortal(parent, position, ++NextPortalId);

			Rpc(nameof(PlacePortalBroadcast), position, dimensionId, NextPortalId);

			return true;
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void PlacePortalBroadcast(Vector2 position, string dimensionId, long portalId)
		{
			var parent = ResolveDimensionParent(dimensionId);

			if (parent == null)
			{
				return;
			}

			SpawnPortal(parent, position, portalId);
		}

		private void SpawnPortal(Node2D parent, Vector2 position, long portalId)
		{
			var portal = PropDB.Get("portal")?.Spawn(parent, position);

			if (portal == null)
			{
				return;
			}

			portal.Name = $"Portal{portalId}";
		}

		private void RestorePortals(WorldSaveData save)
		{
			if (save?.Portals == null)
			{
				return;
			}

			foreach (var portalSave in save.Portals)
			{
				var parent = ResolveDimensionParent(portalSave.DimensionId);

				if (parent == null)
				{
					continue;
				}

				SpawnPortal(parent, new Vector2(portalSave.PositionX, portalSave.PositionY), ++NextPortalId);
			}
		}

		private Godot.Collections.Array<PortalSaveData> CollectPortals()
		{
			var result = new Godot.Collections.Array<PortalSaveData>();

			void CollectFrom(Node2D parent, string dimensionId)
			{
				if (parent == null)
				{
					return;
				}

				foreach (var portal in parent.GetChildren().OfType<Portal>())
				{
					result.Add(new PortalSaveData
					{
						PositionX = portal.Position.X,
						PositionY = portal.Position.Y,
						DimensionId = dimensionId,
					});
				}
			}

			CollectFrom(OverworldParent, ChunkStreamingConstants.OVERWORLD_ID);
			CollectFrom(UpsidedownParent, ChunkStreamingConstants.UPSIDEDOWN_ID);

			return result;
		}

		public void BreakPortalClientRequest(string portalName, string dimensionId)
		{
			if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
			{
				BreakPortalReceive(portalName, dimensionId);

				return;
			}

			RpcId(1, nameof(BreakPortalServerReceive), portalName, dimensionId);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void BreakPortalServerReceive(string portalName, string dimensionId)
		{
			if (!Multiplayer.IsServer())
			{
				return;
			}

			BreakPortalReceive(portalName, dimensionId);
		}

		private void BreakPortalReceive(string portalName, string dimensionId)
		{
			var parent = ResolveDimensionParent(dimensionId);
			var portal = parent?.GetNodeOrNull<Portal>(portalName);

			if (portal == null)
			{
				return;
			}

			var dropPosition = portal.GlobalPosition;

			portal.QueueFree();

			SpawnWorldItemRequest(ItemFactory.CreateInstance("portal"), dropPosition, dimensionId);

			Rpc(nameof(BreakPortalBroadcast), portalName, dimensionId);
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void BreakPortalBroadcast(string portalName, string dimensionId)
		{
			var parent = ResolveDimensionParent(dimensionId);
			var portal = parent?.GetNodeOrNull<Portal>(portalName);

			portal?.QueueFree();
		}

		public void ApplyChunkMutation(TerrainLayer layer, ChunkMutationData mutation, string dimensionId)
		{
			var cell = new Vector2I((int)mutation.Position.X, (int)mutation.Position.Y);

			if (mutation.Type == "break")
			{
				if (layer.GetCellSourceId(cell) == -1)
				{
					BreakDecorationOnly(ResolveDimensionBaseLayer(dimensionId), cell);

					return;
				}

				EraseBlockAndReconnect(layer, cell, dimensionId);

				return;
			}

			if (mutation.Type == "place" && BlockDB.TryGet(mutation.ExtraData, out var block))
			{
				PlaceBlockOnCorrectLayer(layer, ResolveDimensionBaseLayer(dimensionId), cell, block, dimensionId);
			}
		}

		private void EraseBlockAndReconnect(TerrainLayer layer, Vector2I cell, string dimensionId)
		{
			if (layer.TileSet == null || layer.TileSet.GetTerrainSetsCount() <= 0)
			{
				layer.SetCell(cell, -1);

				return;
			}

			var erasedTerrainSet = layer.GetCellTileData(cell)?.TerrainSet ?? 0;

			layer.SetCellsTerrainConnect(new Godot.Collections.Array<Vector2I> { cell }, erasedTerrainSet, -1, false);

			var neighbors = GetSolidNeighborCells(layer, cell);

			var biomeGroups = new Dictionary<int, List<Vector2I>>();

			foreach (var neighbor in neighbors)
			{
				var tileData = layer.GetCellTileData(neighbor);

				if (tileData == null)
				{
					continue;
				}

				if (!biomeGroups.TryGetValue(tileData.TerrainSet, out var group))
				{
					group = new List<Vector2I>();
					biomeGroups[tileData.TerrainSet] = group;
				}

				group.Add(neighbor);
			}

			var touchedCells = new HashSet<Vector2I>(neighbors);

			foreach (var group in biomeGroups)
			{
				layer.Connect(group.Value, group.Key);
				layer.ReconnectForeignBorder(group.Value, group.Key);

				foreach (var foreignCell in layer.GetForeignNeighborCells(group.Value, group.Key))
				{
					touchedCells.Add(foreignCell);
				}
			}

			var expandedNeighbors = GetExpandedNeighborCells(layer, new Godot.Collections.Array<Vector2I>(touchedCells));

			if (expandedNeighbors.Count > 0)
			{
				layer.ReconnectExistingCells(expandedNeighbors);
			}

			var baseLayer = ResolveDimensionBaseLayer(dimensionId);

			EraseCellWithTerrainConnect(baseLayer, cell);
			baseLayer?.RedrawDebugOverlay();

			RepaintDependentLayerForCells(layer, baseLayer, expandedNeighbors, def => def.BorderCapTerrainSet);

			ReconnectDecorationsNear(layer, cell);
			ReconnectDecorationsNear(baseLayer, cell);
		}

		private static void ReconnectDecorationsNear(TerrainLayer layer, Vector2I originCell, int radius = 4)
		{
			if (layer == null)
			{
				return;
			}

			var nearbyCells = new List<Vector2I>();

			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					var cell = originCell + new Vector2I(dx, dy);

					if (layer.GetCellSourceId(cell) != -1)
					{
						nearbyCells.Add(cell);
					}
				}
			}

			if (nearbyCells.Count > 0)
			{
				layer.ReconnectExistingCells(nearbyCells);
			}
		}

		private static void EraseCellWithTerrainConnect(TerrainLayer layer, Vector2I cell)
		{
			if (layer == null || layer.GetCellSourceId(cell) == -1)
			{

				return;
			}

			var terrainSet = layer.GetCellTileData(cell).TerrainSet;

			layer.SetCellsTerrainConnect(new Godot.Collections.Array<Vector2I> { cell }, terrainSet, -1, false);
		}

		private void PaintBlockAndReconnect(TerrainLayer layer, Vector2I cell, BlockDefinition block, string dimensionId)
		{
			if (layer.TileSet == null || layer.TileSet.GetTerrainSetsCount() <= 0)
			{
				layer.SetCell(cell, block.SourceId, block.AtlasCoord);

				return;
			}

			var neighbors = GetSolidNeighborCells(layer, cell);
			var biomeDef = ResolveBiomeForCell(cell, dimensionId);
			var sameBiomeCells = new List<Vector2I> { cell };

			foreach (var neighbor in neighbors)
			{
				var tileData = layer.GetCellTileData(neighbor);

				if (tileData != null && tileData.TerrainSet == biomeDef.TerrainSet)
				{
					sameBiomeCells.Add(neighbor);
				}
			}

			layer.Connect(sameBiomeCells, biomeDef.TerrainSet);
			layer.ReconnectForeignBorder(sameBiomeCells, biomeDef.TerrainSet);

			var foreignCells = layer.GetForeignNeighborCells(sameBiomeCells, biomeDef.TerrainSet);
			var expandedForeignCells = GetExpandedNeighborCells(layer, foreignCells);

			if (expandedForeignCells.Count > 0)
			{
				layer.ReconnectExistingCells(expandedForeignCells);
			}

			var baseLayer = ResolveDimensionBaseLayer(dimensionId);

			if (baseLayer != null)
			{
				baseLayer.ConnectDependent(layer, sameBiomeCells, biomeDef.BorderCapTerrainSet);
				RepaintDependentLayerForCells(layer, baseLayer, expandedForeignCells, def => def.BorderCapTerrainSet);
			}

			ReconnectDecorationsNear(layer, cell);
			ReconnectDecorationsNear(baseLayer, cell);
		}

		private void RepaintDependentLayerForCells(TileMapLayer layer, TerrainLayer dependentLayer, IEnumerable<Vector2I> cells, Func<BiomeDefinition, int> terrainSetSelector)
		{
			if (dependentLayer == null)
			{
				return;
			}

			var groups = new Dictionary<int, List<Vector2I>>();

			foreach (var cell in cells)
			{
				if (layer.GetCellSourceId(cell) == -1)
				{
					EraseCellWithTerrainConnect(dependentLayer, cell);

					continue;
				}

				var tileData = layer.GetCellTileData(cell);

				if (tileData == null)
				{
					continue;
				}

				if (!groups.TryGetValue(tileData.TerrainSet, out var group))
				{
					group = new List<Vector2I>();
					groups[tileData.TerrainSet] = group;
				}

				group.Add(cell);
			}

			foreach (var group in groups)
			{
				var biomeDef = BiomeDB.GetByTerrainSet(group.Key);

				if (biomeDef == null)
				{
					continue;
				}

				dependentLayer.ConnectDependent(layer, group.Value, terrainSetSelector(biomeDef));
			}

			dependentLayer.RedrawDebugOverlay();
		}

		private Godot.Collections.Array<Vector2I> GetExpandedNeighborCells(TileMapLayer layer, IEnumerable<Vector2I> seedCells)
		{
			var seen = new HashSet<Vector2I>();
			var result = new Godot.Collections.Array<Vector2I>();

			foreach (var cell in seedCells)
			{
				if (seen.Add(cell))
				{
					result.Add(cell);
				}
			}

			foreach (var cell in result.ToArray())
			{
				foreach (var neighbor in GetSolidNeighborCells(layer, cell))
				{
					if (seen.Add(neighbor))
					{
						result.Add(neighbor);
					}
				}
			}

			return result;
		}

		private Godot.Collections.Array<Vector2I> GetSolidNeighborCells(TileMapLayer layer, Vector2I cell, int radius = 1)
		{
			var result = new Godot.Collections.Array<Vector2I>();

			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					if (dx == 0 && dy == 0)
					{
						continue;
					}

					var neighbor = cell + new Vector2I(dx, dy);

					if (layer.GetCellSourceId(neighbor) != -1)
					{
						result.Add(neighbor);
					}
				}
			}

			return result;
		}

		private BiomeDefinition ResolveBiomeForCell(Vector2I cell, string dimensionId)
		{
			var chunkStreamingManager = GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(StaticNodePathsConstants.ChunkStreamingManager);

			return chunkStreamingManager?.ResolveBiome(dimensionId, cell.X, cell.Y) ?? BiomeDB.Get(BiomeDB.LimeGroundId);
		}

		#endregion

		#region Core - Rpc - Player state

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void TeleportPlayer(long peerId, Vector2 position)
		{
			var player = GetTree().GetNodesInGroup("players").OfType<Player>().FirstOrDefault(e => e.PeerId == peerId);

			if (player == null)
			{
				GD.Print("[WorldManager.TeleportPlayer] player is null");

				return;
			}

			if (player.GetParent<Node2D>() != UpsidedownParent)
			{
				player.Reparent(UpsidedownParent, true);

				var equippedItemId = player.Data.EquippedItemId;

				if (equippedItemId > 0)
				{
					player.EquipItemRequest(equippedItemId);
				}
			}

			player.GlobalPosition = position;
			player.Velocity = Vector2.Zero;
			player.Data.CurrentHealth = player.GetMaxHealth();
			player.Sprite?.Play("idle");
			player.Input?.RemoveBlocker("dead");

			if (peerId == Multiplayer.GetUniqueId())
			{
				OverContainer.Visible = false;
				UpContainer.Visible = true;
			}
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void TeleportPlayerServerReceive(Vector2 position)
		{
			GD.Print($"[WorldManager.TeleportPlayerServerReceive] received teleport request to {position}");

			if (!Multiplayer.IsServer())
			{
				GD.Print("[WorldManager.TeleportPlayerServerReceive] not the server, ignoring request");

				return;
			}

			long senderId = Multiplayer.GetRemoteSenderId();

			GD.Print($"[WorldManager.TeleportPlayerServerReceive] SenderId={senderId}, sending TeleportPlayer RPC");

			Rpc(nameof(TeleportPlayer), senderId, position);
		}

		public async void TeleportPlayerClientRequest(Vector2 position)
		{
			GD.Print($"[WorldManager.TeleportPlayerClientRequest] sending teleport request to {position} (Peer 1)");

			var loadingUi = GetTree().Root.GetNodeOrNull<LoadingUI>("Main/Ui/LoadingUI");

			loadingUi?.Open();

			var chunkStreamingManager = GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(StaticNodePathsConstants.ChunkStreamingManager);

			if (chunkStreamingManager != null)
			{
				await chunkStreamingManager.PreloadSpawnAreaAsync(ChunkStreamingConstants.UPSIDEDOWN_ID, UpsidedownParent, position);
			}

			RpcId(1, nameof(TeleportPlayerServerReceive), position);

			loadingUi?.Close();
		}

		#endregion

		#region Core - Rpc - Dimension trade

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void TradeDimension(long targetPeerId)
		{
			GD.Print($"[WorldManager.TradeDimension] targetPeerId={targetPeerId}");

			var playerNode = GetTree().GetNodesInGroup("players").OfType<Player>().FirstOrDefault(p => p.PeerId == targetPeerId);

			if (playerNode == null) return;

			Node2D currentParent = playerNode.GetParent<Node2D>();
			Node2D nextParent;

			if (currentParent == OverworldParent)
			{
				nextParent = UpsidedownParent;
			}
			else
			{
				nextParent = OverworldParent;
			}

			GD.Print($"[Dimension] Indo para: {nextParent.Name}");

			playerNode.Reparent(nextParent, true);

			GD.Print($"[Dimension] Chegou em: {nextParent.Name}");

			playerNode.LastDimensionTradeMsec = Time.GetTicksMsec();

			var equippedItemId = playerNode.Data.EquippedItemId;

			if (equippedItemId > 0)
			{
				playerNode.EquipItemRequest(equippedItemId);
			}

			if (targetPeerId == Multiplayer.GetUniqueId())
			{
				OverContainer.Visible = (nextParent == OverworldParent);
				UpContainer.Visible = (nextParent == UpsidedownParent);
			}
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void TradeDimensionServerReceive()
		{
			GD.Print("[WorldManager.ServerReceiveTradeRequest] received trade request");

			if (!Multiplayer.IsServer())
			{
				GD.Print("[WorldManager.ServerReceiveTradeRequest] not the server, ignoring request");

				return;
			}

			long senderId = Multiplayer.GetRemoteSenderId();

			GD.Print($"[WorldManager.ServerReceiveTradeRequest] SenderId={senderId}, sending SyncDimensionTrade RPC");

			Rpc(nameof(TradeDimension), senderId);
		}

		public async void TradeDimensionClientRequest()
		{
			GD.Print("[WorldManager.RequestLocalPlayerTradeDimension] sending trade request to server (Peer 1)");

			var localPlayer = GetLocalPlayer();

			if (localPlayer == null)
			{
				RpcId(1, nameof(TradeDimensionServerReceive));

				return;
			}

			var currentParent = localPlayer.GetParent<Node2D>();
			var targetDimensionId = currentParent == OverworldParent ? ChunkStreamingConstants.UPSIDEDOWN_ID : ChunkStreamingConstants.OVERWORLD_ID;
			var targetParent = currentParent == OverworldParent ? UpsidedownParent : OverworldParent;

			var loadingUi = GetTree().Root.GetNodeOrNull<LoadingUI>("Main/Ui/LoadingUI");

			loadingUi?.Open();

			var chunkStreamingManager = GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(StaticNodePathsConstants.ChunkStreamingManager);

			if (chunkStreamingManager != null)
			{
				await chunkStreamingManager.PreloadSpawnAreaAsync(targetDimensionId, targetParent, localPlayer.Position);
			}

			RpcId(1, nameof(TradeDimensionServerReceive));

			loadingUi?.Close();
		}

		#endregion

		#region Core - Rpc - Entrada com personagem (save)

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void RequestJoinInfoServerReceive()
		{
			if (!Multiplayer.IsServer() || CurrentWorldSave == null)
			{
				return;
			}

			var senderId = Multiplayer.GetRemoteSenderId();

			RpcId(senderId, nameof(JoinInfoReceive), (int)CurrentWorldSave.CharacterMode);
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void JoinInfoReceive(int modeInt)
		{
			var mode = (WorldCharacterMode)modeInt;

			if (mode == WorldCharacterMode.LocalCharacters)
			{
				GetTree().Root.GetNodeOrNull<CharacterSelectUI>("Main/Ui/CharacterSelectUI")?.OpenForPeerJoin();

				return;
			}

			var profile = ResolveSaveManager()?.GetOrCreateLocalProfile();

			RpcId(1, nameof(RequestServerCharacterListServerReceive), profile?.ProfileId ?? "");
		}

		public void SubmitLocalCharacterForJoin(CharacterSaveData character)
		{
			if (character == null)
			{
				return;
			}

			PendingCharacter = character;

			var profile = ResolveSaveManager()?.GetOrCreateLocalProfile();

			RpcId(1, nameof(SubmitLocalCharacterServerReceive), profile?.ProfileId ?? "", GodotDictionaryParser.ToDictionary(character));
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void SubmitLocalCharacterServerReceive(string profileId, Godot.Collections.Dictionary characterDict)
		{
			if (!Multiplayer.IsServer() || CurrentWorldSave == null || CurrentWorldSave.CharacterMode != WorldCharacterMode.LocalCharacters)
			{
				return;
			}

			var senderId = Multiplayer.GetRemoteSenderId();
			var character = GodotDictionaryParser.ToResource<CharacterSaveData>(characterDict);

			if (character == null)
			{
				character = ResolveSaveManager()?.CreateLocalCharacter("Sem nome");
			}

			if (character == null)
			{
				return;
			}

			if (string.IsNullOrEmpty(character.OwnerProfileId))
			{
				character.OwnerProfileId = profileId;
			}

			_peerCharacters[senderId] = character;

			FinishPeerJoin(senderId, character);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void RequestServerCharacterListServerReceive(string profileId)
		{
			if (!Multiplayer.IsServer() || CurrentWorldSave == null || CurrentWorldSave.CharacterMode != WorldCharacterMode.ServerCharacters)
			{
				return;
			}

			var senderId = Multiplayer.GetRemoteSenderId();

			_pendingProfileByPeer[senderId] = profileId;

			SendServerCharacterListTo(senderId);
		}

		private void SendServerCharacterListTo(long senderId)
		{
			if (!_pendingProfileByPeer.TryGetValue(senderId, out var profileId))
			{
				return;
			}

			var saveManager = ResolveSaveManager();
			var characters = saveManager?.ListServerCharacters(CurrentWorldSave.MultiplayerKey)
				.Where(c => c.OwnerProfileId == profileId)
				.ToList() ?? new List<CharacterSaveData>();

			var summaries = new Godot.Collections.Array();

			foreach (var character in characters)
			{
				summaries.Add(new Godot.Collections.Dictionary
				{
					["CharacterId"] = character.CharacterId,
					["Name"] = character.Name,
				});
			}

			RpcId(senderId, nameof(ServerCharacterListReceive), summaries);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void DeleteServerCharacterServerReceive(string characterId)
		{
			if (!Multiplayer.IsServer() || CurrentWorldSave == null || CurrentWorldSave.CharacterMode != WorldCharacterMode.ServerCharacters)
			{
				return;
			}

			var senderId = Multiplayer.GetRemoteSenderId();

			ResolveSaveManager()?.DeleteServerCharacter(CurrentWorldSave.MultiplayerKey, characterId);

			SendServerCharacterListTo(senderId);
		}

		public void DeleteServerCharacterRequest(string characterId)
		{
			RpcId(1, nameof(DeleteServerCharacterServerReceive), characterId);
		}

		[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void ServerCharacterListReceive(Godot.Collections.Array summaries)
		{
			ServerCharacterListAvailable?.Invoke(CurrentWorldSave?.MultiplayerKey ?? "", summaries);
		}

		public void SelectServerCharacterRequest(string characterId)
		{
			RpcId(1, nameof(SelectServerCharacterServerReceive), characterId);
		}

		public void CreateServerCharacterRequest(string name)
		{
			RpcId(1, nameof(CreateServerCharacterServerReceive), name);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void SelectServerCharacterServerReceive(string characterId)
		{
			if (!Multiplayer.IsServer() || CurrentWorldSave == null || CurrentWorldSave.CharacterMode != WorldCharacterMode.ServerCharacters)
			{
				return;
			}

			var senderId = Multiplayer.GetRemoteSenderId();
			var character = ResolveSaveManager()?.LoadServerCharacter(CurrentWorldSave.MultiplayerKey, characterId);

			if (character == null)
			{
				return;
			}

			_peerCharacters[senderId] = character;

			FinishPeerJoin(senderId, character);
		}

		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		public void CreateServerCharacterServerReceive(string name)
		{
			if (!Multiplayer.IsServer() || CurrentWorldSave == null || CurrentWorldSave.CharacterMode != WorldCharacterMode.ServerCharacters)
			{
				return;
			}

			var senderId = Multiplayer.GetRemoteSenderId();
			var saveManager = ResolveSaveManager();
			var ownerProfileId = _pendingProfileByPeer.TryGetValue(senderId, out var profileId) ? profileId : "";
			var character = saveManager?.CreateServerCharacter(CurrentWorldSave.MultiplayerKey, name, ownerProfileId);

			if (character == null)
			{
				return;
			}

			_peerCharacters[senderId] = character;

			FinishPeerJoin(senderId, character);
		}

		#endregion

		#region Save - Core

		private SaveManager ResolveSaveManager()
		{
			return GetTree().Root.GetNodeOrNull<SaveManager>(StaticNodePathsConstants.SaveManager);
		}

		private bool IsHostOrSolo()
		{
			return Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();
		}

		private void StartAutosaveTimer(WorldSaveData save)
		{
			StopAutosaveTimer();

			if (save == null || !IsHostOrSolo())
			{
				return;
			}

			AutosaveTimer = new Timer
			{
				WaitTime = Mathf.Max(1, save.AutosaveIntervalMinutes) * 60.0,
				Autostart = true,
			};

			AutosaveTimer.Timeout += SaveCurrentWorld;

			AddChild(AutosaveTimer);
		}

		private void StopAutosaveTimer()
		{
			if (AutosaveTimer == null)
			{
				return;
			}

			AutosaveTimer.QueueFree();
			AutosaveTimer = null;
		}

		public void SaveCurrentWorld()
		{
			if (CurrentWorldSave == null)
			{
				return;
			}

			var saveManager = ResolveSaveManager();

			if (saveManager == null)
			{
				return;
			}

			var chunkStreamingManager = GetTree().Root.GetNodeOrNull<ChunkStreamingManager>(StaticNodePathsConstants.ChunkStreamingManager);

			if (chunkStreamingManager != null)
			{
				saveManager.SaveDimensionState(CurrentWorldSave.WorldId, ChunkStreamingConstants.OVERWORLD_ID, chunkStreamingManager.ExportState(ChunkStreamingConstants.OVERWORLD_ID));
				saveManager.SaveDimensionState(CurrentWorldSave.WorldId, ChunkStreamingConstants.UPSIDEDOWN_ID, chunkStreamingManager.ExportState(ChunkStreamingConstants.UPSIDEDOWN_ID));
			}

			CurrentWorldSave.Portals = CollectPortals();
			CurrentWorldSave.LastPlayedUtc = SaveManager.NowUtc();

			saveManager.SaveWorldMeta(CurrentWorldSave);

			SaveOwnLocalCharacter();
			SaveRemotePeerCharacters(saveManager);
		}

		private void SaveOwnLocalCharacter()
		{
			if (PendingCharacter == null)
			{
				return;
			}

			var saveManager = ResolveSaveManager();
			var localPlayer = GetLocalPlayer();

			if (saveManager == null || localPlayer == null)
			{
				return;
			}

			PendingCharacter.Data = localPlayer.Data;
			PendingCharacter.LastPlayedUtc = SaveManager.NowUtc();

			saveManager.SaveLocalCharacter(PendingCharacter);
		}

		private void SaveRemotePeerCharacters(SaveManager saveManager)
		{
			if (!IsHostOrSolo())
			{
				return;
			}

			foreach (var player in GetTree().GetNodesInGroup("players").OfType<Player>())
			{
				if (player.PeerId <= 1 || !_peerCharacters.TryGetValue(player.PeerId, out var character))
				{
					continue;
				}

				character.Data = player.Data;
				character.LastPlayedUtc = SaveManager.NowUtc();

				if (CurrentWorldSave.CharacterMode == WorldCharacterMode.ServerCharacters)
				{
					saveManager.SaveServerCharacter(CurrentWorldSave.MultiplayerKey, character);
				}
				else
				{
					saveManager.SaveBackup(character.OwnerProfileId, character);
				}
			}
		}

		private void PersistBeforeLeaving()
		{
			SaveOwnLocalCharacter();

			if (CurrentWorldSave != null && IsHostOrSolo())
			{
				SaveCurrentWorld();
			}
		}

		#endregion

		#region Utils

		public bool IsConnected()
		{
			var connected = Peer != null && Peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;

			return connected;
		}

		public bool IsServer()
		{
			var isServer = Multiplayer.IsServer();

			return isServer;
		}

		#endregion

	}
}
