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
  });

  final AuthController auth;
  final CardPaymentLauncher cardPayment;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      onGenerateTitle: (c) => AppLocalizations.of(c).appName,
      theme: OttomanTheme.light(),
      darkTheme: OttomanTheme.dark(),
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
        signedIn: (_) => MobileShell(auth: auth, cardPayment: cardPayment),
        signedOut: (_) => LoginPage(auth: auth),
      ),
    );
  }
}
