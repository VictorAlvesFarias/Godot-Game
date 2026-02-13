using Godot;
using Jogo25D.Characters;
using Jogo25D.Systems;
using Jogo25D.Weapons;
using System;
using System.Globalization;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;

namespace Jogo25D.Characters
{
    public partial class Player : CharacterBody2D
{
    #region Base stats
    
    [Export] public float Speed { get; set; } = 300.0f;
    [Export] public float JumpVelocity { get; set; } = -750.0f;
    [Export] public float Gravity { get; set; }
    [Export] public int MaxHealth { get; set; } = 5;
    [Export] public int CurrentHealth { get; set; }

    #endregion

    #region Dash

    [Export] public bool IsDashing { get; set; }
    [Export] public bool CanDash { get; set; } = true;
    [Export] public float DashSpeed { get; set; } = 800.0f;
    [Export] public float DashDuration { get; set; } = 0.2f;
    [Export] public float DashCooldown { get; set; } = 0.5f;
    [Export] public float DashTimer { get; set; }
    [Export] public float DashCooldownTimer { get; set; }
    [Export] public Vector2 DashDirection { get; set; }

    #endregion

    #region Weapon System

    private WeaponInventory weaponInventory;

    #endregion

    public float inputX;
    public float inputY;
    public bool inputJump;
    public bool inputDash;
    public bool inputAttack;
    public Vector2 mousePosition;
    public bool isOwner;

    public CpuParticles2D dashParticles;
    public Line2D sprite;
    public float DamageEffectTimer { get; set; } = 0f;
    public float DamageColorDuration { get; set; } = 0.3f;

    public override void _Ready()
    {
        AddToGroup("players");

        Gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
        dashParticles = GetNodeOrNull<CpuParticles2D>("DashParticles");
        sprite = GetNodeOrNull<Line2D>("Sprite/Border");
        
        // Obter referência ao inventário de armas
        weaponInventory = GetNodeOrNull<WeaponInventory>("WeaponInventory");
        
        if (weaponInventory != null)
        {
            GD.Print("[Player] Sistema de armas inicializado!");
        }
        else
        {
            GD.PrintErr("[Player] WeaponInventory não encontrado! Adicione como filho do Player.");
        }

        Rpc(nameof(ResetPlayer));
    }

    public override void _PhysicsProcess(double delta)
    {
        isOwner = GetMultiplayerAuthority() == Multiplayer.GetUniqueId();

        HandleInput();
        HandleMovement((float)delta);
        HandleAttack((float)delta);
        //HandleLogs();
        
        if (DamageEffectTimer > 0)
        {
            DamageEffectTimer -= (float)delta;

            if (DamageEffectTimer <= 0 && sprite != null && !IsDashing)
            {
                sprite.DefaultColor = Colors.White;
            }
        }
    }

    #region Server methods

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void SetServerInput(float x, float y, bool jump, bool dash, bool attack)
    {
        inputX = x;
        inputY = y;
        inputJump = jump;
        inputDash = dash;
        inputAttack = attack;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void SetServerMousePosition(Vector2 pos)
    {
        mousePosition = pos;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void ResetPlayer()
    {
        GlobalPosition = new Vector2(1060, 300);
        Velocity = Vector2.Zero;
        CurrentHealth = MaxHealth;
    }

    #endregion

    #region Local methods

    public void HandleInput()
    {
        if (!isOwner)
        {
            return;
        }

        inputX = Input.GetAxis("move_left", "move_right");
        inputY = Input.GetAxis("move_up", "move_down");
        inputJump = Input.IsActionJustPressed("move_up");
        inputDash = Input.IsActionJustPressed("dash");
        inputAttack = Input.IsActionPressed("shoot");

        Rpc(nameof(SetServerInput), inputX, inputY, inputJump, inputDash, inputAttack);
        Rpc(nameof(SetServerMousePosition), GetGlobalMousePosition());
    }

    public void HandleMovement(float delta)
    {
        var v = Velocity;
        if (!CanDash)
        {
            DashCooldownTimer += delta;

            if (DashCooldownTimer >= DashCooldown)
            {
                CanDash = true;
                DashCooldownTimer = 0;
            }
        }

        if (inputDash && CanDash && !IsDashing)
        {
            Vector2 direction = new Vector2(inputX, inputY);

            if (direction.Length() == 0)
            {
                DashDirection = Vector2.Up;
            }

            DashDirection = direction.Normalized();

            IsDashing = true;
            CanDash = false;
            DashTimer = 0;

            if (dashParticles != null)
            {
                dashParticles.Emitting = true;
            }

            if (sprite != null)
            {
                sprite.DefaultColor = new Color(0.5f, 1f, 1f);
            }
        }

        if (IsDashing)
        {
            DashTimer += delta;

            v = DashDirection * DashSpeed;

            if (DashTimer >= DashDuration)
            {
                IsDashing = false;

                if (sprite != null)
                {
                    sprite.DefaultColor = Colors.White;
                }
            }
        }
        else
        {
            if (!IsOnFloor())
            {
                v.Y += Gravity * delta;
            }

            if (inputJump && IsOnFloor())
            {
                v.Y = JumpVelocity;
            }

            if (inputX != 0)
            {
                v.X = inputX * Speed;
            }
            else
            {
                v.X = Mathf.MoveToward(v.X, 0, Speed);
            }
        }

        Velocity = v;

        MoveAndSlide();
    }

    public void HandleAttack(float delta)
    {
        if (weaponInventory == null)
            return;

        if (inputAttack && weaponInventory.CanAttack())
        {
            // Direção do ataque baseada na posição do mouse
            var direction = (mousePosition - GlobalPosition).Normalized();
            weaponInventory.Attack(direction);
        }
    }

    public void HandleLogs()
    {
        Console.Clear();

        Console.WriteLine(CurrentHealth);
        //Console.WriteLine(inputX);
        //Console.WriteLine(inputY);
        //Console.WriteLine(inputJump);
        //Console.WriteLine(inputDash);
        //Console.WriteLine(inputAttack);
    }

    public void TakeDamage(int damage)
    {
        if (CurrentHealth <= 0)
        {
            return;
        }

        CurrentHealth -= damage;

        if (sprite != null)
        {
            sprite.DefaultColor = new Color(1f, 0.3f, 0.3f);
        }

        DamageEffectTimer = DamageColorDuration;

        if (CurrentHealth <= 0)
        {
            Rpc(nameof(ResetPlayer));
        }
    }

    #endregion
}

public abstract class PlayerAction
{
    public Player Player { get; set; }
    public bool CanUse { get; set; } = true;
    public bool Cooldown { get; set; } = false;

    public PlayerAction(Player player)
    {
        Player = player;
    }

    public abstract void Activate();
    public abstract void Deactivate();
    public abstract void Reset();
    public abstract void ActivateEffects();
    public abstract void DeactivateEffects();
}

public class InputControls { 
}

public class DashAction : PlayerAction
{
    public DashAction(Player player) : base(player)
    {

    }

    public override void Activate()
    {
        throw new NotImplementedException();
    }

    public override void ActivateEffects()
    {
        throw new NotImplementedException();
    }

    public override void Deactivate()
    {
        throw new NotImplementedException();
    }

    public override void DeactivateEffects()
    {
        throw new NotImplementedException();
    }

    public override void Reset()
    {
        throw new NotImplementedException();
    }
    }
}