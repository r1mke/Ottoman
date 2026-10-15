import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:ottoman_core/ottoman_core.dart';

void main() => runApp(const OttomanApp());

class OttomanApp extends StatelessWidget {
  const OttomanApp({super.key});

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
      home: const _Splash(),
    );
  }
}

class _Splash extends StatelessWidget {
  const _Splash();

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Scaffold(
      body: Center(
        child: Text(
          t.appName,
          style: Theme.of(context).textTheme.displaySmall
              ?.copyWith(fontWeight: FontWeight.w800, letterSpacing: 4),
        ),
      ),
    );
  }
}
