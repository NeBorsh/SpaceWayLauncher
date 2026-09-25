namespace SpaceWay.Core.Connecting;

/// <summary>
/// Asks the player to accept the server's privacy policy.
/// </summary>
public interface IPrivacyPolicyPrompt
{
    /// <param name="policy">The policy and where to read it.</param>
    /// <param name="versionChanged">
    /// The player already accepted a different version. Worth stating
    /// explicitly, otherwise the repeated prompt looks like a launcher bug.
    /// </param>
    /// <returns>Whether the player accepted.</returns>
    Task<bool> Ask(ServerPrivacyPolicy policy, bool versionChanged, CancellationToken cancel = default);
}
