namespace Jogo25D.Ores
{
    // Um minerio do subsolo: o bloco que ele vira e onde aparece. A celula do interior e minerio quando esta a pelo menos
    // MinDepth abaixo da superficie gerada da coluna e o ruido do minerio (semeado pela Tag) passa o Threshold; o ruido
    // continuo forma veios. Distancias em celulas na escala de referencia (multiplicadas por worldScale).
    public sealed class OreDefinition
    {
        #region Dinamic properties

        public string Id { get; init; }
        public string BlockId { get; init; }
        public int MinDepth { get; init; }
        public float Frequency { get; init; }
        public float Threshold { get; init; }
        public string Tag { get; init; }

        #endregion
    }
}
