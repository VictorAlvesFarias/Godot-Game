using Godot;
using System;

namespace Jogo25D.UI
{
    public partial class FpsCounterUI : Label
{
	private double pingTimer = 0.0;
	private double pingInterval = 1.0; // Medir ping a cada 1 segundo
	private double lastPingSentTime = 0.0;
	private int currentPing = 0;
	
	public override void _Process(double delta)
	{
		// Atualizar o texto com os FPS atuais
		var fps = Engine.GetFramesPerSecond();
		string fpsText = $"FPS: {fps}";
		
		// Adicionar ping se multiplayer estiver ativo
		if (Multiplayer != null && 
		    Multiplayer.MultiplayerPeer != null && 
		    Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
		{
			try
			{
				// Para clientes, medir ping manualmente
				if (!Multiplayer.IsServer())
				{
					pingTimer += delta;
					if (pingTimer >= pingInterval)
					{
						pingTimer = 0.0;
						lastPingSentTime = Time.GetTicksMsec();
						RpcId(1, nameof(PingPong));
					}
					
					Text = $"{fpsText} | Ping: {currentPing}ms";
				}
				else
				{
					// Servidor não tem ping
					Text = $"{fpsText} | Ping: 0ms";
				}
			}
			catch
			{
				Text = fpsText;
			}
		}
		else
		{
			Text = fpsText;
		}
	}
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void PingPong()
	{
		// Servidor recebe e responde imediatamente
		if (Multiplayer.IsServer())
		{
			int senderId = Multiplayer.GetRemoteSenderId();
			RpcId(senderId, nameof(ReceivePong));
		}
	}
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void ReceivePong()
	{
		// Cliente calcula o ping
		double now = Time.GetTicksMsec();
		currentPing = (int)(now - lastPingSentTime);
	}
    }
}
