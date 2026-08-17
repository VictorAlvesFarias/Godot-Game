using System;

namespace Jogo25D.Core
{
    // Registro dos nodes estaticos da arvore - os que existem desde o estado inicial e nunca sao
    // reinstanciados. A estrutura espelha a arvore da cena, entao o acesso e sempre
    // Game.<NodeName>.Node ou Game.<NodeName>.<SubNodeName>.Node.
    //
    // O Bootstrap preenche tudo de uma vez e so entao chama NotifyReady. Enquanto IsReady for
    // false o registro nao pode ser lido: quem depende de outro node registra a acao em
    // WhenReady em vez de rodar direto no proprio _Ready.
    public static class Game
    {
        #region Dinamic properties

        public static bool IsReady { get; private set; }

        private static event Action ReadyCallbacks;

        #endregion

        #region Core - Ciclo de inicializacao

        // Roda a acao agora se o Bootstrap ja fechou, senao enfileira pra rodar quando fechar.
        // Isso deixa a classe consumidora indiferente a ordem: node estatico (que fica pronto
        // antes do Bootstrap) e node instanciado em runtime (que fica pronto depois) usam a
        // mesma chamada.
        public static void WhenReady(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (IsReady)
            {
                action();

                return;
            }

            ReadyCallbacks += action;
        }

        internal static void NotifyReady()
        {
            IsReady = true;

            var callbacks = ReadyCallbacks;

            ReadyCallbacks = null;

            callbacks?.Invoke();
        }

        // Chamado pelo Bootstrap no inicio do registro. NAO pode limpar ReadyCallbacks: o _Ready
        // das telas roda ANTES do _Ready do Bootstrap (o Godot propaga de baixo pra cima), entao
        // quando isso aqui executa a fila ja esta cheia de Initialize esperando. Limpar aqui
        // descarta todos - e nenhum botao chega a ser ligado.
        internal static void Reset()
        {
            IsReady = false;
        }

        #endregion

        #region Node references

        public static class Main
        {
            public const string Path = "/root/Main";

            public static Godot.Node2D Node { get; internal set; }
        }

        public static class Managers
        {
            public const string Path = "/root/Main/Managers";

            public static Godot.Node Node { get; internal set; }

            public static class WindowManager
            {
                public const string Path = "/root/Main/Managers/WindowManager";

                public static global::Jogo25D.UI.WindowManager Node { get; internal set; }
            }

            public static class RouterManager
            {
                public const string Path = "/root/Main/Managers/RouterManager";

                public static global::Jogo25D.UI.RouterManager Node { get; internal set; }
            }

            public static class WorldManager
            {
                public const string Path = "/root/Main/Managers/WorldManager";

                public static global::Jogo25D.Systems.WorldManager Node { get; internal set; }
            }

            public static class NetworkManager
            {
                public const string Path = "/root/Main/Managers/NetworkManager";

                public static global::Jogo25D.Network.NetworkManager Node { get; internal set; }
            }

            public static class SessionManager
            {
                public const string Path = "/root/Main/Managers/SessionManager";

                public static global::Jogo25D.Session.SessionManager Node { get; internal set; }
            }

            public static class DimensionManager
            {
                public const string Path = "/root/Main/Managers/DimensionManager";

                public static global::Jogo25D.Dimensions.DimensionManager Node { get; internal set; }
            }

            public static class ChunkStreamingManager
            {
                public const string Path = "/root/Main/Managers/ChunkStreamingManager";

                public static global::Jogo25D.Chunks.ChunkStreamingManager Node { get; internal set; }
            }

            public static class SaveManager
            {
                public const string Path = "/root/Main/Managers/SaveManager";

                public static global::Jogo25D.Systems.SaveManager Node { get; internal set; }
            }
        }

        public static class Ui
        {
            public const string Path = "/root/Main/Ui";

            public static Godot.Node2D Node { get; internal set; }

            public static class StartUI
            {
                public const string Path = "/root/Main/Ui/StartUI";

                public static global::Jogo25D.UI.StartUI Node { get; internal set; }

                public static class PlayButton
                {
                    public const string Path = "/root/Main/Ui/StartUI/MarginContainer/Root/MenuColumn/PlayButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class ExitButton
                {
                    public const string Path = "/root/Main/Ui/StartUI/MarginContainer/Root/MenuColumn/ExitButton";

                    public static Godot.Button Node { get; internal set; }
                }
            }

            public static class PauseUI
            {
                public const string Path = "/root/Main/Ui/PauseUI";

                public static global::Jogo25D.UI.PauseUI Node { get; internal set; }

                public static class ResumeButton
                {
                    public const string Path = "/root/Main/Ui/PauseUI/MarginContainer/Root/MenuColumn/ResumeButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class ExitButton
                {
                    public const string Path = "/root/Main/Ui/PauseUI/MarginContainer/Root/MenuColumn/ExitButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class HostButton
                {
                    public const string Path = "/root/Main/Ui/PauseUI/MarginContainer/Root/MenuColumn/HostButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class PvpButton
                {
                    public const string Path = "/root/Main/Ui/PauseUI/MarginContainer/Root/MenuColumn/PvpButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class MenuButton
                {
                    public const string Path = "/root/Main/Ui/PauseUI/MarginContainer/Root/MenuColumn/MenuButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class PortInput
                {
                    public const string Path = "/root/Main/Ui/PauseUI/MarginContainer/Root/MenuColumn/PortInput";

                    public static Godot.LineEdit Node { get; internal set; }
                }
            }

            public static class HudUI
            {
                public const string Path = "/root/Main/Ui/HudUI";

                public static global::Jogo25D.UI.HudUI Node { get; internal set; }

                public static class Minimap
                {
                    public const string Path = "/root/Main/Ui/HudUI/MarginContainer/TopRightColumn/MinimapPanel/Minimap";

                    public static global::Jogo25D.UI.MinimapUI Node { get; internal set; }
                }

                public static class FpsLabel
                {
                    public const string Path = "/root/Main/Ui/HudUI/MarginContainer/VBoxContainer/FpsLabel";

                    public static Godot.Label Node { get; internal set; }
                }

                public static class LegacyHealthBar
                {
                    public const string Path = "/root/Main/Ui/HudUI/MarginContainer/VBoxContainer/LegacyHealthBar";

                    public static Godot.ProgressBar Node { get; internal set; }
                }

                public static class HealthBar
                {
                    public const string Path = "/root/Main/Ui/HudUI/MarginContainer/VBoxContainer/HealthBar";

                    public static Godot.PanelContainer Node { get; internal set; }
                }

                public static class HealthBarBack
                {
                    public const string Path = "/root/Main/Ui/HudUI/MarginContainer/VBoxContainer/HealthBar/BarBack";

                    public static global::Jogo25D.UI.PhysicalSizeTextureRect Node { get; internal set; }
                }

                public static class HealthBarFill
                {
                    public const string Path = "/root/Main/Ui/HudUI/MarginContainer/VBoxContainer/HealthBar/BarFill";

                    public static global::Jogo25D.UI.RatioFillRect Node { get; internal set; }
                }

                public static class AbilitiesContainer
                {
                    public const string Path = "/root/Main/Ui/HudUI/MarginContainer/VBoxContainer/AbilitiesContainer";

                    public static Godot.HBoxContainer Node { get; internal set; }
                }

                public static class AbilityTemplate
                {
                    public const string Path = "/root/Main/Ui/HudUI/MarginContainer/VBoxContainer/AbilitiesContainer/AbilityTemplate";

                    public static Godot.Panel Node { get; internal set; }
                }

                public static class EffectsContainer
                {
                    public const string Path = "/root/Main/Ui/HudUI/MarginContainer/VBoxContainer/EffectsContainer";

                    public static Godot.HBoxContainer Node { get; internal set; }
                }

                public static class EffectTemplate
                {
                    public const string Path = "/root/Main/Ui/HudUI/MarginContainer/VBoxContainer/EffectsContainer/EffectTemplate";

                    public static Godot.Panel Node { get; internal set; }
                }

                public static class HotkeysContainer
                {
                    public const string Path = "/root/Main/Ui/HudUI/MarginContainer/TopRightColumn/HotkeysContainer";

                    public static Godot.VBoxContainer Node { get; internal set; }
                }

                public static class HotkeySlot0
                {
                    public const string Path = "/root/Main/Ui/HudUI/MarginContainer/TopRightColumn/HotkeysContainer/HotkeySlot0";

                    public static Godot.Panel Node { get; internal set; }
                }

                public static class HotbarContainer
                {
                    public const string Path = "/root/Main/Ui/HudUI/MarginContainer/HotbarContainer";

                    public static Godot.HBoxContainer Node { get; internal set; }
                }

                public static class Slot0
                {
                    public const string Path = "/root/Main/Ui/HudUI/MarginContainer/HotbarContainer/Slot0";

                    public static Godot.Panel Node { get; internal set; }
                }

                public static class Slot0Selected
                {
                    public const string Path = "/root/Main/Ui/HudUI/MarginContainer/HotbarContainer/Slot0Selected";

                    public static Godot.Panel Node { get; internal set; }
                }
            }

            public static class InventoryUI
            {
                public const string Path = "/root/Main/Ui/InventoryUI";

                public static global::Jogo25D.UI.InventoryUI Node { get; internal set; }

                public static class MainControl
                {
                    public const string Path = "/root/Main/Ui/InventoryUI/Root";

                    public static Godot.Control Node { get; internal set; }
                }

                public static class DragPreviewTemplate
                {
                    public const string Path = "/root/Main/Ui/InventoryUI/Root/DragPreviewTemplate";

                    public static Godot.Panel Node { get; internal set; }
                }

                public static class ContextMenu
                {
                    public const string Path = "/root/Main/Ui/InventoryUI/ContextMenu";

                    public static Godot.Panel Node { get; internal set; }
                }

                public static class ContextMenuContainer
                {
                    public const string Path = "/root/Main/Ui/InventoryUI/ContextMenu/MarginContainer/VBoxContainer";

                    public static Godot.VBoxContainer Node { get; internal set; }
                }

                public static class DropSlot
                {
                    public const string Path = "/root/Main/Ui/InventoryUI/Root/Panel/MainPanel/MarginContainer/MainRow/InventoryColumn/DropSlot";

                    public static Godot.Panel Node { get; internal set; }
                }

                public static class HotbarRow
                {
                    public const string Path = "/root/Main/Ui/InventoryUI/Root/Panel/MainPanel/MarginContainer/MainRow/InventoryColumn/HotbarRow";

                    public static Godot.HBoxContainer Node { get; internal set; }
                }

                public static class GridContainer
                {
                    public const string Path = "/root/Main/Ui/InventoryUI/Root/Panel/MainPanel/MarginContainer/MainRow/InventoryColumn/GridScroll/GridContainer";

                    public static Godot.GridContainer Node { get; internal set; }
                }

                public static class CharacterSprite
                {
                    public const string Path = "/root/Main/Ui/InventoryUI/Root/Panel/MainPanel/MarginContainer/MainRow/StatsColumn/SpriteBox2/VBoxContainer/CenterContainer/CharacterSprite";

                    public static Godot.AnimatedSprite2D Node { get; internal set; }
                }

                public static class CharacterNameLabel
                {
                    public const string Path = "/root/Main/Ui/InventoryUI/Root/Panel/MainPanel/MarginContainer/MainRow/StatsColumn/SpriteBox/MarginContainer/VBoxContainer/CharacterNameLabel";

                    public static Godot.Label Node { get; internal set; }
                }

                public static class CharacterHealthLabel
                {
                    public const string Path = "/root/Main/Ui/InventoryUI/Root/Panel/MainPanel/MarginContainer/MainRow/StatsColumn/SpriteBox/MarginContainer/VBoxContainer/CharacterHealthLabel";

                    public static Godot.Label Node { get; internal set; }
                }

                public static class BuffsListContainer
                {
                    public const string Path = "/root/Main/Ui/InventoryUI/Root/Panel/MainPanel/MarginContainer/MainRow/StatsColumn/SpriteBox/MarginContainer/VBoxContainer/BuffsScroll/BuffsListContainer";

                    public static Godot.VBoxContainer Node { get; internal set; }
                }

                public static class EquiparButtonTemplate
                {
                    public const string Path = "/root/Main/Ui/InventoryUI/ContextMenu/MarginContainer/VBoxContainer/EquiparButtonTemplate";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class EmptyPropertyLabelTemplate
                {
                    public const string Path = "/root/Main/Ui/InventoryUI/Root/Panel/MainPanel/MarginContainer/MainRow/StatsColumn/SpriteBox/MarginContainer/VBoxContainer/BuffsScroll/BuffsListContainer/EmptyPropertyLabelTemplate";

                    public static Godot.Label Node { get; internal set; }
                }

                public static class PropertyLabelTemplate
                {
                    public const string Path = "/root/Main/Ui/InventoryUI/Root/Panel/MainPanel/MarginContainer/MainRow/StatsColumn/SpriteBox/MarginContainer/VBoxContainer/BuffsScroll/BuffsListContainer/PropertyLabelTemplate";

                    public static Godot.Label Node { get; internal set; }
                }
            }

            public static class ConsoleUI
            {
                public const string Path = "/root/Main/Ui/ConsoleUI";

                public static global::Jogo25D.UI.ConsoleUI Node { get; internal set; }

                public static class HistoryScroll
                {
                    public const string Path = "/root/Main/Ui/ConsoleUI/Background/Margin/VBoxContainer/HistoryScroll";

                    public static Godot.ScrollContainer Node { get; internal set; }
                }

                public static class HistoryContainer
                {
                    public const string Path = "/root/Main/Ui/ConsoleUI/Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer";

                    public static Godot.VBoxContainer Node { get; internal set; }
                }

                public static class SuggestionsPanel
                {
                    public const string Path = "/root/Main/Ui/ConsoleUI/Background/Margin/VBoxContainer/InputContainer/SuggestionsPanel";

                    public static Godot.Panel Node { get; internal set; }
                }

                public static class SuggestionsBar
                {
                    public const string Path = "/root/Main/Ui/ConsoleUI/Background/Margin/VBoxContainer/InputContainer/SuggestionsPanel/Margin/SuggestionsBar";

                    public static Godot.HBoxContainer Node { get; internal set; }
                }

                public static class InputField
                {
                    public const string Path = "/root/Main/Ui/ConsoleUI/Background/Margin/VBoxContainer/InputContainer/InputPanel/Margin/InputRow/Input";

                    public static Godot.LineEdit Node { get; internal set; }
                }

                public static class TemplateNormal
                {
                    public const string Path = "/root/Main/Ui/ConsoleUI/Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer/Normal";

                    public static Godot.Label Node { get; internal set; }
                }

                public static class TemplateEcho
                {
                    public const string Path = "/root/Main/Ui/ConsoleUI/Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer/Echo";

                    public static Godot.Label Node { get; internal set; }
                }

                public static class TemplateInfo
                {
                    public const string Path = "/root/Main/Ui/ConsoleUI/Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer/Info";

                    public static Godot.Label Node { get; internal set; }
                }

                public static class TemplateError
                {
                    public const string Path = "/root/Main/Ui/ConsoleUI/Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer/Error";

                    public static Godot.Label Node { get; internal set; }
                }

                public static class TemplateSuccess
                {
                    public const string Path = "/root/Main/Ui/ConsoleUI/Background/Margin/VBoxContainer/HistoryScroll/HistoryContainer/Success";

                    public static Godot.Label Node { get; internal set; }
                }

                public static class SuggestionTemplate
                {
                    public const string Path = "/root/Main/Ui/ConsoleUI/Background/Margin/VBoxContainer/InputContainer/SuggestionsPanel/Margin/SuggestionsBar/SuggestionTemplate";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class SuggestionHighlightHolder
                {
                    public const string Path = "/root/Main/Ui/ConsoleUI/Background/SuggestionHighlightHolder";

                    public static Godot.Panel Node { get; internal set; }
                }
            }

            public static class WorldSelectUI
            {
                public const string Path = "/root/Main/Ui/WorldSelectUI";

                public static global::Jogo25D.UI.WorldSelectUI Node { get; internal set; }

                public static class SearchInput
                {
                    public const string Path = "/root/Main/Ui/WorldSelectUI/MarginContainer/Root/SearchInput";

                    public static Godot.LineEdit Node { get; internal set; }
                }

                public static class ListContainer
                {
                    public const string Path = "/root/Main/Ui/WorldSelectUI/MarginContainer/Root/ListScroll/ListContainer";

                    public static Godot.VBoxContainer Node { get; internal set; }
                }

                public static class CreateWorldButton
                {
                    public const string Path = "/root/Main/Ui/WorldSelectUI/MarginContainer/Root/ButtonRow/CreateWorldButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class MultiplayerButton
                {
                    public const string Path = "/root/Main/Ui/WorldSelectUI/MarginContainer/Root/ButtonRow/MultiplayerButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class BackButton
                {
                    public const string Path = "/root/Main/Ui/WorldSelectUI/MarginContainer/Root/ButtonRow/BackButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class WorldRowTemplate
                {
                    public const string Path = "/root/Main/Ui/WorldSelectUI/MarginContainer/Root/ListScroll/ListContainer/WorldRowTemplate";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class WorldRowWithDeleteTemplate
                {
                    public const string Path = "/root/Main/Ui/WorldSelectUI/MarginContainer/Root/ListScroll/ListContainer/WorldRowWithDeleteTemplate";

                    public static Godot.HBoxContainer Node { get; internal set; }
                }
            }

            public static class MultiplayerUI
            {
                public const string Path = "/root/Main/Ui/MultiplayerUI";

                public static global::Jogo25D.UI.MultiplayerUI Node { get; internal set; }

                public static class SearchInput
                {
                    public const string Path = "/root/Main/Ui/MultiplayerUI/MarginContainer/Root/SearchInput";

                    public static Godot.LineEdit Node { get; internal set; }
                }

                public static class ListContainer
                {
                    public const string Path = "/root/Main/Ui/MultiplayerUI/MarginContainer/Root/ListScroll/ListContainer";

                    public static Godot.VBoxContainer Node { get; internal set; }
                }

                public static class AddressInput
                {
                    public const string Path = "/root/Main/Ui/MultiplayerUI/MarginContainer/Root/ConnectRow/AddressInput";

                    public static Godot.LineEdit Node { get; internal set; }
                }

                public static class ConnectButton
                {
                    public const string Path = "/root/Main/Ui/MultiplayerUI/MarginContainer/Root/ConnectRow/ConnectButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class WorldsButton
                {
                    public const string Path = "/root/Main/Ui/MultiplayerUI/MarginContainer/Root/ButtonRow/WorldsButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class BackButton
                {
                    public const string Path = "/root/Main/Ui/MultiplayerUI/MarginContainer/Root/ButtonRow/BackButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class StatusLabel
                {
                    public const string Path = "/root/Main/Ui/MultiplayerUI/MarginContainer/Root/StatusLabel";

                    public static Godot.Label Node { get; internal set; }
                }

                public static class ServerRowTemplate
                {
                    public const string Path = "/root/Main/Ui/MultiplayerUI/MarginContainer/Root/ListScroll/ListContainer/ServerRowTemplate";

                    public static Godot.PanelContainer Node { get; internal set; }
                }
            }

            public static class ErrorModalUI
            {
                public const string Path = "/root/Main/Ui/ErrorModalUI";

                public static global::Jogo25D.UI.ErrorModalUI Node { get; internal set; }

                public static class Background
                {
                    public const string Path = "/root/Main/Ui/ErrorModalUI/Background";

                    public static Godot.Panel Node { get; internal set; }
                }

                public static class MessageLabel
                {
                    public const string Path = "/root/Main/Ui/ErrorModalUI/Background/CenterContainer/Panel/MarginContainer/Root/MessageScroll/MessageLabel";

                    public static Godot.Label Node { get; internal set; }
                }

                public static class OkButton
                {
                    public const string Path = "/root/Main/Ui/ErrorModalUI/Background/CenterContainer/Panel/MarginContainer/Root/OkButton";

                    public static Godot.Button Node { get; internal set; }
                }
            }

            public static class SkillTreeUI
            {
                public const string Path = "/root/Main/Ui/SkillTreeUI";

                public static global::Jogo25D.UI.SkillTreeUI Node { get; internal set; }

                public static class PointsLabel
                {
                    public const string Path = "/root/Main/Ui/SkillTreeUI/Background/MainPanel/MarginContainer/Root/PointsLabel";

                    public static Godot.Label Node { get; internal set; }
                }

                public static class SearchInput
                {
                    public const string Path = "/root/Main/Ui/SkillTreeUI/Background/MainPanel/MarginContainer/Root/Toolbar/SearchInput";

                    public static Godot.LineEdit Node { get; internal set; }
                }

                public static class ResetButton
                {
                    public const string Path = "/root/Main/Ui/SkillTreeUI/Background/MainPanel/MarginContainer/Root/Toolbar/ResetButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class GridContainer
                {
                    public const string Path = "/root/Main/Ui/SkillTreeUI/Background/MainPanel/MarginContainer/Root/Scroll/GridContainer";

                    public static Godot.GridContainer Node { get; internal set; }
                }

                public static class MaxedStyleHolder
                {
                    public const string Path = "/root/Main/Ui/SkillTreeUI/Background/MaxedStyleHolder";

                    public static Godot.Panel Node { get; internal set; }
                }
            }

            public static class DeathScreenUI
            {
                public const string Path = "/root/Main/Ui/DeathScreenUI";

                public static global::Jogo25D.UI.DeathScreenUI Node { get; internal set; }

                public static class Background
                {
                    public const string Path = "/root/Main/Ui/DeathScreenUI/Background";

                    public static Godot.Panel Node { get; internal set; }
                }

                public static class ReviveButton
                {
                    public const string Path = "/root/Main/Ui/DeathScreenUI/Background/CenterContainer/Panel/MarginContainer/Root/ReviveButton";

                    public static Godot.Button Node { get; internal set; }
                }
            }

            public static class LoadingUI
            {
                public const string Path = "/root/Main/Ui/LoadingUI";

                public static global::Jogo25D.UI.LoadingUI Node { get; internal set; }

                public static class StatusLabel
                {
                    public const string Path = "/root/Main/Ui/LoadingUI/Background/CenterContainer/StatusLabel";

                    public static Godot.Label Node { get; internal set; }
                }
            }

            public static class FullscreenMapUI
            {
                public const string Path = "/root/Main/Ui/FullscreenMapUI";

                public static global::Jogo25D.UI.FullscreenMapUI Node { get; internal set; }

                public static class MapView
                {
                    public const string Path = "/root/Main/Ui/FullscreenMapUI/Background/MapPanel/MapView";

                    public static global::Jogo25D.UI.MinimapUI Node { get; internal set; }
                }
            }

            public static class CreateWorldUI
            {
                public const string Path = "/root/Main/Ui/CreateWorldUI";

                public static global::Jogo25D.UI.CreateWorldUI Node { get; internal set; }

                public static class NameInput
                {
                    public const string Path = "/root/Main/Ui/CreateWorldUI/MarginContainer/Root/NameInput";

                    public static Godot.LineEdit Node { get; internal set; }
                }

                public static class AutosaveInput
                {
                    public const string Path = "/root/Main/Ui/CreateWorldUI/MarginContainer/Root/AutosaveInput";

                    public static Godot.SpinBox Node { get; internal set; }
                }

                public static class ProceduralCheck
                {
                    public const string Path = "/root/Main/Ui/CreateWorldUI/MarginContainer/Root/ProceduralCheck";

                    public static Godot.CheckBox Node { get; internal set; }
                }

                public static class ModeOption
                {
                    public const string Path = "/root/Main/Ui/CreateWorldUI/MarginContainer/Root/ModeOption";

                    public static Godot.OptionButton Node { get; internal set; }
                }

                public static class KeyLabel
                {
                    public const string Path = "/root/Main/Ui/CreateWorldUI/MarginContainer/Root/KeyLabel";

                    public static Godot.Label Node { get; internal set; }
                }

                public static class KeyInput
                {
                    public const string Path = "/root/Main/Ui/CreateWorldUI/MarginContainer/Root/KeyInput";

                    public static Godot.LineEdit Node { get; internal set; }
                }

                public static class BackButton
                {
                    public const string Path = "/root/Main/Ui/CreateWorldUI/MarginContainer/Root/ButtonRow/BackButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class CreateButton
                {
                    public const string Path = "/root/Main/Ui/CreateWorldUI/MarginContainer/Root/ButtonRow/CreateButton";

                    public static Godot.Button Node { get; internal set; }
                }
            }

            public static class CharacterSelectUI
            {
                public const string Path = "/root/Main/Ui/CharacterSelectUI";

                public static global::Jogo25D.UI.CharacterSelectUI Node { get; internal set; }

                public static class SearchInput
                {
                    public const string Path = "/root/Main/Ui/CharacterSelectUI/MarginContainer/Root/SearchInput";

                    public static Godot.LineEdit Node { get; internal set; }
                }

                public static class ListContainer
                {
                    public const string Path = "/root/Main/Ui/CharacterSelectUI/MarginContainer/Root/ListScroll/ListContainer";

                    public static Godot.VBoxContainer Node { get; internal set; }
                }

                public static class BackButton
                {
                    public const string Path = "/root/Main/Ui/CharacterSelectUI/MarginContainer/Root/ButtonRow/BackButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class CreateCharacterButton
                {
                    public const string Path = "/root/Main/Ui/CharacterSelectUI/MarginContainer/Root/ButtonRow/CreateCharacterButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class CharacterRowTemplate
                {
                    public const string Path = "/root/Main/Ui/CharacterSelectUI/MarginContainer/Root/ListScroll/ListContainer/CharacterRowTemplate";

                    public static Godot.HBoxContainer Node { get; internal set; }
                }
            }

            public static class CreateCharacterUI
            {
                public const string Path = "/root/Main/Ui/CreateCharacterUI";

                public static global::Jogo25D.UI.CreateCharacterUI Node { get; internal set; }

                public static class NameInput
                {
                    public const string Path = "/root/Main/Ui/CreateCharacterUI/MarginContainer/Root/NameInput";

                    public static Godot.LineEdit Node { get; internal set; }
                }

                public static class BackButton
                {
                    public const string Path = "/root/Main/Ui/CreateCharacterUI/MarginContainer/Root/ButtonRow/BackButton";

                    public static Godot.Button Node { get; internal set; }
                }

                public static class CreateButton
                {
                    public const string Path = "/root/Main/Ui/CreateCharacterUI/MarginContainer/Root/ButtonRow/CreateButton";

                    public static Godot.Button Node { get; internal set; }
                }
            }
        }

        #endregion
    }
}
