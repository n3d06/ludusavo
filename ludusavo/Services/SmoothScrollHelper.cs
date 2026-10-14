using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ludusavo.Services;

public static class SmoothScrollHelper
{
    private static readonly DependencyProperty TargetOffsetProperty =
        DependencyProperty.RegisterAttached("TargetOffset", typeof(double), typeof(SmoothScrollHelper), new PropertyMetadata(0.0));

    private static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.RegisterAttached("IsActive", typeof(bool), typeof(SmoothScrollHelper), new PropertyMetadata(false));

    public static readonly DependencyProperty EnableSmoothScrollProperty =
        DependencyProperty.RegisterAttached(
            "EnableSmoothScroll",
            typeof(bool),
            typeof(SmoothScrollHelper),
            new PropertyMetadata(false, OnEnableSmoothScrollChanged));

    public static bool GetEnableSmoothScroll(DependencyObject obj) => (bool)obj.GetValue(EnableSmoothScrollProperty);
    public static void SetEnableSmoothScroll(DependencyObject obj, bool value) => obj.SetValue(EnableSmoothScrollProperty, value);

    private static void OnEnableSmoothScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer sv)
        {
            sv.PreviewMouseWheel -= OnPreviewMouseWheel;
            if ((bool)e.NewValue)
            {
                sv.PreviewMouseWheel += OnPreviewMouseWheel;
            }
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;

        double currentTarget = (bool)sv.GetValue(IsActiveProperty)
            ? (double)sv.GetValue(TargetOffsetProperty)
            : sv.VerticalOffset;

        double newTarget = Math.Clamp(currentTarget - e.Delta, 0, sv.ScrollableHeight);
        sv.SetValue(TargetOffsetProperty, newTarget);
        e.Handled = true;

        if (!(bool)sv.GetValue(IsActiveProperty))
        {
            sv.SetValue(IsActiveProperty, true);

            void OnRendering(object? s, EventArgs args)
            {
                double target = (double)sv.GetValue(TargetOffsetProperty);
                double diff = target - sv.VerticalOffset;

                if (Math.Abs(diff) < 0.5 || !sv.IsLoaded)
                {
                    sv.ScrollToVerticalOffset(target);
                    sv.SetValue(IsActiveProperty, false);
                    CompositionTarget.Rendering -= OnRendering;
                }
                else
                {
                    sv.ScrollToVerticalOffset(sv.VerticalOffset + diff * 0.22);
                }
            }

            CompositionTarget.Rendering += OnRendering;
        }
    }
}

