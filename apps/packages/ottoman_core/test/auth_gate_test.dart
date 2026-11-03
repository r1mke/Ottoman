import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'auth_controller_test.dart' show FakeAdapter, build, sessionJson;

void main() {
  testWidgets('signing out closes every page opened on top of the home', (
    tester,
  ) async {
    final auth = build(
      FakeAdapter((_) => (200, sessionJson())),
      MemoryTokenStorage(),
    );
    await tester.runAsync(() => auth.login('a@b.com', 'pw'));
    await tester.pumpWidget(
      MaterialApp(
        home: AuthGate(
          auth: auth,
          signedIn: (context) => Scaffold(
            body: TextButton(
              key: const Key('open'),
              onPressed: () => Navigator.of(context).push(
                MaterialPageRoute<void>(
                  builder: (_) => const Scaffold(body: Text('checkout page')),
                ),
              ),
              child: const Text('home'),
            ),
          ),
          signedOut: (_) => const Scaffold(body: Text('login page')),
        ),
      ),
    );
    await tester.tap(find.byKey(const Key('open')));
    await tester.pumpAndSettle();
    expect(find.text('checkout page'), findsOneWidget);

    await tester.runAsync(() => auth.logout());
    await tester.pumpAndSettle();

    expect(find.text('checkout page'), findsNothing);
    expect(find.text('login page'), findsOneWidget);
  });

  testWidgets('signing in does not close anything', (tester) async {
    final auth = build(
      FakeAdapter((_) => (200, sessionJson())),
      MemoryTokenStorage(),
    );
    await tester.runAsync(() => auth.restore());
    await tester.pumpWidget(
      MaterialApp(
        home: AuthGate(
          auth: auth,
          signedIn: (_) => const Scaffold(body: Text('home')),
          signedOut: (_) => const Scaffold(body: Text('login page')),
        ),
      ),
    );
    expect(find.text('login page'), findsOneWidget);
    await tester.runAsync(() => auth.login('a@b.com', 'pw'));
    await tester.pumpAndSettle();
    expect(find.text('home'), findsOneWidget);
  });
}
