using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Nexor.Licensing;

namespace Nexor
{
    public partial class LicenseSettingsPage : Page
    {
        // Point this to your API base URL
        private readonly LicensingApiClient _apiClient = new("http://localhost:5239");

        public LicenseSettingsPage()
        {
            InitializeComponent();
            Loaded += LicenseSettingsPage_Loaded;
        }

        private async void LicenseSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            await AnimatePageLoad();
        }

        private async Task AnimatePageLoad()
        {
            try
            {
                // Animate header
                if (HeaderSection != null)
                {
                    AnimateElement(HeaderSection, 0, 1, -30, 0, 0.5, false);
                }

                await Task.Delay(150);

                // Animate card
                if (LicenseCard != null)
                {
                    AnimateElement(LicenseCard, 0, 1, 30, 0, 0.6, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Animation error: {ex.Message}");
            }
        }

        private void AnimateElement(FrameworkElement element, double fromOpacity, double toOpacity,
            double fromTranslate, double toTranslate, double durationSeconds, bool isXAxis = true)
        {
            try
            {
                var storyboard = new Storyboard();

                // Opacity animation
                var opacityAnimation = new DoubleAnimation
                {
                    From = fromOpacity,
                    To = toOpacity,
                    Duration = TimeSpan.FromSeconds(durationSeconds),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                Storyboard.SetTarget(opacityAnimation, element);
                Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
                storyboard.Children.Add(opacityAnimation);

                // Transform animation
                if (element.RenderTransform is TransformGroup)
                {
                    var translateAnimation = new DoubleAnimation
                    {
                        From = fromTranslate,
                        To = toTranslate,
                        Duration = TimeSpan.FromSeconds(durationSeconds),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };

                    Storyboard.SetTarget(translateAnimation, element);

                    string propertyPath = isXAxis
                        ? "(UIElement.RenderTransform).(TransformGroup.Children)[3].(TranslateTransform.X)"
                        : "(UIElement.RenderTransform).(TransformGroup.Children)[3].(TranslateTransform.Y)";

                    Storyboard.SetTargetProperty(translateAnimation, new PropertyPath(propertyPath));
                    storyboard.Children.Add(translateAnimation);
                }

                storyboard.Begin();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Element animation error: {ex.Message}");
            }
        }

        private async void BtnActivate_Click(object sender, RoutedEventArgs e)
        {
            string licenseKey = TxtLicenseKey.Text?.Trim();

            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                ShowStatus(false, "Please enter a license key");
                return;
            }

            try
            {
                // Show loading state
                BtnActivate.IsEnabled = false;
                LoadingPanel.Visibility = Visibility.Visible;
                StatusBorder.Visibility = Visibility.Collapsed;

                // Use machine name as machineId (or your own machine-id logic)
                string machineId = Environment.MachineName;

                // Call the API
                var result = await _apiClient.ActivateAsync(licenseKey, machineId);

                // Hide loading state
                LoadingPanel.Visibility = Visibility.Collapsed;
                BtnActivate.IsEnabled = true;

                // Show result from API
                ShowStatus(result.Success, result.Message);
            }
            catch (Exception ex)
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                BtnActivate.IsEnabled = true;
                ShowStatus(false, $"Error: {ex.Message}");
            }
        }

        private void ShowStatus(bool success, string message)
        {
            try
            {
                // Set icon
                StatusIcon.Text = success ? "✓" : "✗";

                // Set message
                StatusMessage.Text = message;

                // Set colors
                var backgroundColor = success ? "#1A10B981" : "#1AEF4444";
                var textColor = success ? "#10B981" : "#EF4444";

                StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backgroundColor));
                StatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor));
                StatusMessage.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor));

                // Animate in
                StatusBorder.Visibility = Visibility.Visible;
                StatusBorder.Opacity = 0;

                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.3),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                StatusBorder.BeginAnimation(OpacityProperty, fadeIn);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Show status error: {ex.Message}");
            }
        }
    }
}