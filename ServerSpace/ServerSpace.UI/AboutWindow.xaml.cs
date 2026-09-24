using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using ServerSpace.UI.Localization;

namespace ServerSpace.UI;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(AboutWindow).Assembly;
        string version = assembly.GetName().Version?.ToString(3) ?? "Unbekannt";
        VersionTextBlock.Text = LocalizationManager.Format("AboutVersion", version);

        ConfigureLink(ProjectLink, AppLinks.SourceCodeUrl);
        ConfigureLink(OrganizationLink, AppLinks.OrganizationUrl);
        ConfigureLink(LicenseLink, AppLinks.LicenseUrl);
    }

    private static void ConfigureLink(System.Windows.Documents.Hyperlink link, string url)
    {
        link.Tag = url;
    }

    private void ProjectLink_Click(object sender, RoutedEventArgs e) => OpenLink(AppLinks.SourceCodeUrl);

    private void OrganizationLink_Click(object sender, RoutedEventArgs e) => OpenLink(AppLinks.OrganizationUrl);

    private void LicenseLink_Click(object sender, RoutedEventArgs e) => OpenLink(AppLinks.LicenseUrl);

    private void OpenLink(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ShowLinkError(LocalizationManager.GetString("ErrorLinkUnavailable"));
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.ToString(),
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            ShowLinkError(LocalizationManager.Format("ErrorLinkOpen", exception.Message));
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void ShowLinkError(string message) =>
        System.Windows.MessageBox.Show(this, message, LocalizationManager.GetString("DialogReportTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
}
