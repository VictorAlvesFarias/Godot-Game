using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jogo25D.Save
{
    public static class WorldDocument
    {
        #region Chaves

        public const string TYPE = "$type";
        public const string REF = "$ref";
        public const string ID = "id";
        public const string POSITION = "position";
        public const string STATE = "state";
        public const string DIMENSIONS = "dimensions";
        public const string NODES = "nodes";
        public const string ENTITIES = "Entities";

        #endregion

        #region Core - Escrita

        public static Godot.Collections.Dictionary Escrever(Node2D world, IEnumerable<Node2D> dimensoes, Func<Node2D, IEnumerable<Node2D>> descarregados = null)
        {
            var documento = NovoNo(world, comId: false);

            var lista = new Godot.Collections.Array();

            foreach (var dimensao in dimensoes)
            {
                var entrada = NovoNo(dimensao, comId: false);

                entrada[NODES] = EscreverFilhos(dimensao.GetNodeOrNull<Node2D>(ENTITIES), descarregados?.Invoke(dimensao));

                lista.Add(entrada);
            }

            documento[DIMENSIONS] = lista;

            return documento;
        }

        private static Godot.Collections.Array EscreverFilhos(Node dimensao, IEnumerable<Node2D> descarregados)
        {
            var lista = new Godot.Collections.Array();
            var vistos = new HashSet<ulong>();
            var candidatos = dimensao?.GetChildren().OfType<Node2D>() ?? Enumerable.Empty<Node2D>();

            if (descarregados != null)
            {
                candidatos = candidatos.Concat(descarregados);
            }

            foreach (var filho in candidatos)
            {
                if (!SaveSerializer.EhPersistivel(filho) || !vistos.Add(filho.GetInstanceId()))
                {
                    continue;
                }

                var entrada = NovoNo(filho);

                if (entrada != null)
                {
                    lista.Add(entrada);
                }
            }

            return lista;
        }

        private static Godot.Collections.Dictionary NovoNo(Node2D node, bool comId = true)
        {
            var descricao = SaveSerializer.Descrever(node);
            var entrada = new Godot.Collections.Dictionary
            {
                { TYPE, descricao.Type },
            };

            var identidade = string.IsNullOrEmpty(descricao.Ref) ? node.Name.ToString() : IdentidadeExterna(node);

            if (comId && !string.IsNullOrEmpty(identidade))
            {
                entrada[ID] = identidade;
            }

            if (!string.IsNullOrEmpty(descricao.Ref))
            {
                if (string.IsNullOrEmpty(identidade))
                {
                    return null;
                }

                entrada[REF] = string.Format(descricao.Ref, entrada.TryGetValue(ID, out var id) ? id.AsString() : "");

                return entrada;
            }

            var estado = SaveSerializer.Escrever(node);

            if (comId)
            {
                estado[POSITION] = new Godot.Collections.Dictionary { { "x", node.Position.X }, { "y", node.Position.Y } };
            }

            entrada[STATE] = estado;

            return entrada;
        }

        private static string IdentidadeExterna(Node2D node)
        {
            return node is Jogo25D.Characters.Player player ? player.CharacterId : node.Name.ToString();
        }

        public static Godot.Collections.Dictionary NovaReferencia(string tipo, string id, string caminho)
        {
            return new Godot.Collections.Dictionary
            {
                { TYPE, tipo },
                { ID, id },
                { REF, caminho },
            };
        }

        #endregion

        #region Core - Leitura

        public static Godot.Collections.Array Dimensoes(Godot.Collections.Dictionary documento)
        {
            return documento != null && documento.TryGetValue(DIMENSIONS, out var lista)
                ? lista.AsGodotArray()
                : new Godot.Collections.Array();
        }

        public static Godot.Collections.Array Nos(Godot.Collections.Dictionary dimensao)
        {
            return dimensao != null && dimensao.TryGetValue(NODES, out var lista)
                ? lista.AsGodotArray()
                : new Godot.Collections.Array();
        }

        public static Godot.Collections.Dictionary Estado(Godot.Collections.Dictionary entrada)
        {
            return entrada != null && entrada.TryGetValue(STATE, out var estado)
                ? estado.AsGodotDictionary()
                : new Godot.Collections.Dictionary();
        }

        public static string Texto(Godot.Collections.Dictionary entrada, string chave)
        {
            return entrada != null && entrada.TryGetValue(chave, out var valor) ? valor.AsString() : "";
        }

        public static Godot.Collections.Dictionary EstadoDe(Resource meta)
        {
            var estado = new Godot.Collections.Dictionary();

            foreach (var (chave, valor) in Jogo25D.Utils.GodotDictionaryParser.GodotDictionaryParser.ToDictionary(meta))
            {
                var nome = chave.AsString();

                if (nome == TYPE)
                {
                    continue;
                }

                estado[char.ToLowerInvariant(nome[0]) + nome[1..]] = valor;
            }

            return estado;
        }

        public static T MetaDe<T>(Godot.Collections.Dictionary documento) where T : Resource
        {
            var estado = new Godot.Collections.Dictionary();

            foreach (var (chave, valor) in Estado(documento))
            {
                var nome = chave.AsString();

                estado[char.ToUpperInvariant(nome[0]) + nome[1..]] = valor;
            }

            return Jogo25D.Utils.GodotDictionaryParser.GodotDictionaryParser.ToResource<T>(estado);
        }

        public static bool EhReferencia(Godot.Collections.Dictionary entrada)
        {
            return entrada != null && entrada.ContainsKey(REF);
        }

        public static Node2D Construir(Godot.Collections.Dictionary entrada)
        {
            var caminho = SaveSerializer.CenaDe(Texto(entrada, TYPE));

            if (string.IsNullOrEmpty(caminho) || GD.Load<PackedScene>(caminho) is not PackedScene cena)
            {
                GD.PushError($"[WorldDocument] cena nao encontrada para \"{Texto(entrada, TYPE)}\"");

                return null;
            }

            var node = cena.Instantiate<Node2D>();
            var id = Texto(entrada, ID);

            if (!string.IsNullOrEmpty(id))
            {
                node.Name = id;
            }

            var estado = Estado(entrada);

            if (estado.TryGetValue(POSITION, out var posicao))
            {
                var par = posicao.AsGodotDictionary();

                node.Position = new Vector2(par["x"].AsSingle(), par["y"].AsSingle());
            }

            SaveSerializer.Ler(node, estado);

            return node;
        }

        #endregion
    }
}
