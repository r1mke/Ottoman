import 'dart:async';

import 'package:flutter/foundation.dart';

import '../network/api_client.dart';
import '../network/app_exception.dart';
import 'auth_models.dart';
import 'token_storage.dart';

enum AuthStatus { unknown, authenticated, unauthenticated }

/// Owns the session: login, silent restore from the stored refresh token,
/// single-flight refresh (shared with [ApiClient]) and logout.
class AuthController extends ChangeNotifier {
  AuthController({
    required this.baseUrl,
    required this.storage,
    this.allowedRoles,
    ApiClient? api,
  }) {
    this.api =
        api ??
        ApiClient(
          baseUrl: baseUrl,
          readToken: () async => _session?.accessToken,
          refreshSession: refreshSession,
        );
  }

  final String baseUrl;
  final TokenStorage storage;

  /// If set, a login whose user has none of these roles is rejected (e.g. desktop = Admin/Employee).
  final Set<String>? allowedRoles;
  late final ApiClient api;

  AuthStatus _status = AuthStatus.unknown;
  AuthSession? _session;
  Future<bool>? _refreshing;

  AuthStatus get status => _status;
  AppUser? get user => _session?.user;
  String? get accessToken => _session?.accessToken;

  /// Called once at startup.
  Future<void> restore() async {
    final refresh = await storage.readRefreshToken();
    if (refresh == null) return _set(AuthStatus.unauthenticated);
    final ok = await refreshSession();
    if (!ok) _set(AuthStatus.unauthenticated);
  }

  Future<void> login(String email, String password) async {
    final data = await api.post<Map<String, dynamic>>(
      '/api/v1/auth/login',
      body: {'email': email, 'password': password},
    );
    await _accept(AuthSession.fromJson(data));
  }

  /// Registration does not log in (the API never reveals whether the email existed).
  Future<void> register({
    required String firstName,
    required String lastName,
    required String email,
    required String password,
    String? phone,
  }) async {
    await api.post<dynamic>(
      '/api/v1/auth/register',
      body: {
        'firstName': firstName,
        'lastName': lastName,
        'email': email,
        'phone': phone,
        'password': password,
      },
    );
  }

  Future<void> forgotPassword(String email) =>
      api.post<dynamic>('/api/v1/auth/forgot-password', body: {'email': email});

  Future<void> logout() async {
    final refresh = await storage.readRefreshToken();
    if (refresh != null) {
      try {
        await api.post<dynamic>(
          '/api/v1/auth/logout',
          body: {'refreshToken': refresh},
        );
      } on AppException {
        // Best effort: the local session is cleared regardless.
      }
    }
    await _clear();
  }

  /// Re-reads /me (e.g. after a profile edit).
  Future<void> reloadUser() async {
    final data = await api.get<Map<String, dynamic>>('/api/v1/me');
    final s = _session;
    if (s == null) return;
    _session = AuthSession(
      accessToken: s.accessToken,
      refreshToken: s.refreshToken,
      accessTokenExpiresUtc: s.accessTokenExpiresUtc,
      user: AppUser.fromJson(data),
    );
    notifyListeners();
  }

  // ---- internals

  /// Concurrent 401s must trigger exactly one refresh (refresh tokens are single-use).
  Future<bool> refreshSession() =>
      _refreshing ??= _doRefresh().whenComplete(() => _refreshing = null);

  Future<bool> _doRefresh() async {
    final refresh = await storage.readRefreshToken();
    if (refresh == null) {
      await _clear();
      return false;
    }
    try {
      final data = await api.post<Map<String, dynamic>>(
        '/api/v1/auth/refresh',
        body: {'refreshToken': refresh},
      );
      await _accept(AuthSession.fromJson(data));
      return true;
    } on AppException catch (e) {
      // Only a definitive rejection ends the session; a network blip keeps it.
      if (e.isUnauthorized || e.statusCode == 400) await _clear();
      return false;
    }
  }

  Future<void> _accept(AuthSession s) async {
    final allowed = allowedRoles;
    if (allowed != null && !s.user.roles.any(allowed.contains)) {
      // Don't leave a live server session behind for an account this app refuses.
      try {
        await api.post<dynamic>(
          '/api/v1/auth/logout',
          body: {'refreshToken': s.refreshToken},
        );
      } on AppException {
        // ignore: best effort
      }
      throw AppException(
        'Ovaj račun nema pristup ovoj aplikaciji.',
        statusCode: 403,
      );
    }
    _session = s;
    await storage.writeRefreshToken(s.refreshToken);
    _set(AuthStatus.authenticated);
  }

  Future<void> _clear() async {
    _session = null;
    await storage.clear();
    _set(AuthStatus.unauthenticated);
  }

  void _set(AuthStatus s) {
    _status = s;
    notifyListeners();
  }
}
