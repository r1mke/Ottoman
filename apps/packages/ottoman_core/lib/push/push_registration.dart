import 'dart:async';

import '../network/api_client.dart';

/// A push message that arrived while the app was open.
class PushNote {
  const PushNote({required this.title, required this.body, this.kind});

  final String title;
  final String body;
  final String? kind;
}

/// The push provider on the phone (Firebase) behind a port, so the app logic is testable without it.
abstract class PushClient {
  /// Asks the user for permission to show notifications. False when refused.
  Future<bool> requestPermission();

  Future<String?> token();

  Stream<String> get onTokenRefresh;

  Stream<PushNote> get onForeground;

  /// Forgets this phone's token so a new one is issued next time.
  Future<void> deleteToken();
}

/// Used when push is not available (tests, no Firebase configuration, desktop): does nothing.
class NoPushClient implements PushClient {
  const NoPushClient();

  @override
  Future<bool> requestPermission() async => false;

  @override
  Future<String?> token() async => null;

  @override
  Stream<String> get onTokenRefresh => const Stream.empty();

  @override
  Stream<PushNote> get onForeground => const Stream.empty();

  @override
  Future<void> deleteToken() async {}
}

class DevicesApi {
  DevicesApi(this._api);

  final ApiClient _api;

  Future<void> register(String token, String platform) => _api.post<dynamic>(
    '/api/v1/devices',
    body: {'token': token, 'platform': platform},
  );

  /// Token goes in the body, not the URL, so it never ends up in access logs.
  Future<void> unregister(String token) =>
      _api.post<dynamic>('/api/v1/devices/unregister', body: {'token': token});
}

/// Keeps the server informed which phone belongs to the signed-in user.
/// Push is a convenience: nothing here may break signing in or out, so every failure is swallowed.
class PushRegistration {
  PushRegistration({
    required this.devices,
    required this.client,
    this.platform = 'android',
  });

  final DevicesApi devices;
  final PushClient client;
  final String platform;

  StreamSubscription<String>? _refresh;
  String? _registered;
  bool _started = false;

  /// Messages that arrive while the app is open.
  Stream<PushNote> get notes => client.onForeground;

  /// Call after sign-in. Safe to call again.
  Future<void> start() async {
    if (_started) return;
    _started = true;
    try {
      if (!await client.requestPermission()) return;
      await _register(await client.token());
      _refresh = client.onTokenRefresh.listen(_register);
    } on Object {
      // Not fatal: the inbox and mail still work without push.
    }
  }

  Future<void> _register(String? token) async {
    if (token == null || token.isEmpty || token == _registered) return;
    try {
      await devices.register(token, platform);
      _registered = token;
    } on Object {
      // Retried at the next start or token refresh.
    }
  }

  /// Call before sign-out, while the session is still valid, so the next person on this phone
  /// does not receive this person's messages.
  Future<void> stop() async {
    await _refresh?.cancel();
    _refresh = null;
    final token = _registered;
    _registered = null;
    _started = false;
    try {
      if (token != null) await devices.unregister(token);
    } on Object {
      // The server also drops tokens Firebase reports as dead.
    }
    try {
      await client.deleteToken();
    } on Object {
      // Best effort.
    }
  }
}
