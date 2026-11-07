using System;
using System.Collections.Generic;

namespace Jogo25D.UI
{
    public class ConsoleCommands
    {
        #region Dinamic properties

        public string Name { get; set; }
        public string Usage { get; set; }
        public string Description { get; set; }
        public Action<string[], ConsoleUI> Execute { get; set; }
        public Func<string, List<string>> GetCompletions { get; set; }

        #endregion
    }
}