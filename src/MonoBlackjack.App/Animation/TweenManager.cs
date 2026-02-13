namespace MonoBlackjack.Animation;

/// <summary>
/// Drives all active tweens. HasActiveTweens gates player input to ensure animations finish.
/// </summary>
public class TweenManager
{
    private readonly List<Tween> _tweens = [];

    public bool HasActiveTweens => _tweens.Count > 0;

    public void Add(Tween tween)
    {
        _tweens.Add(tween);
    }

    public void Update(float deltaSeconds)
    {
        for (int i = _tweens.Count - 1; i >= 0; i--)
        {
            _tweens[i].Update(deltaSeconds);
            if (_tweens[i].IsComplete)
                _tweens.RemoveAt(i);
        }
    }

    public void Clear()
    {
        _tweens.Clear();
    }
}
