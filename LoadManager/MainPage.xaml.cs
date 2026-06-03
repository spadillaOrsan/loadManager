using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace LoadManager;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            Padding = new Thickness(0, 0, 0, 12);
        }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = blazorWebView.TryDispatchAsync(services =>
        {
            var navigation = services.GetRequiredService<NavigationManager>();
            var relativePath = navigation.ToBaseRelativePath(navigation.Uri);

            if (!string.IsNullOrWhiteSpace(relativePath) &&
                !relativePath.Equals("despacho", StringComparison.OrdinalIgnoreCase))
            {
                navigation.NavigateTo("/");
            }
        });

        return true;
    }
}
