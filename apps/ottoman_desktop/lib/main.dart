import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'app.dart';

const _apiUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: 'http://localhost:5080',
);

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // The desktop app is for staff only; clients use the mobile app.
  final auth = AuthController(
    baseUrl: _apiUrl,
    storage: SecureTokenStorage(keyPrefix: 'ottoman.desktop'),
    allowedRoles: {Roles.admin, Roles.employee},
  );
  await auth.restore();

  runApp(OttomanDesktopApp(auth: auth));
}
