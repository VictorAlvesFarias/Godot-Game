using Godot;
using Jogo25D.Characters;
using Jogo25D.Core;
using Jogo25D.Dimensions;
using Jogo25D.Entities;
using Jogo25D.Items;
using Jogo25D.Save;
using Jogo25D.Save.Resources;
using Jogo25D.Utils.GodotDictionaryParser;

namespace Jogo25D.Props
{
    [Unload(UnloadMode.Global)]
    public partial class Prop : Area2D
    {
        #region Dinamic properties

        [Save, GodotDictionaryField]
        public string PropId { get; set; } = "";

        // Jogador local dentro da area do prop (so ele ve o aviso e usa o E).
        public Player OverlappingLocalPlayer { get; set; }

        // Props interagiveis tratam o E; os outros (a mesa de criacao) deixam o E para a coleta.
        public virtual bool Interactable => false;

        public virtual string InteractPrompt => "";

        public PropDefinition Definition => PropDB.Get(PropId);

        #endregion

        #region Node children references

        public Label PromptLabel { get; set; }

        #endregion

        #region Godot implementation

        // Props na arvore: a mira da picareta procura o prop da celula aqui, sem varrer as entidades a cada passo.
        public static readonly System.Collections.Generic.HashSet<Prop> Active = new();

        public override void _EnterTree()
        {
            Active.Add(this);
        }

        public override void _ExitTree()
        {
            Active.Remove(this);
        }

        public override void _Ready()
        {
            PromptLabel = GetNodeOrNull<Label>("Labels/PromptLabel");

            if (PromptLabel != null)
            {
                PromptLabel.Visible = false;
            }

            BodyEntered += OnBodyEntered;
            BodyExited += OnBodyExited;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!Interactable || OverlappingLocalPlayer?.Input == null || !OverlappingLocalPlayer.Input.Interact)
            {
                return;
            }

            Interact(OverlappingLocalPlayer);
        }

        #endregion

        #region Core - Interacao

        // Chamado enquanto o jogador local, dentro da area, segura o E.
        protected virtual void Interact(Player player)
        {
        }

        private void OnBodyEntered(Node2D body)
        {
            if (body is not Player player || !player.IsOwner())
            {
                return;
            }

            OverlappingLocalPlayer = player;

            if (PromptLabel != null && Interactable)
            {
                PromptLabel.Text = InteractPrompt;
                PromptLabel.Visible = true;
            }
        }

        private void OnBodyExited(Node2D body)
        {
            if (body is not Player player || player != OverlappingLocalPlayer)
            {
                return;
            }

            if (PromptLabel != null)
            {
                PromptLabel.Visible = false;
            }

            OverlappingLocalPlayer = null;
        }

        #endregion

        #region Core - Quebra

        public void BreakClientRequest()
        {
            if (Multiplayer == null || !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
            {
                ProcessBreak();

                return;
            }

            RpcId(1, nameof(BreakServerReceive));
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void BreakServerReceive()
        {
            if (!Multiplayer.IsServer())
            {
                return;
            }

            ProcessBreak();
        }

        // Servidor: avisa os clientes, solta o item do prop no lugar dele e remove o prop.
        private void ProcessBreak()
        {
            Rpc(nameof(BreakBroadcast));
            OnBeforeBreak();
            DropItem();
            QueueFree();
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        public void BreakBroadcast()
        {
            OnBeforeBreak();
            QueueFree();
        }

        protected virtual void OnBeforeBreak()
        {
        }

        private void DropItem()
        {
            var itemId = Definition?.ItemId;

            if (string.IsNullOrEmpty(itemId) || ItemFactory.Create(itemId) == null)
            {
                return;
            }

            EntitySpawner.SpawnWorldItemRequest(ItemFactory.CreateInstance(itemId), GlobalPosition, Dimension.IdOf(this));
        }

        #endregion
    }
}
