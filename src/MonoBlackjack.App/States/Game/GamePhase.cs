namespace MonoBlackjack;

internal enum GamePhase
{
    Betting,
    Playing,
    Bankrupt
}

internal enum GamePhaseActionCommand
{
    None,
    BetDown,
    BetUp,
    Deal,
    RepeatBet,
    ResetBankroll,
    Menu,
    Hit,
    Stand,
    Split,
    Double,
    Surrender,
    InsuranceAccept,
    InsuranceDecline,
    AdvanceRound
}
