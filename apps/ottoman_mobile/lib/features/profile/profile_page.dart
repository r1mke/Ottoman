import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'legal_pages.dart';
import 'profile_photo.dart';

/// "Račun": who is signed in, then the settings in groups (personal data, about us, sign out).
class ProfilePage extends StatelessWidget {
  const ProfilePage({
    super.key,
    required this.auth,
    this.photoChooser = const GalleryPhotoChooser(),
  });

  final AuthController auth;
  final PhotoChooser photoChooser;

  void _open(BuildContext context, Widget page) =>
      Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => page));

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    final user = auth.user;
    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
      children: [
        Row(
          children: [
            ProfilePhoto(auth: auth, chooser: photoChooser),
            const SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    user?.fullName ?? '',
                    key: const Key('account_name'),
                    style: Theme.of(context).textTheme.titleLarge
                        ?.copyWith(fontWeight: FontWeight.w800),
                  ),
                  Text(
                    user?.email ?? '',
                    key: const Key('account_email'),
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(color: scheme.onSurfaceVariant),
                  ),
                ],
              ),
            ),
          ],
        ),
        _Group(
          title: t.groupPersonalData,
          rows: [
            _Row(
              key: const Key('row_edit_profile'),
              icon: Icons.edit_outlined,
              label: t.editProfile,
              onTap: () => _open(context, _EditProfilePage(auth: auth)),
            ),
            _Row(
              key: const Key('row_manage_account'),
              icon: Icons.settings_outlined,
              label: t.manageAccount,
              onTap: () => _open(context, _ManageAccountPage(auth: auth)),
            ),
          ],
        ),
        _Group(
          title: t.groupAboutUs,
          rows: [
            _Row(
              key: const Key('row_privacy'),
              icon: Icons.lock_outline,
              label: t.privacyPolicy,
              onTap: () => _open(
                context,
                LegalPage(title: t.privacyPolicy, sections: privacySections),
              ),
            ),
            _Row(
              key: const Key('row_terms'),
              icon: Icons.shield_outlined,
              label: t.termsOfUse,
              onTap: () => _open(
                context,
                LegalPage(title: t.termsOfUse, sections: termsSections),
              ),
            ),
            _Row(
              key: const Key('row_about_app'),
              icon: Icons.info_outline,
              label: t.aboutApp,
              onTap: () => _open(
                context,
                LegalPage(title: t.aboutApp, sections: aboutAppSections),
              ),
            ),
          ],
        ),
        _Group(
          title: t.groupAccount,
          rows: [
            _Row(
              key: const Key('logout'),
              icon: Icons.logout,
              label: t.logout,
              color: scheme.error,
              onTap: auth.logout,
            ),
          ],
        ),
      ],
    );
  }
}

/// A heading and, under it, one rounded card holding its rows.
class _Group extends StatelessWidget {
  const _Group({required this.title, required this.rows});

  final String title;
  final List<Widget> rows;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(2, 28, 0, 10),
          child: Text(title, style: const TextStyle(fontSize: 17)),
        ),
        // A Material (not a coloured box) so the rows' ink splashes show.
        Material(
          color: scheme.surfaceContainer,
          clipBehavior: Clip.antiAlias,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(24),
            side: BorderSide(color: scheme.outlineVariant),
          ),
          child: Column(children: rows),
        ),
      ],
    );
  }
}

class _Row extends StatelessWidget {
  const _Row({
    super.key,
    required this.icon,
    required this.label,
    required this.onTap,
    this.color,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  /// Overrides the usual colours (the red of "Odjava").
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 20),
        child: Row(
          children: [
            Icon(icon, color: color ?? scheme.onSurfaceVariant),
            const SizedBox(width: 18),
            Expanded(
              child: Text(label, style: TextStyle(fontSize: 17, color: color)),
            ),
            Icon(Icons.chevron_right, color: color ?? scheme.onSurfaceVariant),
          ],
        ),
      ),
    );
  }
}

class _EditProfilePage extends StatelessWidget {
  const _EditProfilePage({required this.auth});

  final AuthController auth;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(t.editProfile)),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [ProfileForm(auth: auth)],
      ),
    );
  }
}

class _ManageAccountPage extends StatelessWidget {
  const _ManageAccountPage({required this.auth});

  final AuthController auth;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    return Scaffold(
      appBar: AppBar(title: Text(t.manageAccount)),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Material(
            color: scheme.surfaceContainer,
            clipBehavior: Clip.antiAlias,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(24),
              side: BorderSide(color: scheme.outlineVariant),
            ),
            child: Column(
              children: [
                ListTile(
                  leading: const Icon(Icons.alternate_email),
                  title: Text(t.email),
                  subtitle: Text(auth.user?.email ?? ''),
                ),
                const Divider(),
                ListTile(
                  key: const Key('row_change_password'),
                  leading: const Icon(Icons.lock_outline),
                  title: Text(t.changePassword),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => showChangePasswordDialog(context, auth),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
