using Godot;
using Jogo25D.Characters;
using Jogo25D.Features.World.Items.Resources;
using Jogo25D.Hitboxes;

namespace Jogo25D.Items
{
    // Picareta - "Use" (segurar o botao de atacar) mira a celula sob o
    // mouse e acumula progresso de quebra nela (ver Player.UpdateMining).
    // O progresso NAO fica na TileEntity nem na propria celula - vive
    // efemero no Player local enquanto ele segura o botao (ver .docs/
    // blocos-quebraveis.md, "blocos burros" - sem entity por bloco).
    public class ToolDefinition : ItemDefinition
    {
        public float Reach { get; init; } = 120f;
        public float BreakTimeSeconds { get; init; } = 1.2f;
        public float SwingRange { get; init; } = 50f;

        public override void Use(Player player, ItemDefinitionData instance)
        {
            var rawDir = player.Input.MousePosition - player.GlobalPosition;
            var dir = rawDir.LengthSquared() > 0.001f ? rawDir.Normalized() : Vector2.Right;
            var angle = dir.Angle();

            player.SetFacing(!(angle >= -1.5f && angle <= 1.5f));

            // Toca uma vez so - o loop=true da animacao "mining" (SpriteFrames
            // embutido em Player.tscn) cuida de manter rodando sozinha
            // enquanto Player.UpdateAnimation nao trocar pra outra coisa.
            if (player.Sprite.Animation != "mining" || !player.Sprite.IsPlaying())
            {
                player.Sprite.Play("mining");
            }

            // Visual da picareta batendo - mesma cena/estrutura das
            // espadas (MeleeHitbox com swing animado), so que sem chamar
            // Initialize() (fica sem dano/colisao de verdade, e so um
            // efeito). Repete no ritmo do Cooldown do item, nao a cada
            // tick, senao viraria um borrao de dezenas de instancias por
            // segundo enquanto o botao fica segurado.
            if (CanUse(instance) && HitboxScene != null && HitboxScene.Instantiate<Area2D>() is BaseHitbox swing)
            {
                swing.DirectionAngle = angle;
                swing.Owner = player;
                swing.DestroyInAllBodies = false;

                if (swing is MeleeHitbox melee)
                {
                    melee.Offset = dir * SwingRange;
                }

                player.GetParent().AddChild(swing);

                TriggerCooldownTimer(instance);
            }

            // So o dono decide o que esta sendo quebrado e manda pro
            // servidor - os outros peers so precisam ver a animacao acima
            // (senao cada peer tentaria minerar por conta propria).
            if (!player.IsOwner())
            {
                return;
            }

            var layer = player.GetActiveTileLayer();

            if (layer == null)
            {
                player.ResetMining();

                return;
            }

            // Mesma resolucao do indicador (ResolveMiningTargetCell) -
            // livre por padrao (so o alcance importa), restrito ao que da
            // pra alcancar de verdade se o player ligar "toggle_mining_mode".
            var (found, targetCell) = player.ResolveMiningTargetCell(layer, Reach);

            if (!found)
            {
                player.ResetMining();

                return;
            }

            player.UpdateMining(layer, targetCell, BreakTimeSeconds);
        }
    }
}
