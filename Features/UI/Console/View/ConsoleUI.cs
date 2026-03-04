using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Jogo25D.Characters;
using Jogo25D.Items;
using Jogo25D.Systems;

namespace Jogo25D.UI
{
	public partial class ConsoleUI : CanvasLayer
	{
		private bool _isOpen;

		private ScrollContainer _historyScroll;
		private VBoxContainer _historyContainer;
		private Panel _suggestionsPanel;
		private HBoxContainer _suggestionsBar;
		private LineEdit _input;

		private Label _templateNormal;
		private Label _templateEcho;
		private Label _templateInfo;
		private Label _templateError;
		private Label _templateSuccess;

		private readonly List<string> _commandHistory = new();
		private int _historyIndex = -1;
		private string _savedInput = "";

		private int _suggestionIndex = 0;

		private readonly Dictionary<string, ConsoleCommands> _commands = new();

		#region Lifecycle

		public override void _Ready()
		{
			_historyScroll    = GetNode<ScrollContainer>("Background/Margin/VBoxContainer/HistoryScroll");
			_historyContainer = GetNode<VBoxContainer>("Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer");
			_suggestionsPanel = GetNode<Panel>("Background/Margin/VBoxContainer/InputContainer/SuggestionsPanel");
			_suggestionsBar   = GetNode<HBoxContainer>("Background/Margin/VBoxContainer/InputContainer/SuggestionsPanel/Margin/SuggestionsBar");
			_input            = GetNode<LineEdit>("Background/Margin/VBoxContainer/InputContainer/InputPanel/Margin/InputRow/Input");

			_templateNormal  = GetNode<Label>("Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer/Normal");
			_templateEcho    = GetNode<Label>("Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer/Echo");
			_templateInfo    = GetNode<Label>("Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer/Info");
			_templateError   = GetNode<Label>("Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer/Error");
			_templateSuccess = GetNode<Label>("Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer/Success");

			_input.TextChanged   += OnInputChanged;
			_input.TextSubmitted += OnInputSubmitted;

			RegisterCommands();

			PrintInfo("Console carregado. Digite 'help' para listar os comandos.");
		}

		public override void _Input(InputEvent @event)
		{
			if (@event is not InputEventKey key || !key.Pressed || key.Echo)
				return;

			if (key.Keycode == Key.Apostrophe)
			{
				Toggle();
				GetViewport().SetInputAsHandled();
				return;
			}

			if (!_isOpen)
				return;

			if (key.Keycode == Key.Tab)
			{
				ApplySelectedSuggestion();
				GetViewport().SetInputAsHandled();
				return;
			}

			if (key.Keycode == Key.Right && _suggestionsPanel.Visible)
			{
				NavigateSuggestions(1);
				GetViewport().SetInputAsHandled();
				return;
			}

			if (key.Keycode == Key.Left && _suggestionsPanel.Visible)
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
				return;

			_commandHistory.Insert(0, text);
			_historyIndex = -1;
			_savedInput   = "";

			PrintEcho($"> {text}");

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
				return;

			string name = parts[0].ToLower();
			string[] args = parts.Skip(1).ToArray();

			if (!_commands.TryGetValue(name, out var cmd))
			{
				PrintError($"Comando desconhecido: '{name}'. Digite 'help' para listar os comandos.");
				return;
			}

			cmd.Execute(args, this);
		}

		#endregion

		#region Autocomplete

		private void RefreshSuggestions(string text)
		{
			foreach (Node child in _suggestionsBar.GetChildren())
				child.QueueFree();

			var suggestions = ComputeSuggestions(text);
			if (suggestions.Count == 0)
			{
				_suggestionsPanel.Visible = false;
				return;
			}

			_suggestionIndex = 0;
			_suggestionsPanel.Visible = true;

			var newButtons = new List<Button>();
			foreach (string s in suggestions.Take(8))
			{
				var btn = new Button();
				btn.Text = s;
				btn.Flat = true;
				btn.AddThemeColorOverride("font_color", new Color(0.6f, 0.85f, 1f));
				btn.AddThemeFontSizeOverride("font_size", 13);

				string captured = s;
				btn.Pressed += () => ApplySuggestion(captured, _input.Text);
				_suggestionsBar.AddChild(btn);
				newButtons.Add(btn);
			}

			UpdateSuggestionHighlight(newButtons);
		}

		private void NavigateSuggestions(int dir)
		{
			var buttons = _suggestionsBar.GetChildren().OfType<Button>().ToList();
			if (buttons.Count == 0) return;

			_suggestionIndex = ((_suggestionIndex + dir + buttons.Count + 2) % (buttons.Count + 1)) - 1;
			UpdateSuggestionHighlight(buttons);
		}

		private void UpdateSuggestionHighlight(List<Button> buttons = null)
		{
			buttons ??= _suggestionsBar.GetChildren().OfType<Button>().ToList();
			for (int i = 0; i < buttons.Count; i++)
			{
				bool selected = i == _suggestionIndex;
				buttons[i].AddThemeColorOverride("font_color",
					selected ? new Color(1f, 1f, 0.5f) : new Color(0.6f, 0.85f, 1f));
				buttons[i].AddThemeStyleboxOverride("normal",
					selected ? MakeHighlightStylebox() : new StyleBoxEmpty());
			}
		}

		private static StyleBoxFlat MakeHighlightStylebox()
		{
			var sb = new StyleBoxFlat();
			sb.BgColor = new Color(0.2f, 0.4f, 0.6f, 0.6f);
			sb.SetCornerRadiusAll(3);
			return sb;
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

		private void ApplySelectedSuggestion()
		{
			var buttons = _suggestionsBar.GetChildren().OfType<Button>().ToList();
			if (buttons.Count == 0) return;

			int idx = _suggestionIndex >= 0 ? _suggestionIndex : 0;
			if (idx < buttons.Count)
				ApplySuggestion(buttons[idx].Text, _input.Text);
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
				string prefix = currentText.EndsWith(' ')
					? string.Join(" ", parts) + " "
					: string.Join(" ", parts.SkipLast(1)) + " ";
				_input.Text = prefix + suggestion + " ";
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

		private void PrintWith(Label template, string text)
		{
			if (string.IsNullOrEmpty(text)) return;

			var label = template.Duplicate() as Label;
			label.Text    = text;
			label.Visible = true;
			_historyContainer.AddChild(label);

			Callable.From(() => { _historyScroll.ScrollVertical = int.MaxValue; }).CallDeferred();
		}

		internal void PrintNormal(string text)  => PrintWith(_templateNormal,  text);
		internal void PrintEcho(string text)    => PrintWith(_templateEcho,    text);
		internal void PrintInfo(string text)    => PrintWith(_templateInfo,    text);
		internal void PrintError(string text)   => PrintWith(_templateError,   text);
		internal void PrintSuccess(string text) => PrintWith(_templateSuccess, text);

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
				execute: (_, console) =>
				{
					console.PrintInfo("Comandos disponíveis:");
					foreach (var c in _commands.Values.OrderBy(c => c.Name))
						console.PrintNormal($"  {c.Usage,-35} {c.Description}");
				},
				getCompletions: _ => new List<string>()
			);

			Register(
				name: "clear",
				usage: "clear",
				description: "Limpa o histórico do console",
				execute: (_, _) =>
				{
					foreach (Node child in _historyContainer.GetChildren())
						if (child is CanvasItem ci && ci.Visible) child.QueueFree();
				},
				getCompletions: _ => new List<string>()
			);

			Register(
				name: "add_item",
				usage: "add_item <id> [quantidade]",
				description: "Adiciona um item ao inventário do jogador",
				execute: (args, console) =>
				{
					if (args.Length < 1)
					{
						console.PrintError("Uso: add_item <id> [quantidade]");
						return;
					}

					ItemDB.Initialize();
					var def = ItemDB.Get(args[0]);
					if (def == null)
					{
						console.PrintError($"Item '{args[0]}' não encontrado. Use 'list_items' para ver os IDs disponíveis.");
						return;
					}

					int qty = 1;
					if (args.Length >= 2 && !int.TryParse(args[1], out qty))
					{
						console.PrintError("Quantidade inválida.");
						return;
					}

					var player = GetLocalPlayer();
					if (player == null)
					{
						console.PrintError("Nenhum jogador encontrado na cena.");
						return;
					}

					bool ok = player.Inventory?.AddItem(def, qty) ?? false;
					if (!ok)
					{
						console.PrintError("Inventário cheio ou item não pôde ser adicionado.");
						return;
					}

					console.PrintSuccess($"+{qty}x {def.Name} adicionado ao inventário.");
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
				execute: (_, console) =>
				{
					ItemDB.Initialize();
					var ids = ItemDB.GetAllIds().OrderBy(id => id).ToList();
					console.PrintInfo($"{ids.Count} item(s) registrado(s):");
					foreach (string id in ids)
					{
						var def = ItemDB.Get(id);
						console.PrintNormal($"  {id,-25} {def?.Name}");
					}
				},
				getCompletions: _ => new List<string>()
			);
		}

		private void Register(string name, string usage, string description, Action<string[], ConsoleUI> execute, Func<string, List<string>> getCompletions)
		{
			_commands[name] = new ConsoleCommands
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
