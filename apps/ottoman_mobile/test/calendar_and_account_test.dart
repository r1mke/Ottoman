import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_core/widgets/availability_calendar.dart';
import 'package:ottoman_mobile/app.dart';

Widget _calendar({
  required DateTime selected,
  required DateTime first,
  Set<DateTime>? available,
  ValueChanged<DateTime>? onSelected,
  ValueChanged<DateTime>? onMonth,
}) => MaterialApp(
  theme: OttomanTheme.barbershop(),
  localizationsDelegates: AppLocalizations.localizationsDelegates,
  supportedLocales: AppLocalizations.supportedLocales,
  locale: const Locale('bs'),
  home: Scaffold(
    body: SingleChildScrollView(
      child: AvailabilityCalendar(
        selected: selected,
        first: first,
        last: first.add(const Duration(days: 60)),
        focused: selected,
        available: available,
        onSelected: onSelected ?? (_) {},
        onMonthChanged: onMonth ?? (_) {},
      ),
    ),
  ),
);

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    requests.add(o);
    Object? body;
    if (o.path.endsWith('/login')) {
      body = {
        'accessToken': 'a',
        'refreshToken': 'r',
        'accessTokenExpiresUtc': DateTime.utc(2030).toIso8601String(),
        'user': {
          'id': 'c1',
          'email': 'kerim@example.com',
          'firstName': 'Kerim',
          'lastName': 'Begić',
          'phone': null,
          'roles': ['Client'],
        },
      };
    } else if (o.path.endsWith('/logout')) {
      body = {};
    } else if (o.path.endsWith('/salon')) {
      body = {
        'name': 'Ottoman',
        'hours': [
          for (final d in [1, 2, 3, 4, 5, 6, 0])
            {'day': d, 'openMinutes': null, 'closeMinutes': null},
        ],
      };
    } else if (o.path.contains('/gallery') ||
        o.path.endsWith('/employees') ||
        o.path.endsWith('/services')) {
      body = o.path.contains('/gallery') ? {'items': [], 'totalCount': 0} : [];
    }
    return ResponseBody.fromString(
      jsonEncode(body),
      200,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

Future<_Api> _openAccount(WidgetTester tester) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(420, 900);
  addTearDown(tester.view.reset);
  final api = _Api();
  late final AuthController auth;
  auth = AuthController(
    baseUrl: 'http://test',
    storage: MemoryTokenStorage(),
    api: ApiClient(
      baseUrl: 'http://test',
      dio: Dio()..httpClientAdapter = api,
      readToken: () async => auth.accessToken,
    ),
  );
  await auth.restore();
  await tester.pumpWidget(OttomanMobileApp(auth: auth));
  await tester.pumpAndSettle();
  await tester.enterText(find.byKey(const Key('login_email')), 'x@y.z');
  await tester.enterText(find.byKey(const Key('login_password')), 'pw');
  await tester.tap(find.byKey(const Key('login_submit')));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Račun').last);
  await tester.pumpAndSettle();
  return api;
}

void main() {
  final today = DateTime(2030, 1, 9);

  testWidgets(
    'the calendar shows the month, a legend and an amber chosen day',
    (tester) async {
      await tester.pumpWidget(_calendar(selected: today, first: today));
      await tester.pumpAndSettle();

      expect(find.text('Januar 2030'), findsOneWidget);
      expect(find.text('Pon'), findsOneWidget);
      expect(
        find.text('Nedostupni termini su označeni crvenom bojom'),
        findsOneWidget,
      );
      final chosen = tester.widget<Container>(
        find.byKey(const Key('day_selected')),
      );
      expect((chosen.decoration! as BoxDecoration).color, OttomanColors.amber);
    },
  );

  testWidgets('days that are full or past carry a red dot and cannot be '
      'chosen; free days can', (tester) async {
    DateTime? picked;
    await tester.pumpWidget(
      _calendar(
        selected: DateTime(2030, 1, 10),
        first: today,
        available: {DateTime(2030, 1, 10), DateTime(2030, 1, 11)},
        onSelected: (d) => picked = d,
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('dot_5')), findsOneWidget); // before the window
    expect(find.byKey(const Key('dot_12')), findsOneWidget); // full
    expect(find.byKey(const Key('dot_11')), findsNothing); // free

    await tester.tap(find.byKey(const Key('day_12')));
    await tester.pump();
    expect(picked, isNull);

    await tester.tap(find.byKey(const Key('day_11')));
    await tester.pump();
    expect(picked, DateTime(2030, 1, 11));
  });

  testWidgets('nothing is blocked while the month is still loading', (
    tester,
  ) async {
    DateTime? picked;
    await tester.pumpWidget(
      _calendar(
        selected: today,
        first: today,
        available: null,
        onSelected: (d) => picked = d,
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('day_12')));
    await tester.pump();
    expect(picked, DateTime(2030, 1, 12));
    expect(find.byKey(const Key('dot_12')), findsNothing);
  });

  testWidgets('the previous-month arrow is off on the first month', (
    tester,
  ) async {
    DateTime? month;
    await tester.pumpWidget(
      _calendar(selected: today, first: today, onMonth: (m) => month = m),
    );
    await tester.pumpAndSettle();

    expect(
      tester
          .widget<IconButton>(find.byKey(const Key('calendar_prev')))
          .onPressed,
      isNull,
    );
    await tester.tap(find.byKey(const Key('calendar_next')));
    await tester.pump();
    expect(month, DateTime(2030, 2));
  });

  testWidgets('the account tab shows who is signed in and the three groups', (
    tester,
  ) async {
    await _openAccount(tester);

    expect(find.byKey(const Key('account_name')), findsOneWidget);
    expect(find.text('Kerim Begić'), findsOneWidget);
    expect(find.text('kerim@example.com'), findsOneWidget);
    expect(find.text('Lični podaci'), findsOneWidget);
    expect(find.text('Uredi profil'), findsOneWidget);
    expect(find.text('Upravljanje računom'), findsOneWidget);
    expect(find.text('O nama'), findsOneWidget);
    expect(find.text('Pravila privatnosti'), findsOneWidget);
    expect(find.text('Uslovi korištenja'), findsOneWidget);
    expect(find.text('O aplikaciji'), findsOneWidget);
    expect(find.text('Odjava'), findsOneWidget);
  });

  testWidgets('each row opens its page, and the text pages say something', (
    tester,
  ) async {
    await _openAccount(tester);

    await tester.tap(find.byKey(const Key('row_privacy')));
    await tester.pumpAndSettle();
    expect(find.text('Koje podatke prikupljamo'), findsOneWidget);
    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('row_terms')));
    await tester.pumpAndSettle();
    expect(find.text('Zakazivanje termina'), findsOneWidget);
    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('row_edit_profile')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('profile_save')), findsOneWidget);
    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('row_manage_account')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('row_change_password')), findsOneWidget);
  });

  testWidgets('"Odjava" signs the person out', (tester) async {
    final api = await _openAccount(tester);

    await tester.tap(find.byKey(const Key('logout')));
    await tester.pumpAndSettle();

    expect(api.requests.any((r) => r.path.endsWith('/logout')), isTrue);
    expect(find.byKey(const Key('login_submit')), findsOneWidget);
  });
}
