namespace MonoBlackjack.Core.Ports;

public sealed record PlayerProfile(int Id, string Name, bool IsActive);

public interface IProfileRepository
{
    PlayerProfile GetOrCreateProfile(string name);
    IReadOnlyList<PlayerProfile> GetProfiles();
    PlayerProfile? GetActiveProfile();
    void SetActiveProfile(int profileId);
}
