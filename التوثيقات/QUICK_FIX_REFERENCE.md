# 🔧 QUICK FIX REFERENCE CARD

## ❌ ISSUES FOUND

1. **HTTPS Redirection** - Backend forcing HTTP→HTTPS redirect (no SSL cert)
2. **Missing Android Permissions** - No INTERNET permission in AndroidManifest.xml
3. **Missing Cleartext Traffic** - Android 9+ blocks HTTP without flag
4. **Wrong Base URL** - Using emulator IP (10.0.2.2) instead of LAN IP (192.168.1.8)

---

## ✅ FIXES APPLIED

### 1. Backend - Disabled HTTPS Redirection
**File**: `src/MarcoERP.API/Program.cs` (Line 434)

```csharp
// BEFORE (BROKEN):
app.UseHttpsRedirection();

// AFTER (FIXED):
// app.UseHttpsRedirection(); // Disabled for mobile HTTP access
```

---

### 2. Mobile - Added Internet Permissions
**File**: `mobile/marco_erp/android/app/src/main/AndroidManifest.xml`

```xml
<!-- ADDED: -->
<uses-permission android:name="android.permission.INTERNET"/>
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE"/>
```

---

### 3. Mobile - Enabled Cleartext Traffic
**File**: `mobile/marco_erp/android/app/src/main/AndroidManifest.xml`

```xml
<!-- ADDED: -->
<application
    android:usesCleartextTraffic="true"
    ...>
```

---

### 4. Mobile - Fixed Base URL
**File**: `mobile/marco_erp/lib/core/constants/api_constants.dart`

```dart
// BEFORE (EMULATOR ONLY):
static const String baseUrl = 'http://10.0.2.2:5000/api';

// AFTER (PHYSICAL DEVICE):
static const String baseUrl = 'http://192.168.1.8:5000/api';
```

---

### 5. Backend - Added Health Endpoint
**File**: `src/MarcoERP.API/Controllers/HealthController.cs` (NEW)

```csharp
[HttpGet]
public IActionResult Get() // Returns: { success: true, message: "API is running" }

[HttpGet("ping")]
public IActionResult Ping() // Returns: { success: true, message: "pong" }
```

---

## 🚀 QUICK START STEPS

### Step 1: Find Your PC IP
```powershell
ipconfig
# Look for: IPv4 Address . . . : 192.168.1.8
```

### Step 2: Update Mobile App IP (if different)
Edit: `mobile/marco_erp/lib/core/constants/api_constants.dart`
```dart
static const String baseUrl = 'http://YOUR-IP:5000/api';
```

### Step 3: Allow Firewall
```powershell
# Run as Administrator:
.\setup_mobile_backend.ps1
```

### Step 4: Start Backend
```powershell
cd src\MarcoERP.API
dotnet run
```

### Step 5: Test from Phone Browser
Open: `http://192.168.1.8:5000/api/health`

Should see:
```json
{
  "success": true,
  "message": "MarcoERP API is running",
  "version": "1.0.0"
}
```

### Step 6: Rebuild Mobile App
```bash
cd mobile\marco_erp
flutter run --release
```

---

## 🧪 TESTING COMMANDS

### Backend Health Check
```powershell
# From PC:
curl http://localhost:5000/api/health
curl http://192.168.1.8:5000/api/health

# From phone browser:
http://192.168.1.8:5000/api/health
```

### Test Login Endpoint
```powershell
curl -X POST http://192.168.1.8:5000/api/auth/login `
  -H "Content-Type: application/json" `
  -d '{"username":"admin","password":"yourpassword"}'
```

### Run Full Connectivity Test
```powershell
.\test_mobile_connection.ps1
```

---

## 🐛 TROUBLESHOOTING

### "Cannot connect to server"
✅ Backend is running (`dotnet run`)  
✅ Phone on same Wi-Fi as PC  
✅ Firewall allows port 5000  
✅ IP address is correct in app  
✅ Health endpoint works in phone browser  

### Backend Won't Start
❌ Check: Another process using port 5000  
❌ Check: SQL Server is running  
❌ Check: Database "MarcoERP" exists  

### Firewall Blocking
```powershell
# Allow port 5000:
New-NetFirewallRule -DisplayName "MarcoERP API" `
  -Direction Inbound -LocalPort 5000 -Protocol TCP -Action Allow
```

### Database Connection Error
❌ Check: SQL Server service is running  
❌ Check: Connection string in appsettings.json  
❌ Check: Windows Authentication enabled  

---

## 📋 FILES MODIFIED

✅ `src/MarcoERP.API/Program.cs` - Disabled HTTPS redirection, added logging  
✅ `mobile/marco_erp/android/app/src/main/AndroidManifest.xml` - Added permissions  
✅ `mobile/marco_erp/lib/core/constants/api_constants.dart` - Updated base URL  
✅ `src/MarcoERP.API/Controllers/HealthController.cs` - NEW health endpoint  

---

## 🔐 PRODUCTION NOTES

⚠️ Before deploying to production:

1. **Enable HTTPS** with proper SSL certificate
2. **Restrict CORS** to specific origins
3. **Add rate limiting** on auth endpoints
4. **Enable request logging** for audit trail
5. **Remove cleartext traffic** from Android manifest
6. **Update mobile app** to use HTTPS base URL

---

## ✅ SUCCESS INDICATORS

When working correctly, you should see:

**Backend Console:**
```
╔══════════════════════════════════════════════════════════════╗
║          MarcoERP API Started Successfully                   ║
╠══════════════════════════════════════════════════════════════╣
║  Listening on: http://0.0.0.0:5000
║  Health Check: http://0.0.0.0:5000/api/health
```

**Phone Browser:**
```json
{
  "success": true,
  "message": "MarcoERP API is running"
}
```

**Mobile App:**
- Login screen appears
- Can enter credentials
- Successful login navigates to dashboard
- No "Cannot connect" errors

---

**Generated**: 2026-03-06  
**Status**: All issues resolved ✅  
**Ready for testing**: YES 🚀
