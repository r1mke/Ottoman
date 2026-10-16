import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'password_reset_dialogs.dart';
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
    final messenger = ScaffoldMessenger.of(context);

    final answer = await showDialog<({String email, bool haveCode})>(
      context: context,
      builder: (_) => const ForgotPasswordDialog(),
    );
    if (answer == null || !answer.email.contains('@')) return;

    if (!answer.haveCode) {
      try {
        await widget.auth.forgotPassword(answer.email);
      } on AppException catch (e) {
        messenger.showSnackBar(SnackBar(content: Text(e.message)));
        return;
      }
      // Same message whether or not the account exists.
      messenger.showSnackBar(SnackBar(content: Text(t.resetEmailSent)));
    }
    if (!mounted) return;

    final changed = await showDialog<bool>(
      context: context,
      builder: (_) =>
          ResetPasswordDialog(auth: widget.auth, email: answer.email),
    );
    if (changed == true) {
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
