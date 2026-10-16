import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_mobile/app.dart';

/// Scriptable fake API for the booking flow.
class _Api implements HttpClientAdapter {
  _Api({this.role = 'Client'});

  final String role;
  final requests = <RequestOptions>[];
  int createStatus = 201;
  int cancelStatus = 200;
  List<Map<String, dynamic>> reservations = [];

  RequestOptions? last(String method, String pathPart) => requests.reversed
      .where((r) => r.method == method && r.path.contains(pathPart))
      .firstOrNull;
  int count(String method, String pathPart) => requests
      .where((r) => r.method == method && r.path.contains(pathPart))
      .length;

  static Map<String, dynamic> reservation({
    int id = 1,
    String status = 'Confirmed',
    bool canCancel = true,
    String localStart = '2030-01-07 10:00',
    DateTime? startUtc,
  }) => {
    'id': id,
    'clientId': 'c1',
    'clientName': 'Ana Anić',
    'employeeId': 'e1',
    'employeeName': 'Kerim Begić',
    'startUtc': (startUtc ?? DateTime.utc(2030, 1, 7, 9)).toIso8601String(),
    'endUtc': (startUtc ?? DateTime.utc(2030, 1, 7, 9))
        .add(const Duration(minutes: 30))
        .toIso8601String(),
    'localStart': localStart,
    'status': status,
    'note': null,
    'totalPrice': 25,
    'totalDurationMinutes': 30,
    'items': [
      {'serviceId': 1, 'name': 'Šišanje', 'price': 25, 'durationMinutes': 30},
    ],
    'canCancel': canCancel,
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
          'id': role == 'Client' ? 'c1' : 'e1',
          'email': 'x@y.z',
          'firstName': 'Ana',
          'lastName': 'Anić',
          'phone': null,
          'roles': [role],
        },
      };
    } else if (p.endsWith('/employees')) {
      body = [
        {
          'id': 'e1',
          'firstName': 'Kerim',
          'lastName': 'Begić',
          'fullName': 'Kerim Begić',
          'title': 'Master Barber',
          'bio': null,
          'photoUrl': null,
          'isActive': true,
          'serviceIds': [1],
          'email': null,
          'phone': null,
        },
      ];
    } else if (p.endsWith('/services')) {
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
      ];
    } else if (p.endsWith('/availability')) {
      final all =
          o.queryParameters['allTimes'] == true ||
          o.queryParameters['allTimes'] == 'true';
      final times = all ? ['09:00', '09:05', '09:15'] : ['09:00', '09:15'];
      body = {
        'date': o.queryParameters['date'],
        'durationMinutes': 30,
        'totalPrice': 25,
        'allTimes': all,
        'starts': [
          for (final t in times)
            '2030-01-07T${(int.parse(t.substring(0, 2)) - 1).toString().padLeft(2, '0')}:${t.substring(3)}:00Z',
        ],
        'localTimes': times,
      };
    } else if (o.method == 'POST' && p.endsWith('/reservations')) {
      status = createStatus;
      body = createStatus == 201
          ? reservation()
          : {
              'title': 'Sukob podataka.',
              'detail': 'Termin je upravo zauzet.',
              'status': 409,
            };
    } else if (o.method == 'POST' && p.endsWith('/cancel')) {
      status = cancelStatus;
      body = cancelStatus == 200
          ? reservation(status: 'Cancelled', canCancel: false)
          : {
              'title': 'Sukob podataka.',
              'detail': 'Termin se može otkazati najkasnije 2 h prije početka.',
              'status': 409,
            };
    } else if (o.method == 'GET' && p.endsWith('/reservations')) {
      body = {'items': reservations, 'totalCount': reservations.length};
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

Future<void> _start(WidgetTester tester, _Api api) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(420, 1100);
  addTearDown(tester.view.reset);

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
}

Future<void> _openSlotPicker(WidgetTester tester) async {
  await tester.tap(find.byKey(const Key('employee_e1')));
  await tester.pumpAndSettle();
  await tester.tap(find.byKey(const Key('service_1')));
  await tester.pump();
  await tester.tap(find.byKey(const Key('continue')));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('client picks barber, service and time, then books', (
    tester,
  ) async {
    final api = _Api();
    await _start(tester, api);
    await _openSlotPicker(tester);

    expect(find.byKey(const Key('slot_calendar')), findsOneWidget);
    expect(find.byKey(const Key('book')), findsOneWidget);
    expect(
      tester.widget<FilledButton>(find.byKey(const Key('book'))).onPressed,
      isNull,
    ); // nothing chosen yet

    await tester.tap(find.byKey(const Key('slot_09:15')));
    await tester.pump();
    expect(
      tester.widget<FilledButton>(find.byKey(const Key('book'))).onPressed,
      isNotNull,
    );

    await tester.enterText(find.byKey(const Key('booking_note')), 'Fade');
    await tester.tap(find.byKey(const Key('book')));
    await tester.pumpAndSettle();

    final post = api.last('POST', '/reservations')!;
    final data = post.data as Map<String, dynamic>;
    expect(data['employeeId'], 'e1');
    expect(data['serviceIds'], [1]);
    expect(data['startUtc'], '2030-01-07T08:15:00.000Z');
    expect(data['note'], 'Fade');
    expect(data.containsKey('price'), isFalse); // the client never sends money
    expect(
      post.headers['Idempotency-Key'],
      matches(RegExp(r'^[0-9a-f]{32}$')),
      reason: 'one key per booking intention',
    );
    expect(find.text('Termin je rezervisan.'), findsOneWidget);
    expect(find.byKey(const Key('book')), findsNothing); // back at the start
  });

  testWidgets('"show all times" asks the server for every 5-minute start', (
    tester,
  ) async {
    final api = _Api();
    await _start(tester, api);
    await _openSlotPicker(tester);

    expect(find.byKey(const Key('slot_09:05')), findsNothing);

    await tester.tap(find.byKey(const Key('toggle_all_times')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('slot_09:05')), findsOneWidget);
    expect(
      api.requests
          .where((r) => r.path.endsWith('/availability'))
          .last
          .queryParameters['allTimes'],
      isTrue,
    );
  });

  testWidgets(
    'a 409 on booking explains it, clears the choice and reloads the times',
    (tester) async {
      final api = _Api()..createStatus = 409;
      await _start(tester, api);
      await _openSlotPicker(tester);
      final before = api.count('GET', '/availability');

      await tester.tap(find.byKey(const Key('slot_09:00')));
      await tester.pump();
      await tester.tap(find.byKey(const Key('book')));
      await tester.pumpAndSettle();

      expect(
        find.text('Termin je upravo zauzet. Odaberite drugi.'),
        findsOneWidget,
      );
      expect(api.count('GET', '/availability'), before + 1);
      expect(
        tester.widget<FilledButton>(find.byKey(const Key('book'))).onPressed,
        isNull,
      );
    },
  );

  testWidgets(
    'appointments tab lists bookings and cancels after confirmation',
    (tester) async {
      final api = _Api()..reservations = [_Api.reservation()];
      await _start(tester, api);

      await tester.tap(find.text('Termini').last);
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('reservation_1')), findsOneWidget);
      expect(find.textContaining('Šišanje'), findsWidgets);

      await tester.tap(find.byKey(const Key('cancel_1')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm_cancel')));
      await tester.pumpAndSettle();

      expect(api.count('POST', '/cancel'), 1);
    },
  );

  testWidgets('a refused cancellation shows the server reason', (tester) async {
    final api = _Api()
      ..cancelStatus = 409
      ..reservations = [_Api.reservation()];
    await _start(tester, api);
    await tester.tap(find.text('Termini').last);
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('cancel_1')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_cancel')));
    await tester.pumpAndSettle();

    expect(find.textContaining('najkasnije 2 h'), findsOneWidget);
  });

  testWidgets(
    'no cancel button when the server says the reservation cannot be cancelled',
    (tester) async {
      final api = _Api()..reservations = [_Api.reservation(canCancel: false)];
      await _start(tester, api);
      await tester.tap(find.text('Termini').last);
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('reservation_1')), findsOneWidget);
      expect(find.byKey(const Key('cancel_1')), findsNothing);
    },
  );

  testWidgets(
    'employee schedule: day navigation queries that day and finishing is offered only once started',
    (tester) async {
      final past = DateTime.now().toUtc().subtract(const Duration(hours: 1));
      final future = DateTime.now().toUtc().add(const Duration(hours: 5));
      final api = _Api(role: 'Employee')
        ..reservations = [
          _Api.reservation(id: 1, startUtc: past),
          _Api.reservation(id: 2, startUtc: future),
        ];
      await _start(tester, api);

      expect(find.byKey(const Key('complete_1')), findsOneWidget);
      expect(find.byKey(const Key('complete_2')), findsNothing);

      final today = api
          .last('GET', '/reservations')!
          .queryParameters['fromDate'];
      await tester.tap(find.byKey(const Key('next_day')));
      await tester.pumpAndSettle();
      final tomorrow = api
          .last('GET', '/reservations')!
          .queryParameters['fromDate'];
      expect(tomorrow, isNot(today));
      expect(
        api.last('GET', '/reservations')!.queryParameters['toDate'],
        tomorrow,
      ); // a single day
    },
  );
}
