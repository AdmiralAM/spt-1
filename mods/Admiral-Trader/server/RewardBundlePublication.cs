namespace AdmiralTrader.Server;

/// Stages reward-definition publication before SPT exposes a quest or offer.
/// Player reward delivery remains entirely owned by the native SPT lifecycle.
public static class RewardBundlePublication
{
    public static void Execute(
        string specId,
        Action apply,
        Func<bool> verify,
        Action rollback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specId);
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(verify);
        ArgumentNullException.ThrowIfNull(rollback);

        try
        {
            apply();
            if (!verify())
                throw new InvalidDataException("post-publication verification returned false");
        }
        catch (Exception publicationError)
        {
            try
            {
                rollback();
            }
            catch (Exception rollbackError)
            {
                throw new RewardBundleException(
                    "publication",
                    specId,
                    $"publication failed ({publicationError.Message}); rollback also failed ({rollbackError.Message})");
            }

            throw new RewardBundleException("publication", specId, publicationError.Message);
        }
    }
}
