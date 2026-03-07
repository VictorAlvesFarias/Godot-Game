using Godot;
using System.Collections.Generic;

namespace Jogo25D.Systems
{
    public partial class InputManager : Node
    {
        public static string DEFAULT_NODE_PATH = "/root/Main/Managers/InputManager";

        public HashSet<string> Blockers { get; set; } = new();

        public bool IsBlocked
        {
            get
            {
                return Blockers.Count > 0;
            }
        }

        public void AddBlocker(string id)
        {
            Blockers.Add(id);
        }

        public void RemoveBlocker(string id)
        {
            Blockers.Remove(id);
        }

        public ControlledInputs Current { get; set; } = new();

        public bool Pause { get; set; }
        public bool ToggleInventory { get; set; }

        public override void _Ready()
        {
        }

        public override void _PhysicsProcess(double delta)
        {
            Poll();
        }

        public float PrevMoveX { get; set; }
        public float PrevMoveY { get; set; }
        public bool PrevAttack { get; set; }
        public bool PrevAbility2 { get; set; }

        public void Poll()
        {
            var screenMousePos = GetViewport().GetMousePosition();

            if (IsBlocked)
            {
                LogReleased("attack",  Current.Attack);
                LogReleased("move",    Current.MoveX != 0f || Current.MoveY != 0f);

                Current = new ControlledInputs { MousePosition = screenMousePos };
                Pause = false;
                ToggleInventory = false;

                PrevMoveX = 0f;
                PrevMoveY = 0f;
                PrevAttack = false;
                PrevAbility2 = false;
                
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
            var newAbility2Held = Input.IsActionPressed("ability_2");
            var newAbility2Released = PrevAbility2 && !newAbility2Held;
            var wasMoving = PrevMoveX != 0f || PrevMoveY != 0f;
            var isMoving = newMoveX   != 0f || newMoveY   != 0f;

            if (!wasMoving && isMoving)
            {
                GD.Print($"[Input] move PRESSIONADO ({newMoveX:F1}, {newMoveY:F1})");
            }
            else if (wasMoving && !isMoving)
            {
                GD.Print("[Input] move SOLTO");
            }

            if (!PrevAttack && newAttack)
            {
                GD.Print("[Input] attack PRESSIONADO");
            }
            else if (PrevAttack && !newAttack)
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

            if (newAbility2Held && !PrevAbility2)
            {
                GD.Print("[Input] ability_2 PRESSIONADO");
            }
            else if (newAbility2Released)
            {
                GD.Print("[Input] ability_2 SOLTO");
            }
            
            if (newPause)   
            { 
                GD.Print("[Input] pause PRESSIONADO"); 
            }
            
            if (newInv)     
            { 
                GD.Print("[Input] toggle_inventory PRESSIONADO"); 
            }

            PrevMoveX = newMoveX;
            PrevMoveY = newMoveY;
            PrevAttack = newAttack;
            PrevAbility2 = newAbility2Held;

            Current = new ControlledInputs
            {
                MoveX = newMoveX,
                MoveY = newMoveY,
                Jump = newJump,
                Dash = newDash,
                Attack = newAttack,
                Reload = newReload,
                Ability = newAbility,
                Ability2Held = newAbility2Held,
                Ability2JustReleased = newAbility2Released,
                ScrollNext = newScrollNext,
                ScrollPrev = newScrollPrev,
                MousePosition = screenMousePos,
            };

            Pause = newPause;
            ToggleInventory = newInv;
        }

        public void LogReleased(string name, bool wasActive)
        {
            if (wasActive)
            {
                GD.Print($"[Input] {name} SOLTO (bloqueado)");
            }
        }

    }
}