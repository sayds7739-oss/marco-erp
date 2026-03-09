# ╔══════════════════════════════════════════════════════════════╗
# ║     MarcoERP Mobile Backend - Network Setup Script           ║
# ║     Configures firewall and tests connectivity               ║
# ╚══════════════════════════════════════════════════════════════╝

Write-Host "`n╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║          MarcoERP Mobile Backend Setup                      ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

# Step 1: Get network configuration
Write-Host "[1/5] Detecting network configuration..." -ForegroundColor Yellow
$ipAddress = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { 
    $_.PrefixOrigin -eq 'Dhcp' -or $_.PrefixOrigin -eq 'Manual' -and 
    $_.IPAddress -notlike '127.*' -and 
    $_.IPAddress -notlike '169.254.*'
} | Select-Object -First 1).IPAddress

if ($ipAddress) {
    Write-Host "✅ PC IP Address: $ipAddress" -ForegroundColor Green
} else {
    Write-Host "❌ Could not detect IP address. Check network connection." -ForegroundColor Red
    exit 1
}

# Step 2: Check if firewall rule exists
Write-Host "`n[2/5] Checking Windows Firewall..." -ForegroundColor Yellow
$ruleName = "MarcoERP API Port 5000"
$existingRule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue

if ($existingRule) {
    Write-Host "✅ Firewall rule already exists" -ForegroundColor Green
} else {
    Write-Host "⚠️  Firewall rule not found. Creating..." -ForegroundColor Yellow
    
    try {
        New-NetFirewallRule -DisplayName $ruleName `
            -Direction Inbound `
            -LocalPort 5000 `
            -Protocol TCP `
            -Action Allow `
            -ErrorAction Stop | Out-Null
        Write-Host "✅ Firewall rule created successfully" -ForegroundColor Green
    } catch {
        Write-Host "❌ Failed to create firewall rule. Run PowerShell as Administrator." -ForegroundColor Red
        Write-Host "   Error: $_" -ForegroundColor Red
        exit 1
    }
}

# Step 3: Check if port 5000 is in use
Write-Host "`n[3/5] Checking if port 5000 is available..." -ForegroundColor Yellow
$portInUse = Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue

if ($portInUse) {
    Write-Host "⚠️  Port 5000 is already in use" -ForegroundColor Yellow
    Write-Host "   Process: $($portInUse.OwningProcess)" -ForegroundColor Gray
    Write-Host "   This is OK if MarcoERP.API is already running" -ForegroundColor Gray
} else {
    Write-Host "✅ Port 5000 is available" -ForegroundColor Green
}

# Step 4: Update mobile app configuration reminder
Write-Host "`n[4/5] Mobile app configuration..." -ForegroundColor Yellow
Write-Host "📱 Update this file:" -ForegroundColor Cyan
Write-Host "   mobile\marco_erp\lib\core\constants\api_constants.dart" -ForegroundColor Gray
Write-Host "`n   Change baseUrl to:" -ForegroundColor Cyan
Write-Host "   static const String baseUrl = 'http://$ipAddress:5000/api';" -ForegroundColor White

# Step 5: Test backend connectivity
Write-Host "`n[5/5] Testing backend connectivity..." -ForegroundColor Yellow

$testUrl = "http://localhost:5000/api/health"
try {
    $response = Invoke-WebRequest -Uri $testUrl -Method Get -TimeoutSec 5 -ErrorAction Stop
    Write-Host "✅ Backend is running and accessible" -ForegroundColor Green
    Write-Host "   Status: $($response.StatusCode)" -ForegroundColor Gray
    Write-Host "   Response: $($response.Content.Substring(0, [Math]::Min(100, $response.Content.Length)))..." -ForegroundColor Gray
} catch {
    if ($_.Exception.Message -like "*Unable to connect*" -or $_.Exception.Message -like "*No connection could be made*") {
        Write-Host "⚠️  Backend is not running" -ForegroundColor Yellow
        Write-Host "   Start it with: dotnet run --project src\MarcoERP.API\MarcoERP.API.csproj" -ForegroundColor Cyan
    } else {
        Write-Host "⚠️  Backend test failed: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# Summary
Write-Host "`n╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                    Setup Summary                             ║" -ForegroundColor Green
Write-Host "╠══════════════════════════════════════════════════════════════╣" -ForegroundColor Green
Write-Host "║  PC IP Address: $ipAddress" -ForegroundColor Green
Write-Host "║  Firewall: Configured for port 5000" -ForegroundColor Green
Write-Host "╠══════════════════════════════════════════════════════════════╣" -ForegroundColor Green
Write-Host "║  Next Steps:                                                 ║" -ForegroundColor Green
Write-Host "║  1. Update mobile app IP in api_constants.dart               ║" -ForegroundColor Green
Write-Host "║  2. Start backend: dotnet run                                ║" -ForegroundColor Green
Write-Host "║  3. Test from phone browser:                                 ║" -ForegroundColor Green
Write-Host "║     http://$ipAddress:5000/api/health" -ForegroundColor Green
Write-Host "║  4. Rebuild mobile app: flutter run                          ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green

Write-Host "`n📋 Test URLs:" -ForegroundColor Cyan
Write-Host "   Health:  http://$ipAddress:5000/api/health" -ForegroundColor White
Write-Host "   Swagger: http://$ipAddress:5000/swagger" -ForegroundColor White
Write-Host "   Login:   http://$ipAddress:5000/api/auth/login" -ForegroundColor White

Write-Host "`n✅ Setup complete! Follow the next steps above.`n" -ForegroundColor Green
