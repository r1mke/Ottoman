import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Persists the refresh token only. The short-lived access token stays in memory.
abstract class TokenStorage {
  Future<String?> readRefreshToken();
  Future<void> writeRefreshToken(String token);
  Future<void> clear();
}

class SecureTokenStorage implements TokenStorage {
  SecureTokenStorage({
    FlutterSecureStorage? storage,
    String keyPrefix = 'ottoman',
  }) : _storage = storage ?? const FlutterSecureStorage(),
       _key = '$keyPrefix.refresh_token';

  final FlutterSecureStorage _storage;
  final String _key;

  @override
  Future<String?> readRefreshToken() => _storage.read(key: _key);

  @override
  Future<void> writeRefreshToken(String token) =>
      _storage.write(key: _key, value: token);

  @override
  Future<void> clear() => _storage.delete(key: _key);
}

class MemoryTokenStorage implements TokenStorage {
  String? _token;

  @override
  Future<String?> readRefreshToken() async => _token;

  @override
  Future<void> writeRefreshToken(String token) async => _token = token;

  @override
  Future<void> clear() async => _token = null;
}
