using Godot;
using Jogo25D.Systems;
using System;
using System.Collections.Generic;

namespace Jogo25D.Testing
{
    // Mede o quanto a camera sacode seguindo o player. O alvo e uma trajetoria do proprio
    // movimento do Player: andar/parar em 300 px/s (sem aceleracao), degrau de 2 blocos subido
    // em 64 px a 450 px/s e um pulo de -750 com a gravidade do projeto.
    //
    // O que interessa nao e a posicao da camera e sim a DERIVADA dela: o solavanco por frame
    // (segunda diferenca). Camera colada no player copia cada quebra de velocidade do corpo;
    // suavizada, a mesma quebra vira uma rampa.
    public partial class CameraSmoothProbe : Node
    {
        private const float Dt = 1f / 60f;
        private const int Frames = 300;          // 5 s
        private const float Speed = 300f;
        private const float JumpVelocity = -750f;
        private const float StepUpSpeed = 450f;  // Speed * StepUpSpeedFactor
        private const float StepUpHeight = 64f;  // 2 blocos de 32

        private readonly float[] _speeds = { 0f, 8f, 12f, 16f, 20f, 25f };

        private Node2D _target;
        private CameraController _camera;

        private int _pass = -1;
        private int _teleportFrames = -1;
        private Vector2 _teleportTarget;
        private int _frame;
        private float _gravity;

        // Estado do corpo simulado.
        private Vector2 _position;
        private float _velocityY;
        private float _stepUpRemaining;

        private readonly List<Vector2> _cameraTrack = new();
        private readonly List<Vector2> _targetTrack = new();

        public override void _Ready()
        {
            _gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

            _target = new Node2D();
            AddChild(_target);

            _camera = new CameraController { PlayerRef = _target, Enabled = true };
            AddChild(_camera);
            _camera.MakeCurrent();

            GD.Print($"gravidade = {_gravity} px/s2   passo fisico = {Dt * 1000f:F2} ms");
            GD.Print("");
            GD.Print("velocidade   solavanco max   solavanco medio   atraso max   atraso no fim do movimento");
            GD.Print("--------------------------------------------------------------------------------------");

            StartPass(0);
        }

        private void StartPass(int index)
        {
            _pass = index;
            _frame = 0;
            _position = Vector2.Zero;
            _velocityY = 0f;
            _stepUpRemaining = 0f;
            _cameraTrack.Clear();
            _targetTrack.Clear();

            float speed = _speeds[index];
            _camera.PositionSmoothingEnabled = speed > 0f;
            _camera.PositionSmoothingSpeed = speed > 0f ? speed : 5f;
            _target.GlobalPosition = _position;
            _camera.GlobalPosition = _position;
            _camera.ResetSmoothing();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_pass < 0)
            {
                return;
            }

            Advance(_frame * Dt);
            _target.GlobalPosition = _position;
        }

        public override void _Process(double delta)
        {
            if (_teleportFrames >= 0)
            {
                float sobra = _camera.GetScreenCenterPosition().DistanceTo(_teleportTarget);

                GD.Print($"  frame +{_teleportFrames}: camera a {sobra:F1} px do destino");

                if (++_teleportFrames > 3)
                {
                    GD.Print("");
                    GD.Print("CAMERA SMOOTH OK");
                    GetTree().Quit();
                }

                return;
            }

            if (_pass < 0)
            {
                return;
            }

            // A suavizacao do Camera2D roda no frame de desenho; e dali que sai a posicao vista.
            _cameraTrack.Add(_camera.GetScreenCenterPosition());
            _targetTrack.Add(_target.GlobalPosition);

            _frame++;

            if (_frame < Frames)
            {
                return;
            }

            Report();

            if (_pass + 1 < _speeds.Length)
            {
                StartPass(_pass + 1);

                return;
            }

            TeleportCheck();
            _pass = -1;
        }

        // Um frame do movimento do Player: andar, parar, subir degrau, pular, cair.
        private void Advance(float t)
        {
            float moveX = t < 1.0f ? 1f : t < 1.4f ? 0f : t < 4.2f ? 1f : 0f;

            if (t >= 1.8f && t < 1.8f + Dt)
            {
                _stepUpRemaining = StepUpHeight;
            }

            if (t >= 2.8f && t < 2.8f + Dt)
            {
                _velocityY = JumpVelocity;
            }

            _position.X += moveX * Speed * Dt;

            if (_stepUpRemaining > 0f)
            {
                float rise = Mathf.Min(_stepUpRemaining, StepUpSpeed * Dt);

                _position.Y -= rise;
                _stepUpRemaining -= rise;
                _velocityY = 0f;

                return;
            }

            float floor = t >= 1.8f ? -StepUpHeight : 0f;

            _velocityY += _gravity * Dt;
            _position.Y += _velocityY * Dt;

            if (_position.Y >= floor)
            {
                _position.Y = floor;
                _velocityY = 0f;
            }
        }

        // Troca de dimensao/respawn: o alvo nao anda, ele muda de lugar. Sem o corte a camera
        // varreria os 4000 px ate alcancar.
        private void TeleportCheck()
        {
            _camera.PositionSmoothingEnabled = true;
            _camera.PositionSmoothingSpeed = _camera.SmoothingSpeed;
            _target.GlobalPosition = Vector2.Zero;
            _camera.GlobalPosition = Vector2.Zero;
            _camera.ResetSmoothing();

            var destino = new Vector2(4000f, -2000f);

            _target.GlobalPosition = destino;
            _teleportTarget = destino;
            _teleportFrames = 0;
        }

        private void Report()
        {
            float maxJerk = 0f;
            double sumJerk = 0d;
            float maxLag = 0f;

            for (int i = 2; i < _cameraTrack.Count; i++)
            {
                var second = _cameraTrack[i] - 2f * _cameraTrack[i - 1] + _cameraTrack[i - 2];
                float jerk = second.Length();

                maxJerk = Mathf.Max(maxJerk, jerk);
                sumJerk += jerk;
            }

            for (int i = 0; i < _cameraTrack.Count; i++)
            {
                maxLag = Mathf.Max(maxLag, (_cameraTrack[i] - _targetTrack[i]).Length());
            }

            float restLag = (_cameraTrack[^1] - _targetTrack[^1]).Length();
            float speed = _speeds[_pass];
            string label = speed > 0f ? $"{speed,4:F0}" : "  --";

            GD.Print($"{label}         {maxJerk,8:F2} px     {sumJerk / (_cameraTrack.Count - 2),8:F3} px" +
                $"      {maxLag,6:F1} px     {restLag,6:F1} px");
        }
    }
}
