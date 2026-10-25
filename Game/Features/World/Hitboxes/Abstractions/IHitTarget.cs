using Godot;
using Jogo25D.Characters;
using Jogo25D.Items;

namespace Jogo25D.Hitboxes
{
    // Alvo atingivel pelos hitboxes que nao e jogador (boneco de treino, inimigos). O corpo dele fica na camada de fisica
    // "targets" e o no que implementa a interface e o corpo ou o pai dele. Chamado so no servidor.
    public interface IHitTarget
    {
        void ReceiveHit(DamageInfo damage, Player source);

        // Empurrao do golpe (direcao do hitbox para o alvo); quem nao e empurrado (o boneco) ignora.
        void ApplyKnockback(Vector2 direction, float force)
        {
        }
    }
}
