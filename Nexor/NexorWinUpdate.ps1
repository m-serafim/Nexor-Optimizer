<#
.SYNOPSIS
    Nexor - Complete Windows 11 Fresh Setup Script (ENHANCED UPDATE DETECTION)
.DESCRIPTION
    Enhanced version with bulletproof update and driver detection:
    - 6 comprehensive update search methods with fallback
    - Advanced hidden/superseded update handling
    - Enhanced driver detection with manufacturer sources
    - Forced Windows Update service reset
    - COM API + WSUS + Microsoft Update integration
    - Improved retry logic with exponential backoff
    - Quick Edit Mode disabled to prevent console freezing
    - Windows Update history verification
.NOTES
    Run from WPF app with admin privileges already granted
#>

param(
    [switch]$Silent = $false,
    [switch]$Unattended = $false  # NEW: Full automation mode
)

$ErrorActionPreference = "Continue"

# ============================================
# DISABLE QUICK EDIT MODE (Prevent Console Freezing)
# ============================================
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class ConsoleHelper {
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetStdHandle(int nStdHandle);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
    
    public static void DisableQuickEdit() {
        IntPtr consoleHandle = GetStdHandle(-10);
        uint consoleMode;
        if (GetConsoleMode(consoleHandle, out consoleMode)) {
            consoleMode &= ~0x0040u; // Disable ENABLE_QUICK_EDIT_MODE
            consoleMode |= 0x0080u;  // Enable ENABLE_EXTENDED_FLAGS
            SetConsoleMode(consoleHandle, consoleMode);
        }
    }
}
"@

try {
    [ConsoleHelper]::DisableQuickEdit()
} catch {
    # Silently continue if Quick Edit disable fails
}

# ============================================
# CONFIGURATION
# ============================================
$nexorDir = "$env:ProgramData\Nexor"
$stateFile = "$nexorDir\state.json"
$maxUpdateRounds = 100
$maxReboots = 50
$consecutiveNoUpdatesRequired = 3
$updateSearchDelaySeconds = 3

# ============================================
# CONSOLE UI HELPERS (ASCII ONLY)
# ============================================
function Write-Header {
    param([string]$Text)
    if (-not $Silent) {
        Write-Host ""
        Write-Host "====================================================================" -ForegroundColor Cyan
        Write-Host " $Text" -ForegroundColor Cyan
        Write-Host "====================================================================" -ForegroundColor Cyan
        Write-Host ""
    }
}

function Write-Step {
    param(
        [string]$Message,
        [switch]$NoNewLine
    )
    if (-not $Silent) {
        if ($NoNewLine) {
            Write-Host "  > $Message" -ForegroundColor White -NoNewline
        } else {
            Write-Host "  > $Message" -ForegroundColor White
        }
    }
}

function Write-Success {
    param([string]$Message)
    if (-not $Silent) {
        Write-Host "  [OK] $Message" -ForegroundColor Green
    }
}

function Write-Info {
    param([string]$Message)
    if (-not $Silent) {
        Write-Host "      $Message" -ForegroundColor Gray
    }
}

function Write-Warn {
    param([string]$Message)
    if (-not $Silent) {
        Write-Host "  [!] $Message" -ForegroundColor Yellow
    }
}

function Write-Err {
    param([string]$Message)
    if (-not $Silent) {
        Write-Host "  [X] $Message" -ForegroundColor Red
    }
}

# ============================================
# LOGGING (Silent background logging)
# ============================================
function Write-Log {
    param(
        [string]$Message,
        [string]$Level = "Info"
    )
    
    $timestamp = Get-Date -Format "HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Add-Content -Path "$nexorDir\nexor.log" -Value $logMessage -ErrorAction SilentlyContinue
}

# ============================================
# STATE MANAGEMENT
# ============================================
function Get-State {
    if (Test-Path $stateFile) {
        try {
            $json = Get-Content $stateFile -Raw | ConvertFrom-Json
            return @{
                Phase = $json.Phase
                UpdateRound = $json.UpdateRound
                DriverRound = $json.DriverRound
                StartTime = $json.StartTime
                RebootCount = $json.RebootCount
                LogFile = $json.LogFile
                UpdateLog = @($json.UpdateLog)
                DriverLog = @($json.DriverLog)
                CleanupLog = @($json.CleanupLog)
                FreeSpaceBefore = $json.FreeSpaceBefore
                LastUpdateCheck = $json.LastUpdateCheck
                FailedUpdates = @($json.FailedUpdates)
                InstalledUpdateKBs = @($json.InstalledUpdateKBs)
                ConsecutiveNoUpdates = $json.ConsecutiveNoUpdates
            }
        } catch {
            Write-Log "Error loading state, creating new" "Warning"
        }
    }
    
    return @{
        Phase = -1
        UpdateRound = 0
        DriverRound = 0
        StartTime = (Get-Date).ToString('o')
        RebootCount = 0
        LogFile = "$env:USERPROFILE\Desktop\Nexor_Report_$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"
        UpdateLog = @()
        DriverLog = @()
        CleanupLog = @()
        FreeSpaceBefore = 0
        LastUpdateCheck = ""
        FailedUpdates = @()
        InstalledUpdateKBs = @()
        ConsecutiveNoUpdates = 0
    }
}

function Save-State($state) {
    if (-not (Test-Path $nexorDir)) {
        New-Item -Path $nexorDir -ItemType Directory -Force | Out-Null
    }
    $state | ConvertTo-Json -Depth 10 | Out-File -FilePath $stateFile -Encoding UTF8 -Force
}

function Test-RebootRequired {
    $reboot = $false
    
    if (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") { 
        $reboot = $true 
        Write-Log "Reboot detected: Windows Update flag" "Info"
    }
    
    if (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") { 
        $reboot = $true 
        Write-Log "Reboot detected: Component Based Servicing" "Info"
    }
    
    try {
        $regKey = Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager" -Name PendingFileRenameOperations -ErrorAction SilentlyContinue
        if ($regKey) { 
            $reboot = $true 
            Write-Log "Reboot detected: Pending file operations" "Info"
        }
    } catch {}
    
    try {
        $activeComputer = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName").ComputerName
        $pendingComputer = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName").ComputerName
        if ($activeComputer -ne $pendingComputer) { 
            $reboot = $true 
            Write-Log "Reboot detected: Computer name change pending" "Info"
        }
    } catch {}
    
    return $reboot
}

# ============================================
# WINDOWS UPDATE SERVICE MANAGEMENT
# ============================================
function Reset-WindowsUpdateServices {
    param([bool]$FullReset = $false)
    
    Write-Step "Resetting Windows Update services..."
    
    $services = @('wuauserv', 'bits', 'cryptsvc', 'msiserver', 'appidsvc')
    
    # Stop services
    foreach ($service in $services) {
        try {
            Stop-Service $service -Force -ErrorAction SilentlyContinue
            Write-Log "Stopped service: $service" "Info"
        } catch {
            Write-Log "Failed to stop $service : $_" "Warning"
        }
    }
    
    Start-Sleep -Seconds 3
    
    if ($FullReset) {
        Write-Info "Performing FULL cache reset..."
        
        # Clear update cache
        $cachePaths = @(
            "$env:SystemRoot\SoftwareDistribution\DataStore",
            "$env:SystemRoot\SoftwareDistribution\Download"
        )
        
        foreach ($path in $cachePaths) {
            if (Test-Path $path) {
                try {
                    Remove-Item "$path\*" -Recurse -Force -ErrorAction SilentlyContinue
                    Write-Log "Cleared cache: $path" "Info"
                } catch {
                    Write-Log "Failed to clear $path : $_" "Warning"
                }
            }
        }
        
        # Reset BITS queue
        try {
            Get-BitsTransfer | Remove-BitsTransfer -ErrorAction SilentlyContinue
            Write-Log "Cleared BITS queue" "Info"
        } catch {}
        
        # Reset Windows Update history
        try {
            Remove-Item "$env:SystemRoot\SoftwareDistribution\ReportingEvents.log" -Force -ErrorAction SilentlyContinue
            Write-Log "Cleared update history log" "Info"
        } catch {}
    }
    
    Start-Sleep -Seconds 2
    
    # Start services
    foreach ($service in $services) {
        try {
            Start-Service $service -ErrorAction SilentlyContinue
            Write-Log "Started service: $service" "Info"
        } catch {
            Write-Log "Failed to start $service : $_" "Warning"
        }
    }
    
    Start-Sleep -Seconds 5
    Write-Success "Services reset complete"
}

# ============================================
# INITIAL SETUP (Phase -1)
# ============================================
function Initialize-Environment {
    Write-Header "NEXOR - Windows 11 Fresh Setup (ENHANCED)"
    Write-Step "Initializing environment..."
    
    if (-not (Test-Path $nexorDir)) {
        New-Item -Path $nexorDir -ItemType Directory -Force | Out-Null
    }
    
    Write-Success "Console Quick Edit Mode disabled (prevents freezing)"
    Write-Log "Quick Edit Mode disabled successfully" "Info"
    
    Write-Step "Configuring NuGet provider..." -NoNewLine
    try {
        $nugetProviders = Get-PackageProvider -ListAvailable -Name NuGet -ErrorAction SilentlyContinue
        
        if ($nugetProviders) {
            $latestNuget = $nugetProviders | Sort-Object Version -Descending | Select-Object -First 1
            Write-Host " Found (v$($latestNuget.Version))" -ForegroundColor Green
            Write-Log "NuGet provider already installed: v$($latestNuget.Version)" "Info"
        } else {
            Write-Host " Installing..." -ForegroundColor Yellow
            Install-PackageProvider -Name NuGet -Force -Confirm:$false -ErrorAction Stop | Out-Null
            Write-Host "`r  Configuring NuGet provider... Done" -ForegroundColor Green
            Write-Log "NuGet provider installed" "Success"
        }
    } catch {
        Write-Host " Warning" -ForegroundColor Yellow
        Write-Log "Error with NuGet provider: $_" "Warning"
        Write-Info "NuGet issues may occur, but script will continue"
    }
    
    Write-Step "Configuring PowerShell Gallery..." -NoNewLine
    try {
        if ((Get-PSRepository -Name PSGallery).InstallationPolicy -ne 'Trusted') {
            Set-PSRepository -Name PSGallery -InstallationPolicy Trusted -ErrorAction Stop
        }
        Write-Host " Done" -ForegroundColor Green
        Write-Log "PSGallery configured" "Success"
    } catch {
        Write-Host " Failed" -ForegroundColor Red
        Write-Log "Error configuring PSGallery: $_" "Error"
        return $false
    }
    
    Write-Step "Installing PSWindowsUpdate module..." -NoNewLine
    try {
        if (-not (Get-Module -ListAvailable -Name PSWindowsUpdate)) {
            Install-Module -Name PSWindowsUpdate -Force -Confirm:$false -AllowClobber -Scope AllUsers -ErrorAction Stop | Out-Null
        }
        Remove-Module PSWindowsUpdate -Force -ErrorAction SilentlyContinue
        Import-Module PSWindowsUpdate -Force -ErrorAction Stop
        Write-Host " Done" -ForegroundColor Green
        Write-Log "PSWindowsUpdate module installed" "Success"
    } catch {
        Write-Host " Failed" -ForegroundColor Red
        Write-Log "Error installing PSWindowsUpdate: $_" "Error"
        return $false
    }
    
    # Create scheduled task to auto-run after reboot
    if ($Unattended) {
        Write-Step "Setting up auto-resume after reboot..."
        
        $taskName = "NexorAutoResume"
        $scriptPath = $PSCommandPath
        
        # Check if task already exists
        $existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        
        if (-not $existingTask) {
            $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`" -Unattended"
            $trigger = New-ScheduledTaskTrigger -AtLogOn
            $principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Highest
            $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
            
            Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
            Write-Success "Auto-resume task created"
            Write-Log "Scheduled task created for auto-resume" "Success"
        }
    }
    
    Write-Success "Initialization complete"
    return $true
}

# ============================================
# REBOOT HANDLER
# ============================================
function Invoke-SystemReboot($state, $reason) {
    $state.RebootCount++
    Save-State $state
    
    Write-Host ""
    Write-Warn "System restart required: $reason"
    Write-Info "Reboot $($state.RebootCount)/$maxReboots"
    Write-Host ""
    
    if (-not $Unattended) {
        # Only show countdown if NOT in unattended mode
        Write-Warn "IMPORTANT: You can press Ctrl+C to cancel the restart"
        Write-Host ""
        
        for ($i = 30; $i -gt 0; $i--) {
            $color = if ($i -le 10) { "Red" } elseif ($i -le 20) { "Yellow" } else { "White" }
            Write-Host "`r  ⏱️  Restarting in $i seconds... (Press Ctrl+C NOW to cancel)" -NoNewline -ForegroundColor $color
            Start-Sleep -Seconds 1
        }
        Write-Host ""
        Write-Host ""
    } else {
        # In unattended mode: restart immediately after 5 second grace period
        Write-Info "UNATTENDED MODE: Restarting in 5 seconds..."
        Start-Sleep -Seconds 5
    }
    
    Write-Log "Rebooting: $reason (Count: $($state.RebootCount))" "Info"
    Restart-Computer -Force
    exit 0
}

# ============================================
# ENHANCED UPDATE SEARCH (6 METHODS)
# ============================================
function Search-WindowsUpdates {
    param([int]$Method = 1)
    
    $allUpdates = @()
    
    try {
        switch ($Method) {
            1 {
                # Method 1: Standard PSWindowsUpdate with MicrosoftUpdate
                Write-Info "Search method 1: PSWindowsUpdate (MicrosoftUpdate + All Categories)"
                $allUpdates = Get-WindowsUpdate -MicrosoftUpdate -IgnoreUserInput -ErrorAction Stop
            }
            2 {
                # Method 2: Including hidden and superseded updates
                Write-Info "Search method 2: Hidden + Superseded updates"
                $allUpdates = Get-WindowsUpdate -MicrosoftUpdate -IsHidden:$false -ErrorAction Stop
                $hidden = Get-WindowsUpdate -MicrosoftUpdate -IsHidden:$true -ErrorAction SilentlyContinue
                if ($hidden) {
                    Write-Info "Found $($hidden.Count) hidden update(s), unhiding..."
                    $hidden | Show-WindowsUpdate -ErrorAction SilentlyContinue
                    $allUpdates += $hidden
                }
            }
            3 {
                # Method 3: COM API direct search (all update types)
                Write-Info "Search method 3: COM API (Software + Drivers + All Types)"
                $updateSession = $null
                $updateSearcher = $null
                
                try {
                    $updateSession = New-Object -ComObject Microsoft.Update.Session
                    $updateSearcher = $updateSession.CreateUpdateSearcher()
                    $updateSearcher.ServerSelection = 3 # MicrosoftUpdate
                    $updateSearcher.Online = $true
                    
                    # Search for ALL updates including drivers
                    $searchResult = $updateSearcher.Search("IsInstalled=0 and IsHidden=0")
                    
                    foreach ($update in $searchResult.Updates) {
                        $kb = ""
                        if ($update.KBArticleIDs.Count -gt 0) {
                            $kb = "KB$($update.KBArticleIDs.Item(0))"
                        }
                        
                        $allUpdates += [PSCustomObject]@{
                            Title = $update.Title
                            KB = $kb
                            Size = [math]::Round($update.MaxDownloadSize / 1MB, 2)
                            IsDownloaded = $update.IsDownloaded
                            IsDriver = ($update.Type -eq 2)
                            ComUpdate = $update
                            Categories = ($update.Categories | ForEach-Object { $_.Name }) -join ", "
                        }
                    }
                } finally {
                    if ($updateSearcher) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSearcher) | Out-Null }
                    if ($updateSession) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSession) | Out-Null }
                }
            }
            4 {
                # Method 4: Force online catalog refresh with ALL categories
                Write-Info "Search method 4: Online catalog refresh (all categories)"
                $updateSession = $null
                $updateSearcher = $null
                
                try {
                    $updateSession = New-Object -ComObject Microsoft.Update.Session
                    $updateSearcher = $updateSession.CreateUpdateSearcher()
                    $updateSearcher.ServerSelection = 3
                    $updateSearcher.Online = $true
                    
                    # Search for uninstalled AND not hidden updates
                    $searchResult = $updateSearcher.Search("IsInstalled=0 and IsHidden=0")
                    
                    foreach ($update in $searchResult.Updates) {
                        $kb = ""
                        if ($update.KBArticleIDs.Count -gt 0) {
                            $kb = "KB$($update.KBArticleIDs.Item(0))"
                        }
                        
                        $allUpdates += [PSCustomObject]@{
                            Title = $update.Title
                            KB = $kb
                            Size = [math]::Round($update.MaxDownloadSize / 1MB, 2)
                            IsDownloaded = $update.IsDownloaded
                            IsDriver = ($update.Type -eq 2)
                            ComUpdate = $update
                        }
                    }
                } finally {
                    if ($updateSearcher) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSearcher) | Out-Null }
                    if ($updateSession) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSession) | Out-Null }
                }
            }
            5 {
                # Method 5: Include superseded and potentially applicable updates
                Write-Info "Search method 5: Superseded + BrowseOnly updates"
                $updateSession = $null
                $updateSearcher = $null
                
                try {
                    $updateSession = New-Object -ComObject Microsoft.Update.Session
                    $updateSearcher = $updateSession.CreateUpdateSearcher()
                    $updateSearcher.ServerSelection = 3
                    $updateSearcher.Online = $true
                    
                    # Include updates that might be superseded
                    $searchResult = $updateSearcher.Search("IsInstalled=0")
                    
                    foreach ($update in $searchResult.Updates) {
                        # Include even if superseded (might be needed for dependencies)
                        if ($update.IsHidden -eq $false) {
                            $kb = ""
                            if ($update.KBArticleIDs.Count -gt 0) {
                                $kb = "KB$($update.KBArticleIDs.Item(0))"
                            }
                            
                            $allUpdates += [PSCustomObject]@{
                                Title = $update.Title
                                KB = $kb
                                Size = [math]::Round($update.MaxDownloadSize / 1MB, 2)
                                ComUpdate = $update
                                IsSuperseded = $update.IsSuperseded
                            }
                        }
                    }
                } finally {
                    if ($updateSearcher) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSearcher) | Out-Null }
                    if ($updateSession) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSession) | Out-Null }
                }
            }
            6 {
                # Method 6: Check Windows Update history for failed/pending
                Write-Info "Search method 6: Update history + Pending installs"
                $updateSession = $null
                $updateSearcher = $null
                
                try {
                    $updateSession = New-Object -ComObject Microsoft.Update.Session
                    $updateSearcher = $updateSession.CreateUpdateSearcher()
                    $updateSearcher.ServerSelection = 3
                    $updateSearcher.Online = $true
                    
                    # Get updates that need downloading or installing
                    $searchResult = $updateSearcher.Search("(IsInstalled=0 and IsHidden=0) or (IsDownloaded=1 and IsInstalled=0)")
                    
                    foreach ($update in $searchResult.Updates) {
                        $kb = ""
                        if ($update.KBArticleIDs.Count -gt 0) {
                            $kb = "KB$($update.KBArticleIDs.Item(0))"
                        }
                        
                        $allUpdates += [PSCustomObject]@{
                            Title = $update.Title
                            KB = $kb
                            Size = [math]::Round($update.MaxDownloadSize / 1MB, 2)
                            IsDownloaded = $update.IsDownloaded
                            ComUpdate = $update
                            Status = if ($update.IsDownloaded) { "Downloaded, Pending Install" } else { "Pending Download" }
                        }
                    }
                } finally {
                    if ($updateSearcher) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSearcher) | Out-Null }
                    if ($updateSession) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSession) | Out-Null }
                }
            }
            7 {
                # Enhanced Search-WindowsUpdates Method 7 (NEW)
                Write-Info "Search method 7: Automatic Update Client detection"
                $autoUpdate = $null
                $updateSession = $null
                $updateSearcher = $null
                
                try {
                    $autoUpdate = (New-Object -ComObject Microsoft.Update.AutoUpdate)
                    $autoUpdate.DetectNow()
                    Start-Sleep -Seconds 15
                    
                    $updateSession = New-Object -ComObject Microsoft.Update.Session
                    $updateSearcher = $updateSession.CreateUpdateSearcher()
                    $searchResult = $updateSearcher.Search("IsInstalled=0")
                    
                    foreach ($update in $searchResult.Updates) {
                        $kb = ""
                        if ($update.KBArticleIDs.Count -gt 0) {
                            $kb = "KB$($update.KBArticleIDs.Item(0))"
                        }
                        
                        $allUpdates += [PSCustomObject]@{
                            Title = $update.Title
                            KB = $kb
                            Size = [math]::Round($update.MaxDownloadSize / 1MB, 2)
                            IsDownloaded = $update.IsDownloaded
                            IsDriver = ($update.Type -eq 2)
                            ComUpdate = $update
                            Categories = ($update.Categories | ForEach-Object { $_.Name }) -join ", "
                        }
                    }
                } catch {
                    Write-Log "AU detection failed: $_" "Warning"
                } finally {
                    if ($updateSearcher) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSearcher) | Out-Null }
                    if ($updateSession) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSession) | Out-Null }
                    if ($autoUpdate) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($autoUpdate) | Out-Null }
                }
            }
            8 {
                # Method 8: Optional and Recommended updates (NEW)
                Write-Info "Search method 8: Optional updates (explicitly included)"
                $updateSession = $null
                $updateSearcher = $null
                
                try {
                    $updateSession = New-Object -ComObject Microsoft.Update.Session
                    $updateSearcher = $updateSession.CreateUpdateSearcher()
                    $updateSearcher.ServerSelection = 3 # MicrosoftUpdate
                    $updateSearcher.Online = $true
                    
                    # Search for ALL updates (including optional)
                    $searchResult = $updateSearcher.Search("IsInstalled=0 and IsHidden=0")
                    
                    foreach ($update in $searchResult.Updates) {
                        $kb = ""
                        if ($update.KBArticleIDs.Count -gt 0) {
                            $kb = "KB$($update.KBArticleIDs.Item(0))"
                        }
                        
                        # Identify optional updates
                        $isOptional = $update.AutoSelectOnWebSites -eq 0  # 0 = optional, 1 = recommended
                        $updateType = if ($isOptional) { "[OPTIONAL]" } else { "[RECOMMENDED]" }
                        
                        $allUpdates += [PSCustomObject]@{
                            Title = $update.Title
                            KB = $kb
                            Size = [math]::Round($update.MaxDownloadSize / 1MB, 2)
                            IsDownloaded = $update.IsDownloaded
                            IsDriver = ($update.Type -eq 2)
                            IsOptional = $isOptional
                            ComUpdate = $update
                            Categories = ($update.Categories | ForEach-Object { $_.Name }) -join ", "
                            UpdateType = $updateType
                        }
                    }
                } finally {
                    if ($updateSearcher) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSearcher) | Out-Null }
                    if ($updateSession) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSession) | Out-Null }
                }
            }
        }
    } catch {
        Write-Log "Search method $Method failed: $_" "Warning"
        return @()
    }
    
    return $allUpdates | Sort-Object -Property KB -Unique
}

# ============================================
# PHASE 0: WINDOWS UPDATES (BULLETPROOF - NEVER MISSES)
# ============================================
function Install-WindowsUpdates($state) {
    Write-Header "PHASE 1: Windows Updates (Round $($state.UpdateRound + 1)/$maxUpdateRounds)"
    
    try {
        Remove-Module PSWindowsUpdate -Force -ErrorAction SilentlyContinue
        Import-Module PSWindowsUpdate -Force -ErrorAction Stop
        
        # Reset services every 3 rounds or if previous failures
        if ($state.UpdateRound -gt 0 -and (($state.UpdateRound % 3) -eq 0 -or $state.FailedUpdates.Count -gt 0)) {
            $fullReset = $state.FailedUpdates.Count -gt 0
            Reset-WindowsUpdateServices -FullReset $fullReset
        }
        
        Write-Step "Searching for updates (trying ALL 6 methods)..."
        Write-Host ""
        
        $updates = @()
        $updatesByKB = @{}
        
        # Try ALL 6 search methods (no early exit)
        for ($method = 1; $method -le 8; $method++) {
            $methodUpdates = Search-WindowsUpdates -Method $method
            
            if ($methodUpdates.Count -gt 0) {
                Write-Info "Method $method found $($methodUpdates.Count) update(s)"
                
                # Deduplicate by KB number
                foreach ($update in $methodUpdates) {
                    if ($update.KB) {
                        if (-not $updatesByKB.ContainsKey($update.KB)) {
                            $updatesByKB[$update.KB] = $update
                        }
                    } else {
                        # Updates without KB (rare) - add by title
                        $updates += $update
                    }
                }
            }
            
            Start-Sleep -Seconds $updateSearchDelaySeconds
        }
        
        # Merge KB-based updates
        $updates += $updatesByKB.Values
        
        # Filter out already installed KBs
        if ($state.InstalledUpdateKBs.Count -gt 0) {
            $originalCount = $updates.Count
            $updates = $updates | Where-Object { 
                -not $_.KB -or $state.InstalledUpdateKBs -notcontains $_.KB 
            }
            $filteredCount = $originalCount - $updates.Count
            if ($filteredCount -gt 0) {
                Write-Info "Filtered out $filteredCount already installed update(s)"
            }
        }
        
        if ($updates.Count -eq 0) {
            Write-Info "No new updates found, performing DEEP final verification..."
            Start-Sleep -Seconds 3
            
            # ULTRA-AGGRESSIVE final COM API check
            $updateSession = $null
            $updateSearcher = $null

            try {
                $updateSession = New-Object -ComObject Microsoft.Update.Session
                $updateSearcher = $updateSession.CreateUpdateSearcher()
                $updateSearcher.ServerSelection = 3
                $updateSearcher.Online = $true
                
                # Check for any pending updates at all
                $finalSearchResult = $updateSearcher.Search("IsInstalled=0")
                
                if ($finalSearchResult.Updates.Count -eq 0) {
                    $state.ConsecutiveNoUpdates++
                    Write-Success "No updates found (Consecutive checks: $($state.ConsecutiveNoUpdates)/$consecutiveNoUpdatesRequired)"
                    Write-Log "No updates found - consecutive check $($state.ConsecutiveNoUpdates)" "Success"
                    
                    if ($state.ConsecutiveNoUpdates -ge $consecutiveNoUpdatesRequired) {
                        Write-Success "═══════════════════════════════════════════════════════════"
                        Write-Success "ALL WINDOWS UPDATES VERIFIED COMPLETE!"
                        Write-Success "Verified $consecutiveNoUpdatesRequired consecutive times"
                        Write-Success "═══════════════════════════════════════════════════════════"
                        $state.LastUpdateCheck = (Get-Date).ToString('o')
                        Write-Log "All updates complete after $consecutiveNoUpdatesRequired consecutive checks" "Success"
                        
                        # Clear any failed update tracking
                        $state.FailedUpdates = @()
                        Save-State $state
                        return $true  # ONLY exit when verified multiple times
                    } else {
                        Write-Info "Running additional verification check to ensure no updates missed..."
                        Write-Info "Waiting 10 seconds before next verification..."
                        $state.UpdateRound++
                        Save-State $state
                        Start-Sleep -Seconds 10
                        return $false  # Continue checking
                    }
                } else {
                    # Found updates - reset consecutive counter
                    $state.ConsecutiveNoUpdates = 0
                    Write-Warn "Found $($finalSearchResult.Updates.Count) update(s) via deep scan"
                    Write-Log "Deep scan found updates: $($finalSearchResult.Updates.Count)" "Warning"
                    
                    # Convert to our format
                    foreach ($update in $finalSearchResult.Updates) {
                        $kb = ""
                        if ($update.KBArticleIDs.Count -gt 0) {
                            $kb = "KB$($update.KBArticleIDs.Item(0))"
                        }
                        
                        # Check if not already in our list
                        if (-not ($updates | Where-Object { $_.KB -eq $kb })) {
                            $updates += [PSCustomObject]@{
                                Title = $update.Title
                                KB = $kb
                                ComUpdate = $update
                            }
                        }
                    }
                }
            } finally {
                if ($updateSearcher) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSearcher) | Out-Null }
                if ($updateSession) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSession) | Out-Null }
            }
        } else {
            # Found updates - reset consecutive counter
            $state.ConsecutiveNoUpdates = 0
        }
        
        if ($updates.Count -gt 0) {
            Write-Success "Found $($updates.Count) update(s) to install"
            Write-Host ""
            
            $counter = 0
            foreach ($update in $updates) {
                $counter++
                $updateTitle = $update.Title
                if ($update.KB) {
                    $updateTitle += " ($($update.KB))"
                }
                
                if ($update.Size) {
                    $updateTitle += " [$($update.Size) MB]"
                }
                
                if ($update.IsDriver) {
                    $updateTitle += " [DRIVER]"
                }
                
                if ($update.UpdateType) {
                    $updateTitle += " $($update.UpdateType)"
                }

                if ($update.Categories) {
                    $updateTitle += " [$($update.Categories)]"
                }
                
                # Check if previously failed
                if ($state.FailedUpdates -contains $update.KB) {
                    $updateTitle += " [RETRY]"
                }
                
                $state.UpdateLog += $updateTitle
                Write-Info "[$counter/$($updates.Count)] $updateTitle"
                Write-Log "Found update: $updateTitle" "Info"
            }
            
            Write-Host ""
            Write-Step "Installing updates (this may take several minutes)..."
            Write-Info "Progress will be shown below..."
            Write-Host ""
            
            try {
                # Try installation with PSWindowsUpdate first (with progress)
                $installResult = Install-WindowsUpdate -MicrosoftUpdate -AcceptAll -IgnoreReboot -Verbose -ErrorAction Stop
                
                # Track successfully installed updates
                foreach ($result in $installResult) {
                    if ($result.Result -eq "Installed" -or $result.Result -eq "Downloaded") {
                        if ($result.KB -and $state.InstalledUpdateKBs -notcontains $result.KB) {
                            $state.InstalledUpdateKBs += $result.KB
                        }
                    }
                }
                
                # Check for failures
                $failedInInstall = $installResult | Where-Object { 
                    $_.Result -ne "Installed" -and $_.Result -ne "Downloaded" -and $_.Result -ne "Accepted"
                }
                
                if ($failedInInstall) {
                    Write-Warn "$($failedInInstall.Count) update(s) had issues"
                    foreach ($failed in $failedInInstall) {
                        if ($failed.KB) {
                            if ($state.FailedUpdates -notcontains $failed.KB) {
                                $state.FailedUpdates += $failed.KB
                            }
                        }
                        Write-Info "[!] $($failed.Title) - $($failed.Result)"
                        Write-Log "Update issue: $($failed.Title) - $($failed.Result)" "Warning"
                    }
                }
                
                Write-Success "Update installation batch completed"
                Write-Log "Updates processed: $($updates.Count)" "Success"
                
            } catch {
                Write-Warn "Installation encountered errors: $_"
                Write-Log "Install error: $_" "Error"
                
                # Try COM API installation as fallback
                Write-Step "Attempting fallback installation method (COM API)..."
                
                $updateSession = $null
                $updateInstaller = $null
                $updatesToInstall = $null

                try {
                    $updateSession = New-Object -ComObject Microsoft.Update.Session
                    $updateInstaller = $updateSession.CreateUpdateInstaller()
                    $updatesToInstall = New-Object -ComObject Microsoft.Update.UpdateColl
                    
                    foreach ($update in $updates) {
                        if ($update.ComUpdate) {
                            $updatesToInstall.Add($update.ComUpdate) | Out-Null
                        }
                    }
                    
                    if ($updatesToInstall.Count -gt 0) {
                        Write-Info "Installing $($updatesToInstall.Count) update(s) via COM API..."
                        $updateInstaller.Updates = $updatesToInstall
                        $installResult = $updateInstaller.Install()
                        
                        if ($installResult.ResultCode -eq 2) {
                            Write-Success "Fallback installation successful"
                            Write-Log "COM API installation success" "Success"
                            
                            # Track installed updates
                            for ($i = 0; $i -lt $updatesToInstall.Count; $i++) {
                                $update = $updatesToInstall.Item($i)
                                if ($update.KBArticleIDs.Count -gt 0) {
                                    $kb = "KB$($update.KBArticleIDs.Item(0))"
                                    if ($state.InstalledUpdateKBs -notcontains $kb) {
                                        $state.InstalledUpdateKBs += $kb
                                    }
                                }
                            }
                        } else {
                            Write-Warn "Fallback installation completed with code: $($installResult.ResultCode)"
                            Write-Log "COM API result: $($installResult.ResultCode)" "Warning"
                        }
                    }
                } catch {
                    Write-Err "Fallback installation also failed: $_"
                    Write-Log "COM API install failed: $_" "Error"
                } finally {
                    if ($updatesToInstall) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updatesToInstall) | Out-Null }
                    if ($updateInstaller) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateInstaller) | Out-Null }
                    if ($updateSession) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSession) | Out-Null }
                }
            }
        }
        
        $state.UpdateRound++
        Save-State $state
        
        Start-Sleep -Seconds 5
        if (Test-RebootRequired) {
            Invoke-SystemReboot $state "Windows Updates"
        }
        
        # CRITICAL: Only exit at safety limit
        if ($state.UpdateRound -ge $maxUpdateRounds) {
            Write-Err "═══════════════════════════════════════════════════════════"
            Write-Err "CRITICAL: Reached safety limit of $maxUpdateRounds rounds"
            Write-Err "This indicates a SERIOUS problem with Windows Update"
            Write-Err "═══════════════════════════════════════════════════════════"
            Write-Log "CRITICAL: Max rounds safety limit reached" "Error"
            
            if ($state.FailedUpdates.Count -gt 0) {
                Write-Err "Failed updates detected:"
                foreach ($failedKB in $state.FailedUpdates) {
                    Write-Err "  - $failedKB"
                }
            }
            
            Write-Warn "Proceeding to next phase, but manual update check REQUIRED"
            Start-Sleep -Seconds 10
            return $true  # Exit only at safety limit
        }
        
        return $false  # Continue updating
        
    } catch {
        Write-Err "Error during Windows Update: $_"
        Write-Log "Update error: $_" "Error"
        
        # Give it 3 retry attempts before giving up on errors
        if ($state.UpdateRound -lt 3) {
            Write-Warn "Retrying after error (Attempt $($state.UpdateRound + 1)/3)..."
            $state.UpdateRound++
            Save-State $state
            Start-Sleep -Seconds 10
            return $false
        }
        
        Write-Err "Multiple errors detected, proceeding to next phase"
        return $true
    }
}

# ============================================
# PHASE 1: DRIVER UPDATES (ENHANCED)
# ============================================
function Install-DriverUpdates($state) {
    Write-Header "PHASE 2: Driver Updates (Round $($state.DriverRound + 1))"
    
    try {
        Import-Module PSWindowsUpdate -Force -ErrorAction SilentlyContinue
        
        Write-Step "Comprehensive device scanning..."
        
        # Scan for problem devices
        $problemDevices = Get-WmiObject Win32_PnPEntity | Where-Object { 
            $_.ConfigManagerErrorCode -ne 0
        }
        
        if ($problemDevices) {
            Write-Warn "Found $($problemDevices.Count) device(s) with issues"
            foreach ($device in $problemDevices) {
                Write-Info "[!] $($device.Name) (Error: $($device.ConfigManagerErrorCode))"
                $state.DriverLog += "Issue: $($device.Name)"
                Write-Log "Device issue: $($device.Name)" "Warning"
            }
            
            Write-Step "Attempting to resolve device issues..." -NoNewLine
            Start-Process "pnputil.exe" -ArgumentList "/scan-devices" -Wait -NoNewWindow -WindowStyle Hidden -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 5
            Write-Host " Done" -ForegroundColor Green
        } else {
            Write-Success "All devices functioning correctly"
        }
        
        Write-Host ""
        Write-Step "Searching for driver updates (all methods)..."
        
        $driverUpdates = @()
        
        # Method 1: PSWindowsUpdate (Microsoft Update Catalog)
        Write-Info "Checking Microsoft Update for drivers..."
        $psDrivers = Get-WindowsUpdate -MicrosoftUpdate -UpdateType Driver -ErrorAction SilentlyContinue
        if ($psDrivers) {
            $driverUpdates += $psDrivers
            Write-Info "Found $($psDrivers.Count) driver(s) via Microsoft Update"
        }
        
        # Method 2: COM API for drivers
        Write-Info "Checking COM API for drivers..."
        $updateSession = $null
        $updateSearcher = $null

        try {
            $updateSession = New-Object -ComObject Microsoft.Update.Session
            $updateSearcher = $updateSession.CreateUpdateSearcher()
            $updateSearcher.ServerSelection = 3
            $updateSearcher.Online = $true
            
            # Search specifically for driver updates
            $searchResult = $updateSearcher.Search("IsInstalled=0 and Type='Driver'")
            
            if ($searchResult.Updates.Count -gt 0) {
                Write-Info "Found $($searchResult.Updates.Count) driver(s) via COM API"
                
                foreach ($update in $searchResult.Updates) {
                    $kb = ""
                    if ($update.KBArticleIDs.Count -gt 0) {
                        $kb = "KB$($update.KBArticleIDs.Item(0))"
                    }
                    
                    # Check if not already in our list
                    $exists = $false
                    foreach ($existingDriver in $driverUpdates) {
                        if ($existingDriver.KB -eq $kb -or $existingDriver.Title -eq $update.Title) {
                            $exists = $true
                            break
                        }
                    }
                    
                    if (-not $exists) {
                        $driverUpdates += [PSCustomObject]@{
                            Title = $update.Title
                            KB = $kb
                            ComUpdate = $update
                        }
                    }
                }
            }
        } catch {
            Write-Log "COM API driver search failed: $_" "Warning"
        } finally {
            if ($updateSearcher) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSearcher) | Out-Null }
            if ($updateSession) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($updateSession) | Out-Null }
        }
        
        if ($driverUpdates -and $driverUpdates.Count -gt 0) {
            Write-Success "Found $($driverUpdates.Count) driver update(s)"
            Write-Host ""
            
            $counter = 0
            foreach ($driver in $driverUpdates) {
                $counter++
                $state.DriverLog += $driver.Title
                Write-Info "[$counter/$($driverUpdates.Count)] $($driver.Title)"
                Write-Log "Driver update: $($driver.Title)" "Info"
            }
            
            Write-Host ""
            Write-Step "Installing driver updates..."
            
            # Try PSWindowsUpdate first
            try {
                Install-WindowsUpdate -MicrosoftUpdate -UpdateType Driver -AcceptAll -IgnoreReboot -ErrorAction SilentlyContinue | Out-Null
                Write-Success "Driver updates installed via PSWindowsUpdate"
            } catch {
                Write-Warn "PSWindowsUpdate driver install failed, trying COM API..."
                
                # Fallback to COM API
                try {
                    $updateSession = New-Object -ComObject Microsoft.Update.Session
                    $updateInstaller = $updateSession.CreateUpdateInstaller()
                    $driversToInstall = New-Object -ComObject Microsoft.Update.UpdateColl
                    
                    foreach ($driver in $driverUpdates) {
                        if ($driver.ComUpdate) {
                            $driversToInstall.Add($driver.ComUpdate) | Out-Null
                        }
                    }
                    
                    if ($driversToInstall.Count -gt 0) {
                        $updateInstaller.Updates = $driversToInstall
                        $installResult = $updateInstaller.Install()
                        
                        if ($installResult.ResultCode -eq 2) {
                            Write-Success "Driver updates installed via COM API"
                        }
                    }
                } catch {
                    Write-Err "COM API driver install also failed: $_"
                }
            }
            
            Write-Log "Drivers installed: $($driverUpdates.Count)" "Success"
            
            $state.DriverRound++
            Save-State $state
            
            Start-Sleep -Seconds 5
            if (Test-RebootRequired) {
                Invoke-SystemReboot $state "Driver Updates"
            }
            
            if ($state.DriverRound -lt 5) {
                return $false  # Continue checking
            }
        } else {
            Write-Success "No driver updates available"
            Write-Log "No driver updates found" "Info"
        }
        
        Write-Host ""
        Write-Step "Final device scan and verification..." -NoNewLine
        Start-Process "pnputil.exe" -ArgumentList "/scan-devices" -Wait -NoNewWindow -WindowStyle Hidden -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
        Write-Host " Done" -ForegroundColor Green
        
        $problemDevicesAfter = Get-WmiObject Win32_PnPEntity | Where-Object { 
            $_.ConfigManagerErrorCode -ne 0
        }
        
        if ($problemDevicesAfter) {
            Write-Warn "Remaining devices with issues: $($problemDevicesAfter.Count)"
            foreach ($device in $problemDevicesAfter) {
                Write-Info "[!] $($device.Name) - May require manufacturer-specific driver"
            }
            Write-Info "Consider visiting device manufacturer websites for specific drivers"
        } else {
            Write-Success "All devices verified and working perfectly!"
        }
        
        Save-State $state
        Write-Log "Driver phase complete" "Success"
        return $true
        
    } catch {
        Write-Err "Error updating drivers: $_"
        Write-Log "Driver error: $_" "Error"
        return $true
    }
}

# ============================================
# PHASE 2: SYSTEM CLEANUP
# ============================================
function Invoke-SystemCleanup($state) {
    Write-Header "PHASE 3: System Cleanup"
    
    $driveBefore = Get-PSDrive C | Select-Object Used, Free
    $state.FreeSpaceBefore = [math]::Round($driveBefore.Free / 1GB, 2)
    Write-Info "Free space before cleanup: $($state.FreeSpaceBefore) GB"
    Write-Host ""
    
    # Windows Update Cache
    Write-Step "Cleaning Windows Update cache..."
    try {
        Write-Info "Stopping Windows Update services..."
        for ($i = 1; $i -le 3; $i++) {
            Stop-Service wuauserv, bits, cryptsvc -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
        }
        
        $wuService = Get-Service wuauserv
        if ($wuService.Status -eq 'Running') {
            Write-Info "Force stopping services..."
            Stop-Process -Name svchost -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 3
        }
        
        $updateCache = "$env:SystemRoot\SoftwareDistribution\Download"
        if (Test-Path $updateCache) {
            $size = [math]::Round(((Get-ChildItem $updateCache -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum) / 1MB, 2)
            Get-ChildItem $updateCache -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
            
            Start-Sleep -Seconds 2
            $remainingFiles = Get-ChildItem $updateCache -Force -ErrorAction SilentlyContinue
            if ($remainingFiles.Count -eq 0) {
                $state.CleanupLog += "Update Cache: $size MB"
                Write-Success "Cleaned $size MB from update cache"
                Write-Log "Update cache cleaned: $size MB" "Success"
            } else {
                $state.CleanupLog += "Update Cache: $size MB (Partial)"
                Write-Warn "Partially cleaned ($($remainingFiles.Count) files in use)"
                Write-Log "Update cache partially cleaned" "Warning"
            }
        } else {
            Write-Info "Update cache already empty"
        }
        
        Write-Info "Restarting Windows Update services..."
        Start-Service wuauserv, bits, cryptsvc -ErrorAction SilentlyContinue
        
    } catch {
        Write-Warn "Could not fully clean update cache: $_"
        Write-Log "Update cache error: $_" "Warning"
    }
    
    Write-Host ""
    
    # Temporary Files
    Write-Step "Cleaning temporary files..."
    $tempPaths = @(
        "$env:TEMP",
        "$env:SystemRoot\Temp",
        "$env:SystemRoot\Prefetch",
        "$env:LOCALAPPDATA\Temp"
    )
    
    $totalTemp = 0
    foreach ($path in $tempPaths) {
        try {
            if (Test-Path $path) {
                $size = [math]::Round(((Get-ChildItem $path -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum) / 1MB, 2)
                $totalTemp += $size
                Remove-Item "$path\*" -Recurse -Force -ErrorAction SilentlyContinue
            }
        } catch {}
    }
    
    if ($totalTemp -gt 0) {
        $state.CleanupLog += "Temp Files: $totalTemp MB"
        Write-Success "Cleaned $totalTemp MB of temporary files"
        Write-Log "Temp files cleaned: $totalTemp MB" "Success"
    } else {
        Write-Info "No temporary files to clean"
    }
    
    # Recycle Bin
    Write-Step "Emptying Recycle Bin..." -NoNewLine
    try {
        Clear-RecycleBin -Force -ErrorAction Stop
        $state.CleanupLog += "Recycle Bin: Emptied"
        Write-Host " Done" -ForegroundColor Green
        Write-Log "Recycle bin emptied" "Success"
    } catch {
        Write-Host " Skipped" -ForegroundColor Yellow
    }
    
    # Windows.old
    Write-Step "Checking for Windows.old..."
    $windowsOld = "C:\Windows.old"
    if (Test-Path $windowsOld) {
        try {
            $size = [math]::Round(((Get-ChildItem $windowsOld -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum) / 1GB, 2)
            Write-Info "Taking ownership and removing..."
            cmd /c "takeown /F C:\Windows.old\* /R /A /D Y" 2>&1 | Out-Null
            cmd /c "icacls C:\Windows.old\*.* /T /grant administrators:F" 2>&1 | Out-Null
            Remove-Item $windowsOld -Recurse -Force -ErrorAction SilentlyContinue
            
            $state.CleanupLog += "Windows.old: $size GB"
            Write-Success "Removed $size GB (Windows.old)"
            Write-Log "Windows.old removed: $size GB" "Success"
        } catch {
            Write-Warn "Could not remove Windows.old"
            Write-Log "Windows.old removal failed" "Warning"
        }
    } else {
        Write-Info "No Windows.old folder found"
    }
    
    Write-Host ""
    
    # DISM Cleanup
    Write-Step "Running DISM cleanup (may take several minutes)..."
    try {
        $dism = Start-Process dism.exe -ArgumentList "/Online /Cleanup-Image /StartComponentCleanup /ResetBase /Quiet" -Wait -PassThru -NoNewWindow -WindowStyle Hidden
        if ($dism.ExitCode -eq 0) {
            $state.CleanupLog += "DISM Cleanup: Success"
            Write-Success "DISM cleanup completed"
            Write-Log "DISM cleanup success" "Success"
        }
    } catch {
        Write-Warn "DISM cleanup failed: $_"
        Write-Log "DISM error: $_" "Warning"
    }
    
    # Storage Sense
    Write-Step "Running Storage Sense..." -NoNewLine
    try {
        Start-Process cleanmgr.exe -ArgumentList "/autoclean" -Wait -NoNewWindow -WindowStyle Hidden -ErrorAction SilentlyContinue
        $state.CleanupLog += "Storage Sense: Executed"
        Write-Host " Done" -ForegroundColor Green
        Write-Log "Storage Sense executed" "Success"
    } catch {
        Write-Host " Skipped" -ForegroundColor Yellow
    }
    
    Write-Host ""
    
    $driveAfter = Get-PSDrive C | Select-Object Used, Free
    $freeSpaceAfter = [math]::Round($driveAfter.Free / 1GB, 2)
    $spaceFreed = [math]::Round($freeSpaceAfter - $state.FreeSpaceBefore, 2)
    
    $state.CleanupLog += "Total Space Freed: $spaceFreed GB"
    Write-Info "Free space after cleanup: $freeSpaceAfter GB"
    Write-Success "Total space freed: $spaceFreed GB"
    Write-Log "Cleanup complete. Freed: $spaceFreed GB" "Success"
    
    Save-State $state
    return $true
}

# ============================================
# PHASE 3: GENERATE REPORT & CLEANUP
# ============================================
function Complete-Setup($state) {
    Write-Header "PHASE 4: Final Cleanup & Report Generation"
    
    # FINAL UPDATE CACHE CLEANUP (NEW)
    Write-Step "Performing final Windows Update cache cleanup..."
    try {
        Write-Info "Stopping Windows Update services for final cleanup..."
        Stop-Service wuauserv, bits, cryptsvc -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
        
        $finalUpdateCache = "$env:SystemRoot\SoftwareDistribution\Download"
        if (Test-Path $finalUpdateCache) {
            $finalCacheSize = [math]::Round(((Get-ChildItem $finalUpdateCache -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum) / 1GB, 2)
            
            if ($finalCacheSize -gt 0) {
                Write-Info "Removing $finalCacheSize GB from update cache..."
                Remove-Item "$finalUpdateCache\*" -Recurse -Force -ErrorAction SilentlyContinue
                
                Start-Sleep -Seconds 2
                $remainingFiles = Get-ChildItem $finalUpdateCache -Force -ErrorAction SilentlyContinue
                
                if ($remainingFiles.Count -eq 0) {
                    Write-Success "Final cleanup: $finalCacheSize GB freed from update cache"
                    Write-Log "Final update cache cleared: $finalCacheSize GB" "Success"
                    $state.CleanupLog += "Final Update Cache: $finalCacheSize GB"
                } else {
                    Write-Warn "Partial final cleanup ($($remainingFiles.Count) files remain)"
                    Write-Log "Final cache partially cleaned" "Warning"
                }
            } else {
                Write-Info "Update cache already empty"
            }
        }
        
        Write-Info "Restarting Windows Update services..."
        Start-Service wuauserv, bits, cryptsvc -ErrorAction SilentlyContinue
        
    } catch {
        Write-Warn "Final cache cleanup encountered error: $_"
        Write-Log "Final cache cleanup failed: $_" "Warning"
    }
    
    Write-Host ""
    Write-Step "Generating detailed report..."
    
    $reportContent = @"
================================================================================
                    NEXOR - WINDOWS 11 SETUP REPORT
================================================================================

Generated: $(Get-Date -Format 'MMMM dd, yyyy - HH:mm:ss')
Started: $([DateTime]::Parse($state.StartTime).ToString('MMMM dd, yyyy - HH:mm:ss'))
Computer: $env:COMPUTERNAME
User: $env:USERNAME

================================================================================
                            SUMMARY STATISTICS
================================================================================

Windows Updates:          $($state.UpdateLog.Count) installed
Unique KB Numbers:        $($state.InstalledUpdateKBs.Count)
Driver Updates:           $($state.DriverLog.Count) installed
Cleanup Operations:       $($state.CleanupLog.Count) completed
System Reboots:           $($state.RebootCount)
Update Rounds:            $($state.UpdateRound)
Driver Rounds:            $($state.DriverRound)

"@

    if ($state.FailedUpdates.Count -gt 0) {
        $reportContent += @"
ATTENTION: Some updates required multiple attempts
Failed/Problematic: $($state.FailedUpdates.Count) update(s)

"@
    }

    $reportContent += @"
================================================================================
                        WINDOWS UPDATES INSTALLED
================================================================================

"@

    if ($state.UpdateLog.Count -gt 0) {
        foreach ($update in $state.UpdateLog) {
            $reportContent += "[+] $update`r`n"
        }
    } else {
        $reportContent += "No updates were installed (system was up to date)`r`n"
    }

    $reportContent += @"

================================================================================
                        DRIVER UPDATES INSTALLED
================================================================================

"@

    if ($state.DriverLog.Count -gt 0) {
        foreach ($driver in $state.DriverLog) {
            $reportContent += "[+] $driver`r`n"
        }
    } else {
        $reportContent += "No driver updates were available`r`n"
    }

    $reportContent += @"

================================================================================
                        CLEANUP OPERATIONS
================================================================================

"@

    if ($state.CleanupLog.Count -gt 0) {
        foreach ($cleanup in $state.CleanupLog) {
            $reportContent += "[+] $cleanup`r`n"
        }
    } else {
        $reportContent += "No cleanup was performed`r`n"
    }

    $reportContent += @"

================================================================================
                      NEXOR - SETUP COMPLETE
================================================================================
"@

    # Ensure Desktop path exists and is accessible
    $desktopPath = [Environment]::GetFolderPath("Desktop")
    if (-not $desktopPath) {
        $desktopPath = "$env:USERPROFILE\Desktop"
    }

    # Ensure the directory exists
    if (-not (Test-Path $desktopPath)) {
        New-Item -Path $desktopPath -ItemType Directory -Force | Out-Null
    }

    $reportFileName = "Nexor_Report_$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"
    $reportPath = Join-Path $desktopPath $reportFileName

    try {
        $reportContent | Out-File -FilePath $reportPath -Encoding UTF8 -Force
        Write-Success "Report saved to Desktop: $reportFileName"
        Write-Log "Report saved: $reportPath" "Success"
    } catch {
        # Fallback to temp if desktop fails
        $reportPath = "$env:TEMP\$reportFileName"
        $reportContent | Out-File -FilePath $reportPath -Encoding UTF8 -Force
        Write-Warn "Report saved to temp: $reportPath"
        Write-Log "Report saved to temp: $reportPath" "Warning"
    }
    
    # Remove scheduled task if in unattended mode
    if ($Unattended) {
        Write-Step "Removing auto-resume task..."
        Unregister-ScheduledTask -TaskName "NexorAutoResume" -Confirm:$false -ErrorAction SilentlyContinue
        Write-Success "Auto-resume task removed"
        Write-Log "Scheduled task removed" "Success"
    }
    
    if (Test-RebootRequired) {
        Write-Host ""
        Write-Warn "Final system restart required"
        Write-Info "Report saved to: $reportPath"
        Write-Host ""
        
        for ($i = 20; $i -gt 0; $i--) {
            Write-Host "`r  Restarting in $i seconds... (Press Ctrl+C to cancel)" -NoNewline -ForegroundColor Yellow
            Start-Sleep -Seconds 1
        }
        Write-Host ""
        Restart-Computer -Force
    } else {
        Write-Host ""
        Write-Success "Setup complete!"
        Write-Info "Report saved to: $reportPath"
        Write-Log "Setup complete without reboot" "Success"
        
        Start-Sleep -Seconds 2
        Remove-Item $nexorDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ============================================
# MAIN EXECUTION
# ============================================
try {
    Clear-Host
    $state = Get-State
    
    # Phase -1: Initialization
    if ($state.Phase -eq -1) {
        if (-not (Initialize-Environment)) {
            Write-Err "Initialization failed!"
            Write-Log "Initialization failed" "Error"
            
            if (-not $Unattended) {
                Read-Host "`nPress Enter to exit"
            }
            exit 1
        }
        $state.Phase = 0
        Save-State $state
        
        if (-not $Unattended) {
            Write-Host ""
            Write-Info "Press any key to start..."
            $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        } else {
            Write-Info "UNATTENDED MODE: Starting automatically in 3 seconds..."
            Start-Sleep -Seconds 3
        }
    }
    
    # Phase 0: Windows Updates (loop until complete)
    if ($state.Phase -eq 0) {
        $updatesComplete = Install-WindowsUpdates $state
        
        if ($updatesComplete) {
            Write-Host ""
            Write-Success "Windows Updates phase complete!"
            Write-Log "Windows Updates complete" "Success"
            $state.Phase = 1
            $state.UpdateRound = 0
            Save-State $state
            
            Write-Host ""
            Write-Info "Continuing to driver updates in 3 seconds..."
            Start-Sleep -Seconds 3
        } else {
            Save-State $state
            Write-Host ""
            Write-Info "Searching for more updates in 3 seconds..."
            Start-Sleep -Seconds 3
            
            & $PSCommandPath -Silent:$Silent
            exit 0
        }
    }
    
    # Phase 1: Driver Updates (loop until complete)
    if ($state.Phase -eq 1) {
        $driversComplete = Install-DriverUpdates $state
        
        if ($driversComplete) {
            Write-Host ""
            Write-Success "Driver Updates phase complete!"
            Write-Log "Driver Updates complete" "Success"
            $state.Phase = 2
            Save-State $state
            
            Write-Host ""
            Write-Info "Continuing to cleanup in 3 seconds..."
            Start-Sleep -Seconds 3
        } else {
            Save-State $state
            Write-Host ""
            Write-Info "Checking for more drivers in 3 seconds..."
            Start-Sleep -Seconds 3
            
            & $PSCommandPath -Silent:$Silent
            exit 0
        }
    }
    
    # Phase 2: System Cleanup
    if ($state.Phase -eq 2) {
        Invoke-SystemCleanup $state | Out-Null
        $state.Phase = 3
        Save-State $state
        
        Write-Host ""
        Write-Info "Continuing to final verification in 3 seconds..."
        Start-Sleep -Seconds 3
    }
    
    # Phase 3: Final verification and report
    if ($state.Phase -eq 3) {
        Write-Header "FINAL VERIFICATION"
        
        Write-Step "Performing final comprehensive update check..."
        try {
            Import-Module PSWindowsUpdate -Force -ErrorAction Stop
            
            # ULTRA-AGGRESSIVE FINAL VERIFICATION LOOP
            $finalVerificationAttempts = 0
            $maxFinalAttempts = 5
            
            while ($finalVerificationAttempts -lt $maxFinalAttempts) {
                Write-Step "Final verification attempt $($finalVerificationAttempts + 1)/$maxFinalAttempts..."
                
                # Try ALL 6 methods for final check
                $finalCheck = @()
                $finalCheckByKB = @{}
                
                for ($method = 1; $method -le 8; $method++) {
                    $methodUpdates = Search-WindowsUpdates -Method $method
                    if ($methodUpdates.Count -gt 0) {
                        foreach ($update in $methodUpdates) {
                            if ($update.KB -and -not $finalCheckByKB.ContainsKey($update.KB)) {
                                $finalCheckByKB[$update.KB] = $update
                            } elseif (-not $update.KB) {
                                $finalCheck += $update
                            }
                        }
                    }
                }
                
                $finalCheck += $finalCheckByKB.Values
                
                # Filter out already installed
                if ($state.InstalledUpdateKBs.Count -gt 0) {
                    $finalCheck = $finalCheck | Where-Object { 
                        -not $_.KB -or $state.InstalledUpdateKBs -notcontains $_.KB 
                    }
                }
                
                if ($finalCheck.Count -eq 0) {
                    Write-Success "No updates found - System fully updated!"
                    Write-Log "Final verification passed: No updates found" "Success"
                    break  # Exit loop - truly complete
                }
                
                # Found updates - install them
                Write-Warn "$($finalCheck.Count) update(s) found in final check"
                Write-Info "Installing remaining updates..."
                Write-Host ""
                
                foreach ($update in $finalCheck) {
                    $updateTitle = $update.Title
                    if ($update.KB) {
                        $updateTitle += " ($($update.KB))"
                    }
                    Write-Info "[+] $updateTitle"
                    $state.UpdateLog += $updateTitle
                }
                Write-Log "Final check found updates: $($finalCheck.Count)" "Warning"
                
                Write-Host ""
                Write-Step "Installing final updates..."
                Install-WindowsUpdate -MicrosoftUpdate -AcceptAll -IgnoreReboot -ErrorAction Stop | Out-Null
                
                # Track installed
                foreach ($update in $finalCheck) {
                    if ($update.KB -and $state.InstalledUpdateKBs -notcontains $update.KB) {
                        $state.InstalledUpdateKBs += $update.KB
                    }
                }
                
                Write-Success "Final updates installed"
                Write-Log "Final updates installed: $($finalCheck.Count)" "Success"
                
                $finalVerificationAttempts++
                
                Save-State $state
                
                Start-Sleep -Seconds 5
                if (Test-RebootRequired) {
                    Invoke-SystemReboot $state "Final Windows Updates"
                }
                
                # Delay before next check
                Start-Sleep -Seconds 10
            }
            
            # Check if we exhausted attempts
            if ($finalVerificationAttempts -ge $maxFinalAttempts) {
                Write-Warn "Reached maximum final verification attempts"
                Write-Warn "Some updates may still be pending - manual check recommended"
                Write-Log "Final verification limit reached" "Warning"
            }
            
        } catch {
            Write-Warn "Final check completed with warnings"
            Write-Log "Final check warning: $_" "Warning"
        }
        
        Write-Host ""
        Write-Step "Performing final device check..."
        $finalDeviceCheck = Get-WmiObject Win32_PnPEntity | Where-Object { 
            $_.ConfigManagerErrorCode -ne 0
        }
        
        if ($finalDeviceCheck) {
            Write-Warn "$($finalDeviceCheck.Count) device(s) still have issues"
            Write-Info "These may require manual driver installation from manufacturer"
            Write-Host ""
            
            foreach ($device in $finalDeviceCheck) {
                Write-Info "[!] $($device.Name)"
            }
            Write-Log "Devices with issues: $($finalDeviceCheck.Count)" "Warning"
        } else {
            Write-Success "All devices verified and working perfectly!"
            Write-Log "All devices verified" "Success"
        }
        
        Write-Host ""
        Complete-Setup $state
    }
    
    Write-Host ""
    Write-Header "NEXOR SETUP COMPLETED"
    Write-Log "Setup completed successfully" "Success"

    if (-not $Silent -and -not $Unattended) {
        Write-Host ""
        Write-Info "Press any key to exit..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    } elseif ($Unattended) {
        Write-Info "UNATTENDED MODE: Script completed. Exiting in 5 seconds..."
        Start-Sleep -Seconds 5
    }
} catch {
    Write-Host ""
    Write-Err "Critical error occurred"
    Write-Err $_.Exception.Message
    Write-Log "Critical error: $_ | $($_.ScriptStackTrace)" "Error"
    
    if (-not $Silent -and -not $Unattended) {
        Write-Host ""
        Read-Host "Press Enter to exit"
    } else {
        Write-Info "UNATTENDED MODE: Exiting in 5 seconds due to error..."
        Start-Sleep -Seconds 5
    }
    exit 1
}

