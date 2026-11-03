import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_mobile/app.dart';

class _Adapter implements HttpClientAdapter {
  _Adapter({this.fail = false});
  final bool fail;
  final calls = <String>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    calls.add(o.uri.toString());
    Object? body;
    var status = 200;
    if (o.path.endsWith('/salon')) {
      body = {
        'name': 'Ottoman',
        'description': 'Opis salona',
        'address': null,
        'phone': null,
        'mapsUrl': null,
        'logoUrl': null,
        'hours': [],
      };
    } else if (o.path.contains('/gallery')) {
      body = {'items': [], 'totalCount': 0};
    } else if (o.path.contains('/recommendations') ||
        o.path.contains('/reviews')) {
      body = [];
    } else if (o.path.endsWith('/login')) {
      body = {
        'accessToken': 'a',
        'refreshToken': 'r',
        'accessTokenExpiresUtc': DateTime.utc(2030).toIso8601String(),
        'user': {
          'id': 'c1',
          'email': 'x@y.z',
          'firstName': 'Ana',
          'lastName': 'Anić',
          'phone': null,
          'roles': ['Client'],
        },
      };
    } else if (o.path.endsWith('/employees')) {
      if (fail) {
        status = 500;
      } else {
        body = [
          {
            'id': 'e1',
            'firstName': 'Kerim',
            'lastName': 'Begić',
            'fullName': 'Kerim Begić',
            'title': 'Master Barber',
            'bio': 'Majstor u usponu',
            'photoUrl': null,
            'isActive': true,
            'serviceIds': [1, 2],
            'email': null,
            'phone': null,
          },
          {
            'id': 'e2',
            'firstName': 'Tarik',
            'lastName': 'Mujkić',
            'fullName': 'Tarik Mujkić',
            'title': 'Junior Barber',
            'bio': null,
            'photoUrl': null,
            'isActive': true,
            'serviceIds': [1],
            'email': null,
            'phone': null,
          },
        ];
      }
    } else if (o.path.endsWith('/services')) {
      body = [
        {
          'id': 1,
          'name': 'Šišanje',
          'description': null,
          'price': 25,
          'durationMinutes': 30,
          'imageUrl': null,
          'isActive': true,
        },
        {
          'id': 2,
          'name': 'Uređivanje brade',
          'description': null,
          'price': 15,
          'durationMinutes': 30,
          'imageUrl': null,
          'isActive': true,
        },
        {
          'id': 3,
          'name': 'Bojenje brade',
          'description': null,
          'price': 20,
          'durationMinutes': 30,
          'imageUrl': null,
          'isActive': true,
        },
      ];
    }
    return ResponseBody.fromString(
      jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

Future<_Adapter> _signedIn(WidgetTester tester, {bool fail = false}) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(420, 900);
  addTearDown(tester.view.reset);

  final adapter = _Adapter(fail: fail);
  late final AuthController auth;
  auth = AuthController(
    baseUrl: 'http://test',
    storage: MemoryTokenStorage(),
    api: ApiClient(
      baseUrl: 'http://test',
      dio: Dio()..httpClientAdapter = adapter,
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
  return adapter;
}

/// "Rezerviši termin" on the home tab leads to the Services tab, where booking starts.
Future<void> _startBooking(WidgetTester tester) async {
  await tester.tap(find.byKey(const Key('book_now')));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets(
    '"Book" opens the services; choosing them shows a running total',
    (tester) async {
      await _signedIn(tester);
      await _startBooking(tester);

      expect(find.text('Šišanje'), findsOneWidget);
      expect(find.byKey(const Key('selection_total')), findsNothing);
      expect(find.byKey(const Key('continue')), findsOneWidget);

      await tester.tap(find.byKey(const Key('service_1')));
      await tester.tap(find.byKey(const Key('service_2')));
      await tester.pump();

      expect(find.text('40.00 KM'), findsOneWidget); // 25 + 15
      expect(find.textContaining('60 min'), findsOneWidget); // 30 + 30
    },
  );

  testWidgets('only the barbers who do every chosen service are offered', (
    tester,
  ) async {
    await _signedIn(tester);
    await _startBooking(tester);

    // Both services: only Kerim does the two of them.
    await tester.tap(find.byKey(const Key('service_1')));
    await tester.tap(find.byKey(const Key('service_2')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('continue')));
    await tester.pumpAndSettle();

    expect(find.text('Kerim Begić'), findsOneWidget);
    expect(find.text('Tarik Mujkić'), findsNothing);

    // Back, and change the choice to the one service both do.
    tester.state<NavigatorState>(find.byType(Navigator).first).pop();
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('service_2')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('continue')));
    await tester.pumpAndSettle();

    expect(find.text('Kerim Begić'), findsOneWidget);
    expect(find.text('Tarik Mujkić'), findsOneWidget);
  });

  testWidgets('a service nobody does says so instead of an empty screen', (
    tester,
  ) async {
    await _signedIn(tester);
    await _startBooking(tester);

    await tester.tap(find.byKey(const Key('service_3')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('continue')));
    await tester.pumpAndSettle();

    expect(find.textContaining('Nijedan uposlenik ne radi'), findsOneWidget);
  });

  testWidgets('"Continue" waits for at least one service', (tester) async {
    await _signedIn(tester);
    await _startBooking(tester);

    final button = tester.widget<FilledButton>(
      find.byKey(const Key('continue')),
    );
    expect(button.onPressed, isNull);
  });

  testWidgets(
    'a failing request for the barbers shows the error state with a retry that reloads',
    (tester) async {
      final adapter = await _signedIn(tester, fail: true);
      await _startBooking(tester);
      await tester.tap(find.byKey(const Key('service_1')));
      await tester.pump();
      await tester.tap(find.byKey(const Key('continue')));
      await tester.pumpAndSettle();

      expect(find.text('Pokušaj ponovo'), findsOneWidget);
      final before = adapter.calls
          .where((c) => c.contains('/employees'))
          .length;

      await tester.tap(find.text('Pokušaj ponovo'));
      await tester.pumpAndSettle();

      expect(
        adapter.calls.where((c) => c.contains('/employees')).length,
        before + 1,
      );
    },
  );

  testWidgets('services tab lists the services with their price and time', (
    tester,
  ) async {
    await _signedIn(tester);

    await tester.tap(find.text('Usluge').last);
    await tester.pumpAndSettle();

    expect(find.text('Šišanje'), findsOneWidget);
    expect(find.textContaining('25.00 KM'), findsOneWidget);
  });
}
