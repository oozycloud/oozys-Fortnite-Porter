using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FortniteObj;

public static class UiMotion
{
	private sealed class MotionState
	{
		public readonly TranslateTransform Offset = new TranslateTransform();

		public readonly ScaleTransform Scale = new ScaleTransform();
	}

	public static readonly DependencyProperty ReduceMotionProperty = DependencyProperty.RegisterAttached("ReduceMotion", typeof(bool), typeof(UiMotion), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

	public static readonly DependencyProperty InteractiveProperty = DependencyProperty.RegisterAttached("Interactive", typeof(bool), typeof(UiMotion), new PropertyMetadata(false, InteractiveChanged));

	private static readonly ConditionalWeakTable<FrameworkElement, MotionState> States = new ConditionalWeakTable<FrameworkElement, MotionState>();

	public static void SetReduceMotion(DependencyObject element, bool value)
	{
		element.SetValue(ReduceMotionProperty, value);
	}

	public static bool GetReduceMotion(DependencyObject element)
	{
		return (bool)element.GetValue(ReduceMotionProperty);
	}

	public static void SetInteractive(DependencyObject element, bool value)
	{
		element.SetValue(InteractiveProperty, value);
	}

	public static bool GetInteractive(DependencyObject element)
	{
		return (bool)element.GetValue(InteractiveProperty);
	}

	private static bool Enabled(DependencyObject element)
	{
		if (SystemParameters.ClientAreaAnimation)
		{
			return !GetReduceMotion(element);
		}
		return false;
	}

	private static MotionState Prepare(FrameworkElement element)
	{
		return States.GetValue(element, (FrameworkElement item) =>
		{
			MotionState motionState = new MotionState();
			item.RenderTransformOrigin = new Point(0.5, 0.5);
			item.RenderTransform = new TransformGroup
			{
				Children = 
				{
					(Transform)motionState.Scale,
					(Transform)motionState.Offset
				}
			};
			return motionState;
		});
	}

	private static void Animate(Animatable target, DependencyProperty property, double value, int milliseconds, bool enabled, double? from = null, int delay = 0)
	{
		if (!enabled)
		{
			target.BeginAnimation(property, null);
			target.SetValue(property, value);
			return;
		}
		target.BeginAnimation(property, new DoubleAnimation
		{
			From = from,
			To = value,
			Duration = TimeSpan.FromMilliseconds(milliseconds),
			BeginTime = TimeSpan.FromMilliseconds(delay),
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		}, HandoffBehavior.SnapshotAndReplace);
	}

	public static void Reveal(FrameworkElement element, int delay = 0, double distance = 9.0)
	{
		if (element.IsVisible)
		{
			MotionState motionState = Prepare(element);
			bool flag = Enabled(element);
			Animate(motionState.Offset, TranslateTransform.YProperty, 0.0, 210, flag, distance, delay);
			element.BeginAnimation(UIElement.OpacityProperty, flag ? new DoubleAnimation(0.35, 1.0, TimeSpan.FromMilliseconds(180L))
			{
				BeginTime = TimeSpan.FromMilliseconds(delay),
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			} : null);
			element.Opacity = 1.0;
		}
	}

	private static void InteractiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d is Control control))
		{
			return;
		}
		if ((bool)e.NewValue)
		{
			control.MouseEnter += InteractionChanged;
			control.MouseLeave += InteractionChanged;
			control.PreviewMouseLeftButtonDown += InteractionChanged;
			control.PreviewMouseLeftButtonUp += InteractionChanged;
			control.GotKeyboardFocus += InteractionChanged;
			control.LostKeyboardFocus += InteractionChanged;
			control.PreviewKeyDown += KeyChanged;
			control.PreviewKeyUp += KeyChanged;
			control.IsEnabledChanged += EnabledChanged;
			if (control is CheckBox checkBox)
			{
				checkBox.Checked += SwitchChanged;
				checkBox.Unchecked += SwitchChanged;
				checkBox.Loaded += SwitchLoaded;
			}
		}
		else
		{
			control.MouseEnter -= InteractionChanged;
			control.MouseLeave -= InteractionChanged;
			control.PreviewMouseLeftButtonDown -= InteractionChanged;
			control.PreviewMouseLeftButtonUp -= InteractionChanged;
			control.GotKeyboardFocus -= InteractionChanged;
			control.LostKeyboardFocus -= InteractionChanged;
			control.PreviewKeyDown -= KeyChanged;
			control.PreviewKeyUp -= KeyChanged;
			control.IsEnabledChanged -= EnabledChanged;
			if (control is CheckBox checkBox2)
			{
				checkBox2.Checked -= SwitchChanged;
				checkBox2.Unchecked -= SwitchChanged;
				checkBox2.Loaded -= SwitchLoaded;
			}
		}
	}

	private static void InteractionChanged(object sender, RoutedEventArgs e)
	{
		bool pressed = e.RoutedEvent == UIElement.PreviewMouseLeftButtonDownEvent;
		ApplyInteraction((Control)sender, pressed);
	}

	private static void KeyChanged(object sender, KeyEventArgs e)
	{
		Key key = e.Key;
		if ((key == Key.Return || key == Key.Space) ? true : false)
		{
			ApplyInteraction((Control)sender, e.IsDown);
		}
	}

	private static void EnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		ApplyInteraction((Control)sender, pressed: false);
	}

	private static void SwitchChanged(object sender, RoutedEventArgs e)
	{
		UpdateSwitch((CheckBox)sender, animate: true);
	}

	private static void SwitchLoaded(object sender, RoutedEventArgs e)
	{
		UpdateSwitch((CheckBox)sender, animate: false);
	}

	private static void UpdateSwitch(CheckBox control, bool animate)
	{
		if (control.Template?.FindName("SwitchThumb", control) is FrameworkElement frameworkElement)
		{
			if (!(frameworkElement.RenderTransform is TranslateTransform) || frameworkElement.RenderTransform.IsFrozen)
			{
				frameworkElement.RenderTransform = new TranslateTransform();
			}
			Animate((TranslateTransform)frameworkElement.RenderTransform, TranslateTransform.XProperty, (control.IsChecked == true) ? 12 : 0, 150, animate && Enabled(control));
		}
	}

	private static void ApplyInteraction(Control control, bool pressed)
	{
		if (control.Template?.FindName("MotionSurface", control) is FrameworkElement element)
		{
			MotionState motionState = Prepare(element);
			bool flag = control.IsEnabled && (control.IsMouseOver || control.IsKeyboardFocusWithin);
			bool flag2 = Enabled(control);
			Animate(motionState.Offset, TranslateTransform.YProperty, (flag && !pressed) ? (-1.5) : 0.0, 150, flag2);
			Animate(motionState.Scale, ScaleTransform.ScaleXProperty, (flag & pressed) ? 0.985 : 1.0, 120, flag2);
			Animate(motionState.Scale, ScaleTransform.ScaleYProperty, (flag & pressed) ? 0.985 : 1.0, 120, flag2);
			if (control.Template.FindName("MotionGlow", control) is UIElement uIElement)
			{
				double num;
				if (flag)
				{
					num = (pressed ? 0.25 : 0.65);
				}
				else
				{
					num = 0.0;
				}
				uIElement.BeginAnimation(UIElement.OpacityProperty, flag2 ? new DoubleAnimation(num, TimeSpan.FromMilliseconds(160L)) : null);
				uIElement.Opacity = num;
			}
		}
	}

	public static void Reset(DependencyObject root)
	{
		if (root is FrameworkElement frameworkElement && States.TryGetValue(frameworkElement, out MotionState value))
		{
			value.Offset.BeginAnimation(TranslateTransform.YProperty, null);
			value.Offset.Y = 0.0;
			value.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			value.Scale.ScaleX = 1.0;
			value.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
			value.Scale.ScaleY = 1.0;
			frameworkElement.BeginAnimation(UIElement.OpacityProperty, null);
			frameworkElement.Opacity = 1.0;
		}
		if (root is Control control && GetInteractive(control))
		{
			ApplyInteraction(control, pressed: false);
		}
		if (root is CheckBox checkBox && GetInteractive(checkBox))
		{
			UpdateSwitch(checkBox, animate: false);
		}
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
		{
			Reset(VisualTreeHelper.GetChild(root, i));
		}
	}
}
