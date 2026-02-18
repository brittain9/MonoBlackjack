using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoBlackjack;

public readonly struct MouseFrameSnapshot
{
    public MouseFrameSnapshot(MouseState current, MouseState previous)
    {
        Current = current;
        Previous = previous;
    }

    public MouseState Current { get; }

    public MouseState Previous { get; }

    public Point Position => Current.Position;

    public Rectangle CursorRect => new(Current.X, Current.Y, 1, 1);

    public int ScrollDelta => Current.ScrollWheelValue - Previous.ScrollWheelValue;

    public bool LeftReleasedThisFrame =>
        Previous.LeftButton == ButtonState.Pressed
        && Current.LeftButton == ButtonState.Released;
}
