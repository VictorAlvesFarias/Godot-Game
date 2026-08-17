using Godot;
using Jogo25D.Biomes;
using Jogo25D.Blocks;
using Jogo25D.Characters;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Dimensions;
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
		#region Events

		#endregion

		#region Dinamic properties

		public WorldSaveData CurrentWorldSave { get; private set; }


		#endregion

		#region Node references


		#endregion

		#region Managers

		private static DimensionManager Dimensions => Game.Managers.DimensionManager.Node;

		#endregion

		#region Godot implementation

		public override void _Ready()
		{
			GD.Print("[WorldManager._Ready] _Ready()");

			// O registro so fecha no Bootstrap, entao a assinatura espera o jogo ficar pronto.
			Game.WhenReady(() => GetTree().Root.CloseRequested += Game.Managers.SaveManager.Node.PersistBeforeLeaving);
		}

		#endregion

		#region Core - World spawning

		public void SpawnWorld()
		{
			if (Dimensions.IsResolved)
			{
				return;
			}
			
			var main = Game.Main.Node;

			if (main == null || main.HasNode("World"))
			{
				Dimensions.ResolveReferences();

				return;
			}

			var world = GD.Load<PackedScene>("res://Scenes/World/World.tscn").Instantiate<Node2D>();

			main.AddChild(world);

			Dimensions.ResolveReferences();

			GD.Print("[WorldManager.SpawnWorld] world instantiated");
		}

		// Mundo nao procedural: o terreno e o que esta desenhado a mao nas cenas de nivel, entao
		// nao ha seed, nem import de mutacao, nem streaming - e as layers NAO sao limpas.
		// De resto e um mundo como outro qualquer: tem save, props e autosave.
		public void SpawnLocalWorldAndPlayer(WorldSaveData save)
		{
			CurrentWorldSave = save;

			SpawnWorld();
			SetChunkStreamingEnabled(false);

			Dimensions.RestoreProps(save);
			RespawnLocalSoloPlayer();

			Game.Managers.SaveManager.Node.StartAutosave(save);

			Game.Managers.RouterManager.Node.Open(Game.Ui.HudUI.Node);
		}

		public async void CreateProceduralWorldAndPlayer(WorldSaveData save)
		{
			if(save == null)
			{
				save = Game.Managers.SaveManager.Node.CreateWorld("Mundo sem nome", (long)GD.Randi(), WorldCharacterMode.LocalCharacters, "", SavesConstants.DEFAULT_AUTOSAVE_INTERVAL_MINUTES);
			}	

			CurrentWorldSave = save;

			SpawnWorld();
			Dimensions.ClearLayers();

            Game.Managers.ChunkStreamingManager.Node.SetWorldSeed(save.Seed);
            Game.Managers.ChunkStreamingManager.Node.ImportState(ChunkStreamingConstants.OVERWORLD_ID, Game.Managers.SaveManager.Node.LoadDimensionState(save.WorldId, ChunkStreamingConstants.OVERWORLD_ID));
            Game.Managers.ChunkStreamingManager.Node.ImportState(ChunkStreamingConstants.UPSIDEDOWN_ID, Game.Managers.SaveManager.Node.LoadDimensionState(save.WorldId, ChunkStreamingConstants.UPSIDEDOWN_ID));

			SetChunkStreamingEnabled(true);

			var loadingUi = Game.Ui.LoadingUI.Node;

			loadingUi?.Open();

			await Game.Managers.ChunkStreamingManager.Node.PreloadSpawnAreaAsync(ChunkStreamingConstants.UPSIDEDOWN_ID, Dimensions.ResolveParent(ChunkStreamingConstants.UPSIDEDOWN_ID), Vector2.Zero);
		
			Dimensions.RestoreProps(save);
			RespawnLocalSoloPlayer();
			Game.Managers.SaveManager.Node.StartAutosave(save);

			loadingUi?.Close();

			Game.Managers.RouterManager.Node.Open(Game.Ui.HudUI.Node);
		}

		private void SetChunkStreamingEnabled(bool enabled)
		{
			var chunkStreamingManager = Game.Managers.ChunkStreamingManager.Node;

			if (chunkStreamingManager != null)
			{
				chunkStreamingManager.Enabled = enabled;
			}
		}

		public void LeaveWorld()
		{
			GD.Print("[WorldManager.LeaveWorld] LeaveWorld()");

			Game.Managers.SaveManager.Node.PersistBeforeLeaving();

			Game.Managers.SaveManager.Node.StopAutosave();

			CurrentWorldSave = null;
			Game.Managers.SessionManager.Node.PendingCharacter = null;

			Game.Managers.NetworkManager.Node.CloseSession();

			var main = Game.Main.Node;
			var world = main?.GetNodeOrNull("World");

			if (world != null)
			{
				world.QueueFree();

				GD.Print("[WorldManager.LeaveWorld] world queued for free");
			}

			Dimensions.Reset();

			Game.Managers.ChunkStreamingManager.Node?.ResetState();

			Game.Managers.RouterManager.Node.Close(Game.Ui.HudUI.Node);
		}

		public void RespawnLocalSoloPlayer()
		{
			var localPlayer = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

			localPlayer.Name = "Player";
			localPlayer.PeerId = 1;
			localPlayer.Position = Dimensions.FindGroundSpawnPosition(ChunkStreamingConstants.UPSIDEDOWN_ID, 0f);

			if (Game.Managers.SessionManager.Node.PendingCharacter != null)
			{
				localPlayer.Data = (PlayerData)Game.Managers.SessionManager.Node.PendingCharacter.Data.Duplicate(true);
				localPlayer.Loaded = true;
			}
			else
			{
				localPlayer.GiveItem(ItemFactory.CreateInstance("portal"));
			}

			Dimensions.SpawnPlayer(localPlayer);

			Dimensions.SpawnTestNPC();

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




	}
}
