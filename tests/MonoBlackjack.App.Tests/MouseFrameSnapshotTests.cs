using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoBlackjack.App.Tests;

public class MouseFrameSnapshotTests
{
    [Fact]
    public void ScrollDelta_IsCurrentMinusPrevious()
    {
        var previous = CreateMouseState(x: 10, y: 20, scroll: 120, left: ButtonState.Released);
        var current = CreateMouseState(x: 10, y: 20, scroll: 240, left: ButtonState.Released);

        var snapshot = new MouseFrameSnapshot(current, previous);

        Assert.Equal(120, snapshot.ScrollDelta);
    }

    [Fact]
    public void LeftReleasedThisFrame_IsTrueOnlyOnPressedToReleasedTransition()
    {
        var released = new MouseFrameSnapshot(
            CreateMouseState(0, 0, 0, ButtonState.Released),
            CreateMouseState(0, 0, 0, ButtonState.Pressed));

        var held = new MouseFrameSnapshot(
            CreateMouseState(0, 0, 0, ButtonState.Pressed),
            CreateMouseState(0, 0, 0, ButtonState.Pressed));

        Assert.True(released.LeftReleasedThisFrame);
        Assert.False(held.LeftReleasedThisFrame);
    }

    [Fact]
    public void CursorRect_UsesCurrentMouseCoordinates()
    {
        var snapshot = new MouseFrameSnapshot(
            CreateMouseState(x: 33, y: 44, scroll: 0, left: ButtonState.Released),
            CreateMouseState(x: 1, y: 2, scroll: 0, left: ButtonState.Released));

        Assert.Equal(new Point(33, 44), snapshot.Position);
        Assert.Equal(new Rectangle(33, 44, 1, 1), snapshot.CursorRect);
    }

    private static MouseState CreateMouseState(int x, int y, int scroll, ButtonState left)
    {
        return new MouseState(
            x,
            y,
            scroll,
            left,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
    }
}
