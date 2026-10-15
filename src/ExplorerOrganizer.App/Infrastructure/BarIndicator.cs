using System.Windows;
using System.Windows.Media;

namespace ExplorerOrganizer.App.Infrastructure;

/// <summary>Thin vertical gauge (track + fill from the bottom), as used by the Home "Organize" card.</summary>
public sealed class BarIndicator : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(BarIndicator),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, null, (_, v) => Math.Clamp((double)v, 0, 1)));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(Brush), typeof(BarIndicator), new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(BarIndicator), new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public BarIndicator()
    {
        Width = 4;
        SetResourceReference(TrackProperty, "BarTrack");
        SetResourceReference(FillProperty, "BarFill");
    }

    /// <summary>0..1</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public Brush Track { get => (Brush)GetValue(TrackProperty); set => SetValue(TrackProperty, value); }
    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight, r = w / 2;
        if (w <= 0 || h <= 0) return;
        dc.DrawRoundedRectangle(Track, null, new Rect(0, 0, w, h), r, r);
        double fill = Math.Max(Value > 0 ? w : 0, h * Value);
        if (fill > 0) dc.DrawRoundedRectangle(Fill, null, new Rect(0, h - fill, w, fill), r, r);
    }
}
