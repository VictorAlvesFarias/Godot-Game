using Godot;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Properties
{
    public partial class AttackPropertyData : BasePropertyData
    {
        [Export, GodotDictionaryField]
        public float AttackRange { get; set; } = 80f;

        [Export, GodotDictionaryField]
        public float AttackArea { get; set; } = 25f;

        // Zero por padrao (nao 200) de proposito: Resolver.Resolve agrega
        // essa classe combinando varias fontes (item + player + skill tree).
        // Um default nao-zero aqui faria QUALQUER arma/acao que nunca
        // definiu KnockbackForce explicitamente (ex: arcos, Fireball,
        // GroundStrike) ganhar empurrao mesmo sem ninguem ter pedido.
        [Export, GodotDictionaryField]
        public float KnockbackForce { get; set; } = 0f;

        [Export, GodotDictionaryField]
        public float ProjectileSpeed { get; set; } = 500f;
    }
}