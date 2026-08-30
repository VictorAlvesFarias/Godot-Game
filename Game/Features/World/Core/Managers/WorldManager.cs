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
using Jogo25D.Save;
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

		public void SpawnLocalWorldAndPlayer(WorldSaveData save, CharacterSaveData character)
		{
			SpawnWorld();
			SetChunkStreamingEnabled(false);

			CarregarDocumento(save);
			RespawnLocalSoloPlayer(character);

			Game.Managers.RouterManager.Node.Open(Game.Ui.HudUI.Node);
		}

		public async void CreateProceduralWorldAndPlayer(WorldSaveData save, CharacterSaveData character)
		{
			SpawnWorld();
			Dimensions.ClearLayers();

            Game.Managers.TileStreamingManager.Node.SetWorldSeed(save.Seed);

            CarregarDocumento(save);

			SetChunkStreamingEnabled(true);

			var loadingUi = Game.Ui.LoadingUI.Node;

			loadingUi?.Open();

			await Game.Managers.TileStreamingManager.Node.PreloadSpawnAreaAsync(ChunkStreamingConstants.UPSIDEDOWN_ID, Dimensions.ResolveParent(ChunkStreamingConstants.UPSIDEDOWN_ID), Vector2.Zero);
		
			RespawnLocalSoloPlayer(character);

			loadingUi?.Close();

			Game.Managers.RouterManager.Node.Open(Game.Ui.HudUI.Node);
		}

		private void CarregarDocumento(WorldSaveData save)
		{
			var documento = SaveStorage.LoadWorldDocument(save.WorldId);

			if (documento == null)
			{
				return;
			}

			foreach (var bruta in WorldDocument.Dimensoes(documento))
			{
				var entrada = bruta.AsGodotDictionary();
				var dimensionId = WorldDocument.Texto(entrada, WorldDocument.TYPE);
				var parent = Dimensions.ResolveParent(dimensionId);

				if (parent == null)
				{
					continue;
				}

				SaveSerializer.Ler(parent, WorldDocument.Estado(entrada));

				foreach (var brutaNo in WorldDocument.Nos(entrada))
				{
					var no = brutaNo.AsGodotDictionary();

					if (WorldDocument.EhReferencia(no))
					{
						continue;
					}

					var node = WorldDocument.Construir(no);

					if (node == null)
					{
						continue;
					}

					if (Streaming != null && Streaming.Enabled)
					{
						Streaming.Adotar(node, dimensionId);
					}
					else
					{
						Dimensions.ResolveEntities(dimensionId)?.AddChild(node);
					}
				}
			}
		}

		public void SalvarDocumento(WorldSaveData save)
		{
			if (save == null || Streaming == null)
			{
				return;
			}

			var dimensoes = new List<Node2D>();

			foreach (var dimensionId in new[] { ChunkStreamingConstants.OVERWORLD_ID, ChunkStreamingConstants.UPSIDEDOWN_ID })
			{
				var parent = Dimensions.ResolveParent(dimensionId);

				if (parent != null)
				{
					dimensoes.Add(parent);
				}
			}

			var documento = WorldDocument.Escrever(Streaming, dimensoes, d => Streaming.Descarregados(d is Dimension dim ? dim.DimensionId : d.Name));

			documento[WorldDocument.STATE] = WorldDocument.EstadoDe(save);

			SaveStorage.SaveWorldDocument(save.WorldId, documento);
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

		public void DespawnWorld()
		{
			Streaming?.ResetState();

			Dimensions.ClearEntities();
			Dimensions.ClearLayers();

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
				localPlayer.CharacterId = character.CharacterId;
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
			var parent = Dimensions.ResolveEntities(dimensionId);

			if (parent == null)
			{
				return new List<Player>();
			}

			return GetAllPlayers().Where(p => p.GetParent() == parent).ToList();
		}

		#endregion
	}
}
