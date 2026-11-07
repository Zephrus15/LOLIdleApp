using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using TabControl = System.Windows.Controls.TabControl;
using Cursors = System.Windows.Input.Cursors;

namespace LoLIdleDesktop;

public sealed record PanelActionItem(string Id, string Label, string Category, string Context);

public sealed class AuxiliaryPanelWindow : Window
{
    private readonly bool _supportsOpacity;
    private readonly TextBlock _opacityHint;
    private readonly TextBlock _contentText;
    private readonly StackPanel _actionsList;
    private readonly TextBlock _actionStatus;
    private double _panelOpacity;

    public AuxiliaryPanelWindow(string title, string description, bool supportsOpacity, double opacity)
    {
        _supportsOpacity = supportsOpacity;
        _panelOpacity = Math.Clamp(opacity, 0.15, 1.0);
        var isCharacterPanel = string.Equals(title, "Herói", StringComparison.Ordinal);

        Title = title;
        Width = 380;
        Height = 330;
        MinWidth = 280;
        MinHeight = 210;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        ShowInTaskbar = true;
        AllowsTransparency = isCharacterPanel;
        Background = isCharacterPanel
            ? Brushes.Transparent
            : new SolidColorBrush(Color.FromRgb(17, 19, 24));
        Opacity = supportsOpacity ? _panelOpacity : 1.0;

        var root = new Grid
        {
            Background = isCharacterPanel ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(17, 19, 24))
        };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid
        {
            Background = isCharacterPanel ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(25, 28, 35)),
            Margin = new Thickness(0)
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
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
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var tabs = new TabControl
        {
            Margin = new Thickness(8),
            Background = isCharacterPanel ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(17, 19, 24)),
            BorderBrush = isCharacterPanel ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(48, 52, 61)),
            BorderThickness = isCharacterPanel ? new Thickness(0) : new Thickness(1)
        };

        var overview = new StackPanel
        {
            Margin = isCharacterPanel ? new Thickness(12, 4, 12, 12) : new Thickness(12),
            Background = Brushes.Transparent
        };
        overview.Children.Add(new TextBlock
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
        overview.Children.Add(_contentText);
        _opacityHint = new TextBlock
        {
            Text = supportsOpacity
                ? $"Transparência: {Math.Round(_panelOpacity * 100)}% (aprox. de opacidade)"
                : "Este painel permanece totalmente visível.",
            Foreground = new SolidColorBrush(Color.FromRgb(130, 139, 153)),
            FontSize = 10,
            Margin = new Thickness(0, 14, 0, 0)
        };
        overview.Children.Add(_opacityHint);
        tabs.Items.Add(new TabItem { Header = "Visão geral", Content = overview });

        _actionsList = new StackPanel { Margin = new Thickness(10) };
        _actionStatus = new TextBlock
        {
            Text = "Aguardando leitura dos controles disponíveis no jogo...",
            Foreground = new SolidColorBrush(Color.FromRgb(150, 158, 172)),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Margin = new Thickness(4, 4, 4, 10)
        };
        var actionContent = new StackPanel
        {
            Background = isCharacterPanel ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(17, 19, 24))
        };
        actionContent.Children.Add(_actionStatus);
        actionContent.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _actionsList
        });
        tabs.Items.Add(new TabItem { Header = "Ações do jogo", Content = actionContent });

        Grid.SetRow(tabs, 1);
        root.Children.Add(tabs);
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

    public void SetPanelActions(IEnumerable<PanelActionItem> actions, Action<PanelActionItem> execute)
    {
        _actionsList.Children.Clear();
        var available = actions
            .Where(x => !string.IsNullOrWhiteSpace(x.Label))
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .Take(80)
            .ToList();

        _actionStatus.Text = available.Count == 0
            ? "Nenhum controle interativo identificável nesta área. Os controles aparecem aqui quando o jogo os expõe ao WebView."
            : $"{available.Count} controle(s) encontrado(s). Selecione para executar a ação correspondente no jogo.";

        foreach (var action in available)
        {
            var button = new Button
            {
                Content = action.Label,
                Tag = action,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 0, 0, 6),
                Background = isCharacterPanel ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(34, 39, 49)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(64, 71, 84)),
                Cursor = Cursors.Hand,
                ToolTip = string.IsNullOrWhiteSpace(action.Context) ? action.Category : action.Context
            };
            button.Click += (_, _) => execute(action);
            _actionsList.Children.Add(button);
        }
    }

    public void SetActionStatus(string message)
    {
        _actionStatus.Text = message;
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
