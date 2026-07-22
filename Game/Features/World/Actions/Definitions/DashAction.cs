using Godot;
using Jogo25D.Characters;
using Jogo25D.Features.World.Resolver.Singletons;
using Jogo25D.Properties;
using System.Linq;

namespace Jogo25D.Actions
{
	public class DashDefinition : ActionDefinition
	{
		public override void OnStartAction(Player player, ActionDefinitionData instance, float delta)
		{
			var input = new Vector2(player.Input.MoveX, player.Input.MoveY);
			var dir = Vector2.Zero;
			var dash = Resolver.Resolve(Properties.OfType<DashPropertyData>().ToList(), player.Data.Properties.OfType<DashPropertyData>().ToList(), player.Properties.OfType<DashPropertyData>().ToList());

			if (input.LengthSquared() > 0.01f)
			{
				dir = input.Normalized();
			}
			else if (player.Velocity.LengthSquared() > 100f)
			{
				dir = player.Velocity.Normalized();
			}
			else
			{
				dir = Vector2.Up;
			}

			instance.DashDirection = dir;

			if (player.Sprite != null)
			{
				if (Mathf.Abs(dir.Y) <= Mathf.Abs(dir.X))
				{
					if (player.Sprite.Animation != "dash")
					{
						player.Sprite.Play("dash");
					}

					if (dir.X != 0)
					{
						player.Sprite.FlipH = dir.X < 0;
					}
				}
			}

			player.Velocity = dir * dash.DashSpeed;
			player.Data.CanUpdateMovement = false;
		}

		public override void OnUpdateWhileActive(Player player, ActionDefinitionData instance, float delta)
		{
			var dir = instance.DashDirection.LengthSquared() > 0.01f ? instance.DashDirection : Vector2.Up;
			var input = new Vector2(player.Input.MoveX, player.Input.MoveY);
			var dash = Resolver.Resolve(Properties.OfType<DashPropertyData>().ToList(), player.Data.Properties.OfType<DashPropertyData>().ToList(), player.Properties.OfType<DashPropertyData>().ToList());

			if (input.LengthSquared() > 0.01f && dash.MovementInfluence > 0f)
			{
				var blended = dir + input.Normalized() * dash.MovementInfluence;

				if (blended.LengthSquared() > 0.01f)
				{
					var finalDir = blended.Normalized();

					player.Velocity = finalDir * dash.DashSpeed;

					if (player.Sprite != null && finalDir.X != 0)
						player.Sprite.FlipH = finalDir.X < 0;

					return;
				}
			}

			player.Velocity = dir * dash.DashSpeed;

			if (player.Sprite != null && dir.X != 0)
			{
				player.Sprite.FlipH = dir.X < 0;
			}
		}

		public override void OnFinishedAction(Player player, ActionDefinitionData instance, float delta)
		{
			player.Data.CanUpdateMovement = true;
			instance.DashDirection = Vector2.Zero;
		}

		public override bool OnStartActionValidation(Player player, ActionDefinitionData instance, float delta)
		{
			return player.Input.Dash && instance.CanUse;
		}

		public override void OnEnableAction(Player player, ActionDefinitionData instance, float delta) { }
	}
}
