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
		public bool IsOpen { get; set; }
		public Player LocalPlayer { get; set; }

		public ScrollContainer HistoryScroll { get; set; }
		public WorldManager WorldManager { get; set; }
        public VBoxContainer HistoryContainer { get; set; }
		public Panel SuggestionsPanel { get; set; }
		public HBoxContainer SuggestionsBar { get; set; }
		public LineEdit InputField { get; set; }

		public Label TemplateNormal { get; set; }
		public Label TemplateEcho { get; set; }
		public Label TemplateInfo { get; set; }
		public Label TemplateError { get; set; }
		public Label TemplateSuccess { get; set; }

		public List<string> CommandHistory { get; set; } = new();
		public int HistoryIndex { get; set; } = -1;
		public string SavedInput { get; set; } = "";

		public int SuggestionIndex { get; set; } = 0;

		public Dictionary<string, ConsoleCommands> Commands { get; set; } = new();

		public override void _Ready()
		{
			HistoryScroll = GetNode<ScrollContainer>("Background/Margin/VBoxContainer/HistoryScroll");
			HistoryContainer = GetNode<VBoxContainer>("Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer");
			SuggestionsPanel = GetNode<Panel>("Background/Margin/VBoxContainer/InputContainer/SuggestionsPanel");
			SuggestionsBar = GetNode<HBoxContainer>("Background/Margin/VBoxContainer/InputContainer/SuggestionsPanel/Margin/SuggestionsBar");
			InputField = GetNode<LineEdit>("Background/Margin/VBoxContainer/InputContainer/InputPanel/Margin/InputRow/Input");

			TemplateNormal = GetNode<Label>("Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer/Normal");
			TemplateEcho = GetNode<Label>("Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer/Echo");
			TemplateInfo = GetNode<Label>("Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer/Info");
			TemplateError = GetNode<Label>("Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer/Error");
			TemplateSuccess = GetNode<Label>("Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer/Success");

			InputField.TextChanged   += OnInputChanged;
			InputField.TextSubmitted += OnInputSubmitted;

            WorldManager = GetTree().Root.GetNodeOrNull<WorldManager>(WorldManager.DEFAULT_NODE_PATH);

            LocalPlayer = WorldManager?.GetLocalPlayer();

            RegisterCommands();

			PrintInfo("Console carregado. Digite 'help' para listar os comandos.");
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

			if (key.Keycode == Key.Right && SuggestionsPanel.Visible)
			{
				NavigateSuggestions(1);
				GetViewport().SetInputAsHandled();
				return;
			}

			if (key.Keycode == Key.Left && SuggestionsPanel.Visible)
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

		public void Toggle()
		{
			IsOpen = !IsOpen;
			Visible = IsOpen;

			if (IsOpen)
			{
				LocalPlayer?.Input?.AddBlocker("console");
				InputField.CallDeferred(LineEdit.MethodName.GrabFocus);
			}
			else
			{
				LocalPlayer?.Input?.RemoveBlocker("console");
			}
		}

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

			InputField.Text = "";
			SuggestionsPanel.Visible = false;
			InputField.CallDeferred(LineEdit.MethodName.GrabFocus);
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

		public void RefreshSuggestions(string text)
		{
			foreach (Node child in SuggestionsBar.GetChildren())
			{
				child.QueueFree();
			}

			var suggestions = ComputeSuggestions(text);
			if (suggestions.Count == 0)
			{
				SuggestionsPanel.Visible = false;
				return;
			}

			SuggestionIndex = 0;
			SuggestionsPanel.Visible = true;

			var newButtons = new List<Button>();
			foreach (string s in suggestions.Take(8))
			{
				var btn = new Button();
				btn.Text = s;
				btn.Flat = true;
				btn.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
				btn.AddThemeFontSizeOverride("font_size", 13);

				string captured = s;
				btn.Pressed += () => ApplySuggestion(captured, InputField.Text);
				SuggestionsBar.AddChild(btn);
				newButtons.Add(btn);
			}

			UpdateSuggestionHighlight(newButtons);
		}

		public void NavigateSuggestions(int dir)
		{
			var buttons = SuggestionsBar.GetChildren().OfType<Button>().ToList();
			if (buttons.Count == 0)
			{
			    return;
			}

			SuggestionIndex = ((SuggestionIndex + dir + buttons.Count + 2) % (buttons.Count + 1)) - 1;
			UpdateSuggestionHighlight(buttons);
		}

		public void UpdateSuggestionHighlight(List<Button> buttons = null)
		{
			buttons ??= SuggestionsBar.GetChildren().OfType<Button>().ToList();
			for (int i = 0; i < buttons.Count; i++)
			{
				bool selected = i == SuggestionIndex;
				buttons[i].AddThemeColorOverride("font_color",
					selected ? new Color(0.62f, 0.36f, 0.92f) : new Color(1f, 1f, 1f));
				buttons[i].AddThemeStyleboxOverride("normal",
					selected ? MakeHighlightStylebox() : new StyleBoxEmpty());
			}
		}

		public static StyleBoxFlat MakeHighlightStylebox()
		{
			var sb = new StyleBoxFlat();
			sb.BgColor = new Color(0.62f, 0.36f, 0.92f, 0.25f);
			sb.SetCornerRadiusAll(3);
			return sb;
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
			var buttons = SuggestionsBar.GetChildren().OfType<Button>().ToList();
			if (buttons.Count == 0)
			{
			    return;
			}

			int idx = SuggestionIndex >= 0 ? SuggestionIndex : 0;
			if (idx < buttons.Count)
			{
				ApplySuggestion(buttons[idx].Text, InputField.Text);
			}
		}

		public void ApplySuggestion(string suggestion, string currentText)
		{
			var parts = currentText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

			if (parts.Length == 0 || (parts.Length == 1 && !currentText.EndsWith(' ')))
			{
				InputField.Text = suggestion + " ";
			}
			else
			{
				string prefix = currentText.EndsWith(' ')
					? string.Join(" ", parts) + " "
					: string.Join(" ", parts.SkipLast(1)) + " ";
				InputField.Text = prefix + suggestion + " ";
			}

			InputField.CaretColumn = InputField.Text.Length;
			RefreshSuggestions(InputField.Text);
		}

		public void NavigateHistory(int dir)
		{
			if (CommandHistory.Count == 0)
			{
				return;
			}

			if (HistoryIndex == -1)
			{
				SavedInput = InputField.Text;
			}

			HistoryIndex = Math.Clamp(HistoryIndex + dir, -1, CommandHistory.Count - 1);

			InputField.Text = HistoryIndex == -1 ? SavedInput : CommandHistory[HistoryIndex];
			InputField.CaretColumn = InputField.Text.Length;
		}

		public void PrintWith(Label template, string text)
		{
			if (string.IsNullOrEmpty(text))
			{
			    return;
			}

			var label = template.Duplicate() as Label;
			label.Text = text;
			label.Visible = true;
			HistoryContainer.AddChild(label);

			Callable.From(() => { HistoryScroll.ScrollVertical = int.MaxValue; }).CallDeferred();
		}

		internal void PrintNormal(string text) => PrintWith(TemplateNormal,  text);
		internal void PrintEcho(string text) => PrintWith(TemplateEcho,    text);
		internal void PrintInfo(string text) => PrintWith(TemplateInfo,    text);
		internal void PrintError(string text) => PrintWith(TemplateError,   text);
		internal void PrintSuccess(string text) => PrintWith(TemplateSuccess, text);

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
					foreach (Node child in HistoryContainer.GetChildren())
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

					ItemDB.Initialize();
					
					var def = ItemDB.Get(args[0]);
					
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

					// Garante que temos a referência correta do player local,
					// mesmo se ele tiver sido spawnado depois do _Ready.
					if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
					{
						if (WorldManager != null)
						{
							LocalPlayer = WorldManager.GetLocalPlayer();
						}
					}

					if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
					{
						console.PrintError("Nenhum jogador encontrado na cena.");
						return;
					}

					LocalPlayer.AddItemRequest(ItemDB.CreateInstance(def.Id));
					
					console.PrintSuccess($"+{qty}x {def.Name} adicionado ao inventÃ¡rio.");
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
				description: "Lista todos os IDs de itens disponÃ­veis no banco de itens",
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

	}
}