import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:window_manager/window_manager.dart';

import 'app.dart';

const _apiUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: 'http://localhost:5080',
);

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // The sidebar, tables and dialogs need room: below this size the layout would overflow.
  await windowManager.ensureInitialized();
  await windowManager.setMinimumSize(const Size(1024, 680));

  // The desktop app is for staff only; clients use the mobile app.
  final auth = AuthController(
    baseUrl: _apiUrl,
    storage: SecureTokenStorage(keyPrefix: 'ottoman.desktop'),
    allowedRoles: {Roles.admin, Roles.employee},
  );
  await auth.restore();

  // The cards on the sign-in screen. A session restored from the stored token is remembered too, so signing out
  // later still leaves a card to pick.
  final profiles = SavedProfiles(
    SecureSavedProfilesStore(keyPrefix: 'ottoman.desktop'),
  );
  await profiles.load();
  final user = auth.user;
  if (user != null) await profiles.remember(user);

  runApp(OttomanDesktopApp(auth: auth, profiles: profiles));
}
