import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

class RegisterPage extends StatefulWidget {
  const RegisterPage({super.key, required this.auth});

  final AuthController auth;

  @override
  State<RegisterPage> createState() => _RegisterPageState();
}

class _RegisterPageState extends State<RegisterPage> {
  final _form = GlobalKey<FormState>();
  final _first = TextEditingController();
  final _last = TextEditingController();
  final _email = TextEditingController();
  final _phone = TextEditingController();
  final _password = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    for (final c in [_first, _last, _email, _phone, _password]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    final t = AppLocalizations.of(context);
    final navigator = Navigator.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.auth.register(
        firstName: _first.text.trim(),
        lastName: _last.text.trim(),
        email: _email.text.trim(),
        phone: _phone.text.trim().isEmpty ? null : _phone.text.trim(),
        password: _password.text,
      );
      messenger.showSnackBar(SnackBar(content: Text(t.registrationDone)));
      navigator.pop();
    } on AppException catch (e) {
      // Server validation errors (e.g. password policy) arrive per field.
      final detail = e.errors.values.expand((x) => x).join('\n');
      if (mounted) setState(() => _error = detail.isEmpty ? e.message : detail);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  String? _required(String? v) {
    final t = AppLocalizations.of(context);
    return (v == null || v.trim().isEmpty) ? t.requiredField : null;
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(t.register)),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Form(
            key: _form,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextFormField(
                  key: const Key('reg_first'),
                  controller: _first,
                  textInputAction: TextInputAction.next,
                  decoration: InputDecoration(labelText: t.firstName),
                  validator: _required,
                ),
                const SizedBox(height: 16),
                TextFormField(
                  key: const Key('reg_last'),
                  controller: _last,
                  textInputAction: TextInputAction.next,
                  decoration: InputDecoration(labelText: t.lastName),
                  validator: _required,
                ),
                const SizedBox(height: 16),
                TextFormField(
                  key: const Key('reg_email'),
                  controller: _email,
                  keyboardType: TextInputType.emailAddress,
                  textInputAction: TextInputAction.next,
                  decoration: InputDecoration(labelText: t.email),
                  validator: (v) =>
                      (v == null || !v.contains('@')) ? t.invalidEmail : null,
                ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: _phone,
                  keyboardType: TextInputType.phone,
                  textInputAction: TextInputAction.next,
                  decoration: InputDecoration(labelText: t.phone),
                  validator: (v) => Validators.phone(t, v),
                ),
                const SizedBox(height: 16),
                TextFormField(
                  key: const Key('reg_password'),
                  controller: _password,
                  obscureText: true,
                  decoration: InputDecoration(
                    labelText: t.password,
                    helperText: t.passwordRules,
                    helperMaxLines: 2,
                  ),
                  validator: _required,
                ),
                if (_error != null) ...[
                  const SizedBox(height: 12),
                  Text(
                    _error!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ],
                const SizedBox(height: 24),
                FilledButton(
                  key: const Key('reg_submit'),
                  onPressed: _busy ? null : _submit,
                  child: _busy
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : Text(t.createAccount),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
