namespace LoadManagerApi.Helpers;

public static class SolutionPathHelper
{
    public static string GetSolutionRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);

        while (directory is not null)
        {
            if (directory.EnumerateFiles("*.slnx").Any()
                || directory.EnumerateFiles("*.sln").Any())
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return startPath;
    }
}
