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
    if (o.path.contains('/recommendations') || o.path.contains('/reviews')) {
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

void main() {
  testWidgets(
    'home lists barbers; picking one shows their services and a running total',
    (tester) async {
      await _signedIn(tester);

      expect(find.text('Kerim Begić'), findsOneWidget);
      expect(find.text('Master Barber'), findsOneWidget);

      await tester.tap(find.byKey(const Key('employee_e1')));
      await tester.pumpAndSettle();

      expect(find.text('Šišanje'), findsOneWidget);
      expect(find.byKey(const Key('selection_total')), findsNothing);

      await tester.tap(find.byKey(const Key('service_1')));
      await tester.tap(find.byKey(const Key('service_2')));
      await tester.pump();

      expect(find.text('40.00 KM'), findsOneWidget); // 25 + 15
      expect(find.textContaining('60 min'), findsOneWidget); // 30 + 30
    },
  );

  testWidgets(
    'a failing request shows the error state with a retry that reloads',
    (tester) async {
      final adapter = await _signedIn(tester, fail: true);

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

  testWidgets('services tab shows the price list', (tester) async {
    await _signedIn(tester);

    await tester.tap(find.text('Usluge').last);
    await tester.pumpAndSettle();

    expect(find.text('Šišanje'), findsOneWidget);
    expect(find.text('25.00 KM'), findsOneWidget);
  });
}
