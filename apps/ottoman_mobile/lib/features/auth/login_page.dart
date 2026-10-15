import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'register_page.dart';

class LoginPage extends StatefulWidget {
  const LoginPage({super.key, required this.auth});

  final AuthController auth;

  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  Future<void> _forgotPassword() async {
    final t = AppLocalizations.of(context);
    final controller = TextEditingController();
    final messenger = ScaffoldMessenger.of(context);

    final email = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(t.forgotPassword),
        content: TextField(
          controller: controller,
          keyboardType: TextInputType.emailAddress,
          decoration: InputDecoration(labelText: t.email),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: Text(t.cancel),
          ),
          FilledButton(
            style: FilledButton.styleFrom(minimumSize: const Size(96, 44)),
            onPressed: () => Navigator.pop(ctx, controller.text.trim()),
            child: Text(t.save),
          ),
        ],
      ),
    );
    controller.dispose();
    if (email == null || !email.contains('@')) return;

    try {
      await widget.auth.forgotPassword(email);
    } on AppException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
      return;
    }
    // Same message whether or not the account exists.
    messenger.showSnackBar(SnackBar(content: Text(t.resetEmailSent)));
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Icon(Icons.content_cut, size: 48),
                  const SizedBox(height: 8),
                  Text(
                    t.appName,
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                      fontWeight: FontWeight.w800,
                      letterSpacing: 4,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    t.signInSubtitle,
                    textAlign: TextAlign.center,
                    style: TextStyle(color: scheme.onSurfaceVariant),
                  ),
                  const SizedBox(height: 32),
                  LoginForm(
                    auth: widget.auth,
                    onForgotPassword: _forgotPassword,
                    footer: TextButton(
                      key: const Key('go_register'),
                      onPressed: () => Navigator.of(context).push(
                        MaterialPageRoute<void>(
                          builder: (_) => RegisterPage(auth: widget.auth),
                        ),
                      ),
                      child: Text(t.noAccount),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
