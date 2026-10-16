import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/app.dart';

class _Api implements HttpClientAdapter {
  _Api(this.role);
  final String role;
  final requests = <RequestOptions>[];
  int cancelStatus = 200;

  Iterable<RequestOptions> where(String method, String part) =>
      requests.where((r) => r.method == method && r.path.contains(part));

  static Map<String, dynamic> reservation({
    int id = 1,
    String status = 'Confirmed',
  }) => {
    'id': id,
    'clientId': 'c1',
    'clientName': 'Elmir Mujkić',
    'employeeId': 'e1',
    'employeeName': 'Kerim Begić',
    'startUtc': DateTime.utc(2030, 1, 7, 9).toIso8601String(),
    'endUtc': DateTime.utc(2030, 1, 7, 9, 30).toIso8601String(),
    'localStart': '2030-01-07 10:00',
    'status': status,
    'note': null,
    'totalPrice': 25,
    'totalDurationMinutes': 30,
    'items': [
      {
        'serviceId': 1,
        'name': 'Uređivanje brade',
        'price': 25,
        'durationMinutes': 30,
      },
    ],
    'canCancel': true,
    'createdAtUtc': DateTime.utc(2029).toIso8601String(),
    'cancelledAtUtc': null,
    'cancelReason': null,
  };

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
        'accessTokenExpiresUtc': DateTime.utc(2031).toIso8601String(),
        'user': {
          'id': role == 'Admin' ? 'a1' : 'e1',
          'email': 'x@y.z',
          'firstName': 'Ad',
          'lastName': 'Min',
          'phone': null,
          'roles': [role],
        },
      };
    } else if (p.endsWith('/summary')) {
      body = {
        'upcoming': 35,
        'completed': 100,
        'cancelled': 10,
        'noShow': 2,
        'totalClients': 77,
      };
    } else if (o.method == 'GET' && p.endsWith('/reservations')) {
      body = {
        'items': [reservation(), reservation(id: 2, status: 'Cancelled')],
        'totalCount': 2,
      };
    } else if (p.endsWith('/cancel')) {
      status = cancelStatus;
      body = cancelStatus == 200
          ? reservation(status: 'Cancelled')
          : {
              'title': 'Sukob podataka.',
              'detail': 'Termin je već počeo.',
              'status': 409,
            };
    } else if (p.endsWith('/employees')) {
      body = [
        {
          'id': 'e1',
          'firstName': 'Kerim',
          'lastName': 'Begić',
          'fullName': 'Kerim Begić',
          'title': 'Master',
          'bio': null,
          'photoUrl': null,
          'isActive': true,
          'serviceIds': [1],
          'email': null,
          'phone': null,
        },
      ];
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

Future<_Api> _open(WidgetTester tester, String role) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(1600, 1000);
  addTearDown(tester.view.reset);

  final api = _Api(role);
  late final AuthController auth;
  auth = AuthController(
    baseUrl: 'http://test',
    storage: MemoryTokenStorage(),
    allowedRoles: {Roles.admin, Roles.employee},
    api: ApiClient(
      baseUrl: 'http://test',
      dio: Dio()..httpClientAdapter = api,
      readToken: () async => auth.accessToken,
    ),
  );
  await auth.restore();
  await tester.pumpWidget(OttomanDesktopApp(auth: auth));
  await tester.pumpAndSettle();
  await tester.enterText(find.byKey(const Key('login_email')), 'x@y.z');
  await tester.enterText(find.byKey(const Key('login_password')), 'pw');
  await tester.tap(find.byKey(const Key('login_submit')));
  await tester.pumpAndSettle();
  // Employees land on "Termini" already (it is their first section); admins pick it.
  if (role == 'Admin') {
    await tester.tap(find.text('Termini').first);
    await tester.pumpAndSettle();
  }
  return api;
}

void main() {
  testWidgets('admin sees the four stat cards and the appointments table', (
    tester,
  ) async {
    await _open(tester, 'Admin');

    expect(
      find.descendant(
        of: find.byKey(const Key('stat_upcoming')),
        matching: find.text('35'),
      ),
      findsOneWidget,
    );
    expect(
      find.descendant(
        of: find.byKey(const Key('stat_completed')),
        matching: find.text('100'),
      ),
      findsOneWidget,
    );
    expect(
      find.descendant(
        of: find.byKey(const Key('stat_cancelled')),
        matching: find.text('10'),
      ),
      findsOneWidget,
    );
    expect(
      find.descendant(
        of: find.byKey(const Key('stat_clients')),
        matching: find.text('77'),
      ),
      findsOneWidget,
    );
    expect(find.text('Elmir Mujkić'), findsNWidgets(2));
    expect(find.text('Uređivanje brade'), findsNWidgets(2));
    expect(find.text('2030-01-07 10:00'), findsNWidgets(2));
  });

  testWidgets('only open appointments can be edited or cancelled', (
    tester,
  ) async {
    await _open(tester, 'Admin');

    expect(find.byKey(const Key('edit_1')), findsOneWidget);
    expect(find.byKey(const Key('cancel_1')), findsOneWidget);
    expect(find.byKey(const Key('edit_2')), findsNothing); // already cancelled
    expect(find.byKey(const Key('cancel_2')), findsNothing);
  });

  testWidgets(
    'changing the period asks the server for that salon-local range',
    (tester) async {
      final api = await _open(tester, 'Admin');

      await tester.tap(find.text('Danas'));
      await tester.pumpAndSettle();

      final list = api.where('GET', '/reservations').last;
      expect(list.queryParameters['fromDate'], isNotNull);
      expect(
        list.queryParameters['fromDate'],
        list.queryParameters['toDate'],
      ); // a single day
      expect(
        api.where('GET', '/summary').last.queryParameters['fromDate'],
        list.queryParameters['fromDate'],
      );
    },
  );

  testWidgets('cancelling asks for confirmation and then calls the API', (
    tester,
  ) async {
    final api = await _open(tester, 'Admin');

    await tester.tap(find.byKey(const Key('cancel_1')));
    await tester.pumpAndSettle();
    expect(
      api.where('POST', '/cancel'),
      isEmpty,
    ); // nothing sent before confirming

    await tester.enterText(
      find.byKey(const Key('cancel_reason')),
      'Frizer je bolestan',
    );
    await tester.tap(find.byKey(const Key('confirm_cancel')));
    await tester.pumpAndSettle();

    expect(
      api.where('POST', '/cancel').single.path,
      contains('/reservations/1/cancel'),
    );
  });

  testWidgets(
    'the salon must give a reason, and it is sent with the cancellation',
    (tester) async {
      final api = await _open(tester, 'Admin');
      await tester.tap(find.byKey(const Key('cancel_1')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('confirm_cancel')));
      await tester.pumpAndSettle();

      expect(
        find.text(
          'Unesite razlog (najmanje 3 znaka). Klijent ga dobija u obavijesti.',
        ),
        findsOneWidget,
        reason: 'the message is shown under the field',
      );
      expect(api.where('POST', '/cancel'), isEmpty);

      await tester.enterText(find.byKey(const Key('cancel_reason')), 'ab');
      await tester.tap(find.byKey(const Key('confirm_cancel')));
      await tester.pumpAndSettle();
      expect(
        api.where('POST', '/cancel'),
        isEmpty,
        reason: 'two characters are not a reason',
      );

      await tester.enterText(
        find.byKey(const Key('cancel_reason')),
        '  Frizer je bolestan ',
      );
      await tester.tap(find.byKey(const Key('confirm_cancel')));
      await tester.pumpAndSettle();

      final sent = api.where('POST', '/cancel').single.data as Map;
      expect(sent['reason'], 'Frizer je bolestan');
    },
  );

  testWidgets('the cancel dialog closes with the X and changes nothing', (
    tester,
  ) async {
    final api = await _open(tester, 'Admin');
    await tester.tap(find.byKey(const Key('cancel_1')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('dialog_close')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('confirm_cancel')), findsNothing);
    expect(api.where('POST', '/cancel'), isEmpty);
  });

  testWidgets('a refused cancellation shows the server reason', (tester) async {
    final api = await _open(tester, 'Admin');
    api.cancelStatus = 409;

    await tester.tap(find.byKey(const Key('cancel_1')));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('cancel_reason')),
      'Frizer je bolestan',
    );
    await tester.tap(find.byKey(const Key('confirm_cancel')));
    await tester.pumpAndSettle();

    expect(find.text('Termin je već počeo.'), findsOneWidget);
  });

  testWidgets(
    'employees cannot create or move appointments and have no barber filter',
    (tester) async {
      await _open(tester, 'Employee');

      expect(find.byKey(const Key('new_appointment')), findsNothing);
      expect(find.byKey(const Key('employee_filter')), findsNothing);
      expect(find.byKey(const Key('edit_1')), findsNothing);
      expect(
        find.byKey(const Key('cancel_1')),
        findsOneWidget,
      ); // staff may cancel (the server enforces the rules)
    },
  );
}
