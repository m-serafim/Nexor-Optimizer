// Filter for known apps
var knownProcesses = allRunningProcesses
    .Where(p => _appInfo.ContainsKey(p.ProcessName.ToLower()))
    .GroupBy(p => p.ProcessName.ToLower())
    .Select(g => g.OrderByDescending(p => p.WorkingSet64).First()) // Get the one using most memory
    .ToList();

var appData = _appInfo[process.ProcessName.ToLower()];