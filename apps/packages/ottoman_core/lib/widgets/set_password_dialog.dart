import 'package:flutter/material.dart';

import '../network/app_exception.dart';
import '../l10n/app_localizations.dart';
import '../validation/validators.dart';
import 'dialog_header.dart';

/// The admin picks a new password for [name] ("Izmijeni lozinku"): two fields, no old password (it is not the
/// admin's to know). [save] sends it; the dialog stays open with the server's message when it is refused. True when
/// the password was changed.
Future<bool> showSetPasswordDialog(
  BuildContext context, {
  required String name,
  required Future<void> Function(String newPassword) save,
}) async {
  final done = await showDialog<bool>(
    context: context,
    builder: (_) => _SetPasswordDialog(name: name, save: save),
  );
  return done == true;
}

// Owns its controllers, so they are disposed only after the dialog is really gone.
class _SetPasswordDialog extends StatefulWidget {
  const _SetPasswordDialog({required this.name, required this.save});

  final String name;
  final Future<void> Function(String newPassword) save;

  @override
  State<_SetPasswordDialog> createState() => _SetPasswordDialogState();
}

class _SetPasswordDialogState extends State<_SetPasswordDialog> {
  final _form = GlobalKey<FormState>();
  final _next = TextEditingController();
  final _repeat = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _next.dispose();
    _repeat.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.save(_next.text);
      if (mounted) Navigator.pop(context, true);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = e.detail);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AlertDialog(
      title: DialogHeader(t.setPasswordTitle),
      content: SizedBox(
        width: 400,
        child: Form(
          key: _form,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(t.setPasswordHint(widget.name)),
                const SizedBox(height: 16),
                TextFormField(
                  key: const Key('setpw_new'),
                  controller: _next,
                  obscureText: true,
                  decoration: InputDecoration(
                    labelText: t.newPassword,
                    helperText: t.passwordRules,
                    helperMaxLines: 2,
                  ),
                  validator: (v) => Validators.newPassword(t, v),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  key: const Key('setpw_repeat'),
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
                      key: const Key('setpw_error'),
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
          onPressed: _busy ? null : () => Navigator.pop(context, false),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: const Key('setpw_save'),
          style: FilledButton.styleFrom(minimumSize: const Size(120, 44)),
          onPressed: _busy ? null : _submit,
          child: Text(t.save),
        ),
      ],
    );
  }
}
