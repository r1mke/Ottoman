import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

class ProfilePage extends StatelessWidget {
  const ProfilePage({super.key, required this.auth});

  final AuthController auth;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        ProfileForm(auth: auth),
        const SizedBox(height: 12),
        OutlinedButton.icon(
          key: const Key('logout'),
          onPressed: auth.logout,
          icon: const Icon(Icons.logout),
          label: Text(t.logout),
        ),
      ],
    );
  }
}
