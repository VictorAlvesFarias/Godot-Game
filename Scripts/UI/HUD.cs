using Godot;
using System;
using Jogo25D.Characters;
using Jogo25D.Systems;
using Jogo25D.Items;

namespace Jogo25D.UI
{
	/// <summary>
	/// HUD unificado com FPS, Health e Weapon display
	/// </summary>
	public partial class HUD : CanvasLayer
	{
		[Export] public string PlayerGroupName { get; set; } = "players";
		
		private Label fpsLabel;
		private Label healthLabel;
		private Label weaponLabel;
		private Inventory inventory;
		private Player localPlayer;
		private double pingTimer = 0.0;
		private double pingInterval = 1.0;
		private double lastPingSentTime = 0.0;
		private int currentPing = 0;

		public override void _Ready()
		{
			// Obter referências dos labels
			fpsLabel = GetNode<Label>("MarginContainer/VBoxContainer/FpsLabel");
			healthLabel = GetNode<Label>("MarginContainer/VBoxContainer/HealthLabel");
			weaponLabel = GetNode<Label>("MarginContainer/VBoxContainer/EquippedWeaponLabel");
		
		// Conectar ao Inventory do player local dinamicamente
		CallDeferred(nameof(FindLocalPlayerInventory));
	}
	public override void _ExitTree()
	{
		// Desconectar sinais para evitar erros ao destruir a cena
		if (inventory != null && IsInstanceValid(inventory))
		{
			inventory.ItemEquipped -= OnItemEquipped;
		}
	}

	public override void _Process(double delta)
		{
			UpdateFpsDisplay(delta);
			UpdateHealthDisplay();
		}

		#region FPS Display
		private void UpdateFpsDisplay(double delta)
		{
			var fps = Engine.GetFramesPerSecond();
			string fpsText = $"FPS: {fps}";
			
			// Adicionar ping se multiplayer estiver ativo
			if (Multiplayer != null && 
				Multiplayer.MultiplayerPeer != null && 
				Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
			{
				try
				{
					if (!Multiplayer.IsServer())
					{
						pingTimer += delta;
						if (pingTimer >= pingInterval)
						{
							pingTimer = 0.0;
							lastPingSentTime = Time.GetTicksMsec();
							RpcId(1, nameof(PingPong));
						}
						
						fpsLabel.Text = $"{fpsText} | Ping: {currentPing}ms";
					}
					else
					{
						fpsLabel.Text = $"{fpsText} | Ping: 0ms";
					}
				}
				catch
				{
					fpsLabel.Text = fpsText;
				}
			}
			else
			{
				fpsLabel.Text = fpsText;
			}
		}
		
		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
		private void PingPong()
		{
			if (Multiplayer.IsServer())
			{
				int senderId = Multiplayer.GetRemoteSenderId();
				RpcId(senderId, nameof(ReceivePong));
			}
		}
		
		[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
		private void ReceivePong()
		{
			double now = Time.GetTicksMsec();
			currentPing = (int)(now - lastPingSentTime);
		}
		#endregion

		#region Health Display
		private void UpdateHealthDisplay()
		{
			if (localPlayer == null || !IsInstanceValid(localPlayer))
			{
				FindLocalPlayer();
			}

			if (localPlayer != null && IsInstanceValid(localPlayer))
			{
				var hearts = "";

				for (int i = 0; i < localPlayer.CurrentHealth; i++)
				{
					hearts += "♥ ";
				}
				
				var lostHealth = localPlayer.MaxHealth - localPlayer.CurrentHealth;

				for (int i = 0; i < lostHealth; i++)
				{
					hearts += "♡ ";
				}
				
				healthLabel.Text = hearts.Trim();
			}
			else
			{
				healthLabel.Text = "";
			}
		}

		private void FindLocalPlayer()
		{
			var players = GetTree().GetNodesInGroup("players");
			var localPeerId = 1;
			var hasMultiplayer = false;
			
			if (Multiplayer != null && Multiplayer.MultiplayerPeer != null && 
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
			
			foreach (Node node in players)
			{
				if (node is Player player)
				{
					if (!hasMultiplayer || player.GetMultiplayerAuthority() == localPeerId)
					{
						localPlayer = player;
				
						// Também buscar o WeaponInventory quando encontrar o player
						FindLocalPlayerInventory();
						break;
					}
				}
			}
		}
		#endregion

		#region Weapon Display
		private void FindLocalPlayerInventory()
		{
			if (localPlayer != null && IsInstanceValid(localPlayer))
			{
				inventory = localPlayer.Inventory;
				if (inventory != null && IsInstanceValid(inventory))
				{
					inventory.ItemEquipped += OnItemEquipped;
					UpdateWeaponDisplay();
				}
			}
		}

		private void OnItemEquipped(Item item, int index)
		{
			UpdateWeaponDisplay();
		}

		private void UpdateWeaponDisplay()
		{
			if (inventory == null || !IsInstanceValid(inventory))
			{
				weaponLabel.Text = "Arma: Nenhuma";
				return;
			}

			var equippedItem = inventory.GetEquippedItem();
			
			if (equippedItem == null || equippedItem.Type != ItemType.Weapon)
			{
				weaponLabel.Text = "Arma: Nenhuma";
				return;
			}

			weaponLabel.Text = $"🗡 {equippedItem.ItemName}";
		}
		#endregion
	}
}
