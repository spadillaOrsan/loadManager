namespace LoadManager.Helpers;

public static class DispenserFrameHelper
{
    public static Dictionary<string, string> ParseDispenserSummary(string? responseFrame)
    {
        var statuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(responseFrame))
        {
            return statuses;
        }

        var payload = responseFrame.StartsWith("RSDI|", StringComparison.OrdinalIgnoreCase)
            ? responseFrame["RSDI|".Length..]
            : responseFrame;

        foreach (var part in payload.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var values = part.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (values.Length >= 2)
            {
                statuses[values[0]] = values[1];
            }
        }

        return statuses;
    }

    public static string? TryParseDetailStatus(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        var payload = response.StartsWith("RTDE|", StringComparison.OrdinalIgnoreCase)
            ? response["RTDE|".Length..]
            : response.StartsWith("R-TDE|", StringComparison.OrdinalIgnoreCase)
                ? response["R-TDE|".Length..]
                : response;

        var firstBlock = payload.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        var values = firstBlock?.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return values?.Length >= 2 ? values[1] : null;
    }

    public static string NormalizeDispenserNumber(string value)
    {
        return int.TryParse(value, out var number) ? number.ToString("00") : value;
    }

    public static string ToCommandNumber(string value)
    {
        return int.TryParse(value, out var number) ? number.ToString() : value;
    }
}
