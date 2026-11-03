import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'features/auth/login_page.dart';
import 'features/shell/mobile_shell.dart';

class OttomanMobileApp extends StatelessWidget {
  const OttomanMobileApp({
    super.key,
    required this.auth,
    this.cardPayment = const UnavailableCardPayment(),
    this.push,
  });

  final AuthController auth;
  final CardPaymentLauncher cardPayment;
  final PushRegistration? push;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      onGenerateTitle: (c) => AppLocalizations.of(c).appName,
      // One look for the whole app: black, white and amber, whatever the phone is set to.
      theme: OttomanTheme.barbershop(),
      darkTheme: OttomanTheme.barbershop(),
      themeMode: ThemeMode.dark,
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: AppLocalizations.supportedLocales,
      locale: const Locale('bs'),
      debugShowCheckedModeBanner: false,
      home: AuthGate(
        auth: auth,
        signedIn: (_) =>
            MobileShell(auth: auth, cardPayment: cardPayment, push: push),
        signedOut: (_) => LoginPage(auth: auth),
      ),
    );
  }
}
