using System;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows;

using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Win32;
using Windows.Win32.Graphics.Dxgi;

using WinRT;
using SharpDX.Direct3D11;

namespace WpfApp1;  
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    private void Button_Click(object sender, RoutedEventArgs e)
    {
        PlayerElement.MediaPlayer.Source = MediaSource.CreateFromUri(new(PlaybackUri.Text));
        PlayerElement.MediaPlayer.MediaFailed += (_, e) => Dispatcher.Invoke(() => MessageBox.Show(e.ToString()));
        PlayerElement.MediaPlayer.Play();
    }

    private async void Button2_Click(object sender, RoutedEventArgs e)
    {
        var d3d11Device = new Device(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport);

        var texture = new Texture2D(d3d11Device, new Texture2DDescription()
        {
            Width = 640,
            Height = 490,
            ArraySize = 1,
            Usage = ResourceUsage.Staging,
            CpuAccessFlags = CpuAccessFlags.Read,
            Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
            MipLevels = 1,
            SampleDescription =
            {
                Count = 1
            }
        });

        var dxgiSurface = texture.QueryInterface<SharpDX.DXGI.Surface>();
        var pDxgiSurface = dxgiSurface.NativePointer;
        PInvoke.CreateDirect3D11SurfaceFromDXGISurface((IDXGISurface)Marshal.GetObjectForIUnknown(pDxgiSurface), out var inspectable).ThrowOnFailure();
        GC.KeepAlive(texture);

        var pUnk = Marshal.GetIUnknownForObject(inspectable);
        var direct3dSurface = MarshalInterface<IDirect3DSurface>.FromAbi(pUnk);
        Marshal.Release(pUnk);

        PlayerElement.MediaPlayer.CopyFrameToVideoSurface(direct3dSurface);
        var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(direct3dSurface);

        unsafe 
        {
            using var buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read);
            var plane0 = buffer.GetPlaneDescription(0);

            var access = buffer.CreateReference().As<Windows.Win32.System.WinRT.IMemoryBufferByteAccess>();
            access.GetBuffer(out var data, out _);

            var image = BitmapSource.Create(bitmap.PixelWidth,
                bitmap.PixelHeight,
                bitmap.DpiX,
                bitmap.DpiY,
                System.Windows.Media.PixelFormats.Bgra32,
                null,
                (nint)(data + plane0.StartIndex),
                plane0.Width * plane0.Height * sizeof(int),
                plane0.Stride);

            CapturedImage.Source = image;
        }

    }
}
