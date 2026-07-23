namespace LoadManager.Helpers;

/// <summary>
/// En Android el teclado nativo ya aparece solo con inputmode="decimal". En
/// Windows, WebView2 no invoca el teclado tactil del sistema automaticamente al
/// enfocar un input: hay que pedirselo a Windows explicitamente.
///
/// Se usa la interfaz COM ITipInvocation (el mecanismo oficial con el que una app
/// solicita el teclado tactil desde el control enfocado) en vez de lanzar
/// TabTip.exe como proceso aparte: lanzarlo aparte abre el teclado generico
/// (QWERTY completo, con simbolos) porque no tiene contexto de que control esta
/// enfocado. Pedido via ITipInvocation, Windows sabe que el foco esta en este
/// input y puede elegir el layout numerico segun inputmode="decimal".
/// </summary>
public static class TouchKeyboardHelper
{
    public static void Show()
    {
#if WINDOWS
        try
        {
            var hwnd = NativeMethods.FindWindow("IPTip_Main_Window", null);
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindowVisible(hwnd))
            {
                ToggleTouchKeyboard();
            }
        }
        catch
        {
            // Sin pantalla tactil o servicio de teclado tactil no disponible.
        }
#endif
    }

    public static void Hide()
    {
#if WINDOWS
        var hwnd = NativeMethods.FindWindow("IPTip_Main_Window", null);
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.PostMessage(hwnd, NativeMethods.WM_SYSCOMMAND, NativeMethods.SC_CLOSE, IntPtr.Zero);
        }
#endif
    }

#if WINDOWS
    private static void ToggleTouchKeyboard()
    {
        var comType = Type.GetTypeFromCLSID(new Guid("4CE576FA-83DC-4F88-951C-9D0782B4E376"));
        if (comType is null)
        {
            return;
        }

        if (Activator.CreateInstance(comType) is ITipInvocation tipInvocation)
        {
            tipInvocation.Toggle(NativeMethods.GetForegroundWindow());
        }
    }

    [System.Runtime.InteropServices.ComImport]
    [System.Runtime.InteropServices.Guid("37c994e7-432b-4834-a2f7-dce1f13b834b")]
    [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITipInvocation
    {
        void Toggle(IntPtr hwnd);
    }

    private static class NativeMethods
    {
        public const uint WM_SYSCOMMAND = 0x0112;
        public const nint SC_CLOSE = 0xF060;

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint msg, nint wParam, IntPtr lParam);
    }
#endif
}
