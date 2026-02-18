using Microsoft.Xna.Framework.Input;

namespace MonoBlackjack.App.Tests;

public class GameInputControllerTests
{
    private static readonly KeybindMap DefaultKeybinds =
        KeybindMap.FromSettings(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void ResolveBettingCommand_MapsDefaultBindings()
    {
        var controller = new GameInputController();
        var previous = new KeyboardState();

        var hitCommand = controller.ResolveBettingCommand(DefaultKeybinds, new KeyboardState(Keys.H), previous);
        var doubleCommand = controller.ResolveBettingCommand(DefaultKeybinds, new KeyboardState(Keys.D), previous);
        var splitCommand = controller.ResolveBettingCommand(DefaultKeybinds, new KeyboardState(Keys.P), previous);
        var standCommand = controller.ResolveBettingCommand(DefaultKeybinds, new KeyboardState(Keys.S), previous);

        Assert.Equal(GamePhaseActionCommand.BetDown, hitCommand);
        Assert.Equal(GamePhaseActionCommand.BetUp, doubleCommand);
        Assert.Equal(GamePhaseActionCommand.RepeatBet, splitCommand);
        Assert.Equal(GamePhaseActionCommand.Deal, standCommand);
    }

    [Fact]
    public void ResolvePlayerTurnCommand_UsesDeterministicPriority()
    {
        var controller = new GameInputController();
        var previous = new KeyboardState();

        // Hit and stand pressed together should resolve to hit first.
        var command = controller.ResolvePlayerTurnCommand(
            DefaultKeybinds,
            new KeyboardState(Keys.H, Keys.S),
            previous);

        Assert.Equal(GamePhaseActionCommand.Hit, command);
    }

    [Fact]
    public void ResolveInsuranceCommand_MapsAcceptAndDecline()
    {
        var controller = new GameInputController();
        var previous = new KeyboardState();

        var accept = controller.ResolveInsuranceCommand(DefaultKeybinds, new KeyboardState(Keys.H), previous);
        var decline = controller.ResolveInsuranceCommand(DefaultKeybinds, new KeyboardState(Keys.S), previous);

        Assert.Equal(GamePhaseActionCommand.InsuranceAccept, accept);
        Assert.Equal(GamePhaseActionCommand.InsuranceDecline, decline);
    }

    [Fact]
    public void ResolveRoundAdvanceCommand_TriggersOnHitOrStand()
    {
        var controller = new GameInputController();
        var previous = new KeyboardState();

        var advanceHit = controller.ResolveRoundAdvanceCommand(DefaultKeybinds, new KeyboardState(Keys.H), previous);
        var advanceStand = controller.ResolveRoundAdvanceCommand(DefaultKeybinds, new KeyboardState(Keys.S), previous);
        var none = controller.ResolveRoundAdvanceCommand(DefaultKeybinds, new KeyboardState(Keys.D), previous);

        Assert.Equal(GamePhaseActionCommand.AdvanceRound, advanceHit);
        Assert.Equal(GamePhaseActionCommand.AdvanceRound, advanceStand);
        Assert.Equal(GamePhaseActionCommand.None, none);
    }
}
