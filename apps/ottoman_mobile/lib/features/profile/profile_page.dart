import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

class ProfilePage extends StatefulWidget {
  const ProfilePage({super.key, required this.auth});

  final AuthController auth;

  @override
  State<ProfilePage> createState() => _ProfilePageState();
}

class _ProfilePageState extends State<ProfilePage> {
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
      final detail = e.errors.values.expand((x) => x).join('\n');
      messenger.showSnackBar(
        SnackBar(content: Text(detail.isEmpty ? e.message : detail)),
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final user = widget.auth.user!;
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text(
          user.email,
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
                controller: _first,
                decoration: InputDecoration(labelText: t.firstName),
                validator: (v) =>
                    (v == null || v.trim().isEmpty) ? t.requiredField : null,
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _last,
                decoration: InputDecoration(labelText: t.lastName),
                validator: (v) =>
                    (v == null || v.trim().isEmpty) ? t.requiredField : null,
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _phone,
                keyboardType: TextInputType.phone,
                decoration: InputDecoration(labelText: t.phone),
              ),
            ],
          ),
        ),
        const SizedBox(height: 24),
        FilledButton(
          onPressed: _busy ? null : _save,
          child: Text(t.saveChanges),
        ),
        const SizedBox(height: 12),
        OutlinedButton.icon(
          key: const Key('logout'),
          onPressed: widget.auth.logout,
          icon: const Icon(Icons.logout),
          label: Text(t.logout),
        ),
      ],
    );
  }
}
