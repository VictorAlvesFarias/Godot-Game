using Godot;
using Jogo25D.Actions;
using Jogo25D.Characters;
using Jogo25D.Constants;
using Jogo25D.Core;
using Jogo25D.Items;
using Jogo25D.Systems;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.UI
{
    public partial class ConsoleUI : ScreenUI
    {
        #region Dinamic properties

        public bool IsOpen { get; set; }
        public Player LocalPlayer { get; set; }
        public List<string> CommandHistory { get; set; } = new();
        public int HistoryIndex { get; set; } = -1;
        public string SavedInput { get; set; } = "";
        public int SuggestionIndex { get; set; } = 0;
        public Dictionary<string, ConsoleCommands> Commands { get; set; } = new();
        public StyleBoxFlat SuggestionHighlightStyle { get; set; }
        public StyleBox SuggestionNormalStyle { get; set; }

        #endregion

        #region Godot implementation

        public override bool IsOverlay => true;

        public override void _Ready()
        {
            Game.WhenReady(Initialize);
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            {
                return;
            }

            if (key.Keycode == Key.Apostrophe)
            {
                RefreshLocalPlayer();

                if (!IsOpen && (LocalPlayer?.Input?.IsBlockedByOther("console") ?? false))
                {
                    return;
                }

                Toggle();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!IsOpen)
            {
                return;
            }

            if (key.Keycode == Key.Tab)
            {
                ApplySelectedSuggestion();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Right && Game.Ui.ConsoleUI.SuggestionsPanel.Node.Visible)
            {
                NavigateSuggestions(1);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Left && Game.Ui.ConsoleUI.SuggestionsPanel.Node.Visible)
            {
                NavigateSuggestions(-1);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Up)
            {
                NavigateHistory(-1);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Down)
            {
                NavigateHistory(1);
                GetViewport().SetInputAsHandled();
            }
        }

        #endregion

        #region Core - Setup

        private void Initialize()
        {
            Game.Ui.ConsoleUI.TemplateNormal.Node.Visible = false;
            Game.Ui.ConsoleUI.TemplateEcho.Node.Visible = false;
            Game.Ui.ConsoleUI.TemplateInfo.Node.Visible = false;
            Game.Ui.ConsoleUI.TemplateError.Node.Visible = false;
            Game.Ui.ConsoleUI.TemplateSuccess.Node.Visible = false;
            Game.Ui.ConsoleUI.SuggestionTemplate.Node.Visible = false;

            SuggestionHighlightStyle = Game.Ui.ConsoleUI.SuggestionHighlightHolder.Node.GetThemeStylebox("panel") as StyleBoxFlat;
            Game.Ui.ConsoleUI.SuggestionHighlightHolder.Node.Visible = false;

            Game.Ui.ConsoleUI.InputField.Node.TextChanged += OnInputChanged;
            Game.Ui.ConsoleUI.InputField.Node.TextSubmitted += OnInputSubmitted;

            SuggestionNormalStyle = Game.Ui.ConsoleUI.InputField.Node.GetThemeStylebox("normal");

            LocalPlayer = Game.Managers.WorldManager.Node.GetLocalPlayer();

            RegisterCommands();

            PrintInfo("Console carregado. Digite 'help' para listar os comandos.");
        }

        #endregion

        #region Core - Toggle

        public void Toggle()
        {
            RefreshLocalPlayer();

            IsOpen = !IsOpen;
            if (IsOpen)
            {
                Game.Managers.RouterManager.Node.Open(this);
            }
            else
            {
                Game.Managers.RouterManager.Node.Close(this);
            }

            if (IsOpen)
            {
                LocalPlayer?.Input?.AddBlocker("console");
                Game.Ui.ConsoleUI.InputField.Node.CallDeferred(LineEdit.MethodName.GrabFocus);
            }
            else
            {
                LocalPlayer?.Input?.RemoveBlocker("console");
            }
        }

        public void RefreshLocalPlayer()
        {
            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                LocalPlayer = Game.Managers.WorldManager.Node?.GetLocalPlayer();
            }
        }

        #endregion

        #region Core - Input handling

        public void OnInputChanged(string text)
        {
            RefreshSuggestions(text);
        }

        public void OnInputSubmitted(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            CommandHistory.Insert(0, text);
            HistoryIndex = -1;
            SavedInput = "";

            PrintEcho($"> {text}");

            ExecuteRaw(text.Trim());

            Game.Ui.ConsoleUI.InputField.Node.Text = "";
            Game.Ui.ConsoleUI.SuggestionsPanel.Node.Visible = false;
            Game.Ui.ConsoleUI.InputField.Node.CallDeferred(LineEdit.MethodName.GrabFocus);
        }

        public void ExecuteRaw(string raw)
        {
            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return;
            }

            string name = parts[0].ToLower();
            string[] args = parts.Skip(1).ToArray();

            if (!Commands.TryGetValue(name, out var cmd))
            {
                PrintError($"Comando desconhecido: '{name}'. Digite 'help' para listar os comandos.");
                return;
            }

            cmd.Execute(args, this);
        }

        #endregion

        #region Core - Suggestions

        public void RefreshSuggestions(string text)
        {
            foreach (Node child in Game.Ui.ConsoleUI.SuggestionsBar.Node.GetChildren())
            {
                if (child == Game.Ui.ConsoleUI.SuggestionTemplate.Node)
                {
                    continue;
                }

                child.QueueFree();
            }

            var suggestions = ComputeSuggestions(text);
            if (suggestions.Count == 0)
            {
                Game.Ui.ConsoleUI.SuggestionsPanel.Node.Visible = false;
                return;
            }

            if (Game.Ui.ConsoleUI.SuggestionTemplate.Node == null)
            {
                GD.PushError("ConsoleUI: Game.Ui.ConsoleUI.SuggestionTemplate.Node não encontrado, não é possível montar sugestões.");
                Game.Ui.ConsoleUI.SuggestionsPanel.Node.Visible = false;
                return;
            }

            SuggestionIndex = 0;
            Game.Ui.ConsoleUI.SuggestionsPanel.Node.Visible = true;

            var newButtons = new List<Button>();
            foreach (string s in suggestions.Take(8))
            {
                var btn = (Button)Game.Ui.ConsoleUI.SuggestionTemplate.Node.Duplicate();
                btn.Text = s;
                btn.Visible = true;
                btn.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
                btn.AddThemeFontSizeOverride("font_size", 13);

                string captured = s;
                btn.Pressed += () => ApplySuggestion(captured, Game.Ui.ConsoleUI.InputField.Node.Text);
                Game.Ui.ConsoleUI.SuggestionsBar.Node.AddChild(btn);
                newButtons.Add(btn);
            }

            UpdateSuggestionHighlight(newButtons);
        }

        public void NavigateSuggestions(int dir)
        {
            var buttons = Game.Ui.ConsoleUI.SuggestionsBar.Node.GetChildren().OfType<Button>().Where(b => b != Game.Ui.ConsoleUI.SuggestionTemplate.Node).ToList();
            if (buttons.Count == 0)
            {
                return;
            }

            SuggestionIndex = ((SuggestionIndex + dir + buttons.Count + 2) % (buttons.Count + 1)) - 1;
            UpdateSuggestionHighlight(buttons);
        }

        public void UpdateSuggestionHighlight(List<Button> buttons = null)
        {
            buttons ??= Game.Ui.ConsoleUI.SuggestionsBar.Node.GetChildren().OfType<Button>().Where(b => b != Game.Ui.ConsoleUI.SuggestionTemplate.Node).ToList();
            for (int i = 0; i < buttons.Count; i++)
            {
                bool selected = i == SuggestionIndex;
                buttons[i].AddThemeColorOverride("font_color",
                    selected ? new Color(0.62f, 0.36f, 0.92f) : new Color(1f, 1f, 1f));
                buttons[i].AddThemeStyleboxOverride("normal",
                    selected ? SuggestionHighlightStyle : SuggestionNormalStyle);
            }
        }

        public List<string> ComputeSuggestions(string text)
        {
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0 || (parts.Length == 1 && !text.EndsWith(' ')))
            {
                string prefix = parts.Length == 1 ? parts[0].ToLower() : "";
                return Commands.Keys
                    .Where(k => k.StartsWith(prefix))
                    .OrderBy(k => k)
                    .ToList();
            }

            string cmdName = parts[0].ToLower();
            if (!Commands.TryGetValue(cmdName, out var cmd))
            {
                return new List<string>();
            }

            string partial = text.EndsWith(' ') ? "" : parts.Last().ToLower();
            return cmd.GetCompletions(partial);
        }

        public void ApplySelectedSuggestion()
        {
            var buttons = Game.Ui.ConsoleUI.SuggestionsBar.Node.GetChildren().OfType<Button>().Where(b => b != Game.Ui.ConsoleUI.SuggestionTemplate.Node).ToList();
            if (buttons.Count == 0)
            {
                return;
            }

            int idx = SuggestionIndex >= 0 ? SuggestionIndex : 0;
            if (idx < buttons.Count)
            {
                ApplySuggestion(buttons[idx].Text, Game.Ui.ConsoleUI.InputField.Node.Text);
            }
        }

        public void ApplySuggestion(string suggestion, string currentText)
        {
            var parts = currentText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0 || (parts.Length == 1 && !currentText.EndsWith(' ')))
            {
                Game.Ui.ConsoleUI.InputField.Node.Text = suggestion + " ";
            }
            else
            {
                string prefix = currentText.EndsWith(' ')
                    ? string.Join(" ", parts) + " "
                    : string.Join(" ", parts.SkipLast(1)) + " ";
                Game.Ui.ConsoleUI.InputField.Node.Text = prefix + suggestion + " ";
            }

            Game.Ui.ConsoleUI.InputField.Node.CaretColumn = Game.Ui.ConsoleUI.InputField.Node.Text.Length;
            RefreshSuggestions(Game.Ui.ConsoleUI.InputField.Node.Text);
        }

        #endregion

        #region Core - History

        public void NavigateHistory(int dir)
        {
            if (CommandHistory.Count == 0)
            {
                return;
            }

            if (HistoryIndex == -1)
            {
                SavedInput = Game.Ui.ConsoleUI.InputField.Node.Text;
            }

            HistoryIndex = Math.Clamp(HistoryIndex + dir, -1, CommandHistory.Count - 1);

            Game.Ui.ConsoleUI.InputField.Node.Text = HistoryIndex == -1 ? SavedInput : CommandHistory[HistoryIndex];
            Game.Ui.ConsoleUI.InputField.Node.CaretColumn = Game.Ui.ConsoleUI.InputField.Node.Text.Length;
        }

        #endregion

        #region Core - Output

        public void PrintWith(Label template, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var label = template.Duplicate() as Label;
            label.Text = text;
            label.Visible = true;
            Game.Ui.ConsoleUI.HistoryContainer.Node.AddChild(label);

            Callable.From(() => { Game.Ui.ConsoleUI.HistoryScroll.Node.ScrollVertical = int.MaxValue; }).CallDeferred();
        }

        internal void PrintNormal(string text) => PrintWith(Game.Ui.ConsoleUI.TemplateNormal.Node,  text);
        internal void PrintEcho(string text) => PrintWith(Game.Ui.ConsoleUI.TemplateEcho.Node,    text);
        internal void PrintInfo(string text) => PrintWith(Game.Ui.ConsoleUI.TemplateInfo.Node,    text);
        internal void PrintError(string text) => PrintWith(Game.Ui.ConsoleUI.TemplateError.Node,   text);
        internal void PrintSuccess(string text) => PrintWith(Game.Ui.ConsoleUI.TemplateSuccess.Node, text);

        #endregion

        #region Core - Player lookup

        public Player GetLocalPlayer()
        {
            foreach (Node n in GetTree().GetNodesInGroup("players"))
            {
                if (n is Player p)
                {
                    return p;
                }
            }
            return null;
        }

        #endregion

        #region Core - Commands

        public void RegisterCommands()
        {
            Register(
                name: "help",
                usage: "help",
                description: "Lista todos os comandos disponÃ­veis",
                execute: (_, console) =>
                {
                    console.PrintInfo("Comandos disponÃ­veis:");
                    foreach (var c in Commands.Values.OrderBy(c => c.Name))
                    {
                        console.PrintNormal($"  {c.Usage,-35} {c.Description}");
                    }
                },
                getCompletions: _ => new List<string>()
            );

            Register(
                name: "clear",
                usage: "clear",
                description: "Limpa o histÃ³rico do console",
                execute: (_, _) =>
                {
                    foreach (Node child in Game.Ui.ConsoleUI.HistoryContainer.Node.GetChildren())
                    {
                        if (child is CanvasItem ci && ci.Visible)
                        {
                            child.QueueFree();
                        }
                    }
                },
                getCompletions: _ => new List<string>()
            );

            Register(
                name: "add_item",
                usage: "add_item <id> [quantidade]",
                description: "Adiciona um item ao inventÃ¡rio do jogador",
                execute: (args, console) =>
                {
                    if (args.Length < 1)
                    {
                        console.PrintError("Uso: add_item <id> [quantidade]");
                        return;
                    }

                    ItemFactory.Initialize();

                    var def = ItemFactory.Create(args[0]);

                    if (def == null)
                    {
                        console.PrintError($"Item '{args[0]}' nÃ£o encontrado. Use 'list_items' para ver os IDs disponÃ­veis.");

                        return;
                    }

                    int qty = 1;

                    if (args.Length >= 2 && !int.TryParse(args[1], out qty))
                    {
                        console.PrintError("Quantidade invÃ¡lida.");
                        return;
                    }

                    RefreshLocalPlayer();

                    if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
                    {
                        console.PrintError("Nenhum jogador encontrado na cena.");
                        return;
                    }

                    LocalPlayer.AddItemRequest(ItemFactory.CreateInstance(def.Id));

                    console.PrintSuccess($"+{qty}x {def.Name} adicionado ao inventÃ¡rio.");
                },
                getCompletions: partial =>
                {
                    ItemFactory.Initialize();
                    return ItemFactory.GetAllIds()
                        .Where(id => id.StartsWith(partial))
                        .OrderBy(id => id)
                        .ToList();
                }
            );

            Register(
                name: "list_items",
                usage: "list_items",
                description: "Lista todos os IDs de itens disponÃ­veis no banco de itens",
                execute: (_, console) =>
                {
                    ItemFactory.Initialize();
                    var ids = ItemFactory.GetAllIds().OrderBy(id => id).ToList();
                    console.PrintInfo($"{ids.Count} item(s) registrado(s):");
                    foreach (string id in ids)
                    {
                        var def = ItemFactory.Create(id);
                        console.PrintNormal($"  {id,-25} {def?.Name}");
                    }
                },
                getCompletions: _ => new List<string>()
            );

            Register(
                name: "teleport",
                usage: "teleport [x] [y]",
                description: "Teleporta o jogador local para (x, y) e reseta a vida - padrÃ£o (0, 0)",
                execute: (args, console) =>
                {
                    if (Game.Managers.WorldManager.Node == null)
                    {
                        console.PrintError("WorldManager nÃ£o encontrado.");

                        return;
                    }

                    float x = 0f;
                    float y = 0f;

                    if (args.Length >= 1 && !float.TryParse(args[0], out x))
                    {
                        console.PrintError("Coordenada x invÃ¡lida.");

                        return;
                    }

                    if (args.Length >= 2 && !float.TryParse(args[1], out y))
                    {
                        console.PrintError("Coordenada y invÃ¡lida.");

                        return;
                    }

                    Game.Managers.WorldManager.Node.GetLocalPlayer()?.TeleportClientRequest(new Vector2(x, y));

                    console.PrintSuccess($"Teleportando para ({x}, {y})...");
                },
                getCompletions: _ => new List<string>()
            );

            Register(
                name: "reset",
                usage: "reset",
                description: "Reseta o jogador local (mesma coisa que morrer e reviver - vida cheia, de volta pro spawn)",
                execute: (_, console) =>
                {
                    if (Game.Managers.WorldManager.Node == null)
                    {
                        console.PrintError("WorldManager nÃ£o encontrado.");

                        return;
                    }

                    Game.Managers.WorldManager.Node.GetLocalPlayer()?.TeleportClientRequest(Vector2.Zero);

                    console.PrintSuccess("Jogador resetado.");
                },
                getCompletions: _ => new List<string>()
            );

            Register(
                name: "give_all",
                usage: "give_all",
                description: "Dá todos os itens (empilháveis com quantidade 50) e todas as habilidades ao jogador, pulando o que ele já tem",
                execute: (_, console) =>
                {
                    RefreshLocalPlayer();

                    if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
                    {
                        console.PrintError("Nenhum jogador encontrado na cena.");

                        return;
                    }

                    ItemFactory.Initialize();
                    ActionFactory.Initialize();

                    int itemsAdded = 0;

                    foreach (var id in ItemFactory.GetAllIds())
                    {
                        var def = ItemFactory.Create(id);

                        if (def == null || LocalPlayer.Inventory.Items.Any(i => i != null && i.Id == id))
                        {
                            continue;
                        }

                        var instance = ItemFactory.CreateInstance(id);

                        if (def.Stackable)
                        {
                            instance.Quantity = 50;
                        }

                        LocalPlayer.AddItemRequest(instance);

                        itemsAdded++;
                    }

                    int abilitiesAdded = 0;

                    foreach (var actionId in ActionFactory.GetAllIds())
                    {
                        if (LocalPlayer.IsAbilityStillGranted(actionId))
                        {
                            continue;
                        }

                        LocalPlayer.UnlockAbilityRequest(actionId);

                        abilitiesAdded++;
                    }

                    console.PrintSuccess($"+{itemsAdded} item(ns) e +{abilitiesAdded} habilidade(s) adicionados.");
                },
                getCompletions: _ => new List<string>()
            );

            Register(
                name: "dimension",
                usage: "dimension",
                description: "Troca o jogador local para a prÃ³xima dimensÃ£o",
                execute: (_, console) =>
                {
                    if (Game.Managers.WorldManager.Node == null)
                    {
                        console.PrintError("WorldManager nÃ£o encontrado.");

                        return;
                    }

                    Game.Managers.WorldManager.Node.GetLocalPlayer()?.TradeDimensionClientRequest();

                    console.PrintSuccess("Trocando de dimensÃ£o.");
                },
                getCompletions: _ => new List<string>()
            );

            Register(
                name: "cam_no_clip",
                usage: "cam_no_clip enable/disable",
                description: "Ativa ou desativa a camera livre (voar pelo mapa com zoom out infinito)",
                execute: (args, console) =>
                {
                    if (args.Length < 1 || (args[0] != "enable" && args[0] != "disable"))
                    {
                        console.PrintError("Uso: cam_no_clip enable/disable");

                        return;
                    }

                    var camera = GetTree().GetNodesInGroup("cameras").OfType<CameraController>().FirstOrDefault();

                    if (camera == null)
                    {
                        console.PrintError("Nenhuma camera encontrada na cena.");

                        return;
                    }

                    camera.FreeCameraEnabled = args[0] == "enable";

                    console.PrintSuccess($"Camera livre {(camera.FreeCameraEnabled ? "ativada" : "desativada")}.");
                },
                getCompletions: partial => new List<string> { "enable", "disable" }
                    .Where(option => option.StartsWith(partial))
                    .ToList()
            );
        }

        public void Register(string name, string usage, string description, Action<string[], ConsoleUI> execute, Func<string, List<string>> getCompletions)
        {
            Commands[name] = new ConsoleCommands {
                Name = name,
                Usage = usage,
                Description = description,
                Execute = execute,
                GetCompletions = getCompletions
            };
        }

        #endregion
    }
}
