using Microsoft.AspNetCore.Components;

namespace LoadManager.Services;

public sealed class ModuleHeaderState
{
    public RenderFragment? Actions { get; private set; }

    public RenderFragment? Subtitle { get; private set; }

    public event Action? Changed;

    public void SetActions(RenderFragment? actions)
    {
        Actions = actions;
        Changed?.Invoke();
    }

    public void SetSubtitle(RenderFragment? subtitle)
    {
        Subtitle = subtitle;
        Changed?.Invoke();
    }

    public void Refresh() => Changed?.Invoke();
}
