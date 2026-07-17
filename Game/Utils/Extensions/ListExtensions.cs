using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jogo25D.Utils.Extensions
{
    public static class ListExtensions
    {
        public static Godot.Collections.Array<T> ToGodotArray<[MustBeVariant] T>(this List<T> list)
        {
            var result = new Godot.Collections.Array<T>();

            foreach (var item in list)
            {
                result.Add(item);
            }

            return result;
        }
    }
}
