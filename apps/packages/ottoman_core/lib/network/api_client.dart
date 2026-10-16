import 'dart:math';
import 'dart:typed_data';

import 'package:dio/dio.dart';

import 'app_exception.dart';

/// A file returned by the API (e.g. an exported report).
class DownloadedFile {
  const DownloadedFile(this.bytes, this.fileName);
  final Uint8List bytes;
  final String fileName;
}

typedef TokenReader = Future<String?> Function();

/// Called once on a 401. Return true if the session was renewed and the request
/// should be retried with the new token.
typedef SessionRefresher = Future<bool> Function();

/// Thin Dio wrapper: bearer auth, one refresh-and-retry on 401, and every
/// failure mapped to [AppException].
class ApiClient {
  ApiClient({
    required String baseUrl,
    required this.readToken,
    this.refreshSession,
    Dio? dio,
  }) : dio = dio ?? Dio() {
    this.dio.options
      ..baseUrl = baseUrl
      ..connectTimeout = const Duration(seconds: 10)
      ..receiveTimeout = const Duration(seconds: 20)
      ..headers['Accept'] = 'application/json';
    this.dio.interceptors.add(_AuthInterceptor(this));
  }

  final Dio dio;
  final TokenReader readToken;
  final SessionRefresher? refreshSession;

  Future<T> get<T>(String path, {Map<String, dynamic>? query}) =>
      _send(() => dio.get<dynamic>(path, queryParameters: query));

  /// [idempotencyKey] makes a repeat of the same write harmless: the server answers the second request with the
  /// first answer instead of doing it again (double tap, retry after a lost response).
  Future<T> post<T>(String path, {Object? body, String? idempotencyKey}) =>
      _send(
        () => dio.post<dynamic>(
          path,
          data: body,
          options: idempotencyKey == null
              ? null
              : Options(headers: {'Idempotency-Key': idempotencyKey}),
        ),
      );

  Future<T> put<T>(String path, {Object? body}) =>
      _send(() => dio.put<dynamic>(path, data: body));

  Future<T> delete<T>(String path) => _send(() => dio.delete<dynamic>(path));

  /// Downloads a file (report export). Goes through the same auth/refresh handling as every other call.
  Future<DownloadedFile> download(
    String path, {
    Map<String, dynamic>? query,
    Object? body,
    String method = 'GET',
  }) async {
    try {
      final res = await dio.request<List<int>>(
        path,
        data: body,
        queryParameters: query,
        options: Options(method: method, responseType: ResponseType.bytes),
      );
      return DownloadedFile(
        Uint8List.fromList(res.data ?? const []),
        _fileNameFrom(res.headers.value('content-disposition')) ?? 'izvjestaj',
      );
    } on DioException catch (e) {
      throw AppException.fromDio(e);
    }
  }

  /// `attachment; filename=x.csv; filename*=UTF-8''x.csv` -> `x.csv`. The name only ever decorates a local file,
  /// so anything that is not a plain file name is replaced.
  static String? _fileNameFrom(String? header) {
    if (header == null) return null;
    final match = RegExp(
      r'''filename\*?=(?:UTF-8'')?"?([^";]+)"?''',
      caseSensitive: false,
    ).firstMatch(header);
    final name = match?.group(1);
    if (name == null) return null;
    final clean = Uri.decodeComponent(name)
        .replaceAll(RegExp(r'[\\/:*?"<>|]'), '_');
    return clean.isEmpty ? null : clean;
  }

  Future<T> _send<T>(Future<Response<dynamic>> Function() call) async {
    try {
      final res = await call();
      return res.data as T;
    } on DioException catch (e) {
      throw AppException.fromDio(e);
    }
  }
}

class _AuthInterceptor extends Interceptor {
  _AuthInterceptor(this._client);
  final ApiClient _client;

  static const _retriedKey = 'ottoman_retried';

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    final token = await _client.readToken();
    if (token != null && token.isNotEmpty) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    final req = err.requestOptions;
    final isAuthCall = req.path.startsWith('/api/v1/auth/');
    if (err.response?.statusCode == 401 &&
        !isAuthCall &&
        req.extra[_retriedKey] != true &&
        _client.refreshSession != null) {
      final renewed = await _client.refreshSession!();
      if (renewed) {
        req.extra[_retriedKey] = true;
        try {
          final res = await _client.dio.fetch<dynamic>(req);
          return handler.resolve(res);
        } on DioException catch (e) {
          return handler.next(e);
        }
      }
    }
    handler.next(err);
  }
}

/// Multipart body for the image endpoints. The server decides the real type from the bytes.
FormData imageForm(List<int> bytes, String filename) => FormData.fromMap({
  'file': MultipartFile.fromBytes(bytes, filename: filename),
});

/// A fresh random key for one user intention (one tap on "Order" or "Book"). Reuse it for retries of that same
/// attempt, never for a different one.
String newIdempotencyKey() {
  final random = Random.secure();
  return List.generate(
    16,
    (_) => random.nextInt(256).toRadixString(16).padLeft(2, '0'),
  ).join();
}
