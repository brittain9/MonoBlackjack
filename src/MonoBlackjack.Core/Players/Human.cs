namespace MonoBlackjack.Core.Players;

public class Human : PlayerBase
{
    public decimal Bank { get; set; }

    public Human(string name, decimal startingBank)
        : base(name)
    {
        Bank = startingBank;
    }
}
