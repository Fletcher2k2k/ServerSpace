using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ServerSpace.UI.Localization;

public enum AppLanguage
{
    German,
    English
}

public static class LocalizationManager
{
    private const string SettingsFileName = "settings.json";
    private static AppLanguage _currentLanguage = AppLanguage.German;

    public static AppLanguage CurrentLanguage => _currentLanguage;

    public static void ApplySavedLanguage(System.Windows.Application application)
    {
        AppLanguage language = GetSystemLanguage();
        try
        {
            string path = GetSettingsPath();
            if (File.Exists(path))
            {
                LanguageSettings? settings = JsonSerializer.Deserialize<LanguageSettings>(File.ReadAllText(path));
                if (settings is not null
                    && TryParseLanguage(settings.Language, out AppLanguage saved))
                {
                    language = saved;
                }
            }
        }
        catch
        {
            // Use the system language and English fallback when settings are invalid.
        }

        ApplyLanguage(application, language);
    }

    public static void SetLanguage(System.Windows.Application application, AppLanguage language)
    {
        ApplyLanguage(application, language);
        try
        {
            string path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            LanguageSettings settings = ReadSettings() ?? new LanguageSettings();
            settings.Language = language == AppLanguage.German ? "de-DE" : "en-US";
            WriteSettings(path, settings);
        }
        catch
        {
            // A language switch remains active if persistence is unavailable.
        }
    }

    public static string GetString(string key)
    {
        return System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
    }

    public static string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, GetString(key), arguments);

    public static void RestoreWindowPlacement(Window window)
    {
        LanguageSettings? settings = ReadSettings();
        if (settings?.WindowLeft is not double left
            || settings.WindowTop is not double top
            || settings.WindowWidth is not double width
            || settings.WindowHeight is not double height
            || width <= 0
            || height <= 0)
        {
            return;
        }

        width = Math.Clamp(width, window.MinWidth, SystemParameters.WorkArea.Width);
        height = Math.Clamp(height, window.MinHeight, SystemParameters.WorkArea.Height);

        if (!IsOnAnyWorkingArea(left, top, width, height))
        {
            Forms.Screen? primary = Forms.Screen.PrimaryScreen;
            if (primary is null)
            {
                return;
            }

            left = primary.WorkingArea.Left + (primary.WorkingArea.Width - width) / 2d;
            top = primary.WorkingArea.Top + (primary.WorkingArea.Height - height) / 2d;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = left;
        window.Top = top;
        window.Width = width;
        window.Height = height;
        window.WindowState = string.Equals(settings.WindowState, "Maximized", StringComparison.OrdinalIgnoreCase)
            ? WindowState.Maximized
            : WindowState.Normal;
    }

    public static void SaveWindowPlacement(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            return;
        }

        try
        {
            LanguageSettings settings = ReadSettings() ?? new LanguageSettings();
            Rect bounds = window.WindowState == WindowState.Maximized
                ? window.RestoreBounds
                : new Rect(window.Left, window.Top, window.Width, window.Height);

            settings.WindowLeft = bounds.Left;
            settings.WindowTop = bounds.Top;
            settings.WindowWidth = bounds.Width;
            settings.WindowHeight = bounds.Height;
            settings.WindowState = window.WindowState == WindowState.Maximized ? "Maximized" : "Normal";

            string path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            WriteSettings(path, settings);
        }
        catch
        {
            // Window placement is optional and must never prevent closing.
        }
    }

    private static void ApplyLanguage(System.Windows.Application application, AppLanguage language)
    {
        ResourceDictionary? current = application.Resources.MergedDictionaries
            .FirstOrDefault(dictionary => dictionary.Source?.OriginalString.Contains("Strings.", StringComparison.OrdinalIgnoreCase) == true);
        ResourceDictionary next = new()
        {
            Source = new Uri($"/ServerSpace;component/Resources/Strings.{(language == AppLanguage.German ? "de-DE" : "en-US")}.xaml", UriKind.Relative)
        };
        if (current is not null)
        {
            application.Resources.MergedDictionaries[application.Resources.MergedDictionaries.IndexOf(current)] = next;
        }
        else
        {
            application.Resources.MergedDictionaries.Insert(0, next);
        }

        _currentLanguage = language;
        CultureInfo culture = CultureInfo.GetCultureInfo(language == AppLanguage.German ? "de-DE" : "en-US");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static AppLanguage GetSystemLanguage() =>
        CultureInfo.CurrentUICulture.Name.StartsWith("de", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.German
            : AppLanguage.English;

    private static bool TryParseLanguage(string? value, out AppLanguage language)
    {
        if (value?.StartsWith("de", StringComparison.OrdinalIgnoreCase) == true)
        {
            language = AppLanguage.German;
            return true;
        }

        if (value?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true)
        {
            language = AppLanguage.English;
            return true;
        }

        language = default;
        return false;
    }

    private static LanguageSettings? ReadSettings()
    {
        try
        {
            string path = GetSettingsPath();
            return File.Exists(path)
                ? JsonSerializer.Deserialize<LanguageSettings>(File.ReadAllText(path))
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteSettings(string path, LanguageSettings settings) =>
        File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));

    private static bool IsOnAnyWorkingArea(double left, double top, double width, double height)
    {
        Rect windowRect = new(left, top, width, height);
        return Forms.Screen.AllScreens.Any(screen =>
        {
            Rect workingArea = new(screen.WorkingArea.Left, screen.WorkingArea.Top,
                screen.WorkingArea.Width, screen.WorkingArea.Height);
            return windowRect.IntersectsWith(workingArea);
        });
    }

    private static string GetSettingsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ServerSpace",
        SettingsFileName);

    private sealed class LanguageSettings
    {
        public string Language { get; set; } = "de-DE";
        public double? WindowLeft { get; set; }
        public double? WindowTop { get; set; }
        public double? WindowWidth { get; set; }
        public double? WindowHeight { get; set; }
        public string? WindowState { get; set; }
    }
}
