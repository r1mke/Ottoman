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

    var haveCode = false;
    final email = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(t.forgotPassword),
        content: TextField(
          key: const Key('forgot_email'),
          controller: controller,
          keyboardType: TextInputType.emailAddress,
          decoration: InputDecoration(labelText: t.email),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: Text(t.cancel),
          ),
          TextButton(
            key: const Key('have_code'),
            onPressed: () {
              haveCode = true;
              Navigator.pop(ctx, controller.text.trim());
            },
            child: Text(t.haveCode),
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

    if (!haveCode) {
      try {
        await widget.auth.forgotPassword(email);
      } on AppException catch (e) {
        messenger.showSnackBar(SnackBar(content: Text(e.message)));
        return;
      }
      // Same message whether or not the account exists.
      messenger.showSnackBar(SnackBar(content: Text(t.resetEmailSent)));
    }
    if (!mounted) return;
    await _enterCode(email);
  }

  /// Second step: the code from the e-mail plus a new password.
  Future<void> _enterCode(String email) async {
    final t = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final code = TextEditingController();
    final password = TextEditingController();

    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) {
        String? error;
        return StatefulBuilder(
          builder: (ctx, setState) => AlertDialog(
            title: Text(t.haveCode),
            content: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextField(
                  key: const Key('reset_code'),
                  controller: code,
                  decoration: InputDecoration(labelText: t.resetCode),
                ),
                const SizedBox(height: 12),
                TextField(
                  key: const Key('reset_password'),
                  controller: password,
                  obscureText: true,
                  decoration: InputDecoration(
                    labelText: t.newPassword,
                    helperText: t.passwordRules,
                    helperMaxLines: 2,
                  ),
                ),
                if (error != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(
                      error!,
                      key: const Key('reset_error'),
                      style: TextStyle(color: Theme.of(ctx).colorScheme.error),
                    ),
                  ),
              ],
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(ctx, false),
                child: Text(t.cancel),
              ),
              FilledButton(
                key: const Key('reset_submit'),
                style: FilledButton.styleFrom(minimumSize: const Size(96, 44)),
                onPressed: () async {
                  try {
                    await widget.auth.resetPassword(
                      email: email,
                      token: code.text.trim(),
                      newPassword: password.text,
                    );
                    if (ctx.mounted) Navigator.pop(ctx, true);
                  } on AppException catch (e) {
                    // The server says what is wrong with the password; a bad code gets a generic message.
                    final detail = e.errors.values.expand((x) => x).join('\n');
                    setState(
                      () => error = detail.isEmpty ? t.resetFailed : detail,
                    );
                  }
                },
                child: Text(t.save),
              ),
            ],
          ),
        );
      },
    );
    code.dispose();
    password.dispose();
    if (ok == true) {
      messenger.showSnackBar(SnackBar(content: Text(t.passwordChanged)));
    }
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
