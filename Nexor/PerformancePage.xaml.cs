using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Nexor
{
    public partial class PerformancePage : Page
    {
        private string _currentLanguage;
        private bool _isProcessing = false;
        private List<OptimizationItem> _selectedOptimizations = new List<OptimizationItem>();

        public PerformancePage(string language)
        {
            InitializeComponent();
            _currentLanguage = language;
            UpdateLanguage();
            InitializeOptimizationTracking();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            AnimatePageLoad();
            LoadCurrentSettings();
        }

        #region Optimization Tracking

        private void InitializeOptimizationTracking()
        {
            // System Tweaks
            ChkGameMode.Tag = new OptimizationItem("EnableGameMode.ps1", "DisableGameMode.ps1", "Game Mode");
            ChkPowerPlan.Tag = new OptimizationItem("SetHighPerformancePower.ps1", "SetBalancedPower.ps1", "High Performance Power Plan");
            ChkUltimatePower.Tag = new OptimizationItem("EnableUltimatePerformance.ps1", "DisableUltimatePerformance.ps1", "Ultimate Performance");
            ChkHibernation.Tag = new OptimizationItem("DisableHibernation.ps1", "EnableHibernation.ps1", "Hibernation");
            ChkFastStartup.Tag = new OptimizationItem("DisableFastStartup.ps1", "EnableFastStartup.ps1", "Fast Startup");

            // Visual Effects
            ChkAnimations.Tag = new OptimizationItem("DisableAnimations.ps1", "EnableAnimations.ps1", "Window Animations");
            ChkTransparency.Tag = new OptimizationItem("DisableTransparency.ps1", "EnableTransparency.ps1", "Transparency Effects");
            ChkBestPerformance.Tag = new OptimizationItem("SetBestPerformance.ps1", "SetBestAppearance.ps1", "Best Performance Mode");

            // Network
            ChkDNS.Tag = new OptimizationItem("SetCloudflareDNS.ps1", "ResetDNS.ps1", "DNS Optimization");
            ChkTCP.Tag = new OptimizationItem("OptimizeTCP.ps1", "ResetTCP.ps1", "TCP/IP Optimization");
            ChkThrottle.Tag = new OptimizationItem("DisableNetworkThrottle.ps1", "EnableNetworkThrottle.ps1", "Network Throttling");

            // Gaming
            ChkGPUScheduling.Tag = new OptimizationItem("EnableGPUScheduling.ps1", "DisableGPUScheduling.ps1", "GPU Scheduling");
            ChkFullscreen.Tag = new OptimizationItem("DisableFullscreenOpt.ps1", "EnableFullscreenOpt.ps1", "Fullscreen Optimizations");
            ChkMSI.Tag = new OptimizationItem("EnableMSIMode.ps1", "DisableMSIMode.ps1", "MSI Mode");

            // Memory & Storage
            ChkSuperfetch.Tag = new OptimizationItem("DisableSuperfetch.ps1", "EnableSuperfetch.ps1", "Superfetch");
            ChkPageFile.Tag = new OptimizationItem("OptimizePageFile.ps1", "ResetPageFile.ps1", "Virtual Memory");
            ChkIndexing.Tag = new OptimizationItem("DisableIndexing.ps1", "EnableIndexing.ps1", "Search Indexing");
            ChkTRIM.Tag = new OptimizationItem("EnableTRIM.ps1", "DisableTRIM.ps1", "TRIM");

            // Privacy
            ChkTelemetry.Tag = new OptimizationItem("DisableTelemetry.ps1", "EnableTelemetry.ps1", "Telemetry");
            ChkBackgroundApps.Tag = new OptimizationItem("DisableBackgroundApps.ps1", "EnableBackgroundApps.ps1", "Background Apps");
            ChkCortana.Tag = new OptimizationItem("DisableCortana.ps1", "EnableCortana.ps1", "Cortana");
            ChkTips.Tag = new OptimizationItem("DisableWindowsTips.ps1", "EnableWindowsTips.ps1", "Windows Tips");

            // Advanced
            ChkCPUPriority.Tag = new OptimizationItem("OptimizeCPUPriority.ps1", "ResetCPUPriority.ps1", "CPU Priority");
            ChkNagle.Tag = new OptimizationItem("DisableNagle.ps1", "EnableNagle.ps1", "Nagle's Algorithm");
            ChkTimer.Tag = new OptimizationItem("EnableHighPrecisionTimer.ps1", "DisableHighPrecisionTimer.ps1", "High Precision Timer");
            ChkCoreParking.Tag = new OptimizationItem("DisableCoreParking.ps1", "EnableCoreParking.ps1", "Core Parking");
        }

        private class OptimizationItem
        {
            public string EnableScript { get; set; }
            public string DisableScript { get; set; }
            public string Name { get; set; }
            public bool Enable { get; set; }

            public OptimizationItem(string enableScript, string disableScript, string name)
            {
                EnableScript = enableScript;
                DisableScript = disableScript;
                Name = name;
            }
        }

        #endregion

        #region Animation Methods
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

        private bool ExtractScript(string scriptName, string outputPath)
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = $"Nexor.{scriptName}";

                using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
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

                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string physicalScriptPath = Path.Combine(appDir, scriptName);

                if (!File.Exists(physicalScriptPath))
                {
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
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (Process? process = Process.Start(psi))
                    {
                        if (process == null)
                        {
                            return new ScriptResult
                            {
                                Success = false,
                                ExitCode = -1,
                                Output = string.Empty,
                                Error = "Failed to start process"
                            };
                        }

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
                        Output = string.Empty,
                        Error = ex.Message
                    };
                }
            });
        }

        private class ScriptResult
        {
            public bool Success { get; set; }
            public int ExitCode { get; set; }
            public string Output { get; set; } = string.Empty;
            public string Error { get; set; } = string.Empty;
        }

        #endregion

        #region Restore Point Management

        private async Task<bool> CreateRestorePoint()
        {
            return await Task.Run(() =>
            {
                try
                {
                    string description = $"Nexor Optimizer - {DateTime.Now:yyyy-MM-dd HH:mm}";

                    ManagementScope scope = new ManagementScope("\\\\localhost\\root\\default");
                    ManagementPath path = new ManagementPath("SystemRestore");
                    ObjectGetOptions options = new ObjectGetOptions();
                    ManagementClass process = new ManagementClass(scope, path, options);

                    ManagementBaseObject inParams = process.GetMethodParameters("CreateRestorePoint");
                    inParams["Description"] = description;
                    inParams["RestorePointType"] = 12;
                    inParams["EventType"] = 100;

                    ManagementBaseObject outParams = process.InvokeMethod("CreateRestorePoint", inParams, null);

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Restore point creation failed: {ex.Message}");
                    return false;
                }
            });
        }

        #endregion

        #region Batch Optimization

        public enum RestorePointDialogResult
        {
            Cancel,
            CreateAndOptimize,
            OptimizeWithoutRestore
        }

        private async void BtnOptimize_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;

            _selectedOptimizations.Clear();
            CollectSelectedOptimizations();

            if (_selectedOptimizations.Count == 0)
            {
                await ShowError(
                    _currentLanguage == "PT" ? "Aviso" : "Warning",
                    _currentLanguage == "PT"
                        ? "Por favor, selecione pelo menos uma otimização!"
                        : "Please select at least one optimization!"
                );
                return;
            }

            var result = await ShowRestorePointDialog();

            if (result == RestorePointDialogResult.Cancel)
            {
                return;
            }

            _isProcessing = true;

            try
            {
                if (result == RestorePointDialogResult.CreateAndOptimize)
                {
                    ShowProgress(_currentLanguage == "PT" ? "Criando ponto de restauração..." : "Creating restore point...");

                    bool restorePointCreated = await CreateRestorePoint();

                    if (!restorePointCreated)
                    {
                        var continueAnyway = MessageBox.Show(
                            _currentLanguage == "PT"
                                ? "Falha ao criar ponto de restauração. Continuar mesmo assim?"
                                : "Failed to create restore point. Continue anyway?",
                            _currentLanguage == "PT" ? "Aviso" : "Warning",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning
                        );

                        if (continueAnyway != MessageBoxResult.Yes)
                        {
                            HideProgress();
                            return;
                        }
                    }
                    else
                    {
                        await Task.Delay(1000);
                    }
                }

                await ApplyOptimizations();
                await ShowCompletionMessage();
            }
            catch (Exception ex)
            {
                await ShowError(
                    _currentLanguage == "PT" ? "Erro" : "Error",
                    $"Exception: {ex.Message}"
                );
            }
            finally
            {
                _isProcessing = false;
                HideProgress();
            }
        }

        private void CollectSelectedOptimizations()
        {
            var checkboxes = new[]
            {
                ChkGameMode, ChkPowerPlan, ChkUltimatePower, ChkHibernation, ChkFastStartup,
                ChkAnimations, ChkTransparency, ChkBestPerformance,
                ChkDNS, ChkTCP, ChkThrottle,
                ChkGPUScheduling, ChkFullscreen, ChkMSI,
                ChkSuperfetch, ChkPageFile, ChkIndexing, ChkTRIM,
                ChkTelemetry, ChkBackgroundApps, ChkCortana, ChkTips,
                ChkCPUPriority, ChkNagle, ChkTimer, ChkCoreParking
            };

            foreach (var checkbox in checkboxes)
            {
                if (checkbox.Tag is OptimizationItem item)
                {
                    item.Enable = checkbox.IsChecked == true;
                    if (item.Enable || checkbox.IsChecked == false)
                    {
                        _selectedOptimizations.Add(item);
                    }
                }
            }
        }

        private async Task ApplyOptimizations()
        {
            int current = 0;
            int total = _selectedOptimizations.Count;

            foreach (var optimization in _selectedOptimizations)
            {
                current++;

                string progressText = _currentLanguage == "PT"
                    ? $"Aplicando: {optimization.Name} ({current}/{total})"
                    : $"Applying: {optimization.Name} ({current}/{total})";

                ShowProgress(progressText);

                string scriptToUse = optimization.Enable ? optimization.EnableScript : optimization.DisableScript;
                string scriptPath = Path.Combine(Path.GetTempPath(), scriptToUse);

                if (ExtractScript(scriptToUse, scriptPath))
                {
                    var result = await RunPowerShellScriptAsync(scriptPath);

                    try { File.Delete(scriptPath); } catch { }

                    if (!result.Success)
                    {
                        Debug.WriteLine($"Failed to apply {optimization.Name}: {result.Error}");
                    }

                    await Task.Delay(300);
                }
            }
        }

        private Task<RestorePointDialogResult> ShowRestorePointDialog()
        {
            var tcs = new TaskCompletionSource<RestorePointDialogResult>();

            Dispatcher.Invoke(() =>
            {
                var dialog = new RestorePointWarningDialog(_currentLanguage);
                dialog.Owner = Window.GetWindow(this);
                dialog.ShowDialog();

                tcs.SetResult(dialog.Result);
            });

            return tcs.Task;
        }

        private async Task ShowCompletionMessage()
        {
            int successCount = _selectedOptimizations.Count;

            string message = _currentLanguage == "PT"
                ? $"✅ {successCount} otimizações aplicadas com sucesso!\n\nÉ recomendado reiniciar o computador para aplicar todas as mudanças."
                : $"✅ {successCount} optimizations applied successfully!\n\nIt's recommended to restart your computer to apply all changes.";

            string title = _currentLanguage == "PT" ? "Concluído" : "Completed";

            await Dispatcher.InvokeAsync(() =>
            {
                var result = MessageBox.Show(
                    message,
                    title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information
                );

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "shutdown",
                        Arguments = "/r /t 10",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
            });
        }

        #endregion

        #region UI Helpers

        private void ShowProgress(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressText.Text = message;
                ProgressOverlay.Visibility = Visibility.Visible;

                var storyboard = new Storyboard();
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
                Storyboard.SetTarget(fadeIn, ProgressOverlay);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
                storyboard.Children.Add(fadeIn);
                storyboard.Begin();
            });
        }

        private void HideProgress()
        {
            Dispatcher.Invoke(() =>
            {
                var storyboard = new Storyboard();
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
                Storyboard.SetTarget(fadeOut, ProgressOverlay);
                Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));
                storyboard.Completed += (s, e) => ProgressOverlay.Visibility = Visibility.Collapsed;
                storyboard.Children.Add(fadeOut);
                storyboard.Begin();
            });
        }

        private async Task ShowSuccess(string tweakName)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                SuccessNotificationText.Text = _currentLanguage == "PT"
                    ? $"✅ {tweakName} aplicado com sucesso!"
                    : $"✅ {tweakName} applied successfully!";

                SuccessNotification.Visibility = Visibility.Visible;

                var storyboard = new Storyboard();

                var slideDown = new DoubleAnimation(-100, 0, TimeSpan.FromSeconds(0.5))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(slideDown, SuccessNotification);
                Storyboard.SetTargetProperty(slideDown, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
                Storyboard.SetTarget(fadeIn, SuccessNotification);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));

                storyboard.Children.Add(slideDown);
                storyboard.Children.Add(fadeIn);

                storyboard.Completed += async (s, e) =>
                {
                    await Task.Delay(3000);

                    var hideStoryboard = new Storyboard();
                    var slideUp = new DoubleAnimation(0, -100, TimeSpan.FromSeconds(0.5))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                    };
                    Storyboard.SetTarget(slideUp, SuccessNotification);
                    Storyboard.SetTargetProperty(slideUp, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

                    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5));
                    Storyboard.SetTarget(fadeOut, SuccessNotification);
                    Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));

                    hideStoryboard.Children.Add(slideUp);
                    hideStoryboard.Children.Add(fadeOut);
                    hideStoryboard.Completed += (s2, e2) => SuccessNotification.Visibility = Visibility.Collapsed;
                    hideStoryboard.Begin();
                };

                storyboard.Begin();
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
        }

        #endregion

        #region Quick Action Buttons

        private void BtnRecommended_Click(object sender, RoutedEventArgs e)
        {
            // Recommended preset: Safe, balanced optimizations for most users
            
            // System Tweaks
            ChkGameMode.IsChecked = true;
            ChkPowerPlan.IsChecked = true;
            ChkUltimatePower.IsChecked = false;
            ChkHibernation.IsChecked = true;
            ChkFastStartup.IsChecked = true;

            // Visual Effects - Keep some for better experience
            ChkAnimations.IsChecked = false;
            ChkTransparency.IsChecked = true;
            ChkBestPerformance.IsChecked = false;

            // Network
            ChkDNS.IsChecked = true;
            ChkTCP.IsChecked = true;
            ChkThrottle.IsChecked = true;

            // Gaming
            ChkGPUScheduling.IsChecked = true;
            ChkFullscreen.IsChecked = false;
            ChkMSI.IsChecked = false;

            // Memory & Storage
            ChkSuperfetch.IsChecked = true;
            ChkPageFile.IsChecked = false;
            ChkIndexing.IsChecked = true;
            ChkTRIM.IsChecked = true;

            // Privacy
            ChkTelemetry.IsChecked = true;
            ChkBackgroundApps.IsChecked = true;
            ChkCortana.IsChecked = true;
            ChkTips.IsChecked = true;

            // Advanced - None for safety
            ChkCPUPriority.IsChecked = false;
            ChkNagle.IsChecked = false;
            ChkTimer.IsChecked = false;
            ChkCoreParking.IsChecked = false;
        }

        private void BtnMinimum_Click(object sender, RoutedEventArgs e)
        {
            // Minimum preset: Only essential, safest optimizations
    
            // System Tweaks
            ChkGameMode.IsChecked = false;
            ChkPowerPlan.IsChecked = false;
            ChkUltimatePower.IsChecked = false;
            ChkHibernation.IsChecked = false;
            ChkFastStartup.IsChecked = false;

            // Visual Effects - Keep all for user experience
            ChkAnimations.IsChecked = false;
            ChkTransparency.IsChecked = false;
            ChkBestPerformance.IsChecked = false;

            // Network
            ChkDNS.IsChecked = true;
            ChkTCP.IsChecked = false;
            ChkThrottle.IsChecked = true;

            // Gaming
            ChkGPUScheduling.IsChecked = false;
            ChkFullscreen.IsChecked = false;
            ChkMSI.IsChecked = false;

            // Memory & Storage
            ChkSuperfetch.IsChecked = false;
            ChkPageFile.IsChecked = false;
            ChkIndexing.IsChecked = false;
            ChkTRIM.IsChecked = true;

            // Privacy
            ChkTelemetry.IsChecked = true;
            ChkBackgroundApps.IsChecked = true;
            ChkCortana.IsChecked = false;
            ChkTips.IsChecked = true;

            // Advanced - None
            ChkCPUPriority.IsChecked = false;
            ChkNagle.IsChecked = false;
            ChkTimer.IsChecked = false;
            ChkCoreParking.IsChecked = false;
        }

        private void BtnGaming_Click(object sender, RoutedEventArgs e)
        {
            // Gaming preset: Maximum performance for gaming
    
            // System Tweaks - All performance options
            ChkGameMode.IsChecked = true;
            ChkPowerPlan.IsChecked = true;
            ChkUltimatePower.IsChecked = true;
            ChkHibernation.IsChecked = true;
            ChkFastStartup.IsChecked = true;

            // Visual Effects - Disable all for maximum FPS
            ChkAnimations.IsChecked = true;
            ChkTransparency.IsChecked = true;
            ChkBestPerformance.IsChecked = true;

            // Network - All optimizations
            ChkDNS.IsChecked = true;
            ChkTCP.IsChecked = true;
            ChkThrottle.IsChecked = true;

            // Gaming - All gaming optimizations
            ChkGPUScheduling.IsChecked = true;
            ChkFullscreen.IsChecked = true;
            ChkMSI.IsChecked = true;

            // Memory & Storage - All optimizations
            ChkSuperfetch.IsChecked = true;
            ChkPageFile.IsChecked = true;
            ChkIndexing.IsChecked = true;
            ChkTRIM.IsChecked = true;

            // Privacy - All for performance
            ChkTelemetry.IsChecked = true;
            ChkBackgroundApps.IsChecked = true;
            ChkCortana.IsChecked = true;
            ChkTips.IsChecked = true;

            // Advanced - All gaming-related tweaks
            ChkCPUPriority.IsChecked = true;
            ChkNagle.IsChecked = true;
            ChkTimer.IsChecked = true;
            ChkCoreParking.IsChecked = true;
        }

        #endregion
    }

    #region Restore Point Warning Dialog

    public partial class RestorePointWarningDialog : Window
    {
        public PerformancePage.RestorePointDialogResult Result { get; private set; } = PerformancePage.RestorePointDialogResult.Cancel;
        private string _language;

        public RestorePointWarningDialog(string language)
        {
            _language = language;
            InitializeComponent();
            SetupUI();
        }

        private void InitializeComponent()
        {
            Width = 500;
            Height = 280;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(15, 20, 25));
            AllowsTransparency = true;

            var mainBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(25, 144, 254)),
                BorderThickness = new Thickness(2, 2, 2, 2),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(30, 30, 30, 30)
            };

            var stackPanel = new StackPanel();

            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var icon = new TextBlock
            {
                Text = "⚠️",
                FontSize = 32,
                Margin = new Thickness(0, 0, 15, 0)
            };

            var title = new TextBlock
            {
                Text = _language == "PT" ? "Ponto de Restauração Recomendado" : "Restore Point Recommended",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            headerPanel.Children.Add(icon);
            headerPanel.Children.Add(title);

            var message = new TextBlock
            {
                Text = _language == "PT"
                    ? "É altamente recomendado criar um ponto de restauração antes de aplicar otimizações ao sistema.\n\nIsso permite reverter as mudanças caso algo não funcione como esperado."
                    : "It is highly recommended to create a restore point before applying system optimizations.\n\nThis allows you to revert changes if something doesn't work as expected.",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 30)
            };

            var buttonPanel = new UniformGrid
            {
                Rows = 1,
                Columns = 3,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var btnCreateAndOptimize = CreateButton(
                _language == "PT" ? "Criar e Otimizar" : "Create & Optimize",
                new SolidColorBrush(Color.FromRgb(16, 185, 129))
            );
            btnCreateAndOptimize.Click += (s, e) =>
            {
                Result = PerformancePage.RestorePointDialogResult.CreateAndOptimize;
                Close();
            };

            var btnOptimizeOnly = CreateButton(
                _language == "PT" ? "Só Otimizar" : "Optimize Only",
                new SolidColorBrush(Color.FromRgb(245, 158, 11))
            );
            btnOptimizeOnly.Click += (s, e) =>
            {
                Result = PerformancePage.RestorePointDialogResult.OptimizeWithoutRestore;
                Close();
            };

            var btnCancel = CreateButton(
                _language == "PT" ? "Cancelar" : "Cancel",
                new SolidColorBrush(Color.FromRgb(107, 114, 128))
            );
            btnCancel.Click += (s, e) =>
            {
                Result = PerformancePage.RestorePointDialogResult.Cancel;
                Close();
            };

            buttonPanel.Children.Add(btnCreateAndOptimize);
            buttonPanel.Children.Add(btnOptimizeOnly);
            buttonPanel.Children.Add(btnCancel);

            stackPanel.Children.Add(headerPanel);
            stackPanel.Children.Add(message);
            stackPanel.Children.Add(buttonPanel);

            mainBorder.Child = stackPanel;
            Content = mainBorder;
        }

        private Button CreateButton(string text, Brush background)
        {
            var button = new Button
            {
                Content = text,
                Background = background,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(15, 10, 15, 10),
                Margin = new Thickness(5, 0, 5, 0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };

            var template = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            factory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(contentFactory);

            template.VisualTree = factory;
            button.Template = template;

            return button;
        }

        private void SetupUI()
        {
            Result = PerformancePage.RestorePointDialogResult.Cancel;
        }

    }
    }

    #endregion