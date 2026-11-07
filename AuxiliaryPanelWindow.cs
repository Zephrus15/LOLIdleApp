using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Cursors = System.Windows.Input.Cursors;

namespace LoLIdleDesktop;

public sealed class AuxiliaryPanelWindow : Window
{
    private readonly bool _supportsOpacity;
    private readonly TextBlock _opacityHint;
    private readonly TextBlock _contentText;
    private double _panelOpacity;

    public AuxiliaryPanelWindow(string title, string description, bool supportsOpacity, double opacity)
    {
        _supportsOpacity = supportsOpacity;
        _panelOpacity = Math.Clamp(opacity, 0.15, 1.0);

        Title = title;
        Width = 340;
        Height = 250;
        MinWidth = 260;
        MinHeight = 160;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        ShowInTaskbar = true;
        Background = new SolidColorBrush(Color.FromRgb(17, 19, 24));
        Opacity = supportsOpacity ? _panelOpacity : 1.0;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid
        {
            Background = new SolidColorBrush(Color.FromRgb(25, 28, 35)),
            Margin = new Thickness(0)
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });

        var titleText = new TextBlock
        {
            Text = title,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        header.Children.Add(titleText);
        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        };

        var close = new Button
        {
            Content = "×",
            Foreground = Brushes.White,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 20,
            Cursor = Cursors.Hand,
            ToolTip = "Fechar painel"
        };
        close.Click += (_, _) => Hide();
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var body = new StackPanel
        {
            Margin = new Thickness(16),
            VerticalAlignment = VerticalAlignment.Center
        };
        body.Children.Add(new TextBlock
        {
            Text = "INFORMAÇÕES DO JOGO",
            Foreground = new SolidColorBrush(Color.FromRgb(232, 207, 143)),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        _contentText = new TextBlock
        {
            Text = description,
            Foreground = new SolidColorBrush(Color.FromRgb(190, 196, 207)),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };
        body.Children.Add(_contentText);
        _opacityHint = new TextBlock
        {
            Text = supportsOpacity
                ? $"Transparência: {Math.Round(_panelOpacity * 100)}% (aprox. de opacidade)"
                : "Este painel permanece totalmente visível.",
            Foreground = new SolidColorBrush(Color.FromRgb(130, 139, 153)),
            FontSize = 10,
            Margin = new Thickness(0, 14, 0, 0)
        };
        body.Children.Add(_opacityHint);
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        Content = root;

        if (supportsOpacity)
        {
            MouseEnter += (_, _) => Opacity = 1.0;
            MouseLeave += (_, _) => Opacity = _panelOpacity;
        }
    }

    public void SetPanelContent(string content)
    {
        _contentText.Text = string.IsNullOrWhiteSpace(content)
            ? "Não encontrei dados visíveis desta área no momento. Abra a seção correspondente no jogo."
            : content;
    }

    public void SetPanelOpacity(double value)
    {
        _panelOpacity = Math.Clamp(value, 0.15, 1.0);
        if (_supportsOpacity)
        {
            Opacity = IsMouseOver ? 1.0 : _panelOpacity;
            _opacityHint.Text = $"Opacidade: {Math.Round(_panelOpacity * 100)}% (ao aproximar o mouse fica 100%)";
        }
    }

    public void SetPanelVisible(bool visible)
    {
        if (visible)
        {
            if (!IsVisible)
                Show();
            Activate();
        }
        else
        {
            Hide();
        }
    }
}
