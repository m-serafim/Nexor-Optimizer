using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Nexor
{
    public partial class PerformancePage : Page
    {
        private string _currentLanguage;
        private bool _isProcessing = false;

        public PerformancePage(string language)
        {
            InitializeComponent();
            _currentLanguage = language;
            UpdateLanguage();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            AnimatePageLoad();
            LoadCurrentSettings();
        }

        #region Animation Methods (keeping existing code)
        private async void AnimatePageLoad()
        {
            try
            {
                AnimateElement(HeaderSection, 0, 1, -30, 0, 0.5);
                await Task.Delay(100);

                AnimateCard(QuickActionsCard, 150);
                await Task.Delay(150);
                AnimateCard(SystemTweaksCard, 150);
                await Task.Delay(150);
                AnimateCard(VisualEffectsCard, 150);
                await Task.Delay(150);
                AnimateCard(NetworkCard, 150);
                await Task.Delay(150);
                AnimateCard(GamingCard, 150);
                await Task.Delay(150);
                AnimateCard(MemoryCard, 150);
                await Task.Delay(150);
                AnimateCard(PrivacyCard, 150);
                await Task.Delay(150);
                AnimateCard(AdvancedCard, 150);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Animation error: {ex.Message}");
            }
        }

        private void AnimateElement(FrameworkElement element, double fromOpacity, double toOpacity,
            double fromTranslate, double toTranslate, double durationSeconds)
        {
            try
            {
                var storyboard = new Storyboard();

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
                    Storyboard.SetTargetProperty(translateAnimation,
                        new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[3].(TranslateTransform.Y)"));
                    storyboard.Children.Add(translateAnimation);
                }

                storyboard.Begin();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Element animation error: {ex.Message}");
            }
        }

        private void AnimateCard(FrameworkElement card, int delay)
        {
            Task.Delay(delay).ContinueWith(_ =>
            {
                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        var storyboard = new Storyboard();

                        var opacityAnimation = new DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = TimeSpan.FromSeconds(0.6),
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };

                        Storyboard.SetTarget(opacityAnimation, card);
                        Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
                        storyboard.Children.Add(opacityAnimation);

                        var scaleXAnimation = new DoubleAnimation
                        {
                            From = 0.95,
                            To = 1,
                            Duration = TimeSpan.FromSeconds(0.6),
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };

                        Storyboard.SetTarget(scaleXAnimation, card);
                        Storyboard.SetTargetProperty(scaleXAnimation,
                            new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
                        storyboard.Children.Add(scaleXAnimation);

                        var scaleYAnimation = new DoubleAnimation
                        {
                            From = 0.95,
                            To = 1,
                            Duration = TimeSpan.FromSeconds(0.6),
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };

                        Storyboard.SetTarget(scaleYAnimation, card);
                        Storyboard.SetTargetProperty(scaleYAnimation,
                            new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
                        storyboard.Children.Add(scaleYAnimation);

                        storyboard.Begin();
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Card animation error: {ex.Message}");
                }
            });
        }
        #endregion

        #region PowerShell Script Execution Infrastructure

        /// <summary>
        /// Extracts a PowerShell script from embedded resources or file system
        /// </summary>
        private bool ExtractScript(string scriptName, string outputPath)
        {
            try
            {
                // Try to load from embedded resources first
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = $"Nexor.{scriptName}";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (FileStream fileStream = File.Create(outputPath))
                        {
                            stream.CopyTo(fileStream);
                        }
                        return true;
                    }
                }

                // Fallback: Try to find in application directory
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string physicalScriptPath = Path.Combine(appDir, scriptName);

                if (!File.Exists(physicalScriptPath))
                {
                    // Try project root (for development)
                    string projectRoot = Path.GetFullPath(Path.Combine(appDir, @"..\..\"));
                    string altScriptPath = Path.Combine(projectRoot, scriptName);
                    if (File.Exists(altScriptPath))
                    {
                        physicalScriptPath = altScriptPath;
                    }
                }

                if (!File.Exists(physicalScriptPath))
                {
                    return false;
                }

                File.Copy(physicalScriptPath, outputPath, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Runs a PowerShell script with ExecutionPolicy Bypass (HIDDEN - no console window)
        /// </summary>
        private async Task<ScriptResult> RunPowerShellScriptAsync(string scriptPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,  // ALWAYS hidden
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (Process process = Process.Start(psi))
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();

                        process.WaitForExit();

                        return new ScriptResult
                        {
                            Success = process.ExitCode == 0,
                            ExitCode = process.ExitCode,
                            Output = output,
                            Error = error
                        };
                    }
                }
                catch (Exception ex)
                {
                    return new ScriptResult
                    {
                        Success = false,
                        ExitCode = -1,
                        Error = ex.Message
                    };
                }
            });
        }

        /// <summary>
        /// Execute a performance tweak script
        /// </summary>
        private async Task ExecuteTweakScript(string scriptName, string tweakName)
        {
            if (_isProcessing) return;

            _isProcessing = true;

            try
            {
                string scriptPath = Path.Combine(Path.GetTempPath(), scriptName);

                if (!ExtractScript(scriptName, scriptPath))
                {
                    await ShowError(
                        _currentLanguage == "PT" ? "Erro" : "Error",
                        _currentLanguage == "PT"
                            ? $"Script '{scriptName}' não encontrado!"
                            : $"Script '{scriptName}' not found!"
                    );
                    return;
                }

                var result = await RunPowerShellScriptAsync(scriptPath);

                // Clean up temp file
                try { File.Delete(scriptPath); } catch { }

                if (result.Success)
                {
                    await ShowSuccess(tweakName);
                }
                else if (result.ExitCode == 1)
                {
                    await ShowError(
                        _currentLanguage == "PT" ? "Erro" : "Error",
                        _currentLanguage == "PT"
                            ? "O programa precisa de privilégios de Administrador!"
                            : "The program needs Administrator privileges!"
                    );
                }
                else
                {
                    await ShowError(
                        _currentLanguage == "PT" ? "Erro" : "Error",
                        _currentLanguage == "PT"
                            ? $"Erro ao executar: {result.Error}"
                            : $"Execution error: {result.Error}"
                    );
                }
            }
            catch (Exception ex)
            {
                await ShowError("Error", $"Exception: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// Script execution result
        /// </summary>
        private class ScriptResult
        {
            public bool Success { get; set; }
            public int ExitCode { get; set; }
            public string Output { get; set; }
            public string Error { get; set; }
        }

        #endregion

        #region UI Helpers

        private async Task ShowSuccess(string tweakName)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                // You can implement a toast notification here
                MessageBox.Show(
                    _currentLanguage == "PT" ? $"{tweakName} aplicado com sucesso!" : $"{tweakName} applied successfully!",
                    _currentLanguage == "PT" ? "Sucesso" : "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            });
        }

        private async Task ShowError(string title, string message)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        #endregion

        #region Settings Management

        private void LoadCurrentSettings()
        {
            try
            {
                // TODO: Load current system settings and update toggles
                // This will check registry/system state and set checkbox states
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load settings error: {ex.Message}");
            }
        }

        #endregion

        #region Language Management

        private void UpdateLanguage()
        {
            // Keep your existing UpdateLanguage() method here
            // (I'm omitting it to keep the code shorter, but keep all your existing translation code)
        }

        #endregion

        #region Event Handlers - System Tweaks

        private async void ChkGameMode_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableGameMode.ps1", "Game Mode");
        }

        private async void ChkGameMode_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableGameMode.ps1", "Game Mode");
        }

        private async void ChkPowerPlan_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("SetHighPerformancePower.ps1", "High Performance Power Plan");
        }

        private async void ChkPowerPlan_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("SetBalancedPower.ps1", "Balanced Power Plan");
        }

        private async void ChkUltimatePower_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableUltimatePerformance.ps1", "Ultimate Performance");
        }

        private async void ChkUltimatePower_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableUltimatePerformance.ps1", "Ultimate Performance");
        }

        private async void ChkHibernation_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableHibernation.ps1", "Hibernation");
        }

        private async void ChkHibernation_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableHibernation.ps1", "Hibernation");
        }

        private async void ChkFastStartup_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableFastStartup.ps1", "Fast Startup");
        }

        private async void ChkFastStartup_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableFastStartup.ps1", "Fast Startup");
        }

        #endregion

        #region Event Handlers - Visual Effects

        private async void ChkAnimations_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableAnimations.ps1", "Window Animations");
        }

        private async void ChkAnimations_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableAnimations.ps1", "Window Animations");
        }

        private async void ChkTransparency_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableTransparency.ps1", "Transparency Effects");
        }

        private async void ChkTransparency_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableTransparency.ps1", "Transparency Effects");
        }

        private async void ChkBestPerformance_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("SetBestPerformance.ps1", "Best Performance Mode");
        }

        private async void ChkBestPerformance_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("SetBestAppearance.ps1", "Best Appearance Mode");
        }

        #endregion

        #region Event Handlers - Network

        private async void ChkDNS_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("SetCloudflareDNS.ps1", "DNS Optimization");
        }

        private async void ChkDNS_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("ResetDNS.ps1", "DNS Reset");
        }

        private async void ChkTCP_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("OptimizeTCP.ps1", "TCP/IP Optimization");
        }

        private async void ChkTCP_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("ResetTCP.ps1", "TCP/IP Reset");
        }

        private async void ChkThrottle_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableNetworkThrottle.ps1", "Network Throttling");
        }

        private async void ChkThrottle_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableNetworkThrottle.ps1", "Network Throttling");
        }

        #endregion

        #region Event Handlers - Gaming

        private async void ChkGPUScheduling_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableGPUScheduling.ps1", "GPU Scheduling");
        }

        private async void ChkGPUScheduling_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableGPUScheduling.ps1", "GPU Scheduling");
        }

        private async void ChkFullscreen_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableFullscreenOpt.ps1", "Fullscreen Optimizations");
        }

        private async void ChkFullscreen_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableFullscreenOpt.ps1", "Fullscreen Optimizations");
        }

        private async void ChkMSI_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableMSIMode.ps1", "MSI Mode");
        }

        private async void ChkMSI_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableMSIMode.ps1", "MSI Mode");
        }

        #endregion

        #region Event Handlers - Memory & Storage

        private async void ChkSuperfetch_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableSuperfetch.ps1", "Superfetch");
        }

        private async void ChkSuperfetch_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableSuperfetch.ps1", "Superfetch");
        }

        private async void ChkPageFile_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("OptimizePageFile.ps1", "Virtual Memory");
        }

        private async void ChkPageFile_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("ResetPageFile.ps1", "Virtual Memory");
        }

        private async void ChkIndexing_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableIndexing.ps1", "Search Indexing");
        }

        private async void ChkIndexing_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableIndexing.ps1", "Search Indexing");
        }

        private async void ChkTRIM_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableTRIM.ps1", "TRIM");
        }

        private async void ChkTRIM_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableTRIM.ps1", "TRIM");
        }

        #endregion

        #region Event Handlers - Privacy

        private async void ChkTelemetry_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableTelemetry.ps1", "Telemetry");
        }

        private async void ChkTelemetry_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableTelemetry.ps1", "Telemetry");
        }

        private async void ChkBackgroundApps_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableBackgroundApps.ps1", "Background Apps");
        }

        private async void ChkBackgroundApps_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableBackgroundApps.ps1", "Background Apps");
        }

        private async void ChkCortana_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableCortana.ps1", "Cortana");
        }

        private async void ChkCortana_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableCortana.ps1", "Cortana");
        }

        private async void ChkTips_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableWindowsTips.ps1", "Windows Tips");
        }

        private async void ChkTips_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableWindowsTips.ps1", "Windows Tips");
        }

        #endregion

        #region Event Handlers - Advanced

        private async void ChkCPUPriority_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("OptimizeCPUPriority.ps1", "CPU Priority");
        }

        private async void ChkCPUPriority_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("ResetCPUPriority.ps1", "CPU Priority");
        }

        private async void ChkNagle_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableNagle.ps1", "Nagle's Algorithm");
        }

        private async void ChkNagle_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableNagle.ps1", "Nagle's Algorithm");
        }

        private async void ChkTimer_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableHighPrecisionTimer.ps1", "High Precision Timer");
        }

        private async void ChkTimer_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableHighPrecisionTimer.ps1", "High Precision Timer");
        }

        private async void ChkCoreParking_Checked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("DisableCoreParking.ps1", "Core Parking");
        }

        private async void ChkCoreParking_Unchecked(object sender, RoutedEventArgs e)
        {
            await ExecuteTweakScript("EnableCoreParking.ps1", "Core Parking");
        }

        #endregion

        #region Quick Action Buttons

        private void BtnOptimizeAll_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Apply all recommended tweaks
        }

        private void BtnClearRAM_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Clear RAM cache
        }

        private void BtnResetTweaks_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Reset all tweaks to default
        }

        #endregion
    }
}