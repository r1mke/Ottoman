import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

class _Server implements HttpClientAdapter {
  final calls = <RequestOptions>[];
  bool failing = false;

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    calls.add(o);
    return ResponseBody.fromString(
      jsonEncode({}),
      failing ? 500 : 204,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

class _Phone implements PushClient {
  bool allowed = true;
  String? current = 'tok1';
  bool deleted = false;
  final refresh = StreamController<String>.broadcast();
  final foreground = StreamController<PushNote>.broadcast();

  @override
  Future<bool> requestPermission() async => allowed;

  @override
  Future<String?> token() async => current;

  @override
  Stream<String> get onTokenRefresh => refresh.stream;

  @override
  Stream<PushNote> get onForeground => foreground.stream;

  @override
  Future<void> deleteToken() async => deleted = true;
}

(PushRegistration, _Server, _Phone) _make() {
  final server = _Server();
  final phone = _Phone();
  final api = ApiClient(
    baseUrl: 'http://test',
    dio: Dio()..httpClientAdapter = server,
    readToken: () async => 't',
  );
  return (
    PushRegistration(devices: DevicesApi(api), client: phone),
    server,
    phone,
  );
}

Map<String, dynamic> _body(RequestOptions o) => o.data as Map<String, dynamic>;

void main() {
  test(
    'start asks permission, then registers the token with the platform',
    () async {
      final (push, server, _) = _make();

      await push.start();

      expect(server.calls, hasLength(1));
      expect(server.calls.single.path, '/api/v1/devices');
      expect(_body(server.calls.single), {
        'token': 'tok1',
        'platform': 'android',
      });
    },
  );

  test('without permission nothing is sent', () async {
    final (push, server, phone) = _make();
    phone.allowed = false;

    await push.start();

    expect(server.calls, isEmpty);
  });

  test(
    'starting twice registers once; a refreshed token is registered again',
    () async {
      final (push, server, phone) = _make();

      await push.start();
      await push.start();
      expect(server.calls, hasLength(1));

      phone.refresh.add('tok2');
      await pumpEventQueue();
      await pumpEventQueue();
      expect(server.calls, hasLength(2));
      expect(_body(server.calls.last)['token'], 'tok2');

      phone.refresh.add('tok2');
      await pumpEventQueue();
      expect(
        server.calls,
        hasLength(2),
        reason: 'the same token is not sent twice',
      );
    },
  );

  test(
    'stop tells the server to forget the phone and drops the token locally',
    () async {
      final (push, server, phone) = _make();
      await push.start();

      await push.stop();

      expect(server.calls.last.path, '/api/v1/devices/unregister');
      expect(_body(server.calls.last), {'token': 'tok1'});
      expect(phone.deleted, isTrue);

      phone.refresh.add('tok9');
      await pumpEventQueue();
      expect(
        server.calls,
        hasLength(2),
        reason: 'no registration after sign-out',
      );
    },
  );

  test('server failures never throw out of start or stop', () async {
    final (push, server, _) = _make();
    server.failing = true;

    await push.start();
    await push.stop();
  });

  test('stop before start is harmless and sends nothing', () async {
    final (push, server, phone) = _make();
    await push.stop();
    expect(server.calls, isEmpty);
    expect(phone.deleted, isTrue);
  });

  test('foreground messages are passed on', () async {
    final (push, _, phone) = _make();
    final seen = <PushNote>[];
    push.notes.listen(seen.add);

    phone.foreground.add(
      const PushNote(title: 'Termin', body: 'sutra u 10', kind: 'Reminder'),
    );
    await pumpEventQueue();

    expect(seen.single.title, 'Termin');
    expect(seen.single.kind, 'Reminder');
  });

  test(
    'a session that ends by a password change resets push, so the next sign-in registers again',
    () async {
      final server = _Server();
      final phone = _Phone();
      final api = ApiClient(
        baseUrl: 'http://test',
        dio: Dio()..httpClientAdapter = server,
        readToken: () async => 't',
      );
      final auth = AuthController(
        baseUrl: 'http://test',
        storage: _MemoryStorage(),
        api: api,
      );
      final push = PushRegistration(devices: DevicesApi(api), client: phone);
      push.attach(auth);
      await push.start();

      await auth.changePassword('old', 'new');
      await pumpEventQueue();

      expect(auth.status, AuthStatus.unauthenticated);
      expect(phone.deleted, isTrue, reason: 'the old token must stop working');
      phone.refresh.add('tok-late');
      await pumpEventQueue();
      expect(
        server.calls.where((c) => c.path == '/api/v1/devices'),
        hasLength(1),
        reason: 'no registration while signed out',
      );

      phone.current = 'tok2';
      await push.start(); // the next sign-in
      final registrations = server.calls
          .where((c) => c.path == '/api/v1/devices')
          .toList();
      expect(registrations, hasLength(2));
      expect(_body(registrations.last)['token'], 'tok2');
    },
  );

  test('reset before start does nothing', () async {
    final (push, server, phone) = _make();
    await push.reset();
    expect(server.calls, isEmpty);
    expect(phone.deleted, isFalse);
  });

  test('logout runs the hook first and survives a failing hook', () async {
    final auth = AuthController(
      baseUrl: 'http://test',
      storage: _MemoryStorage(),
    );
    var ran = false;
    auth.beforeLogout = () async {
      ran = true;
      throw StateError('boom');
    };

    await auth.logout();

    expect(ran, isTrue);
    expect(auth.status, AuthStatus.unauthenticated);
  });
}

class _MemoryStorage implements TokenStorage {
  String? refresh;

  @override
  Future<String?> readRefreshToken() async => refresh;

  @override
  Future<void> writeRefreshToken(String token) async => refresh = token;

  @override
  Future<void> clear() async => refresh = null;
}
