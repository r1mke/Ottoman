import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'features/auth/login_page.dart';
import 'features/shell/desktop_shell.dart';

class OttomanDesktopApp extends StatelessWidget {
  const OttomanDesktopApp({
    super.key,
    required this.auth,
    required this.profiles,
  });

  final AuthController auth;
  final SavedProfiles profiles;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      onGenerateTitle: (c) => AppLocalizations.of(c).appName,
      theme: WorkspaceTheme.build(fontFamily: 'Manrope'),
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
        signedIn: (_) => DesktopShell(auth: auth),
        signedOut: (_) => LoginPage(auth: auth, profiles: profiles),
      ),
    );
  }
}
