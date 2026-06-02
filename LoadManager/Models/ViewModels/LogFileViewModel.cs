namespace LoadManager.Models.ViewModels;

public sealed record LogFileViewModel(string Title, string Path, string Content, DateTime LastWriteTime);
