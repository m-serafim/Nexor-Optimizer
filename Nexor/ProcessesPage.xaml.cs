using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Nexor
{
    public partial class ProcessesPage : Page
    {
        private readonly ObservableCollection<ProcessInfo> _processes = new();
        private readonly List<ProcessInfo> _allProcesses = new();
        private DispatcherTimer? _refreshTimer;
        private readonly string _currentLanguage;
        private readonly Dictionary<string, DateTime> _lastCpuTime = new();
        private readonly Dictionary<string, TimeSpan> _lastTotalTime = new();
        private int _currentSortMode;
        private bool _initialAnimationsRan;

        private readonly Dictionary<string, (string icon, string description)> _appInfo =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Browsers
                { "chrome", ("🌐", "Google Chrome") },
                { "firefox", ("🦊", "Mozilla Firefox") },
                { "msedge", ("🌐", "Microsoft Edge") },
                { "opera", ("🎭", "Opera Browser") },
                { "brave", ("🦁", "Brave Browser") },
                { "safari", ("🧭", "Safari") },
                
                // Communication
                { "discord", ("💬", "Discord") },
                { "spotify", ("🎵", "Spotify") },
                { "teams", ("👥", "Microsoft Teams") },
                { "zoom", ("📹", "Zoom") },
                { "skype", ("📞", "Skype") },
                { "slack", ("💼", "Slack") },
                { "whatsapp", ("💚", "WhatsApp") },
                { "telegram", ("✈️", "Telegram") },
                
                // Gaming
                { "steam", ("🎮", "Steam") },
                { "epicgameslauncher", ("🎮", "Epic Games") },
                { "riotclientservices", ("🎮", "Riot Client") },
                { "battlenet", ("⚔️", "Battle.net") },
                { "leagueclient", ("🎮", "League of Legends") },
                { "valorant", ("🎮", "Valorant") },
                
                // Development
                { "code", ("💻", "VS Code") },
                { "devenv", ("🔧", "Visual Studio") },
                { "notepad++", ("📝", "Notepad++") },
                { "sublime_text", ("📝", "Sublime Text") },
                { "atom", ("⚛️", "Atom") },
                { "rider", ("🔧", "JetBrains Rider") },
                { "pycharm", ("🐍", "PyCharm") },
                { "webstorm", ("🌐", "WebStorm") },
                
                // Media
                { "vlc", ("🎬", "VLC Media Player") },
                { "wmplayer", ("🎵", "Windows Media Player") },
                { "foobar2000", ("🎵", "Foobar2000") },
                { "itunes", ("🎵", "iTunes") },
                
                // Office
                { "winword", ("📄", "Microsoft Word") },
                { "excel", ("📊", "Microsoft Excel") },
                { "powerpnt", ("📽️", "PowerPoint") },
                { "onenote", ("📒", "OneNote") },
                { "outlook", ("📧", "Outlook") },
                
                // Design
                { "photoshop", ("🎨", "Photoshop") },
                { "illustrator", ("🖌️", "Illustrator") },
                { "blender", ("🎨", "Blender") },
                { "gimp", ("🎨", "GIMP") },
                { "figma", ("🎨", "Figma") },
                
                // System
                { "explorer", ("📁", "Windows Explorer") },
                { "notepad", ("📝", "Notepad") },
                { "calc", ("🔢", "Calculator") },
                { "cmd", ("⚫", "Command Prompt") },
                { "powershell", ("🔵", "PowerShell") },
                { "taskmgr", ("⚙️", "Task Manager") },
                { "mmc", ("⚙️", "Management Console") },
                { "regedit", ("📝", "Registry Editor") },
                
                // Other
                { "dropbox", ("📦", "Dropbox") },
                { "onedrive", ("☁️", "OneDrive") },
                { "googledrive", ("☁️", "Google Drive") },
                { "7zfm", ("📦", "7-Zip") },
                { "winrar", ("📦", "WinRAR") }
            };

        public ProcessesPage(string language = "PT")
        {
            InitializeComponent();
            _currentLanguage = language;
            SetLanguage(language);
            SetupSearchPlaceholder();
            UpdateSortButtons();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_initialAnimationsRan)
            {
                _initialAnimationsRan = true;
                RunInitialAnimations();
            }
            
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LoadProcesses();
                SetupAutoRefresh();
            }), DispatcherPriority.Loaded);
        }

        #region Animations
        private void RunInitialAnimations()
        {
            try
            {
                AnimateElement(TitleSection, 0, 1, -30, 0, 0.5, false);
                Task.Delay(200).ContinueWith(_ => Dispatcher.Invoke(() =>
                    AnimateElement(MainListCard, 0, 1, 0, 0, 0.6, false, scaleFrom: 0.95, scaleTo: 1.0)));
                Task.Delay(350).ContinueWith(_ => Dispatcher.Invoke(() =>
                    AnimateElement(SearchBarContainer, 0, 1, -25, 0, 0.5, false)));
                Task.Delay(450).ContinueWith(_ => Dispatcher.Invoke(() =>
                    AnimateElement(HeaderRow, 0, 1, -15, 0, 0.5, false)));
            }
            catch { }
        }

        private void AnimateElement(FrameworkElement? element,
            double fromOpacity, double toOpacity,
            double fromTranslate, double toTranslate,
            double durationSeconds, bool isXAxis,
            double? scaleFrom = null, double? scaleTo = null)
        {
            if (element == null) return;
            var storyboard = new Storyboard();

            var opacity = new DoubleAnimation
            {
                From = fromOpacity,
                To = toOpacity,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(opacity, element);
            Storyboard.SetTargetProperty(opacity, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacity);

            if (element.RenderTransform is TransformGroup tg && tg.Children.Count >= 4)
            {
                var translate = new DoubleAnimation
                {
                    From = fromTranslate,
                    To = toTranslate,
                    Duration = TimeSpan.FromSeconds(durationSeconds),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(translate, element);
                Storyboard.SetTargetProperty(translate, new PropertyPath(
                    isXAxis
                        ? "(UIElement.RenderTransform).(TransformGroup.Children)[3].(TranslateTransform.X)"
                        : "(UIElement.RenderTransform).(TransformGroup.Children)[3].(TranslateTransform.Y)"));
                storyboard.Children.Add(translate);

                if (scaleFrom.HasValue && scaleTo.HasValue)
                {
                    var scaleX = new DoubleAnimation
                    {
                        From = scaleFrom.Value,
                        To = scaleTo.Value,
                        Duration = TimeSpan.FromSeconds(durationSeconds),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    var scaleY = new DoubleAnimation
                    {
                        From = scaleFrom.Value,
                        To = scaleTo.Value,
                        Duration = TimeSpan.FromSeconds(durationSeconds),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    Storyboard.SetTarget(scaleX, element);
                    Storyboard.SetTarget(scaleY, element);
                    Storyboard.SetTargetProperty(scaleX,
                        new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
                    Storyboard.SetTargetProperty(scaleY,
                        new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
                    storyboard.Children.Add(scaleX);
                    storyboard.Children.Add(scaleY);
                }
            }

            storyboard.Begin();
        }

        private static Border? FindBorder(DependencyObject root)
        {
            if (root is Border b) return b;
            int children = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < children; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var result = FindBorder(child);
                if (result != null) return result;
            }
            return null;
        }

        private void AnimateNewProcess(ProcessInfo processInfo)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var container = ProcessList.ItemContainerGenerator.ContainerFromItem(processInfo) as FrameworkElement;
                if (container != null)
                {
                    var border = FindBorder(container);
                    if (border != null)
                    {
                        border.Opacity = 0;
                        
                        if (border.RenderTransform is TransformGroup tg && tg.Children.Count >= 4)
                        {
                            if (tg.IsFrozen)
                            {
                                var newTg = tg.Clone();
                                border.RenderTransform = newTg;
                                tg = newTg;
                            }
                            
                            if (tg.Children[3] is TranslateTransform translate)
                            {
                                if (translate.IsFrozen)
                                {
                                    translate = translate.Clone();
                                    tg.Children[3] = translate;
                                }
                                translate.X = -50;
                            }
                        }

                        var storyboard = new Storyboard();
                        
                        var fadeIn = new DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = TimeSpan.FromSeconds(0.4),
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };
                        Storyboard.SetTarget(fadeIn, border);
                        Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
                        storyboard.Children.Add(fadeIn);

                        var slideIn = new DoubleAnimation
                        {
                            From = -50,
                            To = 0,
                            Duration = TimeSpan.FromSeconds(0.4),
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };
                        Storyboard.SetTarget(slideIn, border);
                        Storyboard.SetTargetProperty(slideIn, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[3].(TranslateTransform.X)"));
                        storyboard.Children.Add(slideIn);

                        storyboard.Begin();
                    }
                }
            }), DispatcherPriority.Loaded);
        }

        private async Task AnimateRemoveProcess(ProcessInfo processInfo)
        {
            try
            {
                var container = ProcessList.ItemContainerGenerator.ContainerFromItem(processInfo) as FrameworkElement;
                if (container != null)
                {
                    var border = FindBorder(container);
                    if (border != null)
                    {
                        var storyboard = new Storyboard();
                        
                        var fadeOut = new DoubleAnimation
                        {
                            From = 1,
                            To = 0,
                            Duration = TimeSpan.FromSeconds(0.3),
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                        };
                        Storyboard.SetTarget(fadeOut, border);
                        Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));
                        storyboard.Children.Add(fadeOut);

                        var slideOut = new DoubleAnimation
                        {
                            From = 0,
                            To = 100,
                            Duration = TimeSpan.FromSeconds(0.3),
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                        };
                        Storyboard.SetTarget(slideOut, border);
                        Storyboard.SetTargetProperty(slideOut, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[3].(TranslateTransform.X)"));
                        storyboard.Children.Add(slideOut);

                        var tcs = new TaskCompletionSource<bool>();
                        storyboard.Completed += (s, e) => tcs.SetResult(true);
                        storyboard.Begin();
                        await tcs.Task;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AnimateRemoveProcess error: {ex.Message}");
            }
        }
        #endregion

        #region Custom Message Box
        private async Task<bool> ShowCustomMessageBox(string title, string message, string icon, bool showYesNo = false)
        {
            MessageOverlay.Visibility = Visibility.Visible;
            MessageIcon.Text = icon;
            MessageTitle.Text = title;
            MessageText.Text = message;

            if (showYesNo)
            {
                BtnYes.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible;
                BtnOk.Visibility = Visibility.Collapsed;
            }
            else
            {
                BtnYes.Visibility = Visibility.Collapsed;
                BtnNo.Visibility = Visibility.Collapsed;
                BtnOk.Visibility = Visibility.Visible;
            }

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            MessageOverlay.BeginAnimation(OpacityProperty, fadeIn);

            var tcs = new TaskCompletionSource<bool>();

            void YesHandler(object s, RoutedEventArgs ev)
            {
                BtnYes.Click -= YesHandler;
                BtnNo.Click -= NoHandler;
                BtnOk.Click -= OkHandler;
                HideMessageBox();
                tcs.SetResult(true);
            }

            void NoHandler(object s, RoutedEventArgs ev)
            {
                BtnYes.Click -= YesHandler;
                BtnNo.Click -= NoHandler;
                BtnOk.Click -= OkHandler;
                HideMessageBox();
                tcs.SetResult(false);
            }

            void OkHandler(object s, RoutedEventArgs ev)
            {
                BtnYes.Click -= YesHandler;
                BtnNo.Click -= NoHandler;
                BtnOk.Click -= OkHandler;
                HideMessageBox();
                tcs.SetResult(true);
            }

            BtnYes.Click += YesHandler;
            BtnNo.Click += NoHandler;
            BtnOk.Click += OkHandler;

            return await tcs.Task;
        }

        private void HideMessageBox()
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.25),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                MessageOverlay.Visibility = Visibility.Collapsed;
            };

            MessageOverlay.BeginAnimation(OpacityProperty, fadeOut);
        }
        #endregion

        private void SetupSearchPlaceholder()
        {
            TxtSearch.Text = _currentLanguage == "PT" ? "🔍 Pesquisar aplicações..." : "🔍 Search applications...";
            TxtSearch.Foreground = (SolidColorBrush)FindResource("TextSecondary");

            TxtSearch.GotFocus += (s, e) =>
            {
                if (TxtSearch.Text.Contains("🔍"))
                {
                    TxtSearch.Text = "";
                    TxtSearch.Foreground = (SolidColorBrush)FindResource("TextPrimary");
                }
            };

            TxtSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(TxtSearch.Text))
                {
                    TxtSearch.Text = _currentLanguage == "PT" ? "🔍 Pesquisar aplicações..." : "🔍 Search applications...";
                    TxtSearch.Foreground = (SolidColorBrush)FindResource("TextSecondary");
                }
            };
        }

        private void SetLanguage(string language)
        {
            if (language == "PT")
            {
                TxtTitle.Text = "Gestor de Processos";
                TxtSubtitle.Text = "Monitorize e controle as aplicações em execução";
                TxtHeaderName.Text = "APLICAÇÃO";
                TxtHeaderCPU.Text = "CPU";
                TxtHeaderMemory.Text = "MEMÓRIA";
                TxtHeaderAction.Text = "AÇÃO";
                BtnSortName.Content = "Nome";
                BtnSortCPU.Content = "CPU";
                BtnSortMemory.Content = "Memória";
            }
            else
            {
                TxtTitle.Text = "Process Manager";
                TxtSubtitle.Text = "Monitor and control running applications";
                TxtHeaderName.Text = "APPLICATION";
                TxtHeaderCPU.Text = "CPU";
                TxtHeaderMemory.Text = "MEMORY";
                TxtHeaderAction.Text = "ACTION";
                BtnSortName.Content = "Name";
                BtnSortCPU.Content = "CPU";
                BtnSortMemory.Content = "Memory";
            }
        }

        private void SetupAutoRefresh()
        {
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _refreshTimer.Tick += (s, e) => LoadProcesses();
            _refreshTimer.Start();
        }

        private void LoadProcesses()
        {
            try
            {
                var allRunningProcesses = Process.GetProcesses()
                    .Where(p => !string.IsNullOrEmpty(p.ProcessName))
                    .ToList();

                var knownProcesses = allRunningProcesses
                    .Where(p => _appInfo.ContainsKey(p.ProcessName.ToLower()))
                    .GroupBy(p => p.ProcessName.ToLower())
                    .Select(g => g.OrderByDescending(p => p.WorkingSet64).First())
                    .ToList();

                var currentProcessIds = new HashSet<int>(_allProcesses.Select(p => p.ProcessId));
                var newProcessIds = new HashSet<int>();

                foreach (var process in knownProcesses)
                {
                    try
                    {
                        if (process.HasExited) continue;

                        newProcessIds.Add(process.Id);

                        double cpuUsage = GetProcessCpuUsage(process);
                        long memoryMB = process.WorkingSet64 / (1024 * 1024);

                        var appData = _appInfo[process.ProcessName.ToLower()];

                        if (!currentProcessIds.Contains(process.Id))
                        {
                            var processInfo = new ProcessInfo
                            {
                                ProcessId = process.Id,
                                ProcessName = appData.description,
                                ProcessDescription = $"PID: {process.Id}",
                                Icon = appData.icon,
                                CpuUsage = $"{cpuUsage:F1}%",
                                MemoryUsage = $"{memoryMB} MB",
                                CpuValue = cpuUsage,
                                MemoryValue = memoryMB
                            };
                            _allProcesses.Add(processInfo);
                        }
                        else
                        {
                            var existing = _allProcesses.FirstOrDefault(p => p.ProcessId == process.Id);
                            if (existing != null)
                            {
                                existing.CpuUsage = $"{cpuUsage:F1}%";
                                existing.MemoryUsage = $"{memoryMB} MB";
                                existing.CpuValue = cpuUsage;
                                existing.MemoryValue = memoryMB;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing {process.ProcessName}: {ex.Message}");
                    }
                }

                var closedProcesses = _allProcesses.Where(p => !newProcessIds.Contains(p.ProcessId)).ToList();
                foreach (var closedProcess in closedProcesses)
                {
                    var processInList = _processes.FirstOrDefault(p => p.ProcessId == closedProcess.ProcessId);
                    if (processInList != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Dispatcher.InvokeAsync(async () =>
                                {
                                    await AnimateRemoveProcess(processInList);
                                    _processes.Remove(processInList);
                                });
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error removing process UI: {ex.Message}");
                            }
                        });
                    }
                    
                    _allProcesses.Remove(closedProcess);
                    
                    var keysToRemove = _lastCpuTime.Keys.Where(k => k.StartsWith($"{closedProcess.ProcessId}_")).ToList();
                    foreach (var k in keysToRemove)
                    {
                        _lastCpuTime.Remove(k);
                        _lastTotalTime.Remove(k);
                    }
                }

                ApplyFiltersAndSort();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadProcesses error: {ex.Message}");
            }
        }

        private double GetProcessCpuUsage(Process process)
        {
            try
            {
                string key = $"{process.Id}_{process.ProcessName}";
                
                if (_lastCpuTime.TryGetValue(key, out DateTime lastTime) &&
                    _lastTotalTime.TryGetValue(key, out TimeSpan lastTotal))
                {
                    DateTime now = DateTime.Now;
                    TimeSpan current = process.TotalProcessorTime;

                    double cpuUsedMs = (current - lastTotal).TotalMilliseconds;
                    double totalMsPassed = (now - lastTime).TotalMilliseconds;

                    if (totalMsPassed > 0)
                    {
                        double cpuPercentage = (cpuUsedMs / (Environment.ProcessorCount * totalMsPassed)) * 100;
                        _lastCpuTime[key] = now;
                        _lastTotalTime[key] = current;
                        return Math.Clamp(cpuPercentage, 0, 100);
                    }
                }

                _lastCpuTime[key] = DateTime.Now;
                _lastTotalTime[key] = process.TotalProcessorTime;
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private void ApplyFiltersAndSort()
        {
            var filtered = _allProcesses.AsEnumerable();

            string searchText = TxtSearch.Text;
            if (!string.IsNullOrWhiteSpace(searchText) && !searchText.Contains("🔍"))
            {
                filtered = filtered.Where(p =>
                    p.ProcessName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    p.ProcessDescription.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            var sorted = _currentSortMode switch
            {
                1 => filtered.OrderByDescending(p => p.CpuValue),
                2 => filtered.OrderByDescending(p => p.MemoryValue),
                _ => filtered.OrderBy(p => p.ProcessName)
            };

            var currentIds = new HashSet<int>(_processes.Select(p => p.ProcessId));
            var newItems = sorted.Where(p => !currentIds.Contains(p.ProcessId)).ToList();

            foreach (var item in newItems)
            {
                _processes.Add(item);
                Task.Delay(100).ContinueWith(_ => Dispatcher.Invoke(() => AnimateNewProcess(item)));
            }

            ProcessList.ItemsSource = _processes;
        }

        private void UpdateSortButtons()
        {
            var defaultBg = (SolidColorBrush)FindResource("CardBg");
            var activeBg = (SolidColorBrush)FindResource("AccentBlue");

            BtnSortName.Background = _currentSortMode == 0 ? activeBg : defaultBg;
            BtnSortCPU.Background = _currentSortMode == 1 ? activeBg : defaultBg;
            BtnSortMemory.Background = _currentSortMode == 2 ? activeBg : defaultBg;
        }

        private void BtnSortName_Click(object sender, RoutedEventArgs e)
        {
            _currentSortMode = 0;
            UpdateSortButtons();
            ApplyFiltersAndSort();
        }

        private void BtnSortCPU_Click(object sender, RoutedEventArgs e)
        {
            _currentSortMode = 1;
            UpdateSortButtons();
            ApplyFiltersAndSort();
        }

        private void BtnSortMemory_Click(object sender, RoutedEventArgs e)
        {
            _currentSortMode = 2;
            UpdateSortButtons();
            ApplyFiltersAndSort();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadProcesses();

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFiltersAndSort();

        private async void BtnKillProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int pid) return;

            try
            {
                var pInfo = _allProcesses.FirstOrDefault(p => p.ProcessId == pid);
                if (pInfo == null) return;

                string name = pInfo.ProcessName;

                bool result = await ShowCustomMessageBox(
                    _currentLanguage == "PT" ? "Confirmar Ação" : "Confirm Action",
                    _currentLanguage == "PT"
                        ? $"Tem a certeza que deseja terminar '{name}'?\n\nIsto pode causar perda de dados não guardados."
                        : $"Are you sure you want to terminate '{name}'?\n\nThis may cause loss of unsaved data.",
                    "⚠️",
                    true);

                if (!result) return;

                try
                {
                    var process = Process.GetProcessById(pid);
                    process.Kill();
                    await Task.Delay(500);
                }
                catch (ArgumentException)
                {
                    Debug.WriteLine($"Process {pid} already terminated");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error killing process: {ex.Message}");
                    throw;
                }

                string key = $"{pid}_";
                var keysToRemove = _lastCpuTime.Keys.Where(k => k.StartsWith(key)).ToList();
                foreach (var k in keysToRemove)
                {
                    _lastCpuTime.Remove(k);
                    _lastTotalTime.Remove(k);
                }

                LoadProcesses();
            }
            catch (Exception ex)
            {
                await ShowCustomMessageBox(
                    _currentLanguage == "PT" ? "Erro" : "Error",
                    _currentLanguage == "PT"
                        ? $"Erro ao terminar o processo:\n{ex.Message}"
                        : $"Error terminating process:\n{ex.Message}",
                    "❌",
                    false);
            }
        }

        ~ProcessesPage()
        {
            _refreshTimer?.Stop();
            _lastCpuTime.Clear();
            _lastTotalTime.Clear();
        }
    }

    public class ProcessInfo : INotifyPropertyChanged
    {
        private string _cpuUsage = "";
        private string _memoryUsage = "";
        private double _cpuValue;
        private long _memoryValue;

        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public string ProcessDescription { get; set; } = "";
        public string Icon { get; set; } = "📦";
        
        public string CpuUsage
        {
            get => _cpuUsage;
            set
            {
                if (_cpuUsage != value)
                {
                    _cpuUsage = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public string MemoryUsage
        {
            get => _memoryUsage;
            set
            {
                if (_memoryUsage != value)
                {
                    _memoryUsage = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public double CpuValue
        {
            get => _cpuValue;
            set
            {
                if (Math.Abs(_cpuValue - value) > 0.01)
                {
                    _cpuValue = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public long MemoryValue
        {
            get => _memoryValue;
            set
            {
                if (_memoryValue != value)
                {
                    _memoryValue = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}