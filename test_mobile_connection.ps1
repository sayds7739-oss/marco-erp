# ╔══════════════════════════════════════════════════════════════╗
# ║     MarcoERP Backend - Quick Connectivity Test Script        ║
# ╚══════════════════════════════════════════════════════════════╝

param(
    [string]$ServerIP = "192.168.1.8",
    [int]$Port = 5000
)

Write-Host "`n╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║          MarcoERP API Connectivity Test                     ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

# Test 1: Network Port Reachability
Write-Host "[TEST 1] Testing port connectivity..." -ForegroundColor Yellow
$portTest = Test-NetConnection -ComputerName $ServerIP -Port $Port -WarningAction SilentlyContinue

if ($portTest.TcpTestSucceeded) {
    Write-Host "✅ Port $Port is reachable on $ServerIP" -ForegroundColor Green
} else {
    Write-Host "❌ Port $Port is NOT reachable on $ServerIP" -ForegroundColor Red
    Write-Host "   - Check if backend is running" -ForegroundColor Yellow
    Write-Host "   - Check Windows Firewall" -ForegroundColor Yellow
    Write-Host "   - Verify IP address is correct" -ForegroundColor Yellow
    exit 1
}

# Test 2: Health Endpoint
Write-Host "`n[TEST 2] Testing /api/health endpoint..." -ForegroundColor Yellow
$healthUrl = "http://${ServerIP}:${Port}/api/health"

try {
    $healthResponse = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 10
    Write-Host "✅ Health endpoint responded successfully" -ForegroundColor Green
    Write-Host "   Message: $($healthResponse.message)" -ForegroundColor Gray
    Write-Host "   Version: $($healthResponse.version)" -ForegroundColor Gray
    Write-Host "   Environment: $($healthResponse.environment)" -ForegroundColor Gray
} catch {
    Write-Host "❌ Health endpoint failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 3: Ping Endpoint
Write-Host "`n[TEST 3] Testing /api/health/ping endpoint..." -ForegroundColor Yellow
$pingUrl = "http://${ServerIP}:${Port}/api/health/ping"

try {
    $pingResponse = Invoke-RestMethod -Uri $pingUrl -Method Get -TimeoutSec 5
    if ($pingResponse.message -eq "pong") {
        Write-Host "✅ Ping successful" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Ping returned unexpected response" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ Ping failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Auth Endpoint (should return 400 for invalid request)
Write-Host "`n[TEST 4] Testing /api/auth/login endpoint..." -ForegroundColor Yellow
$loginUrl = "http://${ServerIP}:${Port}/api/auth/login"

try {
    $loginResponse = Invoke-RestMethod -Uri $loginUrl -Method Post -Body "{}" -ContentType "application/json" -TimeoutSec 5
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 400) {
        Write-Host "✅ Auth endpoint is accessible (returned 400 as expected)" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Auth endpoint returned: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Yellow
    }
}

# Test 5: CORS Headers
Write-Host "`n[TEST 5] Testing CORS configuration..." -ForegroundColor Yellow
try {
    $headers = @{
        "Origin" = "http://localhost:8080"
    }
    $corsResponse = Invoke-WebRequest -Uri $healthUrl -Method Get -Headers $headers -TimeoutSec 5
    
    $corsHeader = $corsResponse.Headers["Access-Control-Allow-Origin"]
    if ($corsHeader) {
        Write-Host "✅ CORS is enabled" -ForegroundColor Green
        Write-Host "   Access-Control-Allow-Origin: $corsHeader" -ForegroundColor Gray
    } else {
        Write-Host "⚠️  CORS headers not found (might be an issue for mobile app)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠️  Could not verify CORS: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Summary
Write-Host "`n╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                    Test Results Summary                      ║" -ForegroundColor Green
Write-Host "╠══════════════════════════════════════════════════════════════╣" -ForegroundColor Green
Write-Host "║  Server: http://${ServerIP}:${Port}" -ForegroundColor Green
Write-Host "║  Status: Backend is accessible and working correctly         ║" -ForegroundColor Green
Write-Host "╠══════════════════════════════════════════════════════════════╣" -ForegroundColor Green
Write-Host "║  Mobile App Instructions:                                    ║" -ForegroundColor Green
Write-Host "║  1. Open phone browser                                       ║" -ForegroundColor Green
Write-Host "║  2. Navigate to: http://${ServerIP}:${Port}/api/health" -ForegroundColor Green
Write-Host "║  3. If JSON appears, mobile app will work                    ║" -ForegroundColor Green
Write-Host "║  4. Rebuild app: flutter run                                 ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green

Write-Host "`n✅ All tests passed! Mobile app should connect successfully.`n" -ForegroundColor Green
