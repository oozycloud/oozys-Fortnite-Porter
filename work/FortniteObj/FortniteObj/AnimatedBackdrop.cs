using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace FortniteObj;

public sealed class AnimatedBackdrop : FrameworkElement
{
	private readonly DispatcherTimer _timer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(33L)
	};

	private readonly Stopwatch _clock = new Stopwatch();

	private readonly SolidColorBrush _spark = new SolidColorBrush(Color.FromArgb(150, byte.MaxValue, 145, 64));

	private readonly Pen _line = new Pen(new SolidColorBrush(Color.FromArgb(55, byte.MaxValue, 133, 51)), 1.0);

	private string _theme = "Embers";

	private string _previous = "Embers";

	private double _switched;

	private bool _reduced;

	public string Theme
	{
		get
		{
			return _theme;
		}
		set
		{
			if (!(_theme == value))
			{
				_previous = ((value == "Still") ? "Still" : _theme);
				_theme = value;
				_switched = _clock.Elapsed.TotalSeconds;
				Refresh();
			}
		}
	}

	public bool ReducedMotion
	{
		get
		{
			return _reduced;
		}
		set
		{
			_reduced = value;
			Refresh();
		}
	}

	public AnimatedBackdrop()
	{
		IsHitTestVisible = false;
		ClipToBounds = true;
		_spark.Freeze();
		_line.Freeze();
		_timer.Tick += (object? _, EventArgs _) =>
		{
			InvalidateVisual();
		};
		Loaded += (object _, RoutedEventArgs _) =>
		{
			_clock.Start();
			Refresh();
		};
		Unloaded += (object _, RoutedEventArgs _) =>
		{
			_timer.Stop();
			_clock.Stop();
		};
		IsVisibleChanged += (object _, DependencyPropertyChangedEventArgs _) =>
		{
			Refresh();
		};
	}

	private void Refresh()
	{
		if (IsLoaded && IsVisible && !_reduced && Theme != "Still")
		{
			_timer.Start();
		}
		else
		{
			_timer.Stop();
		}
		InvalidateVisual();
	}

	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);
		if (!(ActualWidth < 1.0) && !(ActualHeight < 1.0))
		{
			double num = (_reduced ? 0.0 : _clock.Elapsed.TotalSeconds);
			double num2 = (_reduced ? 1.0 : Math.Clamp((num - _switched) / 0.5, 0.0, 1.0));
			if (num2 < 1.0)
			{
				dc.PushOpacity(1.0 - num2);
				Draw(dc, _previous, num);
				dc.Pop();
			}
			dc.PushOpacity(num2);
			Draw(dc, _theme, num);
			dc.Pop();
		}
	}

	private void Draw(DrawingContext dc, string theme, double t)
	{
		double actualWidth = ActualWidth;
		double actualHeight = ActualHeight;
		switch (theme)
		{
		case "Embers":
		{
			for (int k = 0; k < 65; k++)
			{
				double x = (double)k * 0.61803398875 % 1.0 * actualWidth + Math.Sin(t * 0.35 + (double)k) * 18.0;
				double y2 = actualHeight - ((double)k * 43.73 + t * (double)(9 + k % 5 * 3)) % (actualHeight + 30.0);
				double num5 = 0.8 + (double)(k % 4) * 0.5;
				dc.PushOpacity(0.4 + 0.3 * Math.Sin(t + (double)k));
				dc.DrawEllipse(_spark, null, new Point(x, y2), num5, num5);
				dc.Pop();
			}
			break;
		}
		case "Orbit":
		{
			Point center = new Point(actualWidth * 0.76, actualHeight * 0.25);
			for (int j = 0; j < 7; j++)
			{
				int num3 = 60 + j * 47;
				dc.DrawEllipse(null, _line, center, num3, (double)num3 * 0.55);
				double num4 = t * (0.13 + (double)j * 0.015) + (double)j;
				dc.DrawEllipse(_spark, null, new Point(center.X + Math.Cos(num4) * (double)num3, center.Y + Math.Sin(num4) * (double)num3 * 0.55), 3.0, 3.0);
			}
			break;
		}
		case "Rain":
		{
			for (int i = 0; i < 58; i++)
			{
				double num = (double)i * 0.61803398875 % 1.0 * actualWidth;
				double num2 = ((double)i * 53.5 + t * (double)(55 + i % 7 * 9)) % (actualHeight + 90.0) - 60.0;
				dc.DrawLine(_line, new Point(num, num2), new Point(num - 12.0, num2 + 48.0));
			}
			double y = t * 30.0 % (actualHeight + 60.0) - 30.0;
			dc.DrawLine(_line, new Point(0.0, y), new Point(actualWidth, y));
			break;
		}
		}
	}
}
