namespace Jogo25D.Caves
{
    // Um tipo de caverna. Cada tipo gera seu proprio campo (ruido semeado pelo Id); um bioma pode ter varios
    // tipos (BiomeDefinition.CaveIds) e a celula fica aberta se qualquer um deles a abrir.
    // Distancias em celulas na escala de referencia (multiplicadas por worldScale).
    public abstract class CaveDefinition
    {
        #region Dinamic properties

        public string Id { get; init; }

        // Distorcao de dominio aplicada ao ruido do tipo.
        public float WarpFrequency { get; init; }
        public float WarpAmplitude { get; init; }

        #endregion

        #region Core - Abstract

        // depth = distancia em celulas abaixo da superficie gerada da coluna (y - groundHeight).
        public abstract bool IsOpen(CaveGeneratorSystem caves, int x, int y, int depth);

        #endregion
    }
}
