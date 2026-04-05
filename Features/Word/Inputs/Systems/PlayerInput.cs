using Godot;
using Jogo25D.Characters;
using System.Collections.Generic;

namespace Jogo25D.Systems
{
    public partial class PlayerInput : Node
    {
        public HashSet<string> Blockers { get; set; } = new();
        public float MoveX { get; private set; }
        public float MoveY { get; private set; }
        public bool Jump { get; private set; }
        public bool Dash { get; private set; }
        public bool Attack { get; private set; }
        public bool Reload { get; private set; }
        public bool Ability { get; private set; }
        public bool PrevAbility2Held { get; private set; }
        public bool Ability2Held { get; private set; }
        public bool Ability2JustReleased { get; private set; }
        public bool ScrollNext { get; private set; }
        public bool ScrollPrev { get; private set; }
        public int ScrollDirection => ScrollNext ? 1 : (ScrollPrev ? -1 : 0);
        public bool Pause { get; private set; }
        public bool ToggleInventory { get; private set; }
        public Vector2 MousePosition { get; private set; }

        public Player PlayerRef {get;set;}

        public override void _Ready()
        {
            PlayerRef = GetParent().GetParentOrNull<Player>();
        }

        public override void _PhysicsProcess(double delta)
        {
            Poll();
        }

        public bool IsBlocked()
        {
            return Blockers.Count > 0;
        }

        public void Poll()
        {
            if (PlayerRef == null)
            {
                Clear();
                return;
            }

            // Só o dono do Player deve ler teclado/mouse.
            // No servidor, os Players remotos recebem input via RPC (ServerSet*).
            if (!PlayerRef.IsOwner())
            {
                return;
            }

            if (IsBlocked())
            {
                Clear();
                return;
            }

            var oldMoveX = MoveX;
            var oldMoveY = MoveY;
            var oldJump = Jump;
            var oldDash = Dash;
            var oldAttack = Attack;
            var oldReload = Reload;
            var oldAbility = Ability;
            var oldAbility2Held = Ability2Held;
            var oldAbility2Released = Ability2JustReleased;
            var oldScrollNext = ScrollNext;
            var oldScrollPrev = ScrollPrev;
            var oldPause = Pause;
            var oldToggleInventory = ToggleInventory;
            var oldMouse = MousePosition;

            MoveX = Input.GetAxis("move_left", "move_right");
            MoveY = Input.GetAxis("move_up", "move_down");
            Jump = Input.IsActionJustPressed("move_up");
            Dash = Input.IsActionJustPressed("dash");
            Attack = Input.IsActionPressed("shoot");
            Reload = Input.IsActionJustPressed("reload");
            Ability = Input.IsActionJustPressed("ability");
            Ability2Held = Input.IsActionPressed("ability_2");
            Ability2JustReleased = PrevAbility2Held && !Ability2Held;
            PrevAbility2Held = Ability2Held;
            ScrollNext = Input.IsActionJustPressed("weapon_next");
            ScrollPrev = Input.IsActionJustPressed("weapon_prev");
            Pause = Input.IsActionJustPressed("pause");
            ToggleInventory = Input.IsActionJustPressed("toggle_inventory");
            MousePosition = PlayerRef.GetGlobalMousePosition();

            if (oldMoveX != MoveX)
            {
                Rpc(nameof(ServerSetMoveX), MoveX);
            }

            if (oldMoveY != MoveY)
            {
                Rpc(nameof(ServerSetMoveY), MoveY);
            }

            if (oldJump != Jump)
            {
                Rpc(nameof(ServerSetJump), Jump);
            }

            if (oldDash != Dash)
            {
                Rpc(nameof(ServerSetDash), Dash);
            }

            if (oldAttack != Attack)
            {
                Rpc(nameof(ServerSetAttack), Attack);
            }

            if (oldReload != Reload)
            {
                Rpc(nameof(ServerSetReload), Reload);
            }

            if (oldAbility != Ability)
            {
                Rpc(nameof(ServerSetAbility), Ability);
            }

            if (oldAbility2Held != Ability2Held)
            {
                Rpc(nameof(ServerSetAbility2Held), Ability2Held);
            }

            if (oldAbility2Released != Ability2JustReleased)
            {
                Rpc(nameof(ServerSetAbility2Released), Ability2JustReleased);
            }

            if (oldScrollNext != ScrollNext)
            {
                Rpc(nameof(ServerSetScrollNext), ScrollNext);
            }

            if (oldScrollPrev != ScrollPrev)
            {
                Rpc(nameof(ServerSetScrollPrev), ScrollPrev);
            }

            if (oldPause != Pause)
            {
                Rpc(nameof(ServerSetPause), Pause);
            }

            if (oldToggleInventory != ToggleInventory)
            {
                Rpc(nameof(ServerSetToggleInventory), ToggleInventory);
            }

            if (oldMouse != MousePosition)
            {
                Rpc(nameof(ServerSetMousePosition), MousePosition);
            }
        }

        public void Clear()
        {
            MoveX = 0;
            MoveY = 0;
            Jump = false;
            Dash = false;
            Attack = false;
            Reload = false;
            Ability = false;
            Ability2Held = false;
            Ability2JustReleased = false;
            ScrollNext = false;
            ScrollPrev = false;
            Pause = false;
            ToggleInventory = false;
            MousePosition = Vector2.Zero;
        }

        public void AddBlocker(string id)
        {
            Blockers.Add(id);
        }

        public void RemoveBlocker(string id)
        {
            Blockers.Remove(id);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetMoveX(float value)
        {
            MoveX = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetMoveY(float value)
        {
            MoveY = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetJump(bool value)
        {
            Jump = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetDash(bool value)
        {
            Dash = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetAttack(bool value)
        {
            Attack = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetReload(bool value)
        {
            Reload = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetAbility(bool value)
        {
            Ability = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetAbility2Held(bool value)
        {
            Ability2Held = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetAbility2Released(bool value)
        {
            Ability2JustReleased = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetScrollNext(bool value)
        {
            ScrollNext = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetScrollPrev(bool value)
        {
            ScrollPrev = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetPause(bool value)
        {
            Pause = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetToggleInventory(bool value)
        {
            ToggleInventory = value;
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ServerSetMousePosition(Vector2 value)
        {
            MousePosition = value;
        }
    }
}