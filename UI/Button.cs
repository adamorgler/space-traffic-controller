using Microsoft.Xna.Framework;

namespace SpaceTrafficController.UI;

public class Button
{
    public Vector2 Position { get; set; }
    public bool IsPressed { get; set; } = false;
    public ButtonLabel Label { get; set; }
    public float Radius { get; set; }
    public Color Color { get; set; }
    public float Thickness { get; set; }
}

public enum ButtonLabel
{
    Plus,
    Minus,
    V
}
