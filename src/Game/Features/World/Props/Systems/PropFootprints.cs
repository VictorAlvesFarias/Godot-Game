using Godot;
using System.Collections.Generic;

namespace Jogo25D.Props
{
    // Forma em celulas de cada prop, pintada no editor na camada Footprint da cena dele (instancia de
    // res://src/Game/Godot/Footprint/Footprint.tscn, com a celula (0,0) na ancora). Lida da cena uma vez por id: a colocacao precisa
    // dela antes de o prop existir.
    public static class PropFootprints
    {
        #region Properties

        public const string NodeName = "Footprint";

        private static readonly Vector2I[] AnchorOnly = { Vector2I.Zero };
        private static readonly Dictionary<string, Vector2I[]> _byProp = new();

        #endregion

        #region Core - Consulta

        // Celulas da forma do prop (relativas a ancora); so a ancora quando a cena nao tem forma pintada.
        public static IReadOnlyList<Vector2I> Of(string propId)
        {
            if (string.IsNullOrEmpty(propId))
            {
                return AnchorOnly;
            }

            if (_byProp.TryGetValue(propId, out var cells))
            {
                return cells;
            }

            cells = AnchorOnly;

            var path = PropDB.Get(propId)?.ScenePath;

            if (!string.IsNullOrEmpty(path) && GD.Load<PackedScene>(path)?.Instantiate() is Node root)
            {
                cells = Read(root);
                root.Free();
            }

            _byProp[propId] = cells;

            return cells;
        }

        // Celulas pintadas na camada Footprint do no, ou so a ancora.
        public static Vector2I[] Read(Node root)
        {
            var used = root.GetNodeOrNull<TileMapLayer>(NodeName)?.GetUsedCells();

            if (used == null || used.Count == 0)
            {
                return AnchorOnly;
            }

            var cells = new Vector2I[used.Count];

            for (var i = 0; i < used.Count; i++)
            {
                cells[i] = used[i];
            }

            return cells;
        }

        #endregion
    }
}
