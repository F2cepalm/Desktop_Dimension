using System;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using InfiniteSpace.Rendering;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace InfiniteSpace.Shell;

internal static class ArrowHandle
{
	public const double Size = 18.0;

	private const double Art = 16.0;

	private static readonly Brush Backing = Freeze(new SolidColorBrush(System.Windows.Media.Color.FromArgb(230, 30, 30, 30)));

	private static readonly Brush Edge = Freeze(new SolidColorBrush(System.Windows.Media.Color.FromArgb(204, byte.MaxValue, byte.MaxValue, byte.MaxValue)));

	private static readonly Geometry Arrow = Freeze(Geometry.Parse("M1,8 L5,4 L5,6.5 L11,6.5 L11,4 L15,8 L11,12 L11,9.5 L5,9.5 L5,12 Z"));

	public static double AngleFor(BlockGrip grip)
	{
		int num;
		switch (grip)
		{
		case BlockGrip.Left:
		case BlockGrip.Right:
			num = 0;
			break;
		case BlockGrip.Top:
		case BlockGrip.Bottom:
			num = 90;
			break;
		case BlockGrip.Left | BlockGrip.Top:
		case BlockGrip.Right | BlockGrip.Bottom:
			num = 45;
			break;
		default:
			num = 135;
			break;
		}
		return num;
	}

	public static FrameworkElement Stretch(double angle)
	{
		Geometry geometry = Arrow.Clone();
		geometry.Transform = new RotateTransform(angle, 8.0, 8.0);
		geometry.Freeze();
		return Handle(geometry);
	}

	public static FrameworkElement Move()
	{
		Geometry geometry = Arrow.Clone();
		geometry.Transform = new RotateTransform(90.0, 8.0, 8.0);
		GeometryGroup geometryGroup = new GeometryGroup();
		geometryGroup.FillRule = FillRule.Nonzero;
		geometryGroup.Children.Add(Arrow);
		geometryGroup.Children.Add(geometry);
		geometryGroup.Freeze();
		return Handle(geometryGroup);
	}

	private static FrameworkElement Handle(Geometry arrow)
	{
		return new Grid
		{
			Width = 18.0,
			Height = 18.0,
			Children = 
			{
				(UIElement)new System.Windows.Shapes.Ellipse
				{
					Fill = Backing,
					Stroke = Edge,
					StrokeThickness = 1.0
				},
				(UIElement)new Path
				{
					Data = arrow,
					Fill = Brushes.White,
					Width = 16.0,
					Height = 16.0,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					RenderTransform = new ScaleTransform(0.8, 0.8, 8.0, 8.0)
				}
			}
		};
	}

	public static OverlayPicture Picture(GpuCompositor gpu, double angle)
	{
		return OverlayPicture.Paint(gpu, 18.0, 18.0, (ID2D1DeviceContext context) =>
		{
			Matrix3x2 transform = context.Transform;
			using ID2D1SolidColorBrush brush = context.CreateSolidColorBrush(new Color4(30f / 255f, 30f / 255f, 30f / 255f, 230f / 255f));
			using ID2D1SolidColorBrush brush2 = context.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 0.8f));
			using ID2D1SolidColorBrush brush3 = context.CreateSolidColorBrush(new Color4(1f, 1f, 1f));
			Vortice.Direct2D1.Ellipse ellipse = new Vortice.Direct2D1.Ellipse(new Vector2(9f, 9f), 8.5f, 8.5f);
			context.FillEllipse(ellipse, brush);
			context.DrawEllipse(ellipse, brush2, 1f);
			Vector2 centerPoint = new Vector2(8f, 8f);
			context.Transform = Matrix3x2.CreateRotation((float)(angle * Math.PI / 180.0), centerPoint) * Matrix3x2.CreateScale(0.8f, centerPoint) * Matrix3x2.CreateTranslation(1f, 1f) * transform;
			using ID2D1PathGeometry1 iD2D1PathGeometry = gpu.D2DFactory.CreatePathGeometry();
			using (ID2D1GeometrySink iD2D1GeometrySink = iD2D1PathGeometry.Open())
			{
				iD2D1GeometrySink.BeginFigure(new Vector2(1f, 8f), FigureBegin.Filled);
				iD2D1GeometrySink.AddLines(new Vector2[9]
				{
					new Vector2(5f, 4f),
					new Vector2(5f, 6.5f),
					new Vector2(11f, 6.5f),
					new Vector2(11f, 4f),
					new Vector2(15f, 8f),
					new Vector2(11f, 12f),
					new Vector2(11f, 9.5f),
					new Vector2(5f, 9.5f),
					new Vector2(5f, 12f)
				});
				iD2D1GeometrySink.EndFigure(FigureEnd.Closed);
				iD2D1GeometrySink.Close();
			}
			context.FillGeometry(iD2D1PathGeometry, brush3);
			context.Transform = transform;
		});
	}

	private static T Freeze<T>(T freezable) where T : Freezable
	{
		freezable.Freeze();
		return freezable;
	}
}
