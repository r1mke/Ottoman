import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

Widget _host(Widget child) => MaterialApp(
  localizationsDelegates: const [
    AppLocalizations.delegate,
    GlobalMaterialLocalizations.delegate,
    GlobalWidgetsLocalizations.delegate,
    GlobalCupertinoLocalizations.delegate,
  ],
  supportedLocales: AppLocalizations.supportedLocales,
  locale: const Locale('bs'),
  home: Scaffold(body: child),
);

void main() {
  testWidgets('shows the skeleton first, then the data', (tester) async {
    await tester.pumpWidget(
      _host(
        AsyncView<int>(
          load: () async => 7,
          skeleton: const Text('skeleton'),
          builder: (_, d, _) => Text('data $d'),
        ),
      ),
    );
    expect(find.text('skeleton'), findsOneWidget);

    await tester.pumpAndSettle();

    expect(find.text('data 7'), findsOneWidget);
    expect(find.text('skeleton'), findsNothing);
  });

  testWidgets(
    'a quiet refresh changes the data without ever showing the skeleton',
    (tester) async {
      var value = 1;
      var skeletonFrames = 0;
      await tester.pumpWidget(
        _host(
          AsyncView<int>(
            load: () async => value,
            refreshEvery: const Duration(seconds: 30),
            skeleton: Builder(
              builder: (_) {
                skeletonFrames++;
                return const Text('skeleton');
              },
            ),
            builder: (_, d, _) => Text('data $d'),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('data 1'), findsOneWidget);
      final before = skeletonFrames;

      value = 2;
      await tester.pump(const Duration(seconds: 31));
      await tester.pump();

      expect(find.text('data 2'), findsOneWidget);
      expect(
        skeletonFrames,
        before,
        reason: 'no flicker: the old data stayed until the new arrived',
      );
    },
  );

  testWidgets('a failed quiet refresh keeps the good data on screen', (
    tester,
  ) async {
    var fail = false;
    await tester.pumpWidget(
      _host(
        AsyncView<int>(
          load: () async {
            if (fail) throw AppException('mreža');
            return 5;
          },
          refreshEvery: const Duration(seconds: 10),
          builder: (_, d, _) => Text('data $d'),
        ),
      ),
    );
    await tester.pumpAndSettle();

    fail = true;
    await tester.pump(const Duration(seconds: 11));
    await tester.pump();

    expect(find.text('data 5'), findsOneWidget);
    expect(find.text('mreža'), findsNothing);
  });

  testWidgets('a failed first load shows the error with a retry that works', (
    tester,
  ) async {
    var calls = 0;
    await tester.pumpWidget(
      _host(
        AsyncView<int>(
          load: () async {
            calls++;
            if (calls == 1) throw AppException('Server nije dostupan');
            return 9;
          },
          builder: (_, d, _) => Text('data $d'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Server nije dostupan'), findsOneWidget);

    await tester.tap(find.text('Pokušaj ponovo'));
    await tester.pumpAndSettle();

    expect(find.text('data 9'), findsOneWidget);
  });

  testWidgets('the empty state replaces the builder', (tester) async {
    await tester.pumpWidget(
      _host(
        AsyncView<List<int>>(
          load: () async => [],
          isEmpty: (l) => l.isEmpty,
          emptyMessage: 'Nema ničega',
          builder: (_, d, _) => Text('items ${d.length}'),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Nema ničega'), findsOneWidget);
  });

  testWidgets(
    'without refreshEvery nothing runs on its own and disposing is clean',
    (tester) async {
      var calls = 0;
      await tester.pumpWidget(
        _host(
          AsyncView<int>(
            load: () async => ++calls,
            builder: (_, d, _) => Text('data $d'),
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.pump(const Duration(minutes: 5));
      expect(calls, 1);

      await tester.pumpWidget(_host(const SizedBox()));
      await tester.pump(const Duration(minutes: 5));
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets('with refreshEvery the timer stops when the view goes away', (
    tester,
  ) async {
    var calls = 0;
    await tester.pumpWidget(
      _host(
        AsyncView<int>(
          load: () async => ++calls,
          refreshEvery: const Duration(seconds: 5),
          builder: (_, d, _) => Text('data $d'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.pump(const Duration(seconds: 6));
    await tester.pump();
    final seen = calls;
    expect(seen, greaterThan(1));

    await tester.pumpWidget(_host(const SizedBox()));
    await tester.pump(const Duration(seconds: 30));

    expect(calls, seen, reason: 'no more loads after dispose');
  });
}
