using Godot;
using Jogo25D.Characters;
using Jogo25D.Core;
using Jogo25D.Systems;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.UI
{
    // Menu do jogo: barra lateral com uma aba por pagina (Inventario, Criacao, Habilidades, Mapa). Cada tecla abre o menu
    // ja na pagina dela; a mesma tecla na pagina aberta fecha, e Esc fecha. O conteudo fica nos cards de cada pagina, que
    // recebem o jogador local e os avisos de abrir, fechar e inventario mudado.
    public partial class GameMenuUI : ScreenUI
    {
        public enum Page { Inventory, Crafting, Skills, Map }

        // Aba, no da pagina e acao de teclado de cada pagina.
        private static readonly (Page Page, string Tab, string Node, string Action)[] PageSetup =
        {
            (Page.Inventory, "InventoryTab", "InventoryPage", "toggle_inventory"),
            (Page.Crafting, "CraftingTab", "CraftingPage", "toggle_crafting"),
            (Page.Skills, "SkillsTab", "SkillsPage", "toggle_skill_tree"),
            (Page.Map, "MapTab", "MapPage", "toggle_map"),
        };

        #region Node children references

        public Dictionary<Page, Button> Tabs { get; } = new();
        public Dictionary<Page, Control> PageNodes { get; } = new();

        #endregion

        #region Dinamic properties

        private const string Blocker = "game_menu";

        public Player LocalPlayer { get; private set; }
        public PlayerInput PlayerInput => LocalPlayer?.Input;
        public Page CurrentPage { get; private set; } = Page.Inventory;

        public override bool IsOverlay => true;

        private readonly Dictionary<Page, List<CardUI>> _cards = new();

        public IEnumerable<CardUI> AllCards => _cards.Values.SelectMany(cards => cards);

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            foreach (var (page, tabName, nodeName, _) in PageSetup)
            {
                var tab = GetNode<Button>($"Root/Center/Layout/Sidebar/{tabName}");
                var pageNode = GetNode<Control>($"Root/Center/Layout/Pages/{nodeName}");

                tab.Pressed += () => ShowPage(page);

                Tabs[page] = tab;
                PageNodes[page] = pageNode;
                _cards[page] = pageNode.FindChildren("*", "", true, false).OfType<CardUI>().ToList();
            }

            FindLocalPlayer();
        }

        public override void _ExitTree()
        {
            if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
            {
                LocalPlayer.InventoryChanged -= OnInventoryChanged;
            }

            base._ExitTree();
        }

        // As teclas do menu sao lidas depois da interface: com um campo de texto em foco (busca), a tecla vira texto e
        // nao abre nem troca pagina.
        public override void _UnhandledInput(InputEvent @event)
        {
            base._UnhandledInput(@event);

            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                FindLocalPlayer();
            }

            if (@event.IsEcho() || (PlayerInput != null && PlayerInput.IsBlockedByOther(Blocker)))
            {
                return;
            }

            foreach (var (page, _, _, action) in PageSetup)
            {
                if (@event.IsActionPressed(action))
                {
                    Toggle(page);
                    GetViewport().SetInputAsHandled();

                    return;
                }
            }

            if (@event.IsActionPressed("ui_cancel") && Visible)
            {
                Close();
                GetViewport().SetInputAsHandled();
            }
        }

        #endregion

        #region Core - Menu

        // Fechado: abre na pagina. Aberto em outra pagina: troca. Aberto na mesma pagina: fecha.
        public void Toggle(Page page)
        {
            if (!Visible)
            {
                Open(page);
            }
            else if (CurrentPage != page)
            {
                ShowPage(page);
            }
            else
            {
                Close();
            }
        }

        public void Open(Page page)
        {
            if (LocalPlayer?.Inventory == null)
            {
                FindLocalPlayer();

                if (LocalPlayer?.Inventory == null)
                {
                    return;
                }
            }

            RouterContext.Open(this);

            if (!Visible)
            {
                return;
            }

            PlayerInput?.AddBlocker(Blocker);

            ShowPage(page, force: true);
        }

        public void Close()
        {
            if (!Visible)
            {
                return;
            }

            foreach (var card in _cards[CurrentPage])
            {
                card.OnClosed();
            }

            RouterContext.Close(this);

            PlayerInput?.RemoveBlocker(Blocker);
        }

        // Troca a pagina visivel: os cards da pagina que sai fecham, e os da que entra abrem.
        public void ShowPage(Page page, bool force = false)
        {
            if (page == CurrentPage && !force)
            {
                SyncTabs();

                return;
            }

            if (page != CurrentPage)
            {
                foreach (var card in _cards[CurrentPage])
                {
                    card.OnClosed();
                }
            }

            CurrentPage = page;

            foreach (var (key, node) in PageNodes)
            {
                node.Visible = key == page;
            }

            SyncTabs();

            foreach (var card in _cards[page])
            {
                card.OnOpened();
            }
        }

        private void SyncTabs()
        {
            foreach (var (page, tab) in Tabs)
            {
                tab.SetPressedNoSignal(page == CurrentPage);
            }
        }

        #endregion

        #region Core - Jogador

        public void FindLocalPlayer()
        {
            if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
            {
                LocalPlayer.InventoryChanged -= OnInventoryChanged;
            }

            LocalPlayer = Players.GetLocal();

            if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
            {
                return;
            }

            LocalPlayer.InventoryChanged += OnInventoryChanged;

            foreach (var card in AllCards)
            {
                card.Bind(LocalPlayer);
            }
        }

        public void OnInventoryChanged()
        {
            if (!IsInstanceValid(this))
            {
                return;
            }

            foreach (var card in AllCards)
            {
                card.OnInventoryChanged();
            }
        }

        #endregion
    }
}
