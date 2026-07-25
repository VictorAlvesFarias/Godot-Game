using Jogo25D.Characters;

namespace Jogo25D.Actions
{
    // Implementacao concreta do indicador visual de uma action (ex: area
    // de impacto no chao do GroundStrike). Uma unica instancia serve TODOS
    // os players que tem aquela action desbloqueada ao mesmo tempo - por
    // isso nunca guarda estado por-jogador aqui dentro; o node visual de
    // fato mora no Player (ver Player.GetOrCreateIndicator).
    public interface IActionIndicator
    {
        // Sem Hide: actions nao "desequipam" como item, entao o proprio
        // Update roda todo frame (mesmo que a action esteja em cooldown ou
        // parada) e decide sozinho quando mostrar/esconder (ex: olhando o
        // input do player), igual o OnPassiveUpdate ja fazia.
        void Update(Player player, ActionDefinition definition, ActionDefinitionData instance, float delta);
    }
}
