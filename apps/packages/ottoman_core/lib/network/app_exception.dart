import 'package:dio/dio.dart';

/// Error surfaced to the UI. Built from an RFC 7807 ProblemDetails body when the
/// API sends one, otherwise from the transport failure.
class AppException implements Exception {
  AppException(
    this.message, {
    this.statusCode,
    this.errors = const {},
    this.isNetwork = false,
  });

  final String message;
  final int? statusCode;

  /// Field validation errors: field name -> messages.
  final Map<String, List<String>> errors;
  final bool isNetwork;

  /// The server's field messages, one per line ('' when there are none).
  String get fieldErrors => errors.values.expand((x) => x).join('\n');

  /// What to show the user: the field messages when the server sent some, otherwise the general message.
  String get detail => fieldErrors.isEmpty ? message : fieldErrors;

  bool get isUnauthorized => statusCode == 401;
  bool get isForbidden => statusCode == 403;
  bool get isConflict => statusCode == 409;

  factory AppException.fromDio(DioException e) {
    final res = e.response;
    if (res == null) {
      return AppException(
        'Nema veze sa serverom. Provjerite internet i pokušajte ponovo.',
        isNetwork: true,
      );
    }
    final data = res.data;
    if (data is Map<String, dynamic>) {
      final errs = <String, List<String>>{};
      final raw = data['errors'];
      if (raw is Map) {
        raw.forEach((k, v) {
          errs['$k'] = v is List ? v.map((x) => '$x').toList() : ['$v'];
        });
      }
      final detail = data['detail'] ?? data['title'];
      final message = detail is String && detail.isNotEmpty
          ? detail
          : (errs.values.expand((x) => x).firstOrNull ??
                _fallback(res.statusCode));
      return AppException(message, statusCode: res.statusCode, errors: errs);
    }
    return AppException(_fallback(res.statusCode), statusCode: res.statusCode);
  }

  static String _fallback(int? code) => switch (code) {
    401 => 'Prijava je istekla. Prijavite se ponovo.',
    403 => 'Nemate dozvolu za ovu radnju.',
    404 => 'Traženi podatak nije pronađen.',
    409 =>
      'Podatak je u međuvremenu promijenjen. Osvježite i pokušajte ponovo.',
    429 => 'Previše pokušaja. Pokušajte ponovo za nekoliko minuta.',
    final c? when c >= 500 => 'Greška na serveru. Pokušajte kasnije.',
    _ => 'Došlo je do greške.',
  };

  @override
  String toString() => 'AppException($statusCode): $message';
}
