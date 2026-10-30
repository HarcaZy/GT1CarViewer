using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using GT1CarViewer.Core;

namespace GT1CarViewer.App;

public sealed class CarViewport : Control
{
    private readonly System.Windows.Forms.Timer _spinTimer = new() { Interval = 33 };
    private CarModel? _model;
    private CarTexture? _texture;
    private double _yaw = -0.55;
    private double _pitch = 0.2;
    private float _zoom = 1;
    private int _verticalUvOffset;
    private bool _autoRotateRequested;
    private Point _lastMouse;
    private bool _dragging;

    public CarViewport()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BackColor = Color.FromArgb(13, 19, 27);
        AccessibleName = "Aperçu 3D de la voiture";
        TabStop = true;
        Cursor = Cursors.Hand;
        AutoRotate = true;
        _spinTimer.Tick += (_, _) =>
        {
            _yaw += 0.012;
            Invalidate();
        };
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowTexture { get; set; } = true;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowEdges { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ColorIndex { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int VerticalUvOffset
    {
        get => _verticalUvOffset;
        set
        {
            if (value is < -CarTexture.MaximumVerticalUvOffset or > CarTexture.MaximumVerticalUvOffset)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _verticalUvOffset = value;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AutoRotate
    {
        get => _autoRotateRequested;
        set
        {
            _autoRotateRequested = value;
            _spinTimer.Enabled = _model is not null && value;
        }
    }

    public void SetCar(CarModel? model, CarTexture? texture)
    {
        _model = model;
        _texture = texture;
        _zoom = 1;
        _spinTimer.Enabled = _model is not null && _autoRotateRequested;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_model is null || _model.Lods.Count == 0)
        {
            DrawEmptyState(e.Graphics);
            return;
        }

        using Bitmap frame = RenderFrame();
        e.Graphics.DrawImageUnscaled(frame, 0, 0);
        using var font = new Font("Segoe UI", 9);
        using var brush = new SolidBrush(Color.FromArgb(175, 190, 204));
        e.Graphics.DrawString("Glisser pour tourner  |  Molette pour zoomer", font, brush, 18, Height - 34);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            Focus();
            _dragging = true;
            _lastMouse = e.Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging)
        {
            return;
        }

        _yaw += (e.X - _lastMouse.X) * 0.012;
        _pitch = Math.Clamp(_pitch + (e.Y - _lastMouse.Y) * 0.012, -1.35, 1.35);
        _lastMouse = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        _zoom = Math.Clamp(_zoom * (e.Delta > 0 ? 1.1f : 0.9f), 0.35f, 4f);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _spinTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private Bitmap RenderFrame()
    {
        int width = Math.Max(1, ClientSize.Width);
        int height = Math.Max(1, ClientSize.Height);
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        int[] pixels = new int[width * height];
        Array.Fill(pixels, Color.FromArgb(13, 19, 27).ToArgb());
        DrawBackgroundGrid(pixels, width, height);

        CarLod lod = _model!.Lods[0];
        if (lod.Vertices.Count == 0)
        {
            return bitmap;
        }

        float minX = lod.Vertices.Min(vertex => vertex.X);
        float maxX = lod.Vertices.Max(vertex => vertex.X);
        float minY = lod.Vertices.Min(vertex => vertex.Y);
        float maxY = lod.Vertices.Max(vertex => vertex.Y);
        float minZ = lod.Vertices.Min(vertex => vertex.Z);
        float maxZ = lod.Vertices.Max(vertex => vertex.Z);
        float centerX = (minX + maxX) / 2;
        float centerY = (minY + maxY) / 2;
        float centerZ = (minZ + maxZ) / 2;
        float extent = Math.Max(Math.Max(maxX - minX, maxY - minY), maxZ - minZ);
        double objectScale = Math.Min(width * 0.68, height * 0.68) / Math.Max(extent, 1) * _zoom;
        double cosYaw = Math.Cos(_yaw);
        double sinYaw = Math.Sin(_yaw);
        double cosPitch = Math.Cos(_pitch);
        double sinPitch = Math.Sin(_pitch);

        var projected = new ProjectedVertex[lod.Vertices.Count];
        for (int i = 0; i < lod.Vertices.Count; i++)
        {
            CarVertex vertex = lod.Vertices[i];
            double x = vertex.X - centerX;
            double y = vertex.Y - centerY;
            double z = vertex.Z - centerZ;
            double rotatedX = x * cosYaw - z * sinYaw;
            double rotatedZ = x * sinYaw + z * cosYaw;
            double rotatedY = y * cosPitch - rotatedZ * sinPitch;
            double depth = y * sinPitch + rotatedZ * cosPitch;
            projected[i] = new ProjectedVertex(
                (float)(width / 2.0 + rotatedX * objectScale),
                (float)(height * 0.52 - rotatedY * objectScale),
                (float)depth,
                (float)rotatedX,
                (float)rotatedY,
                (float)rotatedZ);
        }

        float[] depthBuffer = new float[pixels.Length];
        Array.Fill(depthBuffer, float.NegativeInfinity);
        foreach (CarFace face in lod.Faces)
        {
            if (face.VertexIndices.Count < 3)
            {
                continue;
            }

            DrawFace(pixels, depthBuffer, width, height, projected, face);
        }

        BitmapData bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(pixels, y * width, IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride), width);
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        if (ShowEdges)
        {
            using Graphics graphics = Graphics.FromImage(bitmap);
            using var pen = new Pen(Color.FromArgb(95, 4, 9, 14), 1);
            foreach (CarFace face in lod.Faces)
            {
                for (int i = 0; i < face.VertexIndices.Count; i++)
                {
                    ProjectedVertex first = projected[face.VertexIndices[i]];
                    ProjectedVertex second = projected[face.VertexIndices[(i + 1) % face.VertexIndices.Count]];
                    graphics.DrawLine(pen, first.X, first.Y, second.X, second.Y);
                }
            }
        }

        return bitmap;
    }

    private void DrawFace(
        int[] pixels,
        float[] depthBuffer,
        int width,
        int height,
        ProjectedVertex[] vertices,
        CarFace face)
    {
        IReadOnlyList<int> indices = face.VertexIndices;
        IReadOnlyList<TextureCoordinate>? uv = face.TextureCoordinates;
        for (int triangle = 1; triangle < indices.Count - 1; triangle++)
        {
            int second = triangle;
            int third = triangle + 1;
            TextureCoordinate? uv0 = uv is null ? null : uv[0];
            TextureCoordinate? uv1 = uv is null ? null : uv[second];
            TextureCoordinate? uv2 = uv is null ? null : uv[third];
            RasterizeTriangle(
                pixels,
                depthBuffer,
                width,
                height,
                vertices[indices[0]],
                vertices[indices[second]],
                vertices[indices[third]],
                uv0,
                uv1,
                uv2,
                face);
        }
    }

    private void RasterizeTriangle(
        int[] pixels,
        float[] depthBuffer,
        int width,
        int height,
        ProjectedVertex a,
        ProjectedVertex b,
        ProjectedVertex c,
        TextureCoordinate? uvA,
        TextureCoordinate? uvB,
        TextureCoordinate? uvC,
        CarFace face)
    {
        float area = Edge(a.X, a.Y, b.X, b.Y, c.X, c.Y);
        if (Math.Abs(area) < 0.001f)
        {
            return;
        }

        int left = Math.Clamp((int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))), 0, width - 1);
        int right = Math.Clamp((int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))), 0, width - 1);
        int top = Math.Clamp((int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))), 0, height - 1);
        int bottom = Math.Clamp((int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))), 0, height - 1);
        if (left > right || top > bottom)
        {
            return;
        }

        bool textured = ShowTexture && _texture is not null && uvA.HasValue && uvB.HasValue && uvC.HasValue;
        float shade = FaceShade(a, b, c);
        Rgb32 flat = FlatColor(face.FlatColor);
        int palette = Math.Clamp(face.PaletteIndex, 0, 15);

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float wa = Edge(b.X, b.Y, c.X, c.Y, px, py) / area;
                float wb = Edge(c.X, c.Y, a.X, a.Y, px, py) / area;
                float wc = 1 - wa - wb;
                if (wa < 0 || wb < 0 || wc < 0)
                {
                    continue;
                }

                int pixelIndex = y * width + x;
                float depth = wa * a.Depth + wb * b.Depth + wc * c.Depth;
                if (depth <= depthBuffer[pixelIndex])
                {
                    continue;
                }

                Rgb32 color;
                if (textured)
                {
                    int u = Math.Clamp((int)(wa * uvA!.Value.U + wb * uvB!.Value.U + wc * uvC!.Value.U), 0, 255);
                    int v = Math.Clamp((int)(wa * uvA.Value.V + wb * uvB.Value.V + wc * uvC.Value.V), 0, CarTexture.TextureHeight - 1);
                    color = _texture!.Sample(
                        (byte)u,
                        (byte)v,
                        Math.Clamp(ColorIndex, 0, _texture.ColorCount - 1),
                        palette,
                        VerticalUvOffset);
                    if (color.A == 0)
                    {
                        continue;
                    }
                }
                else
                {
                    color = flat;
                }

                depthBuffer[pixelIndex] = depth;
                pixels[pixelIndex] = Shade(color, shade).ToArgb();
            }
        }
    }

    private static float Edge(float ax, float ay, float bx, float by, float px, float py) =>
        (bx - ax) * (py - ay) - (by - ay) * (px - ax);

    private static float FaceShade(ProjectedVertex a, ProjectedVertex b, ProjectedVertex c)
    {
        float ux = b.WorldX - a.WorldX;
        float uy = b.WorldY - a.WorldY;
        float uz = b.WorldZ - a.WorldZ;
        float vx = c.WorldX - a.WorldX;
        float vy = c.WorldY - a.WorldY;
        float vz = c.WorldZ - a.WorldZ;
        float nx = uy * vz - uz * vy;
        float ny = uz * vx - ux * vz;
        float nz = ux * vy - uy * vx;
        float length = Math.Max(MathF.Sqrt(nx * nx + ny * ny + nz * nz), 0.001f);
        float diffuse = Math.Abs((nx * 0.25f + ny * 0.55f + nz * 0.8f) / length);
        return 0.34f + 0.66f * diffuse;
    }

    private static Rgb32 FlatColor(uint packed)
    {
        if (packed == 0 || packed == 0xFFFFFF)
        {
            return new Rgb32(155, 168, 181);
        }

        return new Rgb32((byte)packed, (byte)(packed >> 8), (byte)(packed >> 16));
    }

    private static Color Shade(Rgb32 color, float intensity) =>
        Color.FromArgb(
            color.A,
            Math.Clamp((int)(color.R * intensity), 0, 255),
            Math.Clamp((int)(color.G * intensity), 0, 255),
            Math.Clamp((int)(color.B * intensity), 0, 255));

    private static void DrawBackgroundGrid(int[] pixels, int width, int height)
    {
        int horizon = (int)(height * 0.72);
        int gridColor = Color.FromArgb(25, 115, 137, 157).ToArgb();
        int horizonColor = Color.FromArgb(45, 136, 157, 176).ToArgb();
        DrawLine(pixels, width, height, 0, horizon, width - 1, horizon, horizonColor);
        for (int offset = 1; offset <= 8; offset++)
        {
            int x = width / 2 + offset * width / 16;
            DrawLine(pixels, width, height, width / 2, horizon, x, height - 1, gridColor);
            DrawLine(pixels, width, height, width / 2, horizon, width - x, height - 1, gridColor);
        }

        for (int row = 1; row <= 5; row++)
        {
            int y = horizon + row * (height - horizon) / 6;
            DrawLine(pixels, width, height, 0, y, width - 1, y, gridColor);
        }
    }

    private static void DrawLine(int[] pixels, int width, int height, int x0, int y0, int x1, int y1, int color)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;

        while (true)
        {
            if ((uint)x0 < (uint)width && (uint)y0 < (uint)height)
            {
                pixels[y0 * width + x0] = color;
            }

            if (x0 == x1 && y0 == y1)
            {
                return;
            }

            int doubledError = 2 * error;
            if (doubledError >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (doubledError <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private void DrawEmptyState(Graphics graphics)
    {
        using var titleFont = new Font("Segoe UI Semibold", 18, FontStyle.Bold);
        using var bodyFont = new Font("Segoe UI", 10);
        using var titleBrush = new SolidBrush(Color.FromArgb(235, 239, 244));
        using var bodyBrush = new SolidBrush(Color.FromArgb(150, 169, 186));
        string title = "Votre garage Gran Turismo";
        string message = "Ouvrez une image BIN ou un fichier CAR.DAT pour commencer.";
        SizeF titleSize = graphics.MeasureString(title, titleFont);
        SizeF bodySize = graphics.MeasureString(message, bodyFont);
        float centerY = Height / 2f;
        graphics.DrawString(title, titleFont, titleBrush, (Width - titleSize.Width) / 2, centerY - 28);
        graphics.DrawString(message, bodyFont, bodyBrush, (Width - bodySize.Width) / 2, centerY + 8);
    }

    private readonly record struct ProjectedVertex(
        float X,
        float Y,
        float Depth,
        float WorldX,
        float WorldY,
        float WorldZ);
}
