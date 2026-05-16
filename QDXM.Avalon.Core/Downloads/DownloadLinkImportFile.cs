namespace QDXM.Avalon.Core.Downloads;

public static class DownloadLinkImportFile
{
    public static bool IsImportFlag(string? arg)
    {
        return arg?.Equals("--import", StringComparison.OrdinalIgnoreCase) == true ||
            arg?.Equals("/import", StringComparison.OrdinalIgnoreCase) == true;
    }

    public static bool IsInlineImportArgument(string? arg)
    {
        return arg?.StartsWith("--import=", StringComparison.OrdinalIgnoreCase) == true;
    }

    public static bool TryGetImportFilePath(IEnumerable<string>? args, out string? filePath, out string? errorMessage)
    {
        filePath = null;
        errorMessage = null;
        if (args is null)
        {
            return false;
        }

        var pendingImportFlag = false;
        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (pendingImportFlag)
            {
                filePath = arg.Trim();
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    errorMessage = "Import file path is missing after --import.";
                    return false;
                }

                return true;
            }

            if (IsImportFlag(arg))
            {
                pendingImportFlag = true;
                continue;
            }

            const string importPrefix = "--import=";
            if (IsInlineImportArgument(arg))
            {
                filePath = arg[importPrefix.Length..].Trim();
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    errorMessage = "Import file path is missing after --import=.";
                    return false;
                }

                return true;
            }
        }

        if (pendingImportFlag)
        {
            errorMessage = "Import file path is missing after --import.";
        }

        return false;
    }

    public static bool TryReadLinks(string filePath, out IReadOnlyList<string> links, out string? errorMessage)
    {
        links = [];
        if (string.IsNullOrWhiteSpace(filePath))
        {
            errorMessage = "Import file path is empty.";
            return false;
        }

        if (!File.Exists(filePath))
        {
            errorMessage = $"Import file was not found: {filePath}";
            return false;
        }

        try
        {
            links = File.ReadLines(filePath)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Could not read import file '{filePath}': {ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }
}
