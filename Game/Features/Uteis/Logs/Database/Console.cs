using Godot;

namespace Jogo25D.Features.Uteis.Logs.Systems
{
	public static class Console
	{
		public static void Log(string value)
		{
			GD.Print($"{value}");
		}
	}
}
