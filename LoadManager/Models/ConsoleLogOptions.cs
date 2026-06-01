namespace LoadManager.Models;

public sealed class ConsoleLogOptions
{
    public string BasePath { get; set; } = "Logs";

    public string SuccessFolderName { get; set; } = "success";

    public string ErrorFolderName { get; set; } = "error";
}
