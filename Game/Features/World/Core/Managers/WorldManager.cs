using Godot;
using Jogo25D.Biomes;
using Jogo25D.Blocks;
using Jogo25D.Characters;
using Jogo25D.Chunks;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Features.Managers.Save.Resources;
using Jogo25D.Features.Managers.Save.Types;
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
		}

		#endregion

		// O streaming de entidade vive na raiz do World, nao em Managers: ele nasce e morre
		// com o mundo, entao nao precisa de reset nem de aviso de teardown.
		public WorldStreaming Streaming => Game.Main.Node?.GetNodeOrNull<WorldStreaming>("World");

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
		public void SpawnLocalWorldAndPlayer(WorldSaveData save, CharacterSaveData character)
		{
			SpawnWorld();
			SetChunkStreamingEnabled(false);

			// Sem streaming, as entidades vem todas de uma vez - mesmo registro, outra hora.
			ImportDimension(save, ChunkStreamingConstants.OVERWORLD_ID);
			ImportDimension(save, ChunkStreamingConstants.UPSIDEDOWN_ID);

			Streaming?.MaterializeAll(ChunkStreamingConstants.OVERWORLD_ID);
			Streaming?.MaterializeAll(ChunkStreamingConstants.UPSIDEDOWN_ID);
			RespawnLocalSoloPlayer(character);

			Game.Managers.RouterManager.Node.Open(Game.Ui.HudUI.Node);
		}

		public async void CreateProceduralWorldAndPlayer(WorldSaveData save, CharacterSaveData character)
		{
			SpawnWorld();
			Dimensions.ClearLayers();

            Game.Managers.TileStreamingManager.Node.SetWorldSeed(save.Seed);

            ImportDimension(save, ChunkStreamingConstants.OVERWORLD_ID);
            ImportDimension(save, ChunkStreamingConstants.UPSIDEDOWN_ID);

			SetChunkStreamingEnabled(true);

			var loadingUi = Game.Ui.LoadingUI.Node;

			loadingUi?.Open();

			await Game.Managers.TileStreamingManager.Node.PreloadSpawnAreaAsync(ChunkStreamingConstants.UPSIDEDOWN_ID, Dimensions.ResolveParent(ChunkStreamingConstants.UPSIDEDOWN_ID), Vector2.Zero);
		
			RespawnLocalSoloPlayer(character);

			loadingUi?.Close();

			Game.Managers.RouterManager.Node.Open(Game.Ui.HudUI.Node);
		}

		// Tile e entidade leem o MESMO arquivo de dimensao: um pega as mutacoes, o outro os
		// records de entidade. Os dois so indexam aqui - nada e materializado ainda.
		private void ImportDimension(WorldSaveData save, string dimensionId)
		{
			var state = Game.Managers.SaveManager.Node.LoadDimensionState(save.WorldId, dimensionId);

			Game.Managers.TileStreamingManager.Node.ImportState(dimensionId, state);
			Streaming?.ImportState(dimensionId, state);
		}

		// Monta e grava o arquivo de uma dimensao. O DimensionSaveData existe SO aqui dentro:
		// ele e formato de arquivo, nao estado vivo. A verdade das mutacoes esta no
		// TileStreamingManager e a das entidades esta nos proprios nos.
		public void SaveDimensions(string worldId)
		{
			foreach (var dimensionId in new[] { ChunkStreamingConstants.OVERWORLD_ID, ChunkStreamingConstants.UPSIDEDOWN_ID })
			{
				var state = new DimensionSaveData
				{
					WorldId = worldId,
					DimensionId = dimensionId,
				};

				Game.Managers.TileStreamingManager.Node?.ExportInto(dimensionId, state);
				Streaming?.ExportInto(dimensionId, state);

				SaveStorage.SaveDimensionState(worldId, dimensionId, state);
			}
		}

		private void SetChunkStreamingEnabled(bool enabled)
		{
			var tileStreamingManager = Game.Managers.TileStreamingManager.Node;

			if (tileStreamingManager != null)
			{
				tileStreamingManager.Enabled = enabled;
			}

			var streaming = Streaming;

			if (streaming != null)
			{
				streaming.Enabled = enabled;
			}
		}

		// Desmonta a cena do mundo. Quem persiste e limpa a sessao e o SessionManager, antes
		// de chamar aqui.
		public void DespawnWorld()
		{
			GD.Print("[WorldManager.DespawnWorld] DespawnWorld()");

			var main = Game.Main.Node;
			var world = main?.GetNodeOrNull("World");

			if (world != null)
			{
				world.QueueFree();

				GD.Print("[WorldManager.LeaveWorld] world queued for free");
			}

			Dimensions.Reset();

			Game.Managers.TileStreamingManager.Node?.ResetState();

			Game.Managers.RouterManager.Node.Close(Game.Ui.HudUI.Node);
		}

		public void RespawnLocalSoloPlayer(CharacterSaveData character)
		{
			var localPlayer = GD.Load<PackedScene>("res://Scenes/World/Characters/Player.tscn").Instantiate<Player>();

			localPlayer.Name = "Player";
			localPlayer.PeerId = 1;
			localPlayer.Position = Dimensions.FindGroundSpawnPosition(ChunkStreamingConstants.UPSIDEDOWN_ID, 0f);

			if (character != null)
			{
				GodotDictionaryParser.ApplyTo(localPlayer, character.State);
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

		// Sem log: telas chamam isto de dentro do _Process enquanto ainda nao ha player
		// (DeathScreenUI faz exatamente isso), entao imprimir aqui e centenas de linhas por segundo.
		public Player GetLocalPlayer()
		{
			var localPeerId = 1;

			if (
				Multiplayer != null &&
				Multiplayer.MultiplayerPeer != null &&
				Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected
			)
			{
				localPeerId = Multiplayer.GetUniqueId();
			}

			return FindPlayerByPeerId(localPeerId);
		}

		public Player FindPlayerByPeerId(long peerId)
		{
			return GetAllPlayers().FirstOrDefault(p => p.PeerId == peerId);
		}

		// Todo Player da arvore, NPC incluso - quem precisa filtrar filtra.
		public List<Player> GetAllPlayers()
		{
			return GetTree().GetNodesInGroup("players").OfType<Player>().ToList();
		}

		// Players que estao no parent daquela dimensao. E o que o streaming usa pra decidir
		// o que carregar: raio ao redor de quem esta ali, nao de quem esta em outra dimensao.
		public List<Player> GetPlayersInDimension(string dimensionId)
		{
			var parent = Dimensions.ResolveParent(dimensionId);

			if (parent == null)
			{
				return new List<Player>();
			}

			return GetAllPlayers().Where(p => p.GetParent() == parent).ToList();
		}

		#endregion
	}
}
