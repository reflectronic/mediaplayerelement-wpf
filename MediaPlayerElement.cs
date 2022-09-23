using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

using Windows.UI.Composition;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.System.WinRT;
using Windows.Win32.System.WinRT.Composition;
using Windows.System;
using System.Diagnostics.CodeAnalysis;
using WinRT;
using System.Windows;
using Windows.Media.Playback;

namespace WpfApp1;

public sealed class MediaPlayerElement : HwndHost
{
    private IntPtr hwndHost;
    private DpiScale scale;
    private DispatcherQueueController? dispatcherQueue;

    private CompositionTarget compositionTarget = default!;
    private SpriteVisual videoVisual = default!;

    public MediaPlayer MediaPlayer { get; } = new();

    public Compositor? Compositor { get; private set; }

    private void SetChild(Visual value)
    {
        if (Compositor == null)
        {
            InitComposition(hwndHost);
        }

        compositionTarget.Root = value;
    }

    public MediaPlayerElement()
    {
        SizeChanged += CompositionHost_SizeChanged;
        Loaded += CompositionHost_Loaded;
        PresentationSource.AddSourceChangedHandler(this, PresentationSourceChangedHandler);
    }
    private void CompositionHost_Loaded(object sender, RoutedEventArgs e)
    {
        videoVisual = Compositor!.CreateSpriteVisual();
        SetChild(videoVisual);
        Resize((int)ActualWidth, (int)ActualHeight);
    }

    private void PresentationSourceChangedHandler(object sender, SourceChangedEventArgs args)
    {
        if (args.NewSource != null)
        {
            var source = (HwndSource)args.NewSource;
            scale = System.Windows.Media.VisualTreeHelper.GetDpi(source.RootVisual);
        }
    }

    private void CompositionHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Resize((int)e.NewSize.Width, (int)e.NewSize.Height);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        scale = newDpi;
        Resize((int)ActualWidth, (int)ActualHeight);
        base.OnDpiChanged(oldDpi, newDpi);
    }

    private void Resize(int width, int height)
    {
        if (compositionTarget is { Root: not null })
        {
            var scaledWidth = width * scale.DpiScaleX;
            var scaledHeight = height * scale.DpiScaleY;
            MediaPlayer.SetSurfaceSize(new(scaledWidth, scaledHeight));

            var surface = MediaPlayer.GetSurface(Compositor);
            var brush = Compositor!.CreateSurfaceBrush(surface.CompositionSurface);
            var oldBrush = videoVisual.Brush;
            videoVisual.Brush = brush;
            oldBrush?.Dispose();

            compositionTarget.Root.Size = new((float)scaledWidth, (float)scaledHeight);
        }
    }

    protected override unsafe HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        // Create Window
        hwndHost = IntPtr.Zero;
        hwndHost = PInvoke.CreateWindowEx(0,
            "static",
            "",
            WINDOW_STYLE.WS_CHILD | WINDOW_STYLE.WS_VISIBLE | WINDOW_STYLE.WS_CLIPCHILDREN,
            0,
            0,
            0,
            0,
            (HWND)hwndParent.Handle,
            null,
            null,
            null);

        dispatcherQueue = InitializeCoreDispatcher();

        InitComposition(hwndHost);

        return new HandleRef(this, hwndHost);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        compositionTarget.Root?.Dispose();

        PInvoke.DestroyWindow((HWND)hwnd.Handle);
    }

    private static unsafe DispatcherQueueController InitializeCoreDispatcher()
    {
        var options = new DispatcherQueueOptions
        {
            dwSize = (uint)sizeof(DispatcherQueueOptions),
            apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
            threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT
        };

        PInvoke.CreateDispatcherQueueController(options, out DispatcherQueueController queue);
        return queue;
    }

    [MemberNotNull(nameof(Compositor))]
    [MemberNotNull(nameof(compositionTarget))]
    private void InitComposition(IntPtr hwndHost)
    {
        Compositor = new Compositor();
        var interop = this.Compositor.As<ICompositorDesktopInterop>();
        interop.CreateDesktopWindowTarget((HWND)hwndHost, true, out var target);
        compositionTarget = target;
    }
}
