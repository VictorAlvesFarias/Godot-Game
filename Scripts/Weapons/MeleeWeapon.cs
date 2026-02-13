using Godot;
using System;
using System.Collections.Generic;

namespace Jogo25D.Weapons
{
    public partial class MeleeWeapon : Weapon
    {
        [Export] public float Range { get; set; } = 80.0f;
        [Export] public float AttackDuration { get; set; } = 0.2f;
        [Export] public float AttackAngle { get; set; } = 90.0f;
        
        private Area2D hitArea;
        private CollisionShape2D hitShape;
        private Timer attackTimer;
        private Line2D visualEffect;
        private HashSet<Node2D> hitEnemies = new HashSet<Node2D>();
        private Vector2 lastAttackDirection = Vector2.Right;
        private bool isAttacking = false;

        public override void _Ready()
        {
            base._Ready();
            
            hitArea = new Area2D();
            hitArea.CollisionLayer = 0;
            hitArea.CollisionMask = 1;
            hitArea.Monitorable = false;
            hitArea.Monitoring = false;
            hitArea.TopLevel = true; 
            
            AddChild(hitArea);

            var circleShape = new CircleShape2D();
            
            circleShape.Radius = Range * 0.7f;
            hitShape = new CollisionShape2D { Shape = circleShape };

            hitArea.AddChild(hitShape);
            
            hitArea.BodyEntered += OnBodyEntered;
            
            attackTimer = new Timer();
            attackTimer.OneShot = true;
            attackTimer.WaitTime = AttackDuration;
            attackTimer.Timeout += OnAttackEnd;

            AddChild(attackTimer);

            visualEffect = new Line2D { 
                Width = 4.0f, 
                DefaultColor = new Color(1, 1, 1, 0.8f), 
                Visible = false, 
                TopLevel = true 
            };

            AddChild(visualEffect);
        }

        public override void _Process(double delta)
        {
            base._Process(delta); 

            if (isAttacking && owner != null)
            {
                Vector2 offset = lastAttackDirection * Range;
            
                hitArea.GlobalPosition = owner.GlobalPosition + offset;
                visualEffect.GlobalPosition = owner.GlobalPosition;
            }
        }

        public override void Attack(Vector2 direction)
        {
            if (!CanAttack || owner == null) 
            {
                return;
            }

            isAttacking = true;
            
            hitEnemies.Clear();

            lastAttackDirection = direction.Normalized();
            hitArea.GlobalPosition = owner.GlobalPosition + (lastAttackDirection * Range);
            visualEffect.GlobalPosition = owner.GlobalPosition;
            hitArea.Monitoring = true;

            attackTimer.Start();

            ShowAttackEffect(lastAttackDirection);
            StartCooldown();
        }

        private void ShowAttackEffect(Vector2 direction)
        {
            visualEffect.Visible = true;

            visualEffect.ClearPoints();
            
            visualEffect.Modulate = Colors.White;
            visualEffect.GlobalRotation = 0; 

            var angleRad = direction.Angle();
            var halfAngleRad = Mathf.DegToRad(AttackAngle / 2);
            
            visualEffect.AddPoint(Vector2.Zero);
            
            var segments = 10;
            
            for (int i = 0; i <= segments; i++)
            {
                var currentAngle = angleRad - halfAngleRad + (i * Mathf.DegToRad(AttackAngle) / segments);
            
                visualEffect.AddPoint(new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle)) * Range);
            }
            
            visualEffect.AddPoint(Vector2.Zero);

            var tween = CreateTween();

            tween.TweenProperty(visualEffect, "modulate:a", 0.0f, AttackDuration);
            tween.TweenCallback(Callable.From(() => visualEffect.Visible = false));
        }

        private void OnBodyEntered(Node2D body)
        {
            if (body == owner || hitEnemies.Contains(body)) 
            {
                return;
            }
            
            hitEnemies.Add(body);
            
            if (body.HasMethod("TakeDamage"))
            {
                body.Call("TakeDamage", Damage);
            }
        }

        private void OnAttackEnd()
        {
            isAttacking = false;
            hitArea.Monitoring = false;
        }

        private void CheckHits()
        {
            if (!hitArea.Monitoring) 
            {
                return;
            }

            var bodies = hitArea.GetOverlappingBodies();
            
            foreach (var body in bodies)
            {
                if (body is Node2D n) OnBodyEntered(n);
            }
        }
    }
}