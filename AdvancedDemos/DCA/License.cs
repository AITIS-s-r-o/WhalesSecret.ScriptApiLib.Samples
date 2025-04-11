namespace WhalesSecret.ScriptApiLib.DCA;

/// <summary>
/// Container with Whale's Secret license to unlock premium functionality of ScriptApiLib.
/// </summary>
/// <remarks>
/// When ScriptApiLib is run without a license, all its functions are available except for creating larger orders. Without the license, size of orders one can create is restricted.
/// In order to unlock unlimited order sizes in the samples, you need to change the license below to a valid license.
/// </remarks>
public static class License
{
    /// <summary>Whale's Secret license, or <c>null</c> to use the free mode.</summary>
    /// <remarks>Change this to your Whale's Secret License to unlock unlimited order sizes.</remarks>
    public const string? WsLicense = null;
}