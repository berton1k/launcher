using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using Launcher.Models;
using Launcher.Services;

namespace Launcher.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ApiService _apiService;
    private string _status = "Готов к запуску";
    private int _onlineNow;
    private bool _isAuthenticated = true;
    private bool _isBusy;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _selectedSection = "Play";
    private string _selectedSettingsTab = "General";
    private bool _isBackgroundVideoEnabled = true;
    private bool _isBackgroundVideoAvailable = true;
    private bool _hasLastVisited;
    private bool _minimizeOnLaunch;
    private bool _showHiddenServers;
    private bool _isGraphicsModsEnabled;
    private bool _disableGraphicsVersionCheck;
    private double _uiSfxVolume = 100;
    private UiOption? _selectedLanguage;
    private UiOption? _selectedRegion;
    private UiText _labels = UiText.CreateRu();
    private string _gtaLegacyPath = string.Empty;
    private string _majesticPath = string.Empty;

    public MainViewModel(ApiService apiService)
    {
        _apiService = apiService;

        RecommendedServers = new ObservableCollection<ServerInfo>();
        LastVisitedServers = new ObservableCollection<ServerInfo>();
        AllServers = new ObservableCollection<ServerInfo>();

        Languages = new ObservableCollection<UiOption>
        {
            new("Русский", "🇷🇺", "ru"),
            new("English", "🇺🇸", "en"),
            new("Deutsch", "🇩🇪", "de"),
            new("Español", "🇪🇸", "es"),
            new("Português", "🇵🇹", "pt"),
            new("Polski", "🇵🇱", "pl"),
            new("Українська", "🇺🇦", "uk")
        };

        Regions = new ObservableCollection<UiOption>
        {
            new("Глобальный", "🌍", "global"),
            new("СНГ", "🛡️", "cis")
        };

        SelectedLanguage = Languages[0];
        SelectedRegion = Regions[1];

        LoginCommand = new RelayCommand(async _ => await LoginAsync(), _ => !IsBusy);
        RefreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
        PlayCommand = new RelayCommand(OnPlayServer, _ => IsAuthenticated && !IsBusy);
        ShowSectionCommand = new RelayCommand(OnShowSection);
        ShowSettingsTabCommand = new RelayCommand(OnShowSettingsTab);
        OpenDiscordCommand = new RelayCommand(_ => OpenDiscord());
        OpenLauncherFolderCommand = new RelayCommand(_ => OpenLauncherFileLocation());
        ChangeGtaLegacyPathCommand = new RelayCommand(_ => ChangeGtaLegacyPath());
        ChangeMajesticPathCommand = new RelayCommand(_ => ChangeMajesticPath());
        MinimizeCommand = new RelayCommand(_ => MinimizeWindow());
        CloseCommand = new RelayCommand(_ => CloseWindow());

        ApplyLanguage(SelectedLanguage);
        App.SetUiSfxVolume(UiSfxVolume / 100d);
    }

    public ObservableCollection<ServerInfo> RecommendedServers { get; }
    public ObservableCollection<ServerInfo> LastVisitedServers { get; }
    public ObservableCollection<ServerInfo> AllServers { get; }
    public ObservableCollection<UiOption> Languages { get; }
    public ObservableCollection<UiOption> Regions { get; }

    public ICommand LoginCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand ShowSectionCommand { get; }
    public ICommand ShowSettingsTabCommand { get; }
    public ICommand OpenDiscordCommand { get; }
    public ICommand OpenLauncherFolderCommand { get; }
    public ICommand ChangeGtaLegacyPathCommand { get; }
    public ICommand ChangeMajesticPathCommand { get; }
    public ICommand MinimizeCommand { get; }
    public ICommand CloseCommand { get; }

    public int OnlineNow
    {
        get => _onlineNow;
        set => SetField(ref _onlineNow, value);
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set
        {
            if (SetField(ref _isAuthenticated, value))
            {
                RaiseCommands();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetField(ref _isBusy, value))
            {
                RaiseCommands();
            }
        }
    }

    public string Username
    {
        get => _username;
        set => SetField(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetField(ref _password, value);
    }

    public string SelectedSection
    {
        get => _selectedSection;
        set => SetField(ref _selectedSection, value);
    }

    public string SelectedSettingsTab
    {
        get => _selectedSettingsTab;
        set => SetField(ref _selectedSettingsTab, value);
    }

    public bool IsBackgroundVideoEnabled
    {
        get => _isBackgroundVideoEnabled;
        set
        {
            if (SetField(ref _isBackgroundVideoEnabled, value))
            {
                OnPropertyChanged(nameof(IsBackgroundVideoVisible));
            }
        }
    }

    public bool IsBackgroundVideoAvailable
    {
        get => _isBackgroundVideoAvailable;
        set
        {
            if (SetField(ref _isBackgroundVideoAvailable, value))
            {
                OnPropertyChanged(nameof(IsBackgroundVideoVisible));
            }
        }
    }

    public bool IsBackgroundVideoVisible => IsBackgroundVideoAvailable && IsBackgroundVideoEnabled;

    public bool HasLastVisited
    {
        get => _hasLastVisited;
        set => SetField(ref _hasLastVisited, value);
    }

    public bool MinimizeOnLaunch
    {
        get => _minimizeOnLaunch;
        set => SetField(ref _minimizeOnLaunch, value);
    }

    public bool ShowHiddenServers
    {
        get => _showHiddenServers;
        set => SetField(ref _showHiddenServers, value);
    }

    public bool IsGraphicsModsEnabled
    {
        get => _isGraphicsModsEnabled;
        set => SetField(ref _isGraphicsModsEnabled, value);
    }

    public bool DisableGraphicsVersionCheck
    {
        get => _disableGraphicsVersionCheck;
        set => SetField(ref _disableGraphicsVersionCheck, value);
    }

    public double UiSfxVolume
    {
        get => _uiSfxVolume;
        set
        {
            if (SetField(ref _uiSfxVolume, value))
            {
                App.SetUiSfxVolume(value / 100d);
                OnPropertyChanged(nameof(UiSfxVolumeLabel));
            }
        }
    }

    public string UiSfxVolumeLabel => ((int)Math.Round(UiSfxVolume)).ToString();

    public UiOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetField(ref _selectedLanguage, value))
            {
                ApplyLanguage(value);
            }
        }
    }

    public UiOption? SelectedRegion
    {
        get => _selectedRegion;
        set => SetField(ref _selectedRegion, value);
    }

    public UiText Labels
    {
        get => _labels;
        private set => SetField(ref _labels, value);
    }

    public string GtaLegacyPath
    {
        get => _gtaLegacyPath;
        set => SetField(ref _gtaLegacyPath, value);
    }

    public string MajesticPath
    {
        get => _majesticPath;
        set => SetField(ref _majesticPath, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public async Task InitializeAsync()
    {
        Status = "Готов к запуску";
        UpdateBackgroundVideoAvailability();
        await LoadAsync();
    }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            Status = "Заполните логин и пароль";
            return;
        }

        IsBusy = true;
        Status = "Выполняем вход...";

        await Task.Delay(400);
        IsAuthenticated = true;
        Status = "Готов к запуску";

        await LoadAsync();
        IsBusy = false;
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        Status = "Обновление данных...";

        var data = await _apiService.GetLauncherDataAsync(CancellationToken.None);
        if (data.Recommended.Count == 0)
        {
            data.Recommended.Add(new ServerInfo { Name = "Granted", Multiplier = "x1", Online = 0 });
        }

        if (data.LastVisited.Count == 0)
        {
            data.LastVisited.Add(new ServerInfo { Name = "Granted", Multiplier = "x1", Online = 0 });
        }

        if (data.AllServers.Count == 0)
        {
            data.AllServers.Add(new ServerInfo { Name = "Granted", Multiplier = "x1", Online = 0 });
        }
        OnlineNow = data.OnlineNow;

        ReplaceCollection(RecommendedServers, data.Recommended);
        ReplaceCollection(LastVisitedServers, data.LastVisited);
        ReplaceCollection(AllServers, data.AllServers);

        HasLastVisited = false;

        Status = "Готов к запуску";
        IsBusy = false;
    }

    private void OnPlayServer(object? parameter)
    {
        if (parameter is ServerInfo server)
        {
            Status = $"Запуск {server.Name}...";
            var existing = FindLastVisited(server.Name);
            if (existing != null)
            {
                LastVisitedServers.Remove(existing);
            }
            LastVisitedServers.Insert(0, new ServerInfo
            {
                Name = server.Name,
                Multiplier = server.Multiplier,
                Online = server.Online
            });
            HasLastVisited = true;
            if (MinimizeOnLaunch)
            {
                MinimizeWindow();
            }
            return;
        }

        Status = "Запуск игры...";
        HasLastVisited = true;
        if (MinimizeOnLaunch)
        {
            MinimizeWindow();
        }
    }

    private ServerInfo? FindLastVisited(string name)
    {
        foreach (var item in LastVisitedServers)
        {
            if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, System.Collections.Generic.IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private void RaiseCommands()
    {
        if (LoginCommand is RelayCommand login)
        {
            login.RaiseCanExecuteChanged();
        }

        if (RefreshCommand is RelayCommand refresh)
        {
            refresh.RaiseCanExecuteChanged();
        }

        if (PlayCommand is RelayCommand play)
        {
            play.RaiseCanExecuteChanged();
        }
    }

    private void OnShowSection(object? parameter)
    {
        if (parameter is string section)
        {
            SelectedSection = section;
        }
    }

    private void OnShowSettingsTab(object? parameter)
    {
        if (parameter is string tab)
        {
            SelectedSettingsTab = tab;
        }
    }

    private void OpenDiscord()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.gg/B8myaMJ8qx",
                UseShellExecute = true
            });
        }
        catch
        {
            Status = "Не удалось открыть Discord";
        }
    }

    private void OpenLauncherFileLocation()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var exePath = Path.Combine(baseDir, "Launcher.exe");
            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{exePath}\"",
                    UseShellExecute = true
                });
                return;
            }

            if (Directory.Exists(baseDir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = baseDir,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            Status = "Не удалось открыть папку лаунчера";
        }
    }

    private void ChangeGtaLegacyPath()
    {
        var selected = PickFolder(GtaLegacyPath, "GTA V Legacy");
        if (!string.IsNullOrWhiteSpace(selected))
        {
            GtaLegacyPath = selected;
            Status = "Путь GTA V Legacy обновлён";
        }
    }

    private void ChangeMajesticPath()
    {
        var selected = PickFolder(MajesticPath, "Majestic RP");
        if (!string.IsNullOrWhiteSpace(selected))
        {
            MajesticPath = selected;
            Status = "Путь Majestic RP обновлён";
        }
    }

    private static string PickFolder(string initialPath, string description)
    {
        try
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = $"Выберите папку: {description}",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
            {
                dialog.SelectedPath = initialPath;
            }

            var result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                return dialog.SelectedPath;
            }
        }
        catch
        {
            // Ignore folder picker errors.
        }

        return string.Empty;
    }

    private void MinimizeWindow()
    {
        if (System.Windows.Application.Current?.MainWindow is { } window)
        {
            window.WindowState = System.Windows.WindowState.Minimized;
        }
    }

    private void CloseWindow()
    {
        System.Windows.Application.Current?.MainWindow?.Close();
    }

    private void UpdateBackgroundVideoAvailability()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var path = Path.Combine(baseDir, "Assets", "back.mp4");
        IsBackgroundVideoAvailable = File.Exists(path);
        OnPropertyChanged(nameof(IsBackgroundVideoVisible));
    }

    private void ApplyLanguage(UiOption? option)
    {
        if (option is null)
        {
            return;
        }

        Labels = option.Code switch
        {
            "en" => UiText.CreateEn(),
            "de" => UiText.CreateDe(),
            "es" => UiText.CreateEs(),
            "pt" => UiText.CreatePt(),
            "pl" => UiText.CreatePl(),
            "uk" => UiText.CreateUk(),
            _ => UiText.CreateRu()
        };
    }

    public sealed record UiOption(string Name, string Icon, string Code);

    public sealed class UiText
    {
        public string MenuPlay { get; init; } = string.Empty;
        public string MenuStore { get; init; } = string.Empty;
        public string MenuNews { get; init; } = string.Empty;
        public string MenuForum { get; init; } = string.Empty;
        public string MenuDiscord { get; init; } = string.Empty;
        public string MenuMods { get; init; } = string.Empty;
        public string MenuSettings { get; init; } = string.Empty;
        public string OnlineNowLabel { get; init; } = string.Empty;
        public string SectionRecommended { get; init; } = string.Empty;
        public string SectionLastVisited { get; init; } = string.Empty;
        public string SectionAllServers { get; init; } = string.Empty;
        public string PlayButton { get; init; } = string.Empty;
        public string SettingsTitle { get; init; } = string.Empty;
        public string SettingsGeneralTab { get; init; } = string.Empty;
        public string SettingsAdvancedTab { get; init; } = string.Empty;
        public string SettingsModsTab { get; init; } = string.Empty;
        public string RegionLabel { get; init; } = string.Empty;
        public string LanguageLabel { get; init; } = string.Empty;
        public string UiVolumeLabel { get; init; } = string.Empty;
        public string OpenMultiplayerLabel { get; init; } = string.Empty;
        public string MinimizeOnLaunchLabel { get; init; } = string.Empty;
        public string ShowHiddenServersLabel { get; init; } = string.Empty;
        public string ChangeGtaLegacyLabel { get; init; } = string.Empty;
        public string ChangeMajesticLabel { get; init; } = string.Empty;
        public string CleanTempLabel { get; init; } = string.Empty;
        public string FixPermissionsLabel { get; init; } = string.Empty;
        public string VerifyFilesLabel { get; init; } = string.Empty;
        public string ModsEnableLabel { get; init; } = string.Empty;
        public string ModsDisableCheckLabel { get; init; } = string.Empty;
        public string ModsOpenFolderLabel { get; init; } = string.Empty;
        public string ModsNoteText { get; init; } = string.Empty;
        public string ButtonOpen { get; init; } = string.Empty;
        public string ButtonChange { get; init; } = string.Empty;
        public string ButtonClean { get; init; } = string.Empty;
        public string ButtonFix { get; init; } = string.Empty;
        public string ButtonVerify { get; init; } = string.Empty;
        public string DevLabel { get; init; } = string.Empty;
        public string SoonLabel { get; init; } = string.Empty;

        public static UiText CreateRu() => new()
        {
            MenuPlay = "Играть",
            MenuStore = "Магазин",
            MenuNews = "Новости",
            MenuForum = "Форум",
            MenuDiscord = "Discord",
            MenuMods = "Моды",
            MenuSettings = "Настройки",
            OnlineNowLabel = "Сейчас играют:",
            SectionRecommended = "СОВЕТУЕМ ДЛЯ НОВИЧКОВ",
            SectionLastVisited = "ЗАХОДИЛИ В ПОСЛЕДНИЙ РАЗ",
            SectionAllServers = "ВСЕ СЕРВЕРА",
            PlayButton = "ИГРАТЬ",
            SettingsTitle = "НАСТРОЙКИ",
            SettingsGeneralTab = "Основное",
            SettingsAdvancedTab = "Дополнительно",
            SettingsModsTab = "Модификации",
            RegionLabel = "Регион",
            LanguageLabel = "Язык",
            UiVolumeLabel = "Громкость интерфейса",
            OpenMultiplayerLabel = "Открыть папку с файлами мультиплеера",
            MinimizeOnLaunchLabel = "Сворачивать лаунчер после запуска игры",
            ShowHiddenServersLabel = "Показывать скрытые сервера",
            ChangeGtaLegacyLabel = "Сменить место установки GTA V Legacy",
            ChangeMajesticLabel = "Сменить место установки Shibo RP",
            CleanTempLabel = "Очистить резервные копии и временные файлы",
            FixPermissionsLabel = "Починить права доступа к файлам игры",
            VerifyFilesLabel = "Принудительно проверить файлы игры",
            ModsEnableLabel = "Включить поддержку графических модификаций",
            ModsDisableCheckLabel = "Отключить проверку версий для графических модификаций",
            ModsOpenFolderLabel = "Открыть папку с модификациями",
            ModsNoteText = "Примечание: Графические модификации должны быть помещены в нашу папку с модами, так как директория игры не поддерживается.\n\nНа данный момент поддерживаются следующие моды: ENB, Reshade (Standard или NVE). Для их установки скачайте архив с официального сайта и поместите файлы (d3d11.dll или dxgi.dll) и конфиги в папку с модами, которую можно открыть кнопкой выше. Для активации модов включите пункт \"Включить поддержку графических модификаций\" — при отключении модули перестанут грузиться.\n\nПункт \"Отключить проверку версий\" нужен только если вы хотите запускать мод более ранней версии, но делать это не рекомендуется.",
            ButtonOpen = "ОТКРЫТЬ",
            ButtonChange = "СМЕНИТЬ",
            ButtonClean = "ОЧИСТИТЬ",
            ButtonFix = "ПОЧИНИТЬ",
            ButtonVerify = "ПРОВЕРИТЬ",
            DevLabel = "В РАЗРАБОТКЕ",
            SoonLabel = "СКОРО"
        };

        public static UiText CreateEn() => new()
        {
            MenuPlay = "Play",
            MenuStore = "Store",
            MenuNews = "News",
            MenuForum = "Forum",
            MenuDiscord = "Discord",
            MenuMods = "Mods",
            MenuSettings = "Settings",
            OnlineNowLabel = "Playing now:",
            SectionRecommended = "RECOMMENDED FOR NEW PLAYERS",
            SectionLastVisited = "LAST VISITED",
            SectionAllServers = "ALL SERVERS",
            PlayButton = "PLAY",
            SettingsTitle = "SETTINGS",
            SettingsGeneralTab = "General",
            SettingsAdvancedTab = "Advanced",
            SettingsModsTab = "Modifications",
            RegionLabel = "Region",
            LanguageLabel = "Language",
            UiVolumeLabel = "Interface volume",
            OpenMultiplayerLabel = "Open multiplayer files folder",
            MinimizeOnLaunchLabel = "Minimize launcher after game start",
            ShowHiddenServersLabel = "Show hidden servers",
            ChangeGtaLegacyLabel = "Change GTA V Legacy install location",
            ChangeMajesticLabel = "Change Shibo RP install location",
            CleanTempLabel = "Clean backups and temporary files",
            FixPermissionsLabel = "Fix file access permissions",
            VerifyFilesLabel = "Force verify game files",
            ModsEnableLabel = "Enable graphics mod support",
            ModsDisableCheckLabel = "Disable version check for graphics mods",
            ModsOpenFolderLabel = "Open mods folder",
            ModsNoteText = "Note: Graphics mods must be placed into our mods folder, since the game directory is not supported.\n\nCurrently supported mods: ENB, Reshade (Standard or NVE). Download the archive from the official site and place d3d11.dll or dxgi.dll plus configs into the mods folder you can open above. To activate mods, enable \"Enable graphics mod support\" — when turned off, modules stop loading.\n\nThe \"Disable version check\" option is only for loading older mod versions and is not recommended.",
            ButtonOpen = "OPEN",
            ButtonChange = "CHANGE",
            ButtonClean = "CLEAN",
            ButtonFix = "FIX",
            ButtonVerify = "VERIFY",
            DevLabel = "IN DEVELOPMENT",
            SoonLabel = "SOON"
        };

        public static UiText CreateDe() => new()
        {
            MenuPlay = "Spielen",
            MenuStore = "Shop",
            MenuNews = "Neuigkeiten",
            MenuForum = "Forum",
            MenuDiscord = "Discord",
            MenuMods = "Mods",
            MenuSettings = "Einstellungen",
            OnlineNowLabel = "Jetzt online:",
            SectionRecommended = "EMPFOHLEN FÜR NEUE SPIELER",
            SectionLastVisited = "ZULETZT BESUCHT",
            SectionAllServers = "ALLE SERVER",
            PlayButton = "SPIELEN",
            SettingsTitle = "EINSTELLUNGEN",
            SettingsGeneralTab = "Allgemein",
            SettingsAdvancedTab = "Erweitert",
            SettingsModsTab = "Modifikationen",
            RegionLabel = "Region",
            LanguageLabel = "Sprache",
            UiVolumeLabel = "Oberflächenlautstärke",
            OpenMultiplayerLabel = "Multiplayer-Dateiordner öffnen",
            MinimizeOnLaunchLabel = "Launcher nach Spielstart minimieren",
            ShowHiddenServersLabel = "Versteckte Server anzeigen",
            ChangeGtaLegacyLabel = "Installationspfad von GTA V Legacy ändern",
            ChangeMajesticLabel = "Installationspfad von Majestic RP ändern",
            CleanTempLabel = "Sicherungen und temporäre Dateien löschen",
            FixPermissionsLabel = "Dateiberechtigungen reparieren",
            VerifyFilesLabel = "Spieldateien prüfen erzwingen",
            ModsEnableLabel = "Unterstützung für Grafik-Mods aktivieren",
            ModsDisableCheckLabel = "Versionsprüfung für Grafik-Mods deaktivieren",
            ModsOpenFolderLabel = "Mods-Ordner öffnen",
            ModsNoteText = "Hinweis: Grafik-Mods müssen in unseren Mods-Ordner gelegt werden, da das Spielverzeichnis nicht unterstützt wird.\n\nDerzeit werden folgende Mods unterstützt: ENB, Reshade (Standard oder NVE). Laden Sie das Archiv von der offiziellen Seite herunter und legen Sie d3d11.dll oder dxgi.dll sowie Konfigurationen in den Mods-Ordner, den Sie oben öffnen können. Zum Aktivieren der Mods aktivieren Sie \"Unterstützung für Grafik-Mods\" — beim Deaktivieren werden die Module nicht geladen.\n\nDie Option \"Versionsprüfung deaktivieren\" ist nur für ältere Mod-Versionen gedacht und wird nicht empfohlen.",
            ButtonOpen = "ÖFFNEN",
            ButtonChange = "ÄNDERN",
            ButtonClean = "LÖSCHEN",
            ButtonFix = "REPARIEREN",
            ButtonVerify = "PRÜFEN",
            DevLabel = "IN ENTWICKLUNG",
            SoonLabel = "BALD"
        };

        public static UiText CreateEs() => new()
        {
            MenuPlay = "Jugar",
            MenuStore = "Tienda",
            MenuNews = "Noticias",
            MenuForum = "Foro",
            MenuDiscord = "Discord",
            MenuMods = "Mods",
            MenuSettings = "Ajustes",
            OnlineNowLabel = "Jugando ahora:",
            SectionRecommended = "RECOMENDADO PARA PRINCIPIANTES",
            SectionLastVisited = "ÚLTIMA VEZ",
            SectionAllServers = "TODOS LOS SERVIDORES",
            PlayButton = "JUGAR",
            SettingsTitle = "AJUSTES",
            SettingsGeneralTab = "General",
            SettingsAdvancedTab = "Avanzado",
            SettingsModsTab = "Modificaciones",
            RegionLabel = "Región",
            LanguageLabel = "Idioma",
            UiVolumeLabel = "Volumen de interfaz",
            OpenMultiplayerLabel = "Abrir carpeta de archivos multijugador",
            MinimizeOnLaunchLabel = "Minimizar el launcher al iniciar el juego",
            ShowHiddenServersLabel = "Mostrar servidores ocultos",
            ChangeGtaLegacyLabel = "Cambiar ubicación de GTA V Legacy",
            ChangeMajesticLabel = "Cambiar ubicación de Majestic RP",
            CleanTempLabel = "Limpiar copias y archivos temporales",
            FixPermissionsLabel = "Reparar permisos de archivos del juego",
            VerifyFilesLabel = "Verificar archivos del juego",
            ModsEnableLabel = "Activar soporte de mods gráficos",
            ModsDisableCheckLabel = "Desactivar verificación de versiones de mods gráficos",
            ModsOpenFolderLabel = "Abrir carpeta de mods",
            ModsNoteText = "Nota: Los mods gráficos deben colocarse en nuestra carpeta de mods, ya que el directorio del juego no se admite.\n\nMods compatibles actualmente: ENB, Reshade (Standard o NVE). Descargue el archivo del sitio oficial y coloque d3d11.dll o dxgi.dll y configuraciones en la carpeta de mods que puede abrir arriba. Para activar los mods, habilite \"Activar soporte de mods gráficos\" — al desactivarlo, los módulos no se cargarán.\n\nLa opción \"Desactivar verificación de versiones\" solo es para usar versiones antiguas y no se recomienda.",
            ButtonOpen = "ABRIR",
            ButtonChange = "CAMBIAR",
            ButtonClean = "LIMPIAR",
            ButtonFix = "REPARAR",
            ButtonVerify = "VERIFICAR",
            DevLabel = "EN DESARROLLO",
            SoonLabel = "PRONTO"
        };

        public static UiText CreatePt() => new()
        {
            MenuPlay = "Jogar",
            MenuStore = "Loja",
            MenuNews = "Notícias",
            MenuForum = "Fórum",
            MenuDiscord = "Discord",
            MenuMods = "Mods",
            MenuSettings = "Configurações",
            OnlineNowLabel = "Jogando agora:",
            SectionRecommended = "RECOMENDADO PARA INICIANTES",
            SectionLastVisited = "ÚLTIMA VISITA",
            SectionAllServers = "TODOS OS SERVIDORES",
            PlayButton = "JOGAR",
            SettingsTitle = "CONFIGURAÇÕES",
            SettingsGeneralTab = "Geral",
            SettingsAdvancedTab = "Avançado",
            SettingsModsTab = "Modificações",
            RegionLabel = "Região",
            LanguageLabel = "Idioma",
            UiVolumeLabel = "Volume da interface",
            OpenMultiplayerLabel = "Abrir pasta de arquivos do multiplayer",
            MinimizeOnLaunchLabel = "Minimizar launcher ao iniciar o jogo",
            ShowHiddenServersLabel = "Mostrar servidores ocultos",
            ChangeGtaLegacyLabel = "Alterar local do GTA V Legacy",
            ChangeMajesticLabel = "Alterar local do Majestic RP",
            CleanTempLabel = "Limpar cópias e arquivos temporários",
            FixPermissionsLabel = "Corrigir permissões de arquivos",
            VerifyFilesLabel = "Verificar arquivos do jogo",
            ModsEnableLabel = "Ativar suporte a mods gráficos",
            ModsDisableCheckLabel = "Desativar verificação de versão de mods gráficos",
            ModsOpenFolderLabel = "Abrir pasta de mods",
            ModsNoteText = "Nota: Os mods gráficos devem ser colocados na nossa pasta de mods, pois o diretório do jogo não é suportado.\n\nMods suportados: ENB, Reshade (Standard ou NVE). Baixe o arquivo do site oficial e coloque d3d11.dll ou dxgi.dll e configs na pasta de mods, que pode ser aberta acima. Para ativar os mods, habilite \"Ativar suporte a mods gráficos\" — ao desativar, os módulos não carregam.\n\nA opção \"Desativar verificação de versão\" é apenas para versões antigas e não é recomendada.",
            ButtonOpen = "ABRIR",
            ButtonChange = "ALTERAR",
            ButtonClean = "LIMPAR",
            ButtonFix = "CORRIGIR",
            ButtonVerify = "VERIFICAR",
            DevLabel = "EM DESENVOLVIMENTO",
            SoonLabel = "EM BREVE"
        };

        public static UiText CreatePl() => new()
        {
            MenuPlay = "Graj",
            MenuStore = "Sklep",
            MenuNews = "Aktualności",
            MenuForum = "Forum",
            MenuDiscord = "Discord",
            MenuMods = "Mody",
            MenuSettings = "Ustawienia",
            OnlineNowLabel = "Teraz gra:",
            SectionRecommended = "POLECANE DLA NOWYCH GRACZY",
            SectionLastVisited = "OSTATNIO ODWIEDZONE",
            SectionAllServers = "WSZYSTKIE SERWERY",
            PlayButton = "GRAJ",
            SettingsTitle = "USTAWIENIA",
            SettingsGeneralTab = "Ogólne",
            SettingsAdvancedTab = "Zaawansowane",
            SettingsModsTab = "Modyfikacje",
            RegionLabel = "Region",
            LanguageLabel = "Język",
            UiVolumeLabel = "Głośność interfejsu",
            OpenMultiplayerLabel = "Otwórz folder plików multiplayer",
            MinimizeOnLaunchLabel = "Minimalizuj launcher po starcie gry",
            ShowHiddenServersLabel = "Pokaż ukryte serwery",
            ChangeGtaLegacyLabel = "Zmień lokalizację GTA V Legacy",
            ChangeMajesticLabel = "Zmień lokalizację Majestic RP",
            CleanTempLabel = "Wyczyść kopie zapasowe i pliki tymczasowe",
            FixPermissionsLabel = "Napraw uprawnienia plików",
            VerifyFilesLabel = "Wymuś weryfikację plików gry",
            ModsEnableLabel = "Włącz obsługę modów graficznych",
            ModsDisableCheckLabel = "Wyłącz sprawdzanie wersji modów graficznych",
            ModsOpenFolderLabel = "Otwórz folder modów",
            ModsNoteText = "Uwaga: Mody graficzne muszą być umieszczone w naszym folderze modów, ponieważ katalog gry nie jest obsługiwany.\n\nObsługiwane mody: ENB, Reshade (Standard lub NVE). Pobierz archiwum z oficjalnej strony i umieść d3d11.dll lub dxgi.dll oraz konfiguracje w folderze modów, który możesz otworzyć powyżej. Aby aktywować mody, włącz \"Włącz obsługę modów graficznych\" — po wyłączeniu moduły nie będą ładowane.\n\nOpcja \"Wyłącz sprawdzanie wersji\" jest tylko dla starszych wersji i nie jest zalecana.",
            ButtonOpen = "OTWÓRZ",
            ButtonChange = "ZMIEŃ",
            ButtonClean = "WYCZYŚĆ",
            ButtonFix = "NAPRAW",
            ButtonVerify = "SPRAWDŹ",
            DevLabel = "W ROZWOJU",
            SoonLabel = "WKRÓTCE"
        };

        public static UiText CreateUk() => new()
        {
            MenuPlay = "Грати",
            MenuStore = "Магазин",
            MenuNews = "Новини",
            MenuForum = "Форум",
            MenuDiscord = "Discord",
            MenuMods = "Моди",
            MenuSettings = "Налаштування",
            OnlineNowLabel = "Зараз грають:",
            SectionRecommended = "РАДИМО НОВАЧКАМ",
            SectionLastVisited = "ОСТАННІЙ ВІЗИТ",
            SectionAllServers = "УСІ СЕРВЕРИ",
            PlayButton = "ГРАТИ",
            SettingsTitle = "НАЛАШТУВАННЯ",
            SettingsGeneralTab = "Основне",
            SettingsAdvancedTab = "Додатково",
            SettingsModsTab = "Модифікації",
            RegionLabel = "Регіон",
            LanguageLabel = "Мова",
            UiVolumeLabel = "Гучність інтерфейсу",
            OpenMultiplayerLabel = "Відкрити теку файлів мультиплеєра",
            MinimizeOnLaunchLabel = "Згортати лаунчер після запуску гри",
            ShowHiddenServersLabel = "Показувати приховані сервери",
            ChangeGtaLegacyLabel = "Змінити шлях GTA V Legacy",
            ChangeMajesticLabel = "Змінити шлях Majestic RP",
            CleanTempLabel = "Очистити резервні копії та тимчасові файли",
            FixPermissionsLabel = "Виправити права доступу до файлів гри",
            VerifyFilesLabel = "Примусово перевірити файли гри",
            ModsEnableLabel = "Увімкнути підтримку графічних модифікацій",
            ModsDisableCheckLabel = "Вимкнути перевірку версій для графічних модів",
            ModsOpenFolderLabel = "Відкрити теку модів",
            ModsNoteText = "Примітка: Графічні моди потрібно розміщувати в нашій теці модів, оскільки директорія гри не підтримується.\n\nПідтримувані моди: ENB, Reshade (Standard або NVE). Завантажте архів з офіційного сайту та покладіть d3d11.dll або dxgi.dll і конфіги у теку модів, яку можна відкрити вище. Для активації модів увімкніть \"Увімкнути підтримку графічних модифікацій\" — при вимкненні модулі не завантажуються.\n\nПункт \"Вимкнути перевірку версій\" потрібен лише для старих версій і не рекомендується.",
            ButtonOpen = "ВІДКРИТИ",
            ButtonChange = "ЗМІНИТИ",
            ButtonClean = "ОЧИСТИТИ",
            ButtonFix = "ВИПРАВИТИ",
            ButtonVerify = "ПЕРЕВІРИТИ",
            DevLabel = "У РОЗРОБЦІ",
            SoonLabel = "СКОРО"
        };
    }
}
