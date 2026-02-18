using Microsoft.Xna.Framework.Input;

namespace MonoBlackjack;

internal sealed class GameInputController
{
    public bool IsActionJustPressed(
        KeybindMap keybinds,
        InputAction action,
        KeyboardState current,
        KeyboardState previous)
    {
        return keybinds.IsJustPressed(action, current, previous);
    }

    public bool IsKeyJustPressed(Keys key, KeyboardState current, KeyboardState previous)
    {
        return current.IsKeyDown(key) && !previous.IsKeyDown(key);
    }

    public bool IsPausePressed(KeybindMap keybinds, KeyboardState current, KeyboardState previous)
    {
        return IsActionJustPressed(keybinds, InputAction.Pause, current, previous);
    }

    public bool IsBackPressed(KeybindMap keybinds, KeyboardState current, KeyboardState previous)
    {
        return IsActionJustPressed(keybinds, InputAction.Back, current, previous);
    }

    public bool IsDevMenuTogglePressed(KeyboardState current, KeyboardState previous)
    {
        return IsKeyJustPressed(Keys.F1, current, previous);
    }

    public bool IsAlignmentGuideTogglePressed(KeyboardState current, KeyboardState previous)
    {
        return IsKeyJustPressed(Keys.F3, current, previous);
    }

    public GamePhaseActionCommand ResolveBettingCommand(KeybindMap keybinds, KeyboardState current, KeyboardState previous)
    {
        if (IsActionJustPressed(keybinds, InputAction.Hit, current, previous))
            return GamePhaseActionCommand.BetDown;
        if (IsActionJustPressed(keybinds, InputAction.Double, current, previous))
            return GamePhaseActionCommand.BetUp;
        if (IsActionJustPressed(keybinds, InputAction.Split, current, previous))
            return GamePhaseActionCommand.RepeatBet;
        if (IsActionJustPressed(keybinds, InputAction.Stand, current, previous))
            return GamePhaseActionCommand.Deal;

        return GamePhaseActionCommand.None;
    }

    public GamePhaseActionCommand ResolveBankruptCommand(KeybindMap keybinds, KeyboardState current, KeyboardState previous)
    {
        if (IsActionJustPressed(keybinds, InputAction.Hit, current, previous))
            return GamePhaseActionCommand.ResetBankroll;

        if (IsActionJustPressed(keybinds, InputAction.Stand, current, previous)
            || IsActionJustPressed(keybinds, InputAction.Back, current, previous))
            return GamePhaseActionCommand.Menu;

        return GamePhaseActionCommand.None;
    }

    public GamePhaseActionCommand ResolvePlayerTurnCommand(KeybindMap keybinds, KeyboardState current, KeyboardState previous)
    {
        if (IsActionJustPressed(keybinds, InputAction.Hit, current, previous))
            return GamePhaseActionCommand.Hit;
        if (IsActionJustPressed(keybinds, InputAction.Stand, current, previous))
            return GamePhaseActionCommand.Stand;
        if (IsActionJustPressed(keybinds, InputAction.Split, current, previous))
            return GamePhaseActionCommand.Split;
        if (IsActionJustPressed(keybinds, InputAction.Double, current, previous))
            return GamePhaseActionCommand.Double;
        if (IsActionJustPressed(keybinds, InputAction.Surrender, current, previous))
            return GamePhaseActionCommand.Surrender;

        return GamePhaseActionCommand.None;
    }

    public GamePhaseActionCommand ResolveInsuranceCommand(KeybindMap keybinds, KeyboardState current, KeyboardState previous)
    {
        if (IsActionJustPressed(keybinds, InputAction.Hit, current, previous))
            return GamePhaseActionCommand.InsuranceAccept;

        if (IsActionJustPressed(keybinds, InputAction.Stand, current, previous)
            || IsActionJustPressed(keybinds, InputAction.Back, current, previous))
            return GamePhaseActionCommand.InsuranceDecline;

        return GamePhaseActionCommand.None;
    }

    public GamePhaseActionCommand ResolveRoundAdvanceCommand(KeybindMap keybinds, KeyboardState current, KeyboardState previous)
    {
        if (IsActionJustPressed(keybinds, InputAction.Hit, current, previous)
            || IsActionJustPressed(keybinds, InputAction.Stand, current, previous))
            return GamePhaseActionCommand.AdvanceRound;

        return GamePhaseActionCommand.None;
    }
}
