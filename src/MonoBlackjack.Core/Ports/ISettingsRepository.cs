namespace MonoBlackjack.Core.Ports;

public interface ISettingsRepository
{
    IReadOnlyDictionary<string, string> LoadSettings(int profileId);
    void SaveSettings(int profileId, IReadOnlyDictionary<string, string> settings);
}
