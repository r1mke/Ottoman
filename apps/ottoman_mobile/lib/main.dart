import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'app.dart';
import 'features/shop/stripe_card_payment.dart';

// 10.0.2.2 is the host machine as seen from the Android emulator.
const _apiUrl = String.fromEnvironment(
  'API_URL',
  defaultValue: 'http://10.0.2.2:5080',
);

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final auth = AuthController(
    baseUrl: _apiUrl,
    storage: SecureTokenStorage(keyPrefix: 'ottoman.mobile'),
    allowedRoles: {Roles.client, Roles.employee},
  );
  await auth.restore();

  runApp(
    OttomanMobileApp(auth: auth, cardPayment: StripeCardPayment(auth.api)),
  );
}
