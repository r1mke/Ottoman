import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/app.dart';

class _Recorder implements HttpClientAdapter {
  final requests = <RequestOptions>[];

  RequestOptions? lastWhere(String method, String pathEnd) => requests.reversed
      .where((r) => r.method == method && r.path.endsWith(pathEnd))
      .firstOrNull;

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    requests.add(o);
    Object? body;
    var status = 200;
    final p = o.path;
    if (p.endsWith('/login')) {
      body = {
        'accessToken': 'a',
        'refreshToken': 'r',
        'accessTokenExpiresUtc': DateTime.utc(2030).toIso8601String(),
        'user': {
          'id': 'admin1',
          'email': 'a@b.c',
          'firstName': 'Ad',
          'lastName': 'Min',
          'phone': null,
          'roles': ['Admin'],
        },
      };
    } else if (p.endsWith('/services') && o.method == 'GET') {
      body = [
        {
          'id': 1,
          'name': 'Šišanje',
          'description': null,
          'price': 25,
          'durationMinutes': 30,
          'imageKey': null,
          'isActive': true,
        },
        {
          'id': 2,
          'name': 'Stara usluga',
          'description': null,
          'price': 10,
          'durationMinutes': 15,
          'imageKey': null,
          'isActive': false,
        },
      ];
    } else if (p.endsWith('/services') && o.method == 'POST') {
      status = 201;
      body = {
        'id': 9,
        'name': 'X',
        'description': null,
        'price': 1,
        'durationMinutes': 5,
        'imageKey': null,
        'isActive': true,
      };
    } else if (p.endsWith('/employees') && o.method == 'GET') {
      body = [
        {
          'id': 'e1',
          'firstName': 'Kerim',
          'lastName': 'Begić',
          'fullName': 'Kerim Begić',
          'title': 'Master Barber',
          'bio': null,
          'photoKey': null,
          'isActive': true,
          'serviceIds': [1],
          'email': 'kerim@x.y',
          'phone': null,
        },
      ];
    } else if (p.endsWith('/services') && o.method == 'PUT') {
      body = {
        'id': 'e1',
        'firstName': 'Kerim',
        'lastName': 'Begić',
        'fullName': 'Kerim Begić',
        'title': 'Master Barber',
        'bio': null,
        'photoKey': null,
        'isActive': true,
        'serviceIds': [1, 2],
        'email': null,
        'phone': null,
      };
    } else {
      status = 204;
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

Future<_Recorder> _adminShell(WidgetTester tester) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(1500, 1000);
  addTearDown(tester.view.reset);

  final rec = _Recorder();
  late final AuthController auth;
  auth = AuthController(
    baseUrl: 'http://test',
    storage: MemoryTokenStorage(),
    allowedRoles: {Roles.admin, Roles.employee},
    api: ApiClient(
      baseUrl: 'http://test',
      dio: Dio()..httpClientAdapter = rec,
      readToken: () async => auth.accessToken,
    ),
  );
  await auth.restore();
  await tester.pumpWidget(OttomanDesktopApp(auth: auth));
  await tester.pumpAndSettle();
  await tester.enterText(find.byKey(const Key('login_email')), 'a@b.c');
  await tester.enterText(find.byKey(const Key('login_password')), 'pw');
  await tester.tap(find.byKey(const Key('login_submit')));
  await tester.pumpAndSettle();
  return rec;
}

void main() {
  testWidgets('services page lists active and inactive services', (
    tester,
  ) async {
    await _adminShell(tester);

    await tester.tap(find.text('Usluge').first);
    await tester.pumpAndSettle();

    expect(find.text('Šišanje'), findsOneWidget);
    expect(find.text('Stara usluga'), findsOneWidget);
    expect(find.text('Neaktivno'), findsOneWidget);
  });

  testWidgets(
    'a duration that is not a multiple of 5 is rejected before any request',
    (tester) async {
      final rec = await _adminShell(tester);
      await tester.tap(find.text('Usluge').first);
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('add_service')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('svc_name')), 'Nova');
      await tester.enterText(find.byKey(const Key('svc_price')), '12,50');
      await tester.enterText(find.byKey(const Key('svc_duration')), '7');
      await tester.tap(find.byKey(const Key('svc_save')));
      await tester.pumpAndSettle();

      expect(
        find.text('Trajanje mora biti višekratnik od 5 minuta.'),
        findsOneWidget,
      );
      expect(rec.lastWhere('POST', '/services'), isNull);
    },
  );

  testWidgets('saving a valid service posts a normalised body', (tester) async {
    final rec = await _adminShell(tester);
    await tester.tap(find.text('Usluge').first);
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('add_service')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('svc_name')), '  Nova  ');
    await tester.enterText(
      find.byKey(const Key('svc_price')),
      '12,50',
    ); // comma decimal
    await tester.enterText(find.byKey(const Key('svc_duration')), '45');
    await tester.tap(find.byKey(const Key('svc_save')));
    await tester.pumpAndSettle();

    final post = rec.lastWhere('POST', '/services')!;
    expect(post.data, {
      'name': 'Nova',
      'description': null,
      'price': 12.5,
      'durationMinutes': 45,
      'isActive': true,
    });
    expect(find.byKey(const Key('svc_save')), findsNothing); // dialog closed
  });

  testWidgets('assigning services sends the full selected set', (tester) async {
    final rec = await _adminShell(tester);
    await tester.tap(find.text('Uposlenici').first);
    await tester.pumpAndSettle();
    expect(find.text('Kerim Begić'), findsOneWidget);

    await tester.tap(find.byKey(const Key('assign_btn_e1')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('assign_2')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('assign_save')));
    await tester.pumpAndSettle();

    final put = rec.requests
        .where(
          (r) => r.method == 'PUT' && r.path.endsWith('/employees/e1/services'),
        )
        .single;
    expect((put.data as Map)['serviceIds'], unorderedEquals([1, 2]));
  });
}
