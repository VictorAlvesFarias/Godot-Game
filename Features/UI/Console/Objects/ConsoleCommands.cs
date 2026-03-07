using System;
using System.Collections.Generic;

namespace Jogo25D.UI
{
	public class ConsoleCommands
	{
		public string Name;
		public string Usage;
		public string Description;
		public Action<string[], ConsoleUI> Execute;
		public Func<string, List<string>> GetCompletions;
	}
}