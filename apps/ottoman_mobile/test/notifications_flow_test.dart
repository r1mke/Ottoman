import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_mobile/app.dart';

Map<String, dynamic> _n(int id, String title, {bool read = false}) => {
  'id': id,
  'title': title,
  'body': 'Tekst $title',
  'kind': 'ReservationCreated',
  'createdAtUtc': DateTime.utc(2030, 1, 7, 9).toIso8601String(),
  'isRead': read,
};

class _Api implements HttpClientAdapter {
  _Api({this.role = 'Client'});
  final String role;
  final requests = <RequestOptions>[];
  int unread = 2;
  int resetStatus = 204;
  List<Map<String, dynamic>> items = [
    _n(1, 'Termin potvrđen'),
    _n(2, 'Podsjetnik', read: false),
    _n(3, 'Stara poruka', read: true),
    _n(4, 'Nova akcija'),
  ];

  RequestOptions? last(String method, String part) => requests.reversed
      .where((r) => r.method == method && r.path.contains(part))
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
    final path = o.path;
    if (path.endsWith('/login')) {
      body = {
        'accessToken': 'a',
        'refreshToken': 'r',
        'accessTokenExpiresUtc': DateTime.utc(2030).toIso8601String(),
        'user': {
          'id': 'u1',
          'email': 'x@y.z',
          'firstName': 'Ana',
          'lastName': 'Anić',
          'phone': null,
          'roles': [role],
        },
      };
    } else if (path.endsWith('/unread-count')) {
      body = {'count': unread};
    } else if (path.endsWith('/read-all')) {
      unread = 0;
      items = [
        for (final i in items) {...i, 'isRead': true},
      ];
      body = {'count': 0};
    } else if (path.endsWith('/read')) {
      unread = unread > 0 ? unread - 1 : 0;
      status = 204;
    } else if (path.endsWith('/notifications')) {
      body = {'items': items, 'totalCount': items.length};
    } else if (path.endsWith('/reset-password')) {
      status = resetStatus;
      if (resetStatus != 204) {
        body = {'status': 400, 'title': 'Podaci nisu ispravni.'};
      }
    } else if (path.endsWith('/forgot-password')) {
      status = 204;
    } else {
      body = path.contains('/products') ? {'items': [], 'totalCount': 0} : [];
    }
    return ResponseBody.fromString(
      body == null ? '' : jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

Future<_Api> _open(WidgetTester tester, {String role = 'Client'}) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(420, 900);
  addTearDown(tester.view.reset);

  final api = _Api(role: role);
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
  return api;
}

void main() {
  testWidgets('new messages appear without a manual refresh', (tester) async {
    final api = await _open(tester);
    await tester.tap(find.byKey(const Key('open_notifications')));
    await tester.pumpAndSettle();
    expect(find.text('Stigla poruka'), findsNothing);

    api.items = [_n(9, 'Stigla poruka'), ...api.items];
    await tester.pump(const Duration(seconds: 31));
    await tester.pumpAndSettle();

    expect(find.text('Stigla poruka'), findsOneWidget);
  });

  testWidgets('the bell shows the unread count from the server', (
    tester,
  ) async {
    await _open(tester);

    final badge = find.descendant(
      of: find.byKey(const Key('open_notifications')),
      matching: find.byType(Badge),
    );
    expect(tester.widget<Badge>(badge).isLabelVisible, isTrue);
    expect(
      find.descendant(of: badge, matching: find.text('2')),
      findsOneWidget,
    );
  });

  testWidgets('opening a message marks it read and shows its text', (
    tester,
  ) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('open_notifications')));
    await tester.pumpAndSettle();
    expect(find.text('Termin potvrđen'), findsOneWidget);

    await tester.tap(find.byKey(const Key('notification_1')));
    await tester.pumpAndSettle();

    expect(api.last('POST', '/notifications/1/read'), isNotNull);
    expect(
      find.descendant(
        of: find.byType(AlertDialog),
        matching: find.text('Tekst Termin potvrđen'),
      ),
      findsOneWidget,
    );
  });

  testWidgets('a message that is already read is not marked again', (
    tester,
  ) async {
    final api = await _open(tester);
    await tester.tap(find.byKey(const Key('open_notifications')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('notification_3')));
    await tester.pumpAndSettle();

    expect(api.last('POST', '/notifications/3/read'), isNull);
  });

  testWidgets('mark all read clears the badge', (tester) async {
    final api = await _open(tester);
    await tester.tap(find.byKey(const Key('open_notifications')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('mark_all_read')));
    await tester.pumpAndSettle();
    expect(api.last('POST', '/read-all'), isNotNull);

    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();
    final badge = find.descendant(
      of: find.byKey(const Key('open_notifications')),
      matching: find.byType(Badge),
    );
    expect(tester.widget<Badge>(badge).isLabelVisible, isFalse);
  });

  testWidgets('employees get the inbox as a tab', (tester) async {
    await _open(tester, role: 'Employee');

    await tester.tap(find.text('Notifikacije').last);
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('notification_1')), findsOneWidget);
    expect(find.byKey(const Key('mark_all_read')), findsOneWidget);
  });

  testWidgets('a news item with a picture shows it in the list and in the '
      'opened message', (tester) async {
    final api = await _open(tester);
    api.items = [
      {..._n(7, 'Ljetna vijest'), 'imageUrl': 'https://img/vijest.jpg'},
      _n(8, 'Obična poruka'),
    ];

    await tester.tap(find.byKey(const Key('open_notifications')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('notification_picture_7')), findsOneWidget);
    expect(find.byKey(const Key('notification_picture_8')), findsNothing);

    await tester.tap(find.byKey(const Key('notification_7')));
    await tester.pumpAndSettle();
    expect(
      find.byKey(const Key('notification_detail_picture')),
      findsOneWidget,
    );
  });

  testWidgets('a news item shows its category as a small label', (
    tester,
  ) async {
    final api = await _open(tester);
    api.items = [
      {
        ..._n(9, 'Popust na vosak'),
        'kind': 'Announcement',
        'category': 'Promotion',
      },
      _n(10, 'Termin potvrđen'),
    ];

    await tester.tap(find.byKey(const Key('open_notifications')));
    await tester.pumpAndSettle();

    expect(
      find.descendant(
        of: find.byKey(const Key('notification_category_9')),
        matching: find.text('Akcija'),
      ),
      findsOneWidget,
    );
    expect(find.byKey(const Key('notification_category_10')), findsNothing);
  });
}
