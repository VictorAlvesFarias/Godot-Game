using Godot;
using System.Collections.Generic;

namespace Jogo25D.Systems
{
    public partial class InputManager : Node
    {
        public static InputManager Instance { get; private set; }

        #region Blockers

        private readonly HashSet<string> _blockers = new();

        public bool IsBlocked
        {
            get
            {
                return _blockers.Count > 0;
            }
        }

        public void AddBlocker(string id)
        {
            _blockers.Add(id);
        }

        public void RemoveBlocker(string id)
        {
            _blockers.Remove(id);
        }

        #endregion

        #region Game inputs — zeroed when any blocker is active

        public float MoveX { get; private set; }
        public float MoveY { get; private set; }
        public bool Jump { get; private set; }
        public bool Dash { get; private set; }
        public bool Attack { get; private set; }
        public bool Reload { get; private set; }
        public bool Ability { get; private set; }
        public bool ScrollNext { get; private set; }
        public bool ScrollPrev { get; private set; }

        #endregion

        #region UI inputs — also zeroed when any blocker is active

        public bool Pause { get; private set; }
        public bool ToggleInventory { get; private set; }

        #endregion

        #region Mouse — always available

        public Vector2 MouseScreenPosition { get; private set; }

        #endregion

        #region Lifecycle

        public override void _Ready()
        {
            Instance = this;
        }

        public override void _PhysicsProcess(double delta)
        {
            Poll();
        }

        #endregion

        #region Polling

        private float _prevMoveX;
        private float _prevMoveY;
        private bool  _prevAttack;

        private void Poll()
        {
            MouseScreenPosition = GetViewport().GetMousePosition();

            if (IsBlocked)
            {
                LogReleased("attack",  Attack);
                LogReleased("move",    MoveX != 0f || MoveY != 0f);

                MoveX = 0f;
                MoveY = 0f;
                Jump = false;
                Dash = false;
                Attack = false;
                Reload = false;
                Ability = false;
                ScrollNext = false;
                ScrollPrev = false;
                Pause = false;
                ToggleInventory = false;

                _prevMoveX  = 0f;
                _prevMoveY  = 0f;
                _prevAttack = false;
                
                return;
            }

            var newMoveX = Input.GetAxis("move_left", "move_right");
            var newMoveY = Input.GetAxis("move_up", "move_down");
            var newJump = Input.IsActionJustPressed("move_up");
            var newDash = Input.IsActionJustPressed("dash");
            var newAttack = Input.IsActionPressed("shoot");
            var newReload = Input.IsActionJustPressed("reload");
            var newAbility = Input.IsActionJustPressed("ability");
            var newScrollNext = Input.IsActionJustPressed("weapon_next");
            var newScrollPrev = Input.IsActionJustPressed("weapon_prev");
            var newPause = Input.IsActionJustPressed("pause");
            var newInv = Input.IsActionJustPressed("toggle_inventory");
            var wasMoving = _prevMoveX != 0f || _prevMoveY != 0f;
            var isMoving = newMoveX   != 0f || newMoveY   != 0f;

            if (!wasMoving && isMoving)
            {
                GD.Print($"[Input] move PRESSIONADO ({newMoveX:F1}, {newMoveY:F1})");
            }
            else if (wasMoving && !isMoving)
            {
                GD.Print("[Input] move SOLTO");
            }

            if (!_prevAttack && newAttack)
            {
                GD.Print("[Input] attack PRESSIONADO");
            }
            else if (_prevAttack && !newAttack)
            {
                GD.Print("[Input] attack SOLTO");
            }

            if (newJump)    
            { 
                GD.Print("[Input] jump PRESSIONADO"); 
            }

            if (newDash)    
            { 
                GD.Print("[Input] dash PRESSIONADO"); 
            }

            if (newReload)  
            { 
                GD.Print("[Input] reload PRESSIONADO"); 
            }
            
            if (newAbility) 
            { 
                GD.Print("[Input] ability PRESSIONADO"); 
            }
            
            if (newPause)   
            { 
                GD.Print("[Input] pause PRESSIONADO"); 
            }
            
            if (newInv)     
            { 
                GD.Print("[Input] toggle_inventory PRESSIONADO"); 
            }

            _prevMoveX  = newMoveX;
            _prevMoveY  = newMoveY;
            _prevAttack = newAttack;

            MoveX           = newMoveX;
            MoveY           = newMoveY;
            Jump            = newJump;
            Dash            = newDash;
            Attack          = newAttack;
            Reload          = newReload;
            Ability         = newAbility;
            ScrollNext      = newScrollNext;
            ScrollPrev      = newScrollPrev;
            Pause           = newPause;
            ToggleInventory = newInv;
        }

        private void LogReleased(string name, bool wasActive)
        {
            if (wasActive)
            {
                GD.Print($"[Input] {name} SOLTO (bloqueado)");
            }
        }

        #endregion
    }
}
