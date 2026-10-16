import 'package:flutter/material.dart';

import '../auth/auth_controller.dart';
import '../l10n/app_localizations.dart';
import '../network/app_exception.dart';

String _detail(AppException e) {
  final detail = e.errors.values.expand((x) => x).join('\n');
  return detail.isEmpty ? e.message : detail;
}

/// The signed-in person's own details (name, phone) and the way to change the password.
/// Shared by the mobile and the desktop app; the e-mail is shown but cannot be changed here.
class ProfileForm extends StatefulWidget {
  const ProfileForm({super.key, required this.auth});

  final AuthController auth;

  @override
  State<ProfileForm> createState() => _ProfileFormState();
}

class _ProfileFormState extends State<ProfileForm> {
  late final _first = TextEditingController(text: widget.auth.user!.firstName);
  late final _last = TextEditingController(text: widget.auth.user!.lastName);
  late final _phone = TextEditingController(
    text: widget.auth.user!.phone ?? '',
  );
  final _form = GlobalKey<FormState>();
  bool _busy = false;

  @override
  void dispose() {
    _first.dispose();
    _last.dispose();
    _phone.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    final t = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _busy = true);
    try {
      await widget.auth.api.put<dynamic>(
        '/api/v1/me',
        body: {
          'firstName': _first.text.trim(),
          'lastName': _last.text.trim(),
          'phone': _phone.text.trim().isEmpty ? null : _phone.text.trim(),
        },
      );
      await widget.auth.reloadUser();
      messenger.showSnackBar(SnackBar(content: Text(t.profileSaved)));
    } on AppException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(_detail(e))));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final user = widget.auth.user!;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          user.email,
          key: const Key('profile_email'),
          style: TextStyle(
            color: Theme.of(context).colorScheme.onSurfaceVariant,
          ),
        ),
        const SizedBox(height: 16),
        Form(
          key: _form,
          child: Column(
            children: [
              TextFormField(
                key: const Key('profile_first'),
                controller: _first,
                decoration: InputDecoration(labelText: t.firstName),
                validator: (v) =>
                    (v == null || v.trim().isEmpty) ? t.requiredField : null,
              ),
              const SizedBox(height: 16),
              TextFormField(
                key: const Key('profile_last'),
                controller: _last,
                decoration: InputDecoration(labelText: t.lastName),
                validator: (v) =>
                    (v == null || v.trim().isEmpty) ? t.requiredField : null,
              ),
              const SizedBox(height: 16),
              TextFormField(
                key: const Key('profile_phone'),
                controller: _phone,
                keyboardType: TextInputType.phone,
                decoration: InputDecoration(labelText: t.phone),
              ),
            ],
          ),
        ),
        const SizedBox(height: 24),
        FilledButton(
          key: const Key('profile_save'),
          style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
          onPressed: _busy ? null : _save,
          child: Text(t.saveChanges),
        ),
        const SizedBox(height: 12),
        OutlinedButton.icon(
          key: const Key('change_password'),
          style: OutlinedButton.styleFrom(
            minimumSize: const Size.fromHeight(48),
          ),
          onPressed: () => showChangePasswordDialog(context, widget.auth),
          icon: const Icon(Icons.lock_outline),
          label: Text(t.changePassword),
        ),
      ],
    );
  }
}

/// Asks for the current and a new password. On success the account is signed out everywhere (the server ends
/// all sessions), so the person returns to the sign-in screen with a message.
Future<void> showChangePasswordDialog(
  BuildContext context,
  AuthController auth,
) => showDialog<void>(
  context: context,
  builder: (_) => _ChangePasswordDialog(auth: auth),
);

// A widget that owns its controllers, so they are disposed only after the dialog is really gone.
class _ChangePasswordDialog extends StatefulWidget {
  const _ChangePasswordDialog({required this.auth});

  final AuthController auth;

  @override
  State<_ChangePasswordDialog> createState() => _ChangePasswordDialogState();
}

class _ChangePasswordDialogState extends State<_ChangePasswordDialog> {
  final _form = GlobalKey<FormState>();
  final _current = TextEditingController();
  final _next = TextEditingController();
  final _repeat = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _current.dispose();
    _next.dispose();
    _repeat.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    final t = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final navigator = Navigator.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.auth.changePassword(_current.text, _next.text);
      navigator.pop();
      messenger.showSnackBar(SnackBar(content: Text(t.passwordChanged)));
    } on AppException catch (e) {
      if (mounted) setState(() => _error = _detail(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    String? required(String? v) =>
        (v == null || v.isEmpty) ? t.requiredField : null;
    return AlertDialog(
      title: Text(t.changePassword),
      content: SizedBox(
        width: 380,
        child: Form(
          key: _form,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextFormField(
                  key: const Key('pw_current'),
                  controller: _current,
                  obscureText: true,
                  decoration: InputDecoration(labelText: t.currentPassword),
                  validator: required,
                ),
                const SizedBox(height: 12),
                TextFormField(
                  key: const Key('pw_new'),
                  controller: _next,
                  obscureText: true,
                  decoration: InputDecoration(
                    labelText: t.newPassword,
                    helperText: t.passwordRules,
                    helperMaxLines: 2,
                  ),
                  validator: required,
                ),
                const SizedBox(height: 12),
                TextFormField(
                  key: const Key('pw_repeat'),
                  controller: _repeat,
                  obscureText: true,
                  decoration: InputDecoration(labelText: t.repeatPassword),
                  validator: (v) {
                    if (v == null || v.isEmpty) return t.requiredField;
                    return v == _next.text ? null : t.passwordsDoNotMatch;
                  },
                ),
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 12),
                    child: Text(
                      _error!,
                      key: const Key('pw_error'),
                      style: TextStyle(
                        color: Theme.of(context).colorScheme.error,
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _busy ? null : () => Navigator.pop(context),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: const Key('pw_submit'),
          style: FilledButton.styleFrom(minimumSize: const Size(120, 44)),
          onPressed: _busy ? null : _submit,
          child: Text(t.save),
        ),
      ],
    );
  }
}
