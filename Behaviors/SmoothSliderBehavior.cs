using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Launcher.Behaviors;

public static class SmoothSliderBehavior
{
    public static readonly DependencyProperty EnableProperty =
        DependencyProperty.RegisterAttached(
            "Enable",
            typeof(bool),
            typeof(SmoothSliderBehavior),
            new PropertyMetadata(false, OnEnableChanged));

    public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);

    public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Slider slider)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            slider.IsMoveToPointEnabled = true;
            slider.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        }
        else
        {
            slider.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider)
        {
            return;
        }

        if (IsThumbSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var range = slider.Maximum - slider.Minimum;
        if (range <= 0 || slider.ActualWidth <= 0)
        {
            return;
        }

        var position = e.GetPosition(slider).X;
        var percent = Math.Clamp(position / slider.ActualWidth, 0d, 1d);
        var target = slider.Minimum + (range * percent);

        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        slider.BeginAnimation(RangeBase.ValueProperty, animation);
        e.Handled = true;
    }

    private static bool IsThumbSource(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is Thumb)
            {
                return true;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
