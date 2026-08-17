using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Jogo25D.Core
{
    public partial class Bootstrap : Node2D
    {
        #region Dinamic properties

        private readonly List<string> _problems = new();
        private int _registeredCount;

        #endregion

        #region Godot implementation

        public override void _Ready()
        {
            Game.Reset();

            RegisterBranch(typeof(Game));

            if (_problems.Count > 0)
            {
                GD.PushError($"[Bootstrap._Ready] {_problems.Count} problema(s) no registro de nodes, o jogo nao vai iniciar:\n - {string.Join("\n - ", _problems)}");

                return;
            }

            GD.Print($"[Bootstrap._Ready] {_registeredCount} nodes estaticos registrados, jogo pronto");

            Game.NotifyReady();

            OpenStartScreen();
        }

        #endregion

        #region Core - Registro

        private void RegisterBranch(Type branch)
        {
            foreach (var entry in branch.GetNestedTypes(BindingFlags.Public))
            {
                Register(entry);
                RegisterBranch(entry);
            }
        }

        private void Register(Type entry)
        {
            var pathField = entry.GetField(nameof(Game.Main.Path), BindingFlags.Public | BindingFlags.Static);
            var nodeProperty = entry.GetProperty(nameof(Game.Main.Node), BindingFlags.Public | BindingFlags.Static);

            if (pathField == null || nodeProperty == null)
            {
                _problems.Add($"{FullName(entry)}: entrada sem Path e/ou Node");

                return;
            }

            var path = (string)pathField.GetValue(null);
            var node = GetTree().Root.GetNodeOrNull(path);

            if (node == null)
            {
                _problems.Add($"{FullName(entry)}: node ausente em {path}");

                return;
            }

            if (!nodeProperty.PropertyType.IsInstanceOfType(node))
            {
                _problems.Add($"{FullName(entry)}: {path} e {node.GetType().Name}, esperado {nodeProperty.PropertyType.Name}");

                return;
            }

            nodeProperty.GetSetMethod(nonPublic: true).Invoke(null, new object[] { node });

            _registeredCount++;
        }

        private string FullName(Type entry)
        {
            var name = entry.Name;

            for (var parent = entry.DeclaringType; parent != null; parent = parent.DeclaringType)
            {
                name = $"{parent.Name}.{name}";
            }

            return name;
        }

        #endregion

        #region Core - Inicio

        private void OpenStartScreen()
        {
            Game.Managers.RouterManager.Node.Open(Game.Ui.StartUI.Node);
        }

        #endregion
    }
}
