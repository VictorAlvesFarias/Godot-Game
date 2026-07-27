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
        private const string TypeKey = "$type";

        public static Dictionary ToDictionary(Resource resource)
        {
            var dict = new Dictionary();

            if (resource == null)
            {
                return dict;
            }

            dict[TypeKey] = resource.GetType().AssemblyQualifiedName;

            foreach (var property in GetFields(resource.GetType()))
            {
                dict[property.Name] = ToVariant(property.GetValue(resource), property.PropertyType);
            }

            return dict;
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

        private static PropertyInfo[] GetFields(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<GodotDictionaryFieldAttribute>() != null)
                .ToArray();
        }

        private static Variant ToVariant(object value, Type declaredType)
        {
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

            if (declaredType == typeof(string)) return (string)value ?? "";
            if (declaredType == typeof(int)) return (int)value;
            if (declaredType == typeof(long)) return (long)value;
            if (declaredType == typeof(float)) return (float)value;
            if (declaredType == typeof(bool)) return (bool)value;
            if (declaredType == typeof(Vector2)) return (Vector2)value;
            if (declaredType.IsEnum) return (int)value;

            throw new NotSupportedException($"[GodotDictionaryParser] Tipo nao suportado: {declaredType}");
        }

        private static object FromVariant(Variant variant, Type declaredType)
        {
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

            if (declaredType == typeof(string)) return variant.AsString();
            if (declaredType == typeof(int)) return variant.AsInt32();
            if (declaredType == typeof(long)) return variant.AsInt64();
            if (declaredType == typeof(float)) return variant.AsSingle();
            if (declaredType == typeof(bool)) return variant.AsBool();
            if (declaredType == typeof(Vector2)) return variant.AsVector2();
            if (declaredType.IsEnum) return Enum.ToObject(declaredType, variant.AsInt32());

            throw new NotSupportedException($"[GodotDictionaryParser] Tipo nao suportado: {declaredType}");
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
            if (dict.TryGetValue(TypeKey, out var typeNameVariant))
            {
                var typeName = typeNameVariant.AsString();

                if (!string.IsNullOrEmpty(typeName))
                {
                    var resolved = Type.GetType(typeName);

                    if (resolved != null)
                    {
                        return resolved;
                    }
                }
            }

            return fallbackType;
        }
    }
}
