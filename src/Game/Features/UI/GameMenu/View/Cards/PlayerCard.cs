using Godot;

namespace Jogo25D.UI
{
    // Card "Player": o personagem local, com a mesma animacao, quadro e direcao do jogo.
    public partial class PlayerCard : CardUI
    {
        #region Node children references

        public AnimatedSprite2D CharacterSprite { get; private set; }

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            CharacterSprite = GetNode<AnimatedSprite2D>("Box/VBoxContainer/CenterContainer/CharacterSprite");
            CharacterSprite.Play("idle");
        }

        public override void _Process(double delta)
        {
            if (IsOpen)
            {
                UpdateCharacterSprite();
            }
        }

        #endregion

        #region Core - Sprite

        public void UpdateCharacterSprite()
        {
            if (!HasPlayer || LocalPlayer.Sprite == null)
            {
                return;
            }

            var playerAnimation = LocalPlayer.Sprite.Animation;

            if (CharacterSprite.Animation != playerAnimation || !CharacterSprite.IsPlaying())
            {
                CharacterSprite.Play(playerAnimation);
            }

            CharacterSprite.Frame = LocalPlayer.Sprite.Frame;
            CharacterSprite.FlipH = LocalPlayer.FacingLeft();
        }

        #endregion
    }
}
