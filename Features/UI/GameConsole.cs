using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Jogo25D.Characters;
using Jogo25D.Items;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
    public partial class GameConsole : CanvasLayer
    {
        private bool _isOpen;

        private RichTextLabel _history;
        private Panel _suggestionsPanel;
        private HBoxContainer _suggestionsBar;
        private LineEdit _input;

        private readonly List<string> _commandHistory = new();
        private int _historyIndex = -1;
        private string _savedInput = "";

        private class ConsoleCommand
        {
            public string Name;
            public string Usage;
            public string Description;
            public Func<string[], string> Execute;
            public Func<string, List<string>> GetCompletions;
        }

        private readonly Dictionary<string, ConsoleCommand> _commands = new();

        #region Lifecycle

        public override void _Ready()
        {
            _history        = GetNode<RichTextLabel>("Background/Margin/VBoxContainer/History");
            _suggestionsPanel = GetNode<Panel>("Background/Margin/VBoxContainer/SuggestionsPanel");
            _suggestionsBar  = GetNode<HBoxContainer>("Background/Margin/VBoxContainer/SuggestionsPanel/SuggestionsBar");
            _input           = GetNode<LineEdit>("Background/Margin/VBoxContainer/InputRow/Input");

            _input.TextChanged  += OnInputChanged;
            _input.TextSubmitted += OnInputSubmitted;

            RegisterCommands();

            PrintLine("[color=#88ccff]Console carregado. Digite [b]help[/b] para listar os comandos.[/color]");
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            {
                return;
            }

            if (key.Keycode == Key.Apostrophe)
            {
                Toggle();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!_isOpen)
            {
                return;
            }

            if (key.Keycode == Key.Tab)
            {
                ApplyFirstSuggestion();
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

        #region Toggle

        private void Toggle()
        {
            _isOpen = !_isOpen;
            Visible = _isOpen;

            if (_isOpen)
            {
                InputManager.Instance?.AddBlocker("console");
                _input.CallDeferred(LineEdit.MethodName.GrabFocus);
            }
            else
            {
                InputManager.Instance?.RemoveBlocker("console");
            }
        }

        #endregion

        #region Input handlers

        private void OnInputChanged(string text)
        {
            RefreshSuggestions(text);
        }

        private void OnInputSubmitted(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _commandHistory.Insert(0, text);
            _historyIndex = -1;
            _savedInput   = "";

            PrintLine($"[color=#888888]> {GD.VarToStr(text).Replace("[", "[")}[/color]");

            ExecuteRaw(text.Trim());

            _input.Text = "";
            _suggestionsPanel.Visible = false;
            _input.CallDeferred(LineEdit.MethodName.GrabFocus);
        }

        #endregion

        #region Command execution

        private void ExecuteRaw(string raw)
        {
            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return;
            }

            string name = parts[0].ToLower();
            string[] args = parts.Skip(1).ToArray();

            if (!_commands.TryGetValue(name, out var cmd))
            {
                PrintLine($"[color=#ff6666]Comando desconhecido: '{name}'. Digite [b]help[/b] para listar os comandos.[/color]");
                return;
            }

            string result = cmd.Execute(args);
            if (!string.IsNullOrEmpty(result))
            {
                PrintLine(result);
            }
        }

        #endregion

        #region Autocomplete

        private void RefreshSuggestions(string text)
        {
            foreach (Node child in _suggestionsBar.GetChildren())
            {
                child.QueueFree();
            }

            var suggestions = ComputeSuggestions(text);
            if (suggestions.Count == 0)
            {
                _suggestionsPanel.Visible = false;
                return;
            }

            _suggestionsPanel.Visible = true;

            foreach (string s in suggestions.Take(8))
            {
                var btn    = new Button();
                btn.Text   = s;
                btn.Flat   = true;
                btn.AddThemeColorOverride("font_color", new Color(0.6f, 0.85f, 1f));
                btn.AddThemeFontSizeOverride("font_size", 13);

                string captured = s;
                btn.Pressed += () => ApplySuggestion(captured, _input.Text);
                _suggestionsBar.AddChild(btn);
            }
        }

        private List<string> ComputeSuggestions(string text)
        {
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0 || (parts.Length == 1 && !text.EndsWith(' ')))
            {
                string prefix = parts.Length == 1 ? parts[0].ToLower() : "";
                return _commands.Keys
                    .Where(k => k.StartsWith(prefix))
                    .OrderBy(k => k)
                    .ToList();
            }

            string cmdName = parts[0].ToLower();
            if (!_commands.TryGetValue(cmdName, out var cmd))
            {
                return new List<string>();
            }

            string partial = text.EndsWith(' ') ? "" : parts.Last().ToLower();
            return cmd.GetCompletions(partial);
        }

        private void ApplyFirstSuggestion()
        {
            var first = _suggestionsBar.GetChildren().OfType<Button>().FirstOrDefault();
            if (first == null)
            {
                return;
            }

            ApplySuggestion(first.Text, _input.Text);
        }

        private void ApplySuggestion(string suggestion, string currentText)
        {
            var parts = currentText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0 || (parts.Length == 1 && !currentText.EndsWith(' ')))
            {
                _input.Text = suggestion + " ";
            }
            else
            {
                string prefix = string.Join(" ", parts.SkipLast(1)) + " ";
                _input.Text   = prefix + suggestion + " ";
            }

            _input.CaretColumn = _input.Text.Length;
            RefreshSuggestions(_input.Text);
        }

        #endregion

        #region History navigation

        private void NavigateHistory(int dir)
        {
            if (_commandHistory.Count == 0)
            {
                return;
            }

            if (_historyIndex == -1)
            {
                _savedInput = _input.Text;
            }

            _historyIndex = Math.Clamp(_historyIndex + dir, -1, _commandHistory.Count - 1);

            _input.Text        = _historyIndex == -1 ? _savedInput : _commandHistory[_historyIndex];
            _input.CaretColumn = _input.Text.Length;
        }

        #endregion

        #region Helpers

        private void PrintLine(string bbcode)
        {
            _history.AppendText(bbcode + "\n");
        }

        private Player GetLocalPlayer()
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

        #region Command registration

        private void RegisterCommands()
        {
            Register(
                name: "help",
                usage: "help",
                description: "Lista todos os comandos disponíveis",
                execute: _ =>
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("[color=#88ccff]Comandos disponíveis:[/color]");
                    foreach (var c in _commands.Values.OrderBy(c => c.Name))
                    {
                        sb.AppendLine($"  [color=#ffcc44]{c.Usage,-35}[/color]{c.Description}");
                    }
                    return sb.ToString().TrimEnd();
                },
                getCompletions: _ => new List<string>()
            );

            Register(
                name: "clear",
                usage: "clear",
                description: "Limpa o histórico do console",
                execute: _ =>
                {
                    _history.Clear();
                    return null;
                },
                getCompletions: _ => new List<string>()
            );

            Register(
                name: "add_item",
                usage: "add_item <id> [quantidade]",
                description: "Adiciona um item ao inventário do jogador",
                execute: args =>
                {
                    if (args.Length < 1)
                    {
                        return "[color=#ff6666]Uso: add_item <id> [quantidade][/color]";
                    }

                    ItemDB.Initialize();
                    var def = ItemDB.Get(args[0]);
                    if (def == null)
                    {
                        return $"[color=#ff6666]Item '[b]{args[0]}[/b]' não encontrado. Use [b]list_items[/b] para ver os IDs disponíveis.[/color]";
                    }

                    int qty = 1;
                    if (args.Length >= 2 && !int.TryParse(args[1], out qty))
                    {
                        return "[color=#ff6666]Quantidade inválida.[/color]";
                    }

                    var player = GetLocalPlayer();
                    if (player == null)
                    {
                        return "[color=#ff6666]Nenhum jogador encontrado na cena.[/color]";
                    }

                    bool ok = player.Inventory?.AddItem(def, qty) ?? false;
                    if (!ok)
                    {
                        return "[color=#ff6666]Inventário cheio ou item não pôde ser adicionado.[/color]";
                    }

                    return $"[color=#88ff88]+{qty}x [b]{def.Name}[/b] adicionado ao inventário.[/color]";
                },
                getCompletions: partial =>
                {
                    ItemDB.Initialize();
                    return ItemDB.GetAllIds()
                        .Where(id => id.StartsWith(partial))
                        .OrderBy(id => id)
                        .ToList();
                }
            );

            Register(
                name: "list_items",
                usage: "list_items",
                description: "Lista todos os IDs de itens disponíveis no banco de itens",
                execute: _ =>
                {
                    ItemDB.Initialize();
                    var ids = ItemDB.GetAllIds().OrderBy(id => id).ToList();
                    var sb  = new System.Text.StringBuilder();
                    sb.AppendLine($"[color=#88ccff]{ids.Count} item(s) registrado(s):[/color]");
                    foreach (string id in ids)
                    {
                        var def = ItemDB.Get(id);
                        sb.AppendLine($"  [color=#ffcc44]{id,-25}[/color]{def?.Name}");
                    }
                    return sb.ToString().TrimEnd();
                },
                getCompletions: _ => new List<string>()
            );
        }

        private void Register(string name, string usage, string description, Func<string[], string> execute, Func<string, List<string>> getCompletions)
        {
            _commands[name] = new ConsoleCommand
            {
                Name           = name,
                Usage          = usage,
                Description    = description,
                Execute        = execute,
                GetCompletions = getCompletions
            };
        }

        #endregion
    }
}
