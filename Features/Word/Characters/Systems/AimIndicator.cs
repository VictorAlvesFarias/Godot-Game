using Godot;

namespace Jogo25D.Systems
{
    public class AimIndicator
    {
        public float Length { get; set; } = 25.0f;
        public float Width { get; set; } = 3.0f;
        public Color Color { get; set; } = new Color(1f, 1f, 1f, 0.7f);
        public float Offset { get; set; } = 40.0f;
        
        private Line2D line;
        private Node2D owner;
        
        public AimIndicator(Node2D owner)
        {
            this.owner = owner;
            
            line = new Line2D();
            line.Width = Width;
            line.DefaultColor = Color;
            line.ZIndex = 10;
            
            owner.AddChild(line);
        }
        
        public void Update(Vector2 targetPosition, Vector2 ownerPosition)
        {
            if (line == null)
                return;
            
            var direction = (targetPosition - ownerPosition).Normalized();
            
            if (direction.LengthSquared() > 0.01f)
            {
                line.ClearPoints();
                
                var startOffset = direction * Offset;
                var startPoint = startOffset;
                
                var endPoint = startOffset + (direction * Length);
                
                line.AddPoint(startPoint);
                line.AddPoint(endPoint);
                
                line.Visible = true;
            }
            else
            {
                line.Visible = false;
            }
        }
        
        public void UpdateVisualProperties()
        {
            if (line != null)
            {
                line.Width = Width;
                line.DefaultColor = Color;
            }
        }
        
        public void SetVisible(bool visible)
        {
            if (line != null)
            {
                line.Visible = visible;
            }
        }
        
        public void Cleanup()
        {
            if (line != null && GodotObject.IsInstanceValid(line))
            {
                line.QueueFree();
                line = null;
            }
        }
    }
}
