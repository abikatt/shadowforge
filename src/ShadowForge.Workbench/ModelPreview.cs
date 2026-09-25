using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ShadowForge.Formats.HDB;
using SixLabors.ImageSharp.PixelFormats;

namespace ShadowForge.Workbench;

/// <summary>
/// An orbit view of a <see cref="PreviewMesh"/>. Left-drag rotates, right- or middle-drag pans,
/// the wheel zooms and a double-click resets. Frames render on a worker thread, always from the
/// latest camera and size, so a slow frame is skipped rather than queued.
/// </summary>
public sealed class ModelPreview : Control
{
    private readonly object _gate = new();
    private PreviewMesh? _mesh;
    private PreviewCamera _camera = PreviewCamera.Default;
    private PixelSize _size;
    private bool _dirty;
    private bool _rendering;

    private WriteableBitmap? _bitmap;
    private Point? _dragStart;
    private PreviewCamera _dragCamera;
    private bool _panning;

    public ModelPreview()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    /// <summary>
    /// The camera a new mesh starts from and a double-click returns to.
    /// </summary>
    public PreviewCamera HomeCamera { get; set; } = PreviewCamera.Default;

    public PreviewMesh? Mesh
    {
        get => _mesh;
        set
        {
            lock (_gate)
            {
                _mesh = value;
                _camera = HomeCamera;
            }
            if (value is null)
            {
                _bitmap = null;
                InvalidateVisual();
            }
            else
            {
                RequestFrame();
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2C)), new Rect(Bounds.Size));
        if (_bitmap is { } bmp)
            context.DrawImage(bmp, new Rect(Bounds.Size));
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        RequestFrame();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (e.ClickCount == 2 && point.Properties.IsLeftButtonPressed)
        {
            UpdateCamera(_ => HomeCamera);
            return;
        }
        _dragStart = point.Position;
        _dragCamera = _camera;
        _panning = !point.Properties.IsLeftButtonPressed;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragStart is not { } start) return;
        var delta = e.GetPosition(this) - start;
        var origin = _dragCamera;
        if (_panning)
        {
            double side = Math.Max(1, Math.Min(Bounds.Width, Bounds.Height));
            UpdateCamera(_ => origin with
            {
                PanX = origin.PanX + (float)(delta.X / side),
                PanY = origin.PanY + (float)(delta.Y / side),
            });
        }
        else
        {
            UpdateCamera(_ => origin with
            {
                YawDeg = origin.YawDeg + (float)delta.X * 0.5f,
                PitchDeg = Math.Clamp(origin.PitchDeg + (float)delta.Y * 0.5f, -89f, 89f),
            });
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragStart = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        float factor = MathF.Pow(1.15f, (float)e.Delta.Y);
        UpdateCamera(c => c with { Zoom = Math.Clamp(c.Zoom * factor, 0.2f, 25f) });
        e.Handled = true;
    }

    private void UpdateCamera(Func<PreviewCamera, PreviewCamera> change)
    {
        lock (_gate) _camera = change(_camera);
        RequestFrame();
    }

    private void RequestFrame()
    {
        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        var size = new PixelSize(
            (int)Math.Ceiling(Bounds.Width * scaling), (int)Math.Ceiling(Bounds.Height * scaling));
        lock (_gate)
        {
            _size = size;
            _dirty = true;
            if (_rendering || _mesh is null || size.Width <= 0 || size.Height <= 0) return;
            _rendering = true;
        }
        Task.Run(RenderLoop);
    }

    private void RenderLoop()
    {
        float[] zBuffer = [];
        while (true)
        {
            PreviewMesh mesh;
            PreviewCamera camera;
            PixelSize size;
            lock (_gate)
            {
                if (!_dirty || _mesh is null || _size.Width <= 0 || _size.Height <= 0)
                {
                    _rendering = false;
                    return;
                }
                _dirty = false;
                (mesh, camera, size) = (_mesh, _camera, _size);
            }

            int count = size.Width * size.Height;
            var pixels = new int[count];
            if (zBuffer.Length < count) zBuffer = new float[count];
            PreviewRenderer.RenderInto(mesh, camera, MemoryMarshal.Cast<int, Rgba32>(pixels.AsSpan()),
                zBuffer, size.Width, size.Height);

            Dispatcher.UIThread.Post(() => Present(mesh, pixels, size));
        }
    }

    private void Present(PreviewMesh mesh, int[] pixels, PixelSize size)
    {
        if (!ReferenceEquals(mesh, _mesh)) return;
        if (_bitmap is null || _bitmap.PixelSize != size)
            _bitmap = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Opaque);
        using (var fb = _bitmap.Lock())
        {
            for (int y = 0; y < size.Height; y++)
                System.Runtime.InteropServices.Marshal.Copy(pixels, y * size.Width, fb.Address + y * fb.RowBytes, size.Width);
        }
        InvalidateVisual();
    }
}
