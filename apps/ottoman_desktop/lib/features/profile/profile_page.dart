import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// The signed-in admin's or employee's own account: name, phone and password.
class ProfilePage extends StatelessWidget {
  const ProfilePage({super.key, required this.auth});

  final AuthController auth;

  @override
  Widget build(BuildContext context) => SingleChildScrollView(
    padding: const EdgeInsets.fromLTRB(36, 0, 36, 24),
    child: Align(
      alignment: Alignment.topLeft,
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 460),
        child: Card(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: ProfileForm(auth: auth),
          ),
        ),
      ),
    ),
  );
}
