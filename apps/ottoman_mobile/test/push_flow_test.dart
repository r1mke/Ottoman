import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_mobile/app.dart';

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  int unread = 0;

  Iterable<RequestOptions> to(String method, String pathEnd) =>
      requests.where((r) => r.method == method && r.path.endsWith(pathEnd));

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
          'roles': ['Client'],
        },
      };
    } else if (path.endsWith('/unread-count')) {
      body = {'count': unread};
    } else if (path.contains('/devices') || path.endsWith('/logout')) {
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

class _Phone implements PushClient {
  final foreground = StreamController<PushNote>.broadcast();

  @override
  Future<bool> requestPermission() async => true;

  @override
  Future<String?> token() async => 'phone-token';

  @override
  Stream<String> get onTokenRefresh => const Stream.empty();

  @override
  Stream<PushNote> get onForeground => foreground.stream;

  @override
  Future<void> deleteToken() async {}
}

Future<(_Api, _Phone, AuthController)> _open(WidgetTester tester) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(420, 900);
  addTearDown(tester.view.reset);

  final api = _Api();
  final phone = _Phone();
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
  final push = PushRegistration(devices: DevicesApi(auth.api), client: phone);
  push.attach(auth);
  await auth.restore();
  await tester.pumpWidget(OttomanMobileApp(auth: auth, push: push));
  await tester.pumpAndSettle();
  await tester.enterText(find.byKey(const Key('login_email')), 'x@y.z');
  await tester.enterText(find.byKey(const Key('login_password')), 'pw');
  await tester.tap(find.byKey(const Key('login_submit')));
  await tester.pumpAndSettle();
  return (api, phone, auth);
}

void main() {
  testWidgets(
    'signing in registers this phone, signing out forgets it',
    timeout: const Timeout(Duration(seconds: 60)),
    (tester) async {
      final (api, _, auth) = await _open(tester);

      final registered = api.to('POST', '/api/v1/devices').toList();
      expect(registered, hasLength(1));
      expect(registered.single.data, {
        'token': 'phone-token',
        'platform': 'android',
      });

      await tester.runAsync(() => auth.logout());
      await tester.pumpAndSettle();

      final forgotten = api.to('POST', '/api/v1/devices/unregister').toList();
      expect(forgotten, hasLength(1));
      expect(forgotten.single.data, {'token': 'phone-token'});
      expect(
        api.requests.indexOf(forgotten.single),
        lessThan(api.requests.indexWhere((r) => r.path.endsWith('/logout'))),
        reason: 'the phone is forgotten while the session is still valid',
      );
    },
  );

  testWidgets(
    'a push that arrives while the app is open is shown and refreshes the bell',
    (tester) async {
      final (api, phone, _) = await _open(tester);
      final before = api.to('GET', '/unread-count').length;
      api.unread = 1;

      phone.foreground.add(
        const PushNote(
          title: 'Podsjetnik',
          body: 'Termin sutra u 10:00',
          kind: 'Reminder',
        ),
      );
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));

      expect(find.byKey(const Key('push_snack')), findsOneWidget);
      expect(find.textContaining('Termin sutra u 10:00'), findsOneWidget);
      expect(api.to('GET', '/unread-count').length, greaterThan(before));
    },
  );

  testWidgets(
    'signing out closes the pages opened on top of the shell and shows the login',
    (tester) async {
      final (_, _, auth) = await _open(tester);
      await tester.tap(find.byKey(const Key('open_notifications')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('open_notifications')), findsNothing);

      await tester.runAsync(() => auth.logout());
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('login_email')), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets('without a push configuration the app works as before', (
    tester,
  ) async {
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

    expect(find.byKey(const Key('open_notifications')), findsOneWidget);
    expect(api.to('POST', '/api/v1/devices'), isEmpty);
  });
}
