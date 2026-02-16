using Launcher.Services;
using Launcher.ViewModels;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace Launcher.Views;

public partial class MainWindow : System.Windows.Window
{
    private readonly MainViewModel _viewModel;
    private readonly MediaPlayer _backgroundPlayer = new();
    private readonly VideoDrawing _backgroundDrawing = new();
    private readonly DrawingBrush _backgroundBrush;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel(new ApiService());
        DataContext = _viewModel;

        _backgroundBrush = new DrawingBrush(_backgroundDrawing)
        {
            Stretch = Stretch.UniformToFill
        };

        App.EnsurePlayableAssetFile("back.mp4");
        App.EnsurePlayableAssetFile("buttons.mp3");

        InitializeBackgroundVideo();

        BackgroundVideo.Loaded += (_, _) => UpdateBackgroundVideoRect();
        BackgroundVideo.SizeChanged += (_, _) => UpdateBackgroundVideoRect();

        Loaded += async (_, _) => await _viewModel.InitializeAsync();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Deactivated += OnWindowDeactivated;
    }

    private void InitializeBackgroundVideo()
    {
        try
        {
            var videoPath = App.EnsurePlayableAssetFile("back.mp4")
                ?? App.EnsurePlayableAssetFile("backg.mp4");
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                TryLogAsset("Background video not found.");
                return;
            }
            _backgroundPlayer.IsMuted = true;
            _backgroundPlayer.MediaOpened += OnBackgroundMediaOpened;
            _backgroundPlayer.MediaFailed += OnBackgroundMediaFailed;
            _backgroundPlayer.MediaEnded += (_, _) =>
            {
                _backgroundPlayer.Position = TimeSpan.Zero;
                _backgroundPlayer.Play();
            };
            _backgroundDrawing.Player = _backgroundPlayer;
            _backgroundDrawing.Rect = new Rect(0, 0, 1, 1);
            BackgroundVideo.Fill = _backgroundBrush;

            _backgroundPlayer.Open(new Uri(videoPath, UriKind.Absolute));
            TryLogAsset($"Background video opening: {videoPath}");
        }
        catch
        {
            TryLogAsset("Background video initialization failed.");
            // Ignore video initialization errors.
        }
    }

    private void OnBackgroundMediaOpened(object? sender, EventArgs e)
    {
        _backgroundPlayer.MediaOpened -= OnBackgroundMediaOpened;
        _viewModel.IsBackgroundVideoAvailable = true;
        _backgroundPlayer.Play();
        TryLogAsset("Background video opened.");
    }

    private void OnBackgroundMediaFailed(object? sender, ExceptionEventArgs e)
    {
        _backgroundPlayer.MediaFailed -= OnBackgroundMediaFailed;
        _viewModel.IsBackgroundVideoAvailable = false;
        TryLogAsset($"Background video failed: {e.ErrorException?.Message}");
    }

    private static void TryLogAsset(string message)
    {
        try
        {
            var localRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Launcher");
            Directory.CreateDirectory(localRoot);
            var logPath = Path.Combine(localRoot, "asset.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] {message}\n");
            return;
        }
        catch
        {
            // Ignore logging failures.
        }
    }

    private void UpdateBackgroundVideoRect()
    {
        if (BackgroundVideo.ActualWidth <= 0 || BackgroundVideo.ActualHeight <= 0)
        {
            return;
        }

        _backgroundDrawing.Rect = new Rect(0, 0, BackgroundVideo.ActualWidth, BackgroundVideo.ActualHeight);
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void OnMinimizeClick(object sender, System.Windows.RoutedEventArgs e)
    {
        WindowState = System.Windows.WindowState.Minimized;
    }

    private void OnCloseClick(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }

    private void OnButtonMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.PlayUiSound();
        }
    }

    private void OnButtonPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.PlayUiSound();
        }
    }

    private void OnSettingsPanelLoaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateSettingsPanelHeight(false);
            AnimateSettingsPanelWidth(false);
            AnimateSettingsTabShift();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnSettingsContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSettingsPanelHeight(true);
    }

    private void UpdateSettingsPanelHeight(bool animate)
    {
        var border = FindName("SettingsPanelBorder") as Border;
        var content = FindName("SettingsPanelContent") as FrameworkElement;
        if (border is null || content is null)
        {
            return;
        }

        var activeContent = GetActiveSettingsContent();
        if (activeContent is null)
        {
            content.UpdateLayout();
            var fallbackHeight = Math.Max(border.MinHeight, content.ActualHeight + border.Padding.Top + border.Padding.Bottom);
            SetSettingsPanelHeight(border, fallbackHeight, animate);
            return;
        }

        activeContent.UpdateLayout();

        var availableWidth = border.ActualWidth - border.Padding.Left - border.Padding.Right;
        if (availableWidth <= 0)
        {
            availableWidth = Math.Max(border.MinWidth, activeContent.ActualWidth);
        }

        if (activeContent.ActualHeight <= 0 || activeContent.DesiredSize.Height <= 0)
        {
            activeContent.Measure(new System.Windows.Size(availableWidth, double.PositiveInfinity));
        }

        var targetHeight = Math.Max(border.MinHeight, activeContent.DesiredSize.Height + border.Padding.Top + border.Padding.Bottom);
        if (_viewModel.SelectedSettingsTab == "Mods")
        {
            targetHeight += 20;
        }
        SetSettingsPanelHeight(border, targetHeight, animate);
    }

    private FrameworkElement? GetActiveSettingsContent()
    {
        if (FindName("SettingsGeneral") is FrameworkElement general && general.Visibility == Visibility.Visible)
        {
            return general;
        }

        if (FindName("SettingsAdvanced") is FrameworkElement advanced && advanced.Visibility == Visibility.Visible)
        {
            return advanced;
        }

        if (FindName("SettingsMods") is FrameworkElement mods && mods.Visibility == Visibility.Visible)
        {
            return mods;
        }

        return null;
    }

    private void SetSettingsPanelHeight(Border border, double targetHeight, bool animate)
    {
        if (!animate)
        {
            border.BeginAnimation(HeightProperty, null);
            border.Height = targetHeight;
            return;
        }

        var animation = new System.Windows.Media.Animation.DoubleAnimation(targetHeight, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };

        border.BeginAnimation(HeightProperty, animation);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedSection))
        {
            RunSectionFade();
        }

        if (e.PropertyName == nameof(MainViewModel.SelectedSection) || e.PropertyName == nameof(MainViewModel.SelectedSettingsTab))
        {
            Dispatcher.BeginInvoke(new Action(() => UpdateSettingsPanelHeight(true)), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        if (e.PropertyName == nameof(MainViewModel.SelectedSettingsTab))
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                AnimateSettingsPanelWidth(true);
                AnimateSettingsTabShift();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void AnimateSettingsTabShift()
    {
        if (FindName("SettingsTabsPanel") is not FrameworkElement tabsPanel)
        {
            return;
        }

        if (tabsPanel.RenderTransform is not TranslateTransform translate)
        {
            translate = new TranslateTransform(0, 0);
            tabsPanel.RenderTransform = translate;
        }

        var targetX = _viewModel.SelectedSettingsTab == "Mods" ? -14 : 0;
        var animation = new System.Windows.Media.Animation.DoubleAnimation(targetX, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };

        translate.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private void AnimateSettingsPanelWidth(bool animate)
    {
        if (FindName("SettingsPanelBorder") is not Border border)
        {
            return;
        }

        var baseWidth = 380d;
        var modsWidth = 410d;
        var targetWidth = _viewModel.SelectedSettingsTab == "Mods" ? modsWidth : baseWidth;

        if (!animate)
        {
            border.BeginAnimation(WidthProperty, null);
            border.Width = targetWidth;
            return;
        }

        var animation = new System.Windows.Media.Animation.DoubleAnimation(targetWidth, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };

        border.BeginAnimation(WidthProperty, animation);
    }

    private void RunSectionFade()
    {
        var overlay = FindName("SectionFadeOverlay") as UIElement;
        if (overlay is null)
        {
            return;
        }

        var storyboard = new System.Windows.Media.Animation.Storyboard();
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 0.35, TimeSpan.FromMilliseconds(120))
        {
            FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop
        };
        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0.35, 0, TimeSpan.FromMilliseconds(220))
        {
            BeginTime = TimeSpan.FromMilliseconds(120),
            FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop
        };

        System.Windows.Media.Animation.Storyboard.SetTarget(fadeIn, overlay);
        System.Windows.Media.Animation.Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));
        System.Windows.Media.Animation.Storyboard.SetTarget(fadeOut, overlay);
        System.Windows.Media.Animation.Storyboard.SetTargetProperty(fadeOut, new PropertyPath(OpacityProperty));

        storyboard.Children.Add(fadeIn);
        storyboard.Children.Add(fadeOut);
        storyboard.Completed += (_, _) => overlay.Opacity = 0;
        storyboard.Begin();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (FindName("RegionCombo") is System.Windows.Controls.ComboBox region)
        {
            region.IsDropDownOpen = false;
        }

        if (FindName("LanguageCombo") is System.Windows.Controls.ComboBox language)
        {
            language.IsDropDownOpen = false;
        }
    }

    private void OnComboBoxItemLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ComboBoxItem item)
        {
            return;
        }

        var index = System.Windows.Controls.ItemsControl.GetAlternationIndex(item);
        var delay = TimeSpan.FromMilliseconds(35 * Math.Max(0, index));

        var translate = new TranslateTransform(0, -6);
        item.RenderTransform = translate;

        item.Opacity = 0;

        var opacityAnimation = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            BeginTime = delay
        };

        var yAnimation = new System.Windows.Media.Animation.DoubleAnimation(-6, 0, TimeSpan.FromMilliseconds(180))
        {
            BeginTime = delay
        };

        item.BeginAnimation(OpacityProperty, opacityAnimation);
        translate.BeginAnimation(TranslateTransform.YProperty, yAnimation);
    }

}
