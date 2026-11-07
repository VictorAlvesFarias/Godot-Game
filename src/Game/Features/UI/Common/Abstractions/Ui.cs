using Godot;
using System;
using System.Collections.Generic;

namespace Jogo25D.UI
{
    public static class Ui
    {
        #region Dinamic properties

        private static readonly Dictionary<Type, ScreenUI> Screens = new();

        #endregion

        #region Core - Registro

        public static void Register(ScreenUI screen)
        {
            if (screen == null)
            {
                return;
            }

            Screens[screen.GetType()] = screen;
        }

        public static void Unregister(ScreenUI screen)
        {
            if (screen == null)
            {
                return;
            }

            if (Screens.TryGetValue(screen.GetType(), out var current) && current == screen)
            {
                Screens.Remove(screen.GetType());
            }
        }

        #endregion

        #region Core - Consulta

        public static T Get<T>() where T : ScreenUI
        {
            if (Screens.TryGetValue(typeof(T), out var screen) && GodotObject.IsInstanceValid(screen))
            {
                return (T)screen;
            }

            return null;
        }

        #endregion
    }
}
