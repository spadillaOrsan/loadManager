namespace LoadManager.Helpers;

public enum AuthorizationFrameStatus
{
    NotAuthorization,
    Authorized,
    NotAuthorized,
    UnknownAuthorizationResponse
}

public static class ConsoleFrameHelper
{
    public static string ApplyReplacements(
        string requestFrame,
        IReadOnlyDictionary<string, string>? replacements)
    {
        if (replacements is null)
        {
            return requestFrame;
        }

        foreach (var replacement in replacements)
        {
            requestFrame = requestFrame.Replace($"{{{replacement.Key}}}", replacement.Value);
        }

        return requestFrame;
    }

    public static string ToCommandNumber(string value)
    {
        return int.TryParse(value, out var number) ? number.ToString() : value;
    }

    public static AuthorizationFrameStatus GetAuthorizationStatus(string requestFrame, string responseFrame)
    {
        if (!requestFrame.TrimStart().StartsWith("AUTH|", StringComparison.OrdinalIgnoreCase))
        {
            return AuthorizationFrameStatus.NotAuthorization;
        }

        if (responseFrame.Contains("NoAutorizado", StringComparison.OrdinalIgnoreCase) ||
            responseFrame.Contains("No autorizado", StringComparison.OrdinalIgnoreCase))
        {
            return AuthorizationFrameStatus.NotAuthorized;
        }

        if (responseFrame.Contains("Autorizado", StringComparison.OrdinalIgnoreCase))
        {
            return AuthorizationFrameStatus.Authorized;
        }

        return AuthorizationFrameStatus.UnknownAuthorizationResponse;
    }

    public static string GetRawFrameUserMessage(AuthorizationFrameStatus authorizationStatus)
    {
        return authorizationStatus switch
        {
            AuthorizationFrameStatus.Authorized => "La carga fue autorizada.",
            AuthorizationFrameStatus.NotAuthorized => "La carga no fue autorizada.",
            _ => "La consola respondio correctamente."
        };
    }
}
