using Godot;

namespace Jogo25D
{
    public static class NodePaths
    {
        public static class Hud
        {
            public const string MarginContainer = "MarginContainer";
            public const string VBoxContainer = MarginContainer + "/VBoxContainer";
            public const string FpsLabel = VBoxContainer + "/FpsLabel";
            public const string HealthBar = VBoxContainer + "/HealthBar";
            public const string HealthBarLabel = HealthBar + "/HealthBarLabel";
            public const string EquippedWeaponLabel = VBoxContainer + "/EquippedWeaponLabel";
            public const string AbilitiesContainer = VBoxContainer + "/AbilitiesContainer";
            public const string Minimap = "MarginContainer/MinimapPanel/Minimap";
            public const string AbilityPanelName = "AbilityPanel";
            public const string AbilityCooldownFillName = "CooldownFill";
            public const string AbilityTimerLabelName = "TimerLabel";
            public const string AbilityNameLabelName = "AbilityNameLabel";
        }

        public static class Player
        {
            public const string SpriteBorder = "Sprite/Border";
            public const string Inventory = "Inventory";
        }

        public static class Entities
        {
            public const string PlatformSprite = "Sprite2D";
            public const string PlatformCollisionShape = "CollisionShape2D";
        }

        public static class Actions
        {
            public const string DashParticles = "DashParticles";
        }

        public static class InventoryUI
        {
            public const string Root = "CenterContainer";
            public const string GridContainer = "CenterContainer/MainPanel/MarginContainer/VBoxContainer/GridContainer";
            public const string ContextMenuPanel = "ContextMenu";
            public const string ContextMenuVBox = "ContextMenu/VBoxContainer";
            public const string SlotMarginContainer = "MarginContainer";
            public const string SlotCenterContainer = "CenterContainer";
            public const string SlotIcon = "Icon";
            public const string SlotNameLabel = "NameLabel";
            public const string SlotQuantityLabel = "QuantityLabel";
            public const string DragPreviewIcon = "Icon";
        }

        public static class PauseMenu
        {
            public const string ResetButton = "Panel/VBoxContainer/ResetButton";
            public const string ResumeButton = "Panel/VBoxContainer/NetworkContainer/ResumeButton";
            public const string ExitButton = "Panel/VBoxContainer/NetworkContainer/ExitButton";
            public const string HostButton = "Panel/VBoxContainer/NetworkContainer/HostButton";
            public const string ConnectButton = "Panel/VBoxContainer/NetworkContainer/ConnectButton";
            public const string PortInput = "Panel/VBoxContainer/NetworkContainer/PortInput";
            public const string AddressInput = "Panel/VBoxContainer/NetworkContainer/AddressInput";
            public const string StatusLabel = "Panel/VBoxContainer/NetworkContainer/StatusLabel";
        }

        public static class Network
        {
            public const string MainRoot = "Main";
            public const string RootNetworkManager = "/root/Main/NetworkManager";
        }
    }
}

