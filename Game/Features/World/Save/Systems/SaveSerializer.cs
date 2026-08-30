using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Save
{
    public static class SaveSerializer
    {
        #region Core - Identidade de tipo

        private static Dictionary<string, SaveSceneAttribute> _porTipo;
        private static Dictionary<Type, SaveSceneAttribute> _porClasse;

        private static void GarantirMapa()
        {
            if (_porTipo != null)
            {
                return;
            }

            _porTipo = new Dictionary<string, SaveSceneAttribute>();
            _porClasse = new Dictionary<Type, SaveSceneAttribute>();

            foreach (var tipo in typeof(SaveSerializer).Assembly.GetTypes())
            {
                var atributo = tipo.GetCustomAttribute<SaveSceneAttribute>(inherit: false);

                if (atributo == null)
                {
                    continue;
                }

                if (_porTipo.TryGetValue(atributo.Type, out var conflito))
                {
                    GD.PushError($"[SaveSerializer] tipo duplicado \"{atributo.Type}\": {conflito.Scene} e {tipo}");

                    continue;
                }

                _porTipo[atributo.Type] = atributo;
                _porClasse[tipo] = atributo;
            }
        }

        public static SaveSceneAttribute Descrever(Node node)
        {
            GarantirMapa();

            return node != null && _porClasse.TryGetValue(node.GetType(), out var atributo) ? atributo : null;
        }

        public static bool EhPersistivel(Node node)
        {
            return Descrever(node) != null;
        }

        public static string CenaDe(string tipo)
        {
            GarantirMapa();

            return _porTipo.TryGetValue(tipo, out var atributo) ? atributo.Scene : null;
        }

        #endregion

        #region Core - Estado

        public static Godot.Collections.Dictionary Escrever(Node node)
        {
            var estado = new Godot.Collections.Dictionary();

            foreach (var propriedade in Declaradas(node.GetType()))
            {
                var atributo = propriedade.GetCustomAttribute<SaveAttribute>();
                var chave = string.IsNullOrEmpty(atributo.Name) ? CamelCase(propriedade.Name) : atributo.Name;

                estado[chave] = ParaVariant(propriedade.GetValue(node));
            }

            return estado;
        }

        public static void Ler(Node node, Godot.Collections.Dictionary estado)
        {
            if (node == null || estado == null)
            {
                return;
            }

            foreach (var propriedade in Declaradas(node.GetType()))
            {
                var atributo = propriedade.GetCustomAttribute<SaveAttribute>();
                var chave = string.IsNullOrEmpty(atributo.Name) ? CamelCase(propriedade.Name) : atributo.Name;

                if (!estado.TryGetValue(chave, out var valor))
                {
                    continue;
                }

                propriedade.SetValue(node, DeVariant(valor, propriedade.PropertyType));
            }
        }

        #endregion

        #region Utils

        private static PropertyInfo[] Declaradas(Type tipo)
        {
            return tipo
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<SaveAttribute>() != null)
                .ToArray();
        }

        private static string CamelCase(string nome)
        {
            return string.IsNullOrEmpty(nome) ? nome : char.ToLowerInvariant(nome[0]) + nome[1..];
        }

        private static Variant ParaVariant(object valor)
        {
            return valor switch
            {
                null => new Variant(),
                string s => s,
                bool b => b,
                int i => i,
                long l => l,
                float f => f,
                double d => d,
                Vector2 vector => new Godot.Collections.Dictionary { { "x", vector.X }, { "y", vector.Y } },
                Godot.Collections.Dictionary dict => dict,
                Godot.Collections.Array array => array,
                Resource resource => GodotDictionaryParser.ToDictionary(resource),
                Variant v => v,
                _ => throw new NotSupportedException($"[SaveSerializer] tipo nao suportado: {valor.GetType()}. Use primitivo, Dictionary ou Array."),
            };
        }

        private static object DeVariant(Variant valor, Type tipo)
        {
            if (tipo == typeof(string)) return valor.AsString();
            if (tipo == typeof(bool)) return valor.AsBool();
            if (tipo == typeof(int)) return valor.AsInt32();
            if (tipo == typeof(long)) return valor.AsInt64();
            if (tipo == typeof(float)) return valor.AsSingle();
            if (tipo == typeof(double)) return valor.AsDouble();
            if (tipo == typeof(Godot.Collections.Dictionary)) return valor.AsGodotDictionary();
            if (tipo == typeof(Godot.Collections.Array)) return valor.AsGodotArray();

            if (tipo == typeof(Vector2))
            {
                var par = valor.AsGodotDictionary();

                return new Vector2(par["x"].AsSingle(), par["y"].AsSingle());
            }

            if (typeof(Resource).IsAssignableFrom(tipo))
            {
                return GodotDictionaryParser.ToResource(valor.AsGodotDictionary(), tipo);
            }

            throw new NotSupportedException($"[SaveSerializer] tipo nao suportado: {tipo}. Use primitivo, Dictionary ou Array.");
        }

        #endregion
    }
}
