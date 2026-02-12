namespace MonoBlackjack.Core.Players;

public class Human : PlayerBase
{
    public int Bank { get; set; }

    public Human(string name = "Player", int? startingBank = null)
        : base(name)
    {
        Bank = startingBank ?? GameConfig.StartingBank;
    }
}
