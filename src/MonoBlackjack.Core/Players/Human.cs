namespace MonoBlackjack.Core.Players;

public class Human : PlayerBase
{
    public decimal Bank { get; set; }

    public Human(string name = "Player", decimal? startingBank = null)
        : base(name)
    {
        Bank = startingBank ?? GameConfig.StartingBank;
    }
}
