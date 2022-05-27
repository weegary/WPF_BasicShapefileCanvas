using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NetTopologySuite.IO;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace WPF_BasicShapefileCanvas
{
    
    public partial class MainWindow : Window
    {
        List<string> m_fileName = new List<string>();
        Shapefiles m_shapefiles = new Shapefiles();
        public MainWindow()
        {
            InitializeComponent();
            isClicked = false;
        }

        #region Map Canvas Mouse Control - Transform and Scale
        public bool isClicked
        {
            get;
            set;
        }
        System.Windows.Point mouse_position;
        private void ImageMouseDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition((IInputElement)sender);
            mouse_position = e.GetPosition((Canvas)sender);
            isClicked = true;
        }

        private void ImageMouseUp(object sender, MouseButtonEventArgs e)
        {
            isClicked = false;

        }

        private void ImageMouseMove(object sender, MouseEventArgs e)
        {
            if (isClicked)
            {
                System.Windows.Point mouse_position_now = e.GetPosition(map_canvas);
                TranslateTransform tt = new TranslateTransform();
                tt.X = (mouse_position_now.X - mouse_position.X) / 300;
                tt.Y = (mouse_position_now.Y - mouse_position.Y) / 300;

                foreach (var child in map_canvas.Children)
                {
                    System.Windows.Shapes.Polygon p = (System.Windows.Shapes.Polygon)child;
                    if (p.Name != "background")
                    {
                        TransformGroup tg = (TransformGroup)p.RenderTransform;
                        tg.Children.Add(tt);
                        p.RenderTransform = tg;
                    }
                }
            }
        }

        private void ImageMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition((IInputElement)sender);
            mouse_position = e.GetPosition((Canvas)sender);
            ScaleTransform st = new ScaleTransform();
            st.CenterX = mouse_position.X;
            st.CenterY = mouse_position.Y;
            double scale = 1.01;
            if (e.Delta > 0)
            {
                st.ScaleX *= scale;
                st.ScaleY *= scale;
            }
            else
            {
                st.ScaleX /= scale;
                st.ScaleY /= scale;
            }

            foreach (var child in map_canvas.Children)
            {
                System.Windows.Shapes.Polygon p = (System.Windows.Shapes.Polygon)child;
                if (p.Name != "background")
                {
                    TransformGroup tg = (TransformGroup)p.RenderTransform;
                    tg.Children.Add(st);
                    p.RenderTransform = tg;
                }
            }
        }
        #endregion

        private void btn_AddShapefile(object sender, RoutedEventArgs e)
        {
            m_fileName.Add(@"shp\臺北市區.shp");
            m_shapefiles.Canvas = map_canvas;
            map_canvas.Children.Clear();
            System.Windows.Shapes.Polygon background = new System.Windows.Shapes.Polygon();
            background.Name = "background";
            background.Points = new PointCollection() { new System.Windows.Point(0, 0), new System.Windows.Point(map_canvas.ActualWidth, 0), new System.Windows.Point(map_canvas.ActualWidth, map_canvas.ActualHeight), new System.Windows.Point(0, map_canvas.ActualHeight) };
            background.Fill = Brushes.White;
            map_canvas.Children.Add(background);
            LoadShapefiles();
        }
        public void LoadShapefiles()
        {
            for (int i = 0; i < m_fileName.Count; i++)
            {
                m_shapefiles.Add(new Shapefile(m_fileName[i]));
            }
        }
    }
    public class Shapefiles : ObservableCollection<Shapefile>
    {
        public Canvas Canvas { get; set; }
        public Shapefiles()
        {
            this.CollectionChanged += OnCollectionChanged;
        }
        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (Shapefile shp in e.NewItems)
                {
                    shp.Plot(Canvas);
                }
            }
        }

    }
    public class Shapefile
    {
        FeatureCollection features = new FeatureCollection();
        ShapeGeometryType shapeGeometryType;
        Envelope bounds;

        public Shapefile(string shapefile_fileName)
        {
            ShapefileDataReader reader = new ShapefileDataReader(shapefile_fileName, NetTopologySuite.Geometries.GeometryFactory.Default);
            shapeGeometryType = reader.ShapeHeader.ShapeType;
            bounds = reader.ShapeHeader.Bounds;
            var dt = reader.DbaseHeader.Fields;
            DbaseFileHeader header = reader.DbaseHeader;
            string[] keys = new string[header.NumFields];
            while (reader.Read())
            {
                Feature feature = new Feature();
                feature.Geometry = (NetTopologySuite.Geometries.Geometry)reader.Geometry;

                AttributesTable attributesTable = new AttributesTable();
                for (int i = 1; i <= header.NumFields; i++)
                {
                    DbaseFieldDescriptor fldDescriptor = header.Fields[i - 1];
                    keys[i - 1] = fldDescriptor.Name;
                    attributesTable.Add(fldDescriptor.Name, reader.GetValue(i));
                }
                feature.Attributes = attributesTable;
                features.Add(feature);
            }

            reader.Close();
            reader.Dispose();
        }
        public void Plot(Canvas canvas)
        {
            TransformGroup tg = GetTransformGroup(canvas);
            foreach (Feature feature in features)
            {
                for (int i = 0; i < feature.Geometry.NumGeometries; i++)
                {
                    var g = feature.Geometry.GetGeometryN(i);
                    System.Windows.Shapes.Polygon p = new System.Windows.Shapes.Polygon();
                    p.Points = ConvertToPointCollection(g.Coordinates);
                    p.Stroke = Brushes.Black;
                    p.StrokeThickness = 1 / ((ScaleTransform)(tg.Children[1])).ScaleX;
                    p.Fill = Brushes.LightBlue;
                    p.RenderTransform = tg;
                    canvas.Children.Add(p);
                }
            }
        }
        private TransformGroup GetTransformGroup(Canvas canvas)
        {
            Coordinate feature_center = bounds.Centre;
            System.Windows.Point canvas_center = new System.Windows.Point(canvas.ActualWidth / 2, canvas.ActualHeight / 2);
            TranslateTransform tt = new TranslateTransform();
            tt.X = (canvas_center.X - feature_center.X);
            tt.Y = (canvas_center.Y - feature_center.Y);
            ScaleTransform st = new ScaleTransform();
            double scale = 1;
            if (bounds.Height >= bounds.Width)
            {
                scale = canvas.ActualHeight / bounds.Height;
            }
            if (bounds.Width > bounds.Height)
            {
                scale = canvas.ActualWidth / bounds.Width;
            }
            st.CenterX = canvas_center.X;
            st.CenterY = canvas_center.Y;
            st.ScaleX *= scale;
            st.ScaleY *= -scale;
            TransformGroup tg = new TransformGroup();
            tg.Children.Add(tt);
            tg.Children.Add(st);
            return tg;
        }

        public static PointCollection ConvertToPointCollection(NetTopologySuite.Geometries.Coordinate[] coordinates)
        {
            PointCollection p = new PointCollection();
            for (int i = 0; i < coordinates.Length; i++)
            {
                p.Add(new System.Windows.Point(coordinates[i].X, coordinates[i].Y));
            }
            return p;
        }
    }
}
