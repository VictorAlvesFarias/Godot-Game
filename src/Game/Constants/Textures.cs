namespace Jogo25D.Constants
{
    public static class Textures
    {
        public static class Characters
        {
            public const string CHARACTER_FRAMES = "res://src/Game/Assets/Textures/Characters/CharacterFrames.tres";
            public const string CHARACTER_TEXTURE = "res://src/Game/Assets/Textures/Characters/character.png";
        }

        public static class Tiles
        {
            public const string WORLD_TILE_SET = "res://src/Game/Assets/Textures/Tiles/world_biomes_tileset.tres";
            public const string TX_CHEST_ANIMATION_TEXTURE = "res://src/Game/Assets/Textures/Tiles/TX Chest Animation.png";
            public const string TX_TILESET_GROUND_TEXTURE = "res://src/Game/Assets/Textures/Tiles/lime_ground/lime_ground_texture.png";
            public const string TX_VILLAGE_PROPS_TEXTURE = "res://src/Game/Assets/Textures/Tiles/props/props_texture.png";
            public const string TILE_03_TEXTURE = "res://src/Game/Assets/Textures/Tiles/olive_ground/olive_ground_texture.png";
        }

        // Icones de acoes e efeitos. As imagens dos itens seguem a convencao de pasta (ItemDefinition).
        public static class Items
        {
            public const string DASH_ICON = "res://src/Game/Assets/Textures/Items/dash/icon.png";
            public const string FIREBALL_ICON = "res://src/Game/Assets/Textures/Items/fireball/icon.png";
            public const string GROUND_STRIKE_ICON = "res://src/Game/Assets/Textures/Items/ground_strike/icon.png";
            public const string EFFECT_POISON_DAMAGE_ICON = "res://src/Game/Assets/Textures/Items/effect_poison_damage/icon.png";

            // Efeitos que usam como icone o da pocao que os aplica.
            public const string FIRE_DAMAGE_POTION_ICON = "res://src/Game/Assets/Textures/Items/fire_damage_potion/icon.png";
            public const string HEALTH_REGEN_POTION_ICON = "res://src/Game/Assets/Textures/Items/health_regen_potion/icon.png";
            public const string SPEED_POTION_ICON = "res://src/Game/Assets/Textures/Items/speed_potion/icon.png";
            public const string INSTANT_HEAL_POTION_ICON = "res://src/Game/Assets/Textures/Items/instant_heal_potion/icon.png";
        }

        public static class Skills
        {
            public const string VITALITY_1_ICON = "res://src/Game/Assets/Textures/Skills/vitality_1/icon.png";
            public const string VITALITY_2_ICON = "res://src/Game/Assets/Textures/Skills/vitality_2/icon.png";
            public const string SWIFT_STEPS_ICON = "res://src/Game/Assets/Textures/Skills/swift_steps/icon.png";
            public const string ARCANE_DASH_ICON = "res://src/Game/Assets/Textures/Skills/arcane_dash/icon.png";
            public const string POWER_1_ICON = "res://src/Game/Assets/Textures/Skills/power_1/icon.png";
        }

        public static class Objects
        {
            public const string FIREBALL_SPRITE = "res://src/Game/Assets/Textures/Objects/fireball/sprite.png";
            public const string GROUND_STRIKE_SPRITE = "res://src/Game/Assets/Textures/Objects/ground_strike/sprite.png";
        }
    }
}
