import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/auth/auth_provider.dart';
import '../../../core/api/api_client.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _usernameController = TextEditingController();
  final _passwordController = TextEditingController();
  final _serverController = TextEditingController(text: 'https://10.0.2.2:5000/api');
  bool _obscurePassword = true;
  String? _errorMessage;
  bool _showServerConfig = false;

  @override
  void dispose() {
    _usernameController.dispose();
    _passwordController.dispose();
    _serverController.dispose();
    super.dispose();
  }

  Future<void> _login() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _errorMessage = null);

    // Update server URL if changed — enforce HTTPS
    if (_showServerConfig && _serverController.text.isNotEmpty) {
      final url = _serverController.text.trim();
      final uri = Uri.tryParse(url);
      if (uri == null || !uri.hasScheme || (!url.startsWith('https://') && !url.startsWith('http://localhost') && !url.startsWith('http://10.'))) {
        setState(() => _errorMessage = 'عنوان الخادم غير صالح. يجب استخدام HTTPS.');
        return;
      }
      context.read<ApiClient>().updateBaseUrl(url);
    }

    final error = await context.read<AuthProvider>().login(
          _usernameController.text.trim(),
          _passwordController.text,
        );

    if (error != null && mounted) {
      setState(() => _errorMessage = error);
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Form(
              key: _formKey,
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  // Logo
                  Container(
                    width: 100,
                    height: 100,
                    decoration: BoxDecoration(
                      color: Theme.of(context).colorScheme.primary,
                      borderRadius: BorderRadius.circular(24),
                    ),
                    child: const Icon(
                      Icons.business,
                      size: 56,
                      color: Colors.white,
                    ),
                  ),
                  const SizedBox(height: 24),
                  Text(
                    '\u0645\u0627\u0631\u0643\u0648 ERP',
                    style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                          fontWeight: FontWeight.bold,
                          color: Theme.of(context).colorScheme.primary,
                        ),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    '\u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062f\u062e\u0648\u0644',
                    style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                          color: Colors.grey,
                        ),
                  ),
                  const SizedBox(height: 40),

                  // Error message
                  if (_errorMessage != null)
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(12),
                      margin: const EdgeInsets.only(bottom: 16),
                      decoration: BoxDecoration(
                        color: Colors.red.shade50,
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(color: Colors.red.shade200),
                      ),
                      child: Text(
                        _errorMessage!,
                        style: TextStyle(color: Colors.red.shade700),
                        textAlign: TextAlign.center,
                      ),
                    ),

                  // Username
                  TextFormField(
                    controller: _usernameController,
                    textDirection: TextDirection.ltr,
                    decoration: const InputDecoration(
                      labelText: '\u0627\u0633\u0645 \u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645',
                      prefixIcon: Icon(Icons.person_outline),
                    ),
                    validator: (v) => v == null || v.isEmpty ? '\u0645\u0637\u0644\u0648\u0628' : null,
                  ),
                  const SizedBox(height: 16),

                  // Password
                  TextFormField(
                    controller: _passwordController,
                    obscureText: _obscurePassword,
                    textDirection: TextDirection.ltr,
                    decoration: InputDecoration(
                      labelText: '\u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631',
                      prefixIcon: const Icon(Icons.lock_outline),
                      suffixIcon: IconButton(
                        icon: Icon(_obscurePassword
                            ? Icons.visibility_off
                            : Icons.visibility),
                        onPressed: () =>
                            setState(() => _obscurePassword = !_obscurePassword),
                      ),
                    ),
                    validator: (v) => v == null || v.isEmpty ? '\u0645\u0637\u0644\u0648\u0628' : null,
                    onFieldSubmitted: (_) => _login(),
                  ),
                  const SizedBox(height: 24),

                  // Login button
                  SizedBox(
                    width: double.infinity,
                    height: 52,
                    child: ElevatedButton(
                      onPressed: auth.isLoading ? null : _login,
                      child: auth.isLoading
                          ? const SizedBox(
                              width: 24,
                              height: 24,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: Colors.white,
                              ),
                            )
                          : const Text(
                              '\u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062f\u062e\u0648\u0644',
                              style: TextStyle(fontSize: 16),
                            ),
                    ),
                  ),
                  const SizedBox(height: 16),

                  // Server config toggle
                  TextButton.icon(
                    onPressed: () =>
                        setState(() => _showServerConfig = !_showServerConfig),
                    icon: const Icon(Icons.settings, size: 18),
                    label: const Text('\u0625\u0639\u062f\u0627\u062f\u0627\u062a \u0627\u0644\u062e\u0627\u062f\u0645'),
                  ),

                  if (_showServerConfig) ...[
                    const SizedBox(height: 8),
                    TextFormField(
                      controller: _serverController,
                      textDirection: TextDirection.ltr,
                      decoration: const InputDecoration(
                        labelText: '\u0639\u0646\u0648\u0627\u0646 \u0627\u0644\u062e\u0627\u062f\u0645',
                        prefixIcon: Icon(Icons.dns_outlined),
                        hintText: 'http://192.168.1.100:5000/api',
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
