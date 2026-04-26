using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace MCSkinViewer.WPF
{
    /// <summary>
    /// Interaction logic for SkinViewer.xaml
    /// </summary>
    public partial class SkinViewer : UserControl
    {
        private Point _lastMousePos;
        private bool _isDragging = false;
        private double _rotationY = 0;
        private double _rotationX = 0;
        private double _cameraDistance = 5.0;

        private double _targetRotationX = 0;
        private double _targetRotationY = 0;
        private double _currentRotationX = 0;
        private double _currentRotationY = 0;

        private TimeSpan _lastRenderTime = TimeSpan.Zero;
        private bool _autoRotate = false;

        private Transform3DGroup _modelTransform;
        private AxisAngleRotation3D _rotY;
        private AxisAngleRotation3D _rotX;

        private BitmapImage _skinTexture;
        private Model3DGroup _lastBuiltGroup;

        public static readonly DependencyProperty SkinPathProperty =
            DependencyProperty.Register(nameof(SkinPath), typeof(string), typeof(SkinViewer),
                new PropertyMetadata(null, OnSkinPathChanged));

        public string SkinPath
        {
            get => (string)GetValue(SkinPathProperty);
            set => SetValue(SkinPathProperty, value);
        }

        private static void OnSkinPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SkinViewer viewer && e.NewValue is string path && !string.IsNullOrEmpty(path))
                viewer.LoadSkinFromFile(path);
        }

        public SkinViewer()
        {
            InitializeComponent();

            // Drag & Drop
            AllowDrop = true;
            Drop += MainWindow_Drop;

            _rotY = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
            _rotX = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
            _modelTransform = new Transform3DGroup();
            _modelTransform.Children.Add(new RotateTransform3D(_rotX));
            _modelTransform.Children.Add(new RotateTransform3D(_rotY));
            CompositionTarget.Rendering += OnRenderFrame;

            //_rotateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            //_rotateTimer.Tick += (s, e) =>
            //{
            //    _rotationY += 0.3;
            //    _rotY.Angle = _rotationY;
            //};
        }

        private void OnRenderFrame(object sender, EventArgs e)
        {
            var args = (RenderingEventArgs)e;

            if (_lastRenderTime == args.RenderingTime)
                return;

            var deltaTime = _lastRenderTime == TimeSpan.Zero
                ? 0
                : (args.RenderingTime - _lastRenderTime).TotalSeconds;

            _lastRenderTime = args.RenderingTime;

            if (_autoRotate && !_isDragging)
            {
                _targetRotationY += 45.0 * deltaTime;
            }

            var lerpFactor = 1.0 - Math.Exp(-10.0 * deltaTime);

            if (deltaTime > 0)
            {
                _currentRotationX += (_targetRotationX - _currentRotationX) * lerpFactor;
                _currentRotationY += (_targetRotationY - _currentRotationY) * lerpFactor;
            }
            else
            {
                _currentRotationX = _targetRotationX;
                _currentRotationY = _targetRotationY;
            }

            _rotX.Angle = _currentRotationX;
            _rotY.Angle = _currentRotationY;
        }

        private void FillRect(byte[] pixels, int width, int x, int y, int w, int h, byte r, byte g, byte b, byte a)
        {
            for (var row = y; row < y + h; row++)
                for (var col = x; col < x + w; col++)
                {
                    var idx = (row * width + col) * 4;
                    pixels[idx + 0] = b;
                    pixels[idx + 1] = g;
                    pixels[idx + 2] = r;
                    pixels[idx + 3] = a;
                }
        }

        private void LoadSkin_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select skin Minecraft PNG-file ",
                Filter = "PNG Image|*.png",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };
            if (dlg.ShowDialog() == true)
                LoadSkinFromFile(dlg.FileName);
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && files[0].EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    LoadSkinFromFile(files[0]);
            }
        }

        private void LoadSkinFromFile(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();

                // texture size (64x64 -> 2048x2048)
                var sharpBmp = UpscaleImage(bmp, 32);

                _skinTexture = sharpBmp as BitmapImage;
                BuildPlayerModel(sharpBmp);
                statusText.Text = $"Skin loaded: {Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Skin loading error: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private BitmapSource UpscaleImage(BitmapSource source, int scale)
        {
            var originalWidth = source.PixelWidth;
            var originalHeight = source.PixelHeight;

            var newWidth = originalWidth * scale;
            var newHeight = originalHeight * scale;

            var format = PixelFormats.Bgra32;
            var stride = (originalWidth * format.BitsPerPixel + 7) / 8;
            var originalPixels = new byte[originalHeight * stride];

            if (source.Format != format)
            {
                var convertedSource = new FormatConvertedBitmap(source, format, null, 0);
                convertedSource.CopyPixels(originalPixels, stride, 0);
            }
            else
            {
                source.CopyPixels(originalPixels, stride, 0);
            }

            var newStride = (newWidth * format.BitsPerPixel + 7) / 8;
            var newPixels = new byte[newHeight * newStride];

            for (var y = 0; y < newHeight; y++)
            {
                var origY = y / scale;

                for (var x = 0; x < newWidth; x++)
                {
                    var origX = x / scale;

                    var origIndex = origY * stride + origX * 4;
                    var newIndex = y * newStride + x * 4;

                    // Copy 4 bytes (B, G, R, A)
                    newPixels[newIndex] = originalPixels[origIndex];         // B
                    newPixels[newIndex + 1] = originalPixels[origIndex + 1]; // G
                    newPixels[newIndex + 2] = originalPixels[origIndex + 2]; // R
                    newPixels[newIndex + 3] = originalPixels[origIndex + 3]; // A
                }
            }

            return BitmapSource.Create(
                newWidth, newHeight,
                96, 96,
                format, null,
                newPixels, newStride);
        }

        private void BuildPlayerModel(ImageSource skin)
        {
            var rootGroup = new Model3DGroup();
            double W = 64.0, H = 64.0;

            void AddPart(Point3D center, double sx, double sy, double sz,
                (double, double, double, double) fr, (double, double, double, double) bk,
                (double, double, double, double) le, (double, double, double, double) ri,
                (double, double, double, double) tp, (double, double, double, double) bt)
            {
                CreateCuboid(center, sx, sy, sz, skin, W, H, fr, bk, le, ri, tp, bt);
                rootGroup.Children.Add(_lastBuiltGroup);
            }

            // head
            AddPart(new Point3D(0, 1.25, 0), 0.5, 0.5, 0.5,
                (8, 8, 8, 8), (24, 8, 8, 8), (0, 8, 8, 8), (16, 8, 8, 8), (8, 0, 8, 8), (16, 0, 8, 8));
            // body
            AddPart(new Point3D(0, 0.625, 0), 0.5, 0.75, 0.25,
                (20, 20, 8, 12), (32, 20, 8, 12), (16, 20, 4, 12), (28, 20, 4, 12), (20, 16, 8, 4), (28, 16, 8, 4));
            // right hand
            AddPart(new Point3D(-0.375, 0.625, 0), 0.25, 0.75, 0.25,
                (44, 20, 4, 12), // Front
                (52, 20, 4, 12), // Back
                (48, 20, 4, 12), // Left
                (40, 20, 4, 12), // Right
                (44, 16, 4, 4),  // Top
                (48, 16, 4, 4)   // Bottom
            );
            // left hand
            AddPart(new Point3D(0.375, 0.625, 0), 0.25, 0.75, 0.25,
                (36, 52, 4, 12), (44, 52, 4, 12), (40, 52, 4, 12), (32, 52, 4, 12), (36, 48, 4, 4), (40, 48, 4, 4));
            // right leg
            AddPart(new Point3D(-0.125, -0.125, 0), 0.25, 0.75, 0.25,
                (4, 20, 4, 12), (12, 20, 4, 12), (0, 20, 4, 12), (8, 20, 4, 12), (4, 16, 4, 4), (8, 16, 4, 4));
            // left leg
            AddPart(new Point3D(0.125, -0.125, 0), 0.25, 0.75, 0.25,
                (20, 52, 4, 12), (28, 52, 4, 12), (24, 52, 4, 12), (16, 52, 4, 12), (20, 48, 4, 4), (24, 48, 4, 4));


            // OVERLAY / LAYER 2
            var inflate = 0.03;

            // Head (Hat) - UV: x + 32
            AddPart(new Point3D(0, 1.25, 0), 0.5 + inflate, 0.5 + inflate, 0.5 + inflate,
                (40, 8, 8, 8), (56, 8, 8, 8), (32, 8, 8, 8), (48, 8, 8, 8), (40, 0, 8, 8), (48, 0, 8, 8));

            // Body (Jacket) - UV: x=20, y=36 (instead of y=20)
            AddPart(new Point3D(0, 0.625, 0), 0.5 + inflate, 0.75 + inflate, 0.25 + inflate,
                (20, 36, 8, 12), (32, 36, 8, 12), (16, 36, 4, 12), (28, 36, 4, 12), (20, 32, 8, 4), (28, 32, 8, 4));

            // Right hand (Sleeve) - UV: x=44, y=36 (instead of y=20)
            AddPart(new Point3D(-0.375, 0.625, 0), 0.25 + inflate, 0.75 + inflate, 0.25 + inflate,
                (44, 36, 4, 12), (52, 36, 4, 12), (40, 36, 4, 12), (48, 36, 4, 12), (44, 32, 4, 4), (48, 32, 4, 4));

            // Left arm (Sleeve) - UV: x=52, y=52 (instead of x=36, y=52)
            AddPart(new Point3D(0.375, 0.625, 0), 0.25 + inflate, 0.75 + inflate, 0.25 + inflate,
                (52, 52, 4, 12), (60, 52, 4, 12), (56, 52, 4, 12), (48, 52, 4, 12), (52, 48, 4, 4), (56, 48, 4, 4));

            // Right leg (Pant leg) - UV: x=4, y=36 (instead of y=20)
            AddPart(new Point3D(-0.125, -0.125, 0), 0.25 + inflate, 0.75 + inflate, 0.25 + inflate,
                (4, 36, 4, 12), (12, 36, 4, 12), (0, 36, 4, 12), (8, 36, 4, 12), (4, 32, 4, 4), (8, 32, 4, 4));

            // Left leg (Pant leg) - UV: x=4, y=52 (instead of x=20, y=52)
            AddPart(new Point3D(0.125, -0.125, 0), 0.25 + inflate, 0.75 + inflate, 0.25 + inflate,
                (4, 52, 4, 12), (12, 52, 4, 12), (8, 52, 4, 12), (0, 52, 4, 12), (4, 48, 4, 4), (8, 48, 4, 4));


            var modelVisual = new ModelVisual3D
            {
                Content = rootGroup,
                Transform = _modelTransform
            };

            playerModel.Children.Clear();
            playerModel.Children.Add(modelVisual);
        }

        private void CreateCuboid(Point3D center,
            double sizeX, double sizeY, double sizeZ,
            ImageSource skin, double texW, double texH,
            (double x, double y, double w, double h) front,
            (double x, double y, double w, double h) back,
            (double x, double y, double w, double h) left,
            (double x, double y, double w, double h) right,
            (double x, double y, double w, double h) top,
            (double x, double y, double w, double h) bottom)
        {
            double hx = sizeX / 2, hy = sizeY / 2, hz = sizeZ / 2;
            double cx = center.X, cy = center.Y, cz = center.Z;

            var group = new Model3DGroup();

            void AddFace(
                Point3D p0, Point3D p1, Point3D p2, Point3D p3,
                (double x, double y, double w, double h) uv,
                Vector3D normal)
            {
                var mesh = new MeshGeometry3D();

                mesh.Positions.Add(p0);
                mesh.Positions.Add(p1);
                mesh.Positions.Add(p2);
                mesh.Positions.Add(p3);

                mesh.Normals.Add(normal);
                mesh.Normals.Add(normal);
                mesh.Normals.Add(normal);
                mesh.Normals.Add(normal);

                mesh.TextureCoordinates.Add(new Point(0, 0));
                mesh.TextureCoordinates.Add(new Point(1, 0));
                mesh.TextureCoordinates.Add(new Point(1, 1));
                mesh.TextureCoordinates.Add(new Point(0, 1));

                mesh.TriangleIndices.Add(0);
                mesh.TriangleIndices.Add(1);
                mesh.TriangleIndices.Add(2);
                mesh.TriangleIndices.Add(0);
                mesh.TriangleIndices.Add(2);
                mesh.TriangleIndices.Add(3);

                var mat = CreateFaceMaterial(skin, texW, texH, uv);

                var model = new GeometryModel3D(mesh, mat)
                {
                    BackMaterial = mat
                };
                group.Children.Add(model);
            }


            // Front (+Z)
            AddFace(new Point3D(cx - hx, cy + hy, cz + hz),
                    new Point3D(cx + hx, cy + hy, cz + hz),
                    new Point3D(cx + hx, cy - hy, cz + hz),
                    new Point3D(cx - hx, cy - hy, cz + hz), front,
                    new Vector3D(0, 0, 1));

            // Back (-Z)
            AddFace(new Point3D(cx + hx, cy + hy, cz - hz),
                    new Point3D(cx - hx, cy + hy, cz - hz),
                    new Point3D(cx - hx, cy - hy, cz - hz),
                    new Point3D(cx + hx, cy - hy, cz - hz), back,
                    new Vector3D(0, 0, -1));

            // Left (-X)
            AddFace(new Point3D(cx - hx, cy + hy, cz - hz),
                    new Point3D(cx - hx, cy + hy, cz + hz),
                    new Point3D(cx - hx, cy - hy, cz + hz),
                    new Point3D(cx - hx, cy - hy, cz - hz), left,
                    new Vector3D(-1, 0, 0));

            // Right (+X)
            AddFace(new Point3D(cx + hx, cy + hy, cz + hz),
                    new Point3D(cx + hx, cy + hy, cz - hz),
                    new Point3D(cx + hx, cy - hy, cz - hz),
                    new Point3D(cx + hx, cy - hy, cz + hz), right,
                    new Vector3D(1, 0, 0));

            // Top (+Y)
            AddFace(new Point3D(cx - hx, cy + hy, cz - hz),
                    new Point3D(cx + hx, cy + hy, cz - hz),
                    new Point3D(cx + hx, cy + hy, cz + hz),
                    new Point3D(cx - hx, cy + hy, cz + hz), top,
                    new Vector3D(0, 1, 0));

            // Bottom (-Y)
            AddFace(new Point3D(cx - hx, cy - hy, cz + hz),
                    new Point3D(cx + hx, cy - hy, cz + hz),
                    new Point3D(cx + hx, cy - hy, cz - hz),
                    new Point3D(cx - hx, cy - hy, cz - hz), bottom,
                    new Vector3D(0, -1, 0));


            _lastBuiltGroup = group;
        }

        private Material CreateFaceMaterial(ImageSource skin, double texW, double texH, (double x, double y, double w, double h) uv)
        {
            var brush = new ImageBrush(skin)
            {
                Viewbox = new Rect(
                    uv.x / texW,
                    uv.y / texH,
                    uv.w / texW,
                    uv.h / texH),
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewport = new Rect(0, 0, 1, 1),
                TileMode = TileMode.None,
                Stretch = Stretch.Fill
            };

            RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.NearestNeighbor);

            var materialGroup = new MaterialGroup();
            materialGroup.Children.Add(new DiffuseMaterial(brush));

            return materialGroup;
        }

        private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _lastMousePos = e.GetPosition(viewport);
            viewport.CaptureMouse();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _isDragging = false;
            viewport.ReleaseMouseCapture();
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging)
                return;

            var pos = e.GetPosition(viewport);
            var deltaX = pos.X - _lastMousePos.X;
            var deltaY = pos.Y - _lastMousePos.Y;

            _targetRotationY += deltaX * 0.5;
            _targetRotationX += deltaY * 0.5;

            _targetRotationX = Math.Max(-80, Math.Min(80, _targetRotationX));

            _lastMousePos = pos;
        }

        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            _cameraDistance -= e.Delta / 500.0;
            _cameraDistance = Math.Max(2.0, Math.Min(10.0, _cameraDistance));
            camera.Position = new Point3D(0, 0, _cameraDistance);
        }

        private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var baseFov = 45.0;
            var width = e.NewSize.Width;
            var height = e.NewSize.Height;

            if (width == 0 || height == 0)
                return;

            var aspectRatio = width / height;

            if (aspectRatio > 1.0)
            {
                var fovRad = baseFov * Math.PI / 180.0;
                var newFovRad = 2.0 * Math.Atan(Math.Tan(fovRad / 2.0) * aspectRatio);
                camera.FieldOfView = newFovRad * 180.0 / Math.PI;
            }
            else
            {
                camera.FieldOfView = baseFov;
            }
        }

        private void AutoRotate_Click(object sender, RoutedEventArgs e)
        {
            _autoRotate = !_autoRotate;
            btnAutoRotate.BorderBrush = new SolidColorBrush(_autoRotate ? Colors.LimeGreen : Color.FromRgb(0x0f, 0x34, 0x60));
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            _targetRotationX = 0;
            _targetRotationY = 0;

            _cameraDistance = 5.0;
            camera.Position = new Point3D(0, 0, _cameraDistance);
        }
    }
}