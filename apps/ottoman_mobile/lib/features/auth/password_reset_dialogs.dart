import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// Step 1: ask for the e-mail. Pops with the address and whether the person already has a code.
class ForgotPasswordDialog extends StatefulWidget {
  const ForgotPasswordDialog({super.key});

  @override
  State<ForgotPasswordDialog> createState() => _ForgotPasswordDialogState();
}

class _ForgotPasswordDialogState extends State<ForgotPasswordDialog> {
  final _email = TextEditingController();

  @override
  void dispose() {
    _email.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AlertDialog(
      title: DialogHeader(t.forgotPassword),
      content: TextField(
        key: const Key('forgot_email'),
        controller: _email,
        keyboardType: TextInputType.emailAddress,
        decoration: InputDecoration(labelText: t.email),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: Text(t.cancel),
        ),
        TextButton(
          key: const Key('have_code'),
          onPressed: () => Navigator.pop(context, (
            email: _email.text.trim(),
            haveCode: true,
          )),
          child: Text(t.haveCode),
        ),
        FilledButton(
          style: FilledButton.styleFrom(minimumSize: const Size(96, 44)),
          onPressed: () => Navigator.pop(context, (
            email: _email.text.trim(),
            haveCode: false,
          )),
          child: Text(t.save),
        ),
      ],
    );
  }
}

/// Step 2: the code from the e-mail plus a new password. Pops true once the server accepted the reset.
class ResetPasswordDialog extends StatefulWidget {
  const ResetPasswordDialog({
    super.key,
    required this.auth,
    required this.email,
  });

  final AuthController auth;
  final String email;

  @override
  State<ResetPasswordDialog> createState() => _ResetPasswordDialogState();
}

class _ResetPasswordDialogState extends State<ResetPasswordDialog> {
  final _code = TextEditingController();
  final _password = TextEditingController();
  String? _error;
  bool _busy = false;

  @override
  void dispose() {
    _code.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final t = AppLocalizations.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.auth.resetPassword(
        email: widget.email,
        token: _code.text.trim(),
        newPassword: _password.text,
      );
      if (mounted) Navigator.pop(context, true);
    } on AppException catch (e) {
      // The server says what is wrong with the password; a bad code gets one generic message.
      final detail = e.errors.values.expand((x) => x).join('\n');
      if (mounted) {
        setState(() {
          _busy = false;
          _error = detail.isEmpty ? t.resetFailed : detail;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AlertDialog(
      title: DialogHeader(t.haveCode),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              key: const Key('reset_code'),
              controller: _code,
              decoration: InputDecoration(labelText: t.resetCode),
            ),
            const SizedBox(height: 12),
            TextField(
              key: const Key('reset_password'),
              controller: _password,
              obscureText: true,
              decoration: InputDecoration(
                labelText: t.newPassword,
                helperText: t.passwordRules,
                helperMaxLines: 2,
              ),
            ),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Text(
                  _error!,
                  key: const Key('reset_error'),
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context, false),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: const Key('reset_submit'),
          style: FilledButton.styleFrom(minimumSize: const Size(96, 44)),
          onPressed: _busy ? null : _submit,
          child: Text(t.save),
        ),
      ],
    );
  }
}
