using Godot;
using Godot.Collections;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace Jogo25D.Utils.GodotDictionaryParser
{
    public static class GodotDictionaryParser
    {
        #region Dinamic properties

        private static System.Collections.Generic.Dictionary<string, Type> _typeById;
        private static System.Collections.Generic.Dictionary<Type, string> _idByType;

        #endregion

        #region Core - Conversion

        public static Dictionary ToDictionary(GodotObject source)
        {
            var dict = new Dictionary();

            if (source == null)
            {
                return dict;
            }

            var type = source.GetType();

            if (source is Resource)
            {
                dict["$type"] = ResolveTypeId(type);
            }

            foreach (var property in GetFields(type))
            {
                dict[property.Name] = ToVariant(property.GetValue(source), property.PropertyType);
            }

            return dict;
        }

        public static void ApplyTo(GodotObject target, Dictionary dict)
        {
            if (target == null || dict == null)
            {
                return;
            }

            foreach (var property in GetFields(target.GetType()))
            {
                if (!dict.ContainsKey(property.Name))
                {
                    continue;
                }

                property.SetValue(target, FromVariant(dict[property.Name], property.PropertyType));
            }
        }

        public static bool HasSerializableFields(GodotObject source)
        {
            return source != null && GetFields(source.GetType()).Length > 0;
        }

        public static T ToResource<T>(Dictionary dict) where T : Resource
        {
            return (T)ToResource(dict, typeof(T));
        }

        public static Resource ToResource(Dictionary dict, Type fallbackType = null)
        {
            if (dict == null || dict.Count == 0)
            {
                return null;
            }

            var type = ResolveType(dict, fallbackType);

            if (type == null)
            {
                return null;
            }

            var resource = (Resource)Activator.CreateInstance(type);

            foreach (var property in GetFields(type))
            {
                if (!dict.ContainsKey(property.Name))
                {
                    continue;
                }

                property.SetValue(resource, FromVariant(dict[property.Name], property.PropertyType));
            }

            return resource;
        }

        #endregion

        #region Core - Identidade de type

        private static void EnsureTypeMap()
        {
            if (_typeById != null)
            {
                return;
            }

            _typeById = new System.Collections.Generic.Dictionary<string, Type>();
            _idByType = new System.Collections.Generic.Dictionary<Type, string>();

            foreach (var type in typeof(GodotDictionaryParser).Assembly.GetTypes())
            {
                var attribute = type.GetCustomAttribute<SaveTypeAttribute>();

                if (attribute == null || string.IsNullOrEmpty(attribute.Id))
                {
                    continue;
                }

                if (_typeById.TryGetValue(attribute.Id, out var conflict))
                {
                    GD.PushError($"[GodotDictionaryParser] id de save duplicado {attribute.Id}: {conflict} e {type}");

                    continue;
                }

                _typeById[attribute.Id] = type;
                _idByType[type] = attribute.Id;
            }
        }

        private static string ResolveTypeId(Type type)
        {
            EnsureTypeMap();

            return _idByType.TryGetValue(type, out var id) ? id : type.FullName;
        }

        private static Type ResolveTypeById(string id)
        {
            EnsureTypeMap();

            return _typeById.TryGetValue(id, out var type) ? type : null;
        }

        #endregion

        #region Core - Parsing

        private static PropertyInfo[] GetFields(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<GodotDictionaryFieldAttribute>() != null)
                .ToArray();
        }

        private static Variant ToVariant(object value, Type declaredType)
        {
            if (declaredType == typeof(Dictionary))
            {
                return (Dictionary)value ?? new Dictionary();
            }

            if (IsDictionaryArrayType(declaredType))
            {
                var records = new Godot.Collections.Array();

                if (value is IEnumerable dictionaries)
                {
                    foreach (var item in dictionaries)
                    {
                        records.Add((Dictionary)item);
                    }
                }

                return records;
            }

            if (IsResourceArrayType(declaredType, out _))
            {
                var array = new Godot.Collections.Array();

                if (value is IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        array.Add(ToDictionary(item as Resource));
                    }
                }

                return array;
            }

            if (typeof(Resource).IsAssignableFrom(declaredType))
            {
                return ToDictionary(value as Resource);
            }

            if (declaredType == typeof(string))
            {
                return (string)value ?? "";
            }

            if (declaredType == typeof(int))
            {
                return (int)value;
            }

            if (declaredType == typeof(long))
            {
                return (long)value;
            }

            if (declaredType == typeof(float))
            {
                return (float)value;
            }

            if (declaredType == typeof(bool))
            {
                return (bool)value;
            }

            if (declaredType == typeof(Vector2))
            {
                var vector = (Vector2)value;

                return new Dictionary { { "x", vector.X }, { "y", vector.Y } };
            }

            if (declaredType.IsEnum)
            {
                return (int)value;
            }

            throw new NotSupportedException($"[GodotDictionaryParser] Tipo nao suportado: {declaredType}");
        }

        private static object FromVariant(Variant variant, Type declaredType)
        {
            if (declaredType == typeof(Dictionary))
            {
                return variant.AsGodotDictionary();
            }

            if (IsDictionaryArrayType(declaredType))
            {
                var records = new Godot.Collections.Array<Dictionary>();

                foreach (var element in variant.AsGodotArray())
                {
                    records.Add(element.AsGodotDictionary());
                }

                return records;
            }

            if (IsResourceArrayType(declaredType, out var elementType))
            {
                var list = Activator.CreateInstance(declaredType);
                var addMethod = declaredType.GetMethod("Add", new[] { elementType });

                foreach (var element in variant.AsGodotArray())
                {
                    var resource = ToResource(element.AsGodotDictionary(), elementType);

                    if (resource != null)
                    {
                        addMethod.Invoke(list, new object[] { resource });
                    }
                }

                return list;
            }

            if (typeof(Resource).IsAssignableFrom(declaredType))
            {
                return ToResource(variant.AsGodotDictionary(), declaredType);
            }

            if (declaredType == typeof(string))
            {
                return variant.AsString();
            }

            if (declaredType == typeof(int))
            {
                return variant.AsInt32();
            }

            if (declaredType == typeof(long))
            {
                return variant.AsInt64();
            }

            if (declaredType == typeof(float))
            {
                return variant.AsSingle();
            }

            if (declaredType == typeof(bool))
            {
                return variant.AsBool();
            }

            if (declaredType == typeof(Vector2))
            {
                var vector = variant.AsGodotDictionary();

                return new Vector2(
                    vector.TryGetValue("x", out var x) ? x.AsSingle() : 0f,
                    vector.TryGetValue("y", out var y) ? y.AsSingle() : 0f);
            }

            if (declaredType.IsEnum)
            {
                return Enum.ToObject(declaredType, variant.AsInt32());
            }

            throw new NotSupportedException($"[GodotDictionaryParser] Tipo nao suportado: {declaredType}");
        }

        #endregion

        #region Utils

        private static bool IsDictionaryArrayType(Type declaredType)
        {
            return declaredType == typeof(Godot.Collections.Array<Dictionary>);
        }

        private static bool IsResourceArrayType(Type type, out Type elementType)
        {
            elementType = null;

            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Array<>))
            {
                return false;
            }

            elementType = type.GetGenericArguments()[0];

            return typeof(Resource).IsAssignableFrom(elementType);
        }

        private static Type ResolveType(Dictionary dict, Type fallbackType)
        {
            if (dict.TryGetValue("$type", out var typeNameVariant))
            {
                var typeName = typeNameVariant.AsString();

                if (!string.IsNullOrEmpty(typeName))
                {
                    var mapped = ResolveTypeById(typeName);

                    if (mapped != null)
                    {
                        return mapped;
                    }

                    var resolved = Type.GetType(typeName);

                    if (resolved != null)
                    {
                        return resolved;
                    }
                }
            }

            return fallbackType;
        }

        #endregion
    }
}
