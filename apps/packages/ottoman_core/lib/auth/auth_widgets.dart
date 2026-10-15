import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import '../network/app_exception.dart';
import 'auth_controller.dart';

/// Chooses what to show from the session state.
class AuthGate extends StatelessWidget {
  const AuthGate({
    super.key,
    required this.auth,
    required this.signedIn,
    required this.signedOut,
    this.loading = const Scaffold(
      body: Center(child: CircularProgressIndicator()),
    ),
  });

  final AuthController auth;
  final WidgetBuilder signedIn;
  final WidgetBuilder signedOut;
  final Widget loading;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: auth,
      builder: (context, _) => switch (auth.status) {
        AuthStatus.unknown => loading,
        AuthStatus.authenticated => signedIn(context),
        AuthStatus.unauthenticated => signedOut(context),
      },
    );
  }
}

/// Email + password form with validation, busy state and server error display.
class LoginForm extends StatefulWidget {
  const LoginForm({
    super.key,
    required this.auth,
    this.onForgotPassword,
    this.footer,
  });

  final AuthController auth;
  final VoidCallback? onForgotPassword;
  final Widget? footer;

  @override
  State<LoginForm> createState() => _LoginFormState();
}

class _LoginFormState extends State<LoginForm> {
  final _form = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _password = TextEditingController();
  bool _busy = false;
  bool _obscure = true;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.auth.login(_email.text.trim(), _password.text);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Form(
      key: _form,
      child: AutofillGroup(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            TextFormField(
              key: const Key('login_email'),
              controller: _email,
              keyboardType: TextInputType.emailAddress,
              autofillHints: const [AutofillHints.username],
              textInputAction: TextInputAction.next,
              decoration: InputDecoration(labelText: t.email),
              validator: (v) =>
                  (v == null || !v.contains('@')) ? t.invalidEmail : null,
            ),
            const SizedBox(height: 16),
            TextFormField(
              key: const Key('login_password'),
              controller: _password,
              obscureText: _obscure,
              autofillHints: const [AutofillHints.password],
              onFieldSubmitted: (_) => _submit(),
              decoration: InputDecoration(
                labelText: t.password,
                suffixIcon: IconButton(
                  icon: Icon(
                    _obscure
                        ? Icons.visibility_outlined
                        : Icons.visibility_off_outlined,
                  ),
                  onPressed: () => setState(() => _obscure = !_obscure),
                ),
              ),
              validator: (v) =>
                  (v == null || v.isEmpty) ? t.requiredField : null,
            ),
            if (_error != null) ...[
              const SizedBox(height: 12),
              Text(
                _error!,
                key: const Key('login_error'),
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ],
            const SizedBox(height: 20),
            FilledButton(
              key: const Key('login_submit'),
              onPressed: _busy ? null : _submit,
              child: _busy
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : Text(t.login),
            ),
            if (widget.onForgotPassword != null)
              TextButton(
                onPressed: widget.onForgotPassword,
                child: Text(t.forgotPassword),
              ),
            if (widget.footer != null) widget.footer!,
          ],
        ),
      ),
    );
  }
}
