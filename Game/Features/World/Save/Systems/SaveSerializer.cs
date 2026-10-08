using Godot;
using Jogo25D.Utils.GodotDictionaryParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Jogo25D.Save
{
    public static class SaveSerializer
    {
        #region Core - Identidade de type

        private static Dictionary<string, SaveSceneAttribute> _byType;
        private static Dictionary<Type, SaveSceneAttribute> _byClass;

        private static void EnsureMap()
        {
            if (_byType != null)
            {
                return;
            }

            _byType = new Dictionary<string, SaveSceneAttribute>();
            _byClass = new Dictionary<Type, SaveSceneAttribute>();

            foreach (var type in typeof(SaveSerializer).Assembly.GetTypes())
            {
                var attribute = type.GetCustomAttribute<SaveSceneAttribute>(inherit: false);

                if (attribute == null)
                {
                    continue;
                }

                if (_byType.TryGetValue(attribute.Type, out var conflict))
                {
                    GD.PushError($"[SaveSerializer] tipo duplicado \"{attribute.Type}\": {conflict.Scene} e {type}");

                    continue;
                }

                _byType[attribute.Type] = attribute;
                _byClass[type] = attribute;
            }
        }

        public static SaveSceneAttribute Describe(Node node)
        {
            EnsureMap();

            return node != null && _byClass.TryGetValue(node.GetType(), out var attribute) ? attribute : null;
        }

        public static bool IsPersistable(Node node)
        {
            return Describe(node) != null;
        }

        public static string SceneOf(string type)
        {
            EnsureMap();

            return _byType.TryGetValue(type, out var attribute) ? attribute.Scene : null;
        }

        #endregion

        #region Core - StateOf

        public static Godot.Collections.Dictionary Write(Node node)
        {
            var state = new Godot.Collections.Dictionary();

            foreach (var property in DeclaredProperties(node.GetType()))
            {
                var attribute = property.GetCustomAttribute<SaveAttribute>();
                var key = string.IsNullOrEmpty(attribute.Name) ? CamelCase(property.Name) : attribute.Name;

                state[key] = ToVariant(property.GetValue(node));
            }

            (node as ISaveState)?.WriteState(state);

            return state;
        }

        public static void Read(Node node, Godot.Collections.Dictionary state)
        {
            if (node == null || state == null)
            {
                return;
            }

            foreach (var property in DeclaredProperties(node.GetType()))
            {
                var attribute = property.GetCustomAttribute<SaveAttribute>();
                var key = string.IsNullOrEmpty(attribute.Name) ? CamelCase(property.Name) : attribute.Name;

                if (!state.TryGetValue(key, out var value))
                {
                    continue;
                }

                property.SetValue(node, FromVariant(value, property.PropertyType));
            }

            (node as ISaveState)?.ReadState(state);
        }

        #endregion

        #region Utils

        private static PropertyInfo[] DeclaredProperties(Type type)
        {
            return type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<SaveAttribute>() != null)
                .ToArray();
        }

        private static string CamelCase(string name)
        {
            return string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
        }

        private static Variant ToVariant(object value)
        {
            return value switch
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
                _ => throw new NotSupportedException($"[SaveSerializer] tipo nao suportado: {value.GetType()}. Use primitivo, Dictionary ou Array."),
            };
        }

        private static object FromVariant(Variant value, Type type)
        {
            if (type == typeof(string))
            {
                return value.AsString();
            }

            if (type == typeof(bool))
            {
                return value.AsBool();
            }

            if (type == typeof(int))
            {
                return value.AsInt32();
            }

            if (type == typeof(long))
            {
                return value.AsInt64();
            }

            if (type == typeof(float))
            {
                return value.AsSingle();
            }

            if (type == typeof(double))
            {
                return value.AsDouble();
            }

            if (type == typeof(Godot.Collections.Dictionary))
            {
                return value.AsGodotDictionary();
            }

            if (type == typeof(Godot.Collections.Array))
            {
                return value.AsGodotArray();
            }

            if (type == typeof(Vector2))
            {
                var pair = value.AsGodotDictionary();

                return new Vector2(pair["x"].AsSingle(), pair["y"].AsSingle());
            }

            if (typeof(Resource).IsAssignableFrom(type))
            {
                return GodotDictionaryParser.ToResource(value.AsGodotDictionary(), type);
            }

            throw new NotSupportedException($"[SaveSerializer] tipo nao suportado: {type}. Use primitivo, Dictionary ou Array.");
        }

        #endregion
    }
}
