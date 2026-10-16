import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'app.dart';
import 'features/shop/stripe_card_payment.dart';
import 'push/firebase_push_client.dart';

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

  final push = PushRegistration(
    devices: DevicesApi(auth.api),
    client: await _pushClient(),
  );
  // Forget this phone while the session is still valid, so the next person on it gets no messages of ours.
  auth.beforeLogout = push.stop;

  runApp(
    OttomanMobileApp(
      auth: auth,
      push: push,
      cardPayment: StripeCardPayment(auth.api),
    ),
  );
}

/// Push needs the Firebase configuration file in the build; without it the app simply runs without push.
Future<PushClient> _pushClient() async {
  try {
    await Firebase.initializeApp();
    return FirebasePushClient();
  } on Object {
    return const NoPushClient();
  }
}
