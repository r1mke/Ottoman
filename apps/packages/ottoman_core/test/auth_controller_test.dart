import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// Scriptable HTTP layer: handler returns (status, json) per request.
class FakeAdapter implements HttpClientAdapter {
  FakeAdapter(this.handler);
  final (int, Object?) Function(RequestOptions) handler;
  final calls = <String>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    calls.add('${options.method} ${options.path}');
    final (status, body) = handler(options);
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

Map<String, dynamic> sessionJson({
  String access = 'access-1',
  String refresh = 'refresh-1',
  List<String> roles = const ['Client'],
}) => {
  'accessToken': access,
  'refreshToken': refresh,
  'accessTokenExpiresUtc': DateTime.utc(2030).toIso8601String(),
  'user': {
    'id': 'u1',
    'email': 'a@b.com',
    'firstName': 'Ana',
    'lastName': 'Anić',
    'phone': null,
    'roles': roles,
  },
};

AuthController build(
  FakeAdapter adapter,
  MemoryTokenStorage storage, {
  Set<String>? allowedRoles,
}) {
  final dio = Dio()..httpClientAdapter = adapter;
  late final AuthController c;
  c = AuthController(
    baseUrl: 'http://test',
    storage: storage,
    allowedRoles: allowedRoles,
    api: ApiClient(
      baseUrl: 'http://test',
      dio: dio,
      readToken: () async => c.accessToken,
      refreshSession: () => c.refreshSession(),
    ),
  );
  return c;
}

void main() {
  test('login stores the refresh token and authenticates', () async {
    final storage = MemoryTokenStorage();
    final c = build(FakeAdapter((_) => (200, sessionJson())), storage);

    await c.login('a@b.com', 'pw');

    expect(c.status, AuthStatus.authenticated);
    expect(c.user!.fullName, 'Ana Anić');
    expect(await storage.readRefreshToken(), 'refresh-1');
  });

  test(
    'wrong credentials surface the server message and stay logged out',
    () async {
      final c = build(
        FakeAdapter((_) => (401, {'detail': 'Neispravan email ili lozinka.'})),
        MemoryTokenStorage(),
      );

      await expectLater(
        c.login('a@b.com', 'bad'),
        throwsA(
          isA<AppException>().having(
            (e) => e.message,
            'message',
            'Neispravan email ili lozinka.',
          ),
        ),
      );
      expect(c.status, isNot(AuthStatus.authenticated));
    },
  );

  test(
    'a role the app does not allow is rejected and the session revoked',
    () async {
      final adapter = FakeAdapter((o) {
        if (o.path.endsWith('/login')) {
          return (200, sessionJson(roles: ['Client']));
        }
        return (204, null);
      });
      final storage = MemoryTokenStorage();
      final c = build(
        adapter,
        storage,
        allowedRoles: {Roles.admin, Roles.employee},
      );

      await expectLater(
        c.login('a@b.com', 'pw'),
        throwsA(
          isA<AppException>().having((e) => e.isForbidden, 'forbidden', true),
        ),
      );
      expect(c.status, isNot(AuthStatus.authenticated));
      expect(await storage.readRefreshToken(), isNull);
      expect(adapter.calls, contains('POST /api/v1/auth/logout'));
    },
  );

  test('restore with a valid refresh token authenticates', () async {
    final storage = MemoryTokenStorage()..writeRefreshToken('stored');
    final c = build(
      FakeAdapter((_) => (200, sessionJson(refresh: 'rotated'))),
      storage,
    );

    await c.restore();

    expect(c.status, AuthStatus.authenticated);
    expect(await storage.readRefreshToken(), 'rotated');
  });

  test('restore with a rejected refresh token clears the session', () async {
    final storage = MemoryTokenStorage()..writeRefreshToken('stale');
    final c = build(FakeAdapter((_) => (401, {'detail': 'x'})), storage);

    await c.restore();

    expect(c.status, AuthStatus.unauthenticated);
    expect(await storage.readRefreshToken(), isNull);
  });

  test('a network failure during restore keeps the stored token', () async {
    final storage = MemoryTokenStorage()..writeRefreshToken('keep');
    final dio = Dio()..httpClientAdapter = _ThrowingAdapter();
    late final AuthController c;
    c = AuthController(
      baseUrl: 'http://test',
      storage: storage,
      api: ApiClient(
        baseUrl: 'http://test',
        dio: dio,
        readToken: () async => null,
        refreshSession: () => c.refreshSession(),
      ),
    );

    await c.restore();

    expect(await storage.readRefreshToken(), 'keep');
  });

  test('parallel 401s trigger exactly one refresh and then succeed', () async {
    final storage = MemoryTokenStorage();
    var refreshed = false;
    final adapter = FakeAdapter((o) {
      if (o.path.endsWith('/login')) return (200, sessionJson());
      if (o.path.endsWith('/refresh')) {
        refreshed = true;
        return (200, sessionJson(access: 'access-2', refresh: 'refresh-2'));
      }
      final ok = o.headers['Authorization'] == 'Bearer access-2';
      return ok ? (200, {'ok': true}) : (401, {'detail': 'expired'});
    });
    final c = build(adapter, storage);
    await c.login('a@b.com', 'pw');

    final results = await Future.wait(
      List.generate(5, (_) => c.api.get<Map<String, dynamic>>('/api/v1/me')),
    );

    expect(results.every((r) => r['ok'] == true), isTrue);
    expect(refreshed, isTrue);
    expect(adapter.calls.where((c) => c.endsWith('/refresh')).length, 1);
  });
}

class _ThrowingAdapter implements HttpClientAdapter {
  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) => throw DioException(
    requestOptions: options,
    type: DioExceptionType.connectionError,
  );

  @override
  void close({bool force = false}) {}
}
