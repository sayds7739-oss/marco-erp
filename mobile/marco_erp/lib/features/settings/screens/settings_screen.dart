import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../core/auth/auth_provider.dart';
import '../../../core/api/api_client.dart';

class SettingsScreen extends StatelessWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();

    return Scaffold(
      appBar: AppBar(title: const Text('\u0627\u0644\u0625\u0639\u062f\u0627\u062f\u0627\u062a')),
      body: ListView(
        children: [
          // User info card
          Card(
            margin: const EdgeInsets.all(12),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  const CircleAvatar(
                    radius: 28,
                    child: Icon(Icons.person, size: 32),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          auth.userName,
                          style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                        ),
                        Text(
                          auth.roleName,
                          style: TextStyle(color: Colors.grey.shade600),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
          const Divider(),

          ListTile(
            leading: const Icon(Icons.dns_outlined),
            title: const Text('\u0625\u0639\u062f\u0627\u062f\u0627\u062a \u0627\u0644\u062e\u0627\u062f\u0645'),
            subtitle: const Text('\u062a\u063a\u064a\u064a\u0631 \u0639\u0646\u0648\u0627\u0646 \u0627\u0644\u0627\u062a\u0635\u0627\u0644 \u0628\u0627\u0644\u062e\u0627\u062f\u0645'),
            trailing: const Icon(Icons.chevron_left),
            onTap: () => _showServerDialog(context),
          ),
          ListTile(
            leading: const Icon(Icons.lock_outline),
            title: const Text('\u062a\u063a\u064a\u064a\u0631 \u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631'),
            trailing: const Icon(Icons.chevron_left),
            onTap: () => _showChangePasswordDialog(context),
          ),
          ListTile(
            leading: const Icon(Icons.info_outline),
            title: const Text('\u062d\u0648\u0644 \u0627\u0644\u062a\u0637\u0628\u064a\u0642'),
            subtitle: const Text('\u0627\u0644\u0625\u0635\u062f\u0627\u0631 1.0.0'),
          ),
          const Divider(),
          ListTile(
            leading: const Icon(Icons.logout, color: Colors.red),
            title: const Text('\u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062e\u0631\u0648\u062c', style: TextStyle(color: Colors.red)),
            onTap: () async {
              final confirmed = await showDialog<bool>(
                context: context,
                builder: (ctx) => AlertDialog(
                  title: const Text('\u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062e\u0631\u0648\u062c'),
                  content: const Text('\u0647\u0644 \u0623\u0646\u062a \u0645\u062a\u0623\u0643\u062f \u0645\u0646 \u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062e\u0631\u0648\u062c\u061f'),
                  actions: [
                    TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('\u0625\u0644\u063a\u0627\u0621')),
                    TextButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('\u062a\u0623\u0643\u064a\u062f')),
                  ],
                ),
              );
              if (confirmed == true && context.mounted) {
                await auth.logout();
                if (context.mounted) {
                  Navigator.of(context).popUntil((route) => route.isFirst);
                }
              }
            },
          ),
        ],
      ),
    );
  }

  void _showServerDialog(BuildContext context) {
    final controller = TextEditingController(text: 'http://10.0.2.2:5000/api');
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('\u0639\u0646\u0648\u0627\u0646 \u0627\u0644\u062e\u0627\u062f\u0645'),
        content: TextField(
          controller: controller,
          textDirection: TextDirection.ltr,
          decoration: const InputDecoration(
            hintText: 'http://192.168.1.100:5000/api',
          ),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('\u0625\u0644\u063a\u0627\u0621')),
          TextButton(
            onPressed: () {
              context.read<ApiClient>().updateBaseUrl(controller.text);
              Navigator.pop(ctx);
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(content: Text('\u062a\u0645 \u062a\u062d\u062f\u064a\u062b \u0639\u0646\u0648\u0627\u0646 \u0627\u0644\u062e\u0627\u062f\u0645')),
              );
            },
            child: const Text('\u062d\u0641\u0638'),
          ),
        ],
      ),
    );
  }

  void _showChangePasswordDialog(BuildContext context) {
    final currentPw = TextEditingController();
    final newPw = TextEditingController();
    final confirmPw = TextEditingController();

    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('\u062a\u063a\u064a\u064a\u0631 \u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              controller: currentPw,
              obscureText: true,
              decoration: const InputDecoration(labelText: '\u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631 \u0627\u0644\u062d\u0627\u0644\u064a\u0629'),
            ),
            const SizedBox(height: 8),
            TextField(
              controller: newPw,
              obscureText: true,
              decoration: const InputDecoration(labelText: '\u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631 \u0627\u0644\u062c\u062f\u064a\u062f\u0629'),
            ),
            const SizedBox(height: 8),
            TextField(
              controller: confirmPw,
              obscureText: true,
              decoration: const InputDecoration(labelText: '\u062a\u0623\u0643\u064a\u062f \u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631'),
            ),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('\u0625\u0644\u063a\u0627\u0621')),
          TextButton(
            onPressed: () async {
              final error = await context.read<AuthProvider>().changePassword(
                    currentPw.text,
                    newPw.text,
                    confirmPw.text,
                  );
              if (ctx.mounted) {
                Navigator.pop(ctx);
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(content: Text(error ?? '\u062a\u0645 \u062a\u063a\u064a\u064a\u0631 \u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631 \u0628\u0646\u062c\u0627\u062d')),
                );
              }
            },
            child: const Text('\u062a\u063a\u064a\u064a\u0631'),
          ),
        ],
      ),
    );
  }
}
