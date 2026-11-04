import '../network/api_client.dart';
import 'reservation_models.dart';

String _date(DateTime d) =>
    '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

class ReservationsApi {
  ReservationsApi(this._api);

  final ApiClient _api;

  Future<Reservation> create({
    required String employeeId,
    required List<int> serviceIds,
    required DateTime startUtc,
    String? note,
    String? clientId,
    String? idempotencyKey,
  }) async => Reservation.fromJson(
    await _api.post<Map<String, dynamic>>(
      '/api/v1/reservations',
      idempotencyKey: idempotencyKey,
      body: {
        'employeeId': employeeId,
        'serviceIds': serviceIds,
        'startUtc': startUtc.toUtc().toIso8601String(),
        'note': note,
        'clientId': clientId,
      },
    ),
  );

  /// [from]/[to] are salon-local calendar days (inclusive); the server resolves the time zone.
  Future<Paged<Reservation>> list({
    bool? upcoming,
    DateTime? from,
    DateTime? to,
    String? employeeId,
    String? status,
    int page = 1,
    int pageSize = 50,
  }) async {
    final data = await _api.get<Map<String, dynamic>>(
      '/api/v1/reservations',
      query: {
        'upcoming': ?upcoming,
        if (from != null) 'fromDate': _date(from),
        if (to != null) 'toDate': _date(to),
        'employeeId': ?employeeId,
        'status': ?status,
        'page': page,
        'pageSize': pageSize,
      },
    );
    return Paged(
      (data['items'] as List)
          .map((e) => Reservation.fromJson(e as Map<String, dynamic>))
          .toList(),
      data['totalCount'] as int,
    );
  }

  Future<ReservationSummary> summary({
    DateTime? from,
    DateTime? to,
    String? employeeId,
  }) async => ReservationSummary.fromJson(
    await _api.get<Map<String, dynamic>>(
      '/api/v1/reservations/summary',
      query: {
        if (from != null) 'fromDate': _date(from),
        if (to != null) 'toDate': _date(to),
        'employeeId': ?employeeId,
      },
    ),
  );

  Future<Reservation> cancel(int id, {String? reason}) async =>
      Reservation.fromJson(
        await _api.post<Map<String, dynamic>>(
          '/api/v1/reservations/$id/cancel',
          body: {'reason': reason},
        ),
      );

  /// Staff: confirms a booking that waits for the salon.
  Future<Reservation> confirm(int id) async => Reservation.fromJson(
    await _api.post<Map<String, dynamic>>('/api/v1/reservations/$id/confirm'),
  );

  /// Staff: turns a waiting booking down. The reason is required; the client gets it.
  Future<Reservation> decline(int id, {required String reason}) async =>
      Reservation.fromJson(
        await _api.post<Map<String, dynamic>>(
          '/api/v1/reservations/$id/decline',
          body: {'reason': reason},
        ),
      );

  Future<Reservation> complete(int id) async => Reservation.fromJson(
    await _api.post<Map<String, dynamic>>('/api/v1/reservations/$id/complete'),
  );

  Future<Reservation> markNoShow(int id) async => Reservation.fromJson(
    await _api.post<Map<String, dynamic>>('/api/v1/reservations/$id/no-show'),
  );

  Future<Reservation> reschedule(
    int id, {
    required DateTime startUtc,
    String? employeeId,
  }) async => Reservation.fromJson(
    await _api.put<Map<String, dynamic>>(
      '/api/v1/reservations/$id/reschedule',
      body: {
        'startUtc': startUtc.toUtc().toIso8601String(),
        'employeeId': employeeId,
      },
    ),
  );

  Future<Paged<ClientItem>> clients({String? search, int pageSize = 20}) async {
    final data = await _api.get<Map<String, dynamic>>(
      '/api/v1/clients',
      query: {
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
        'pageSize': pageSize,
      },
    );
    return Paged(
      (data['items'] as List)
          .map((e) => ClientItem.fromJson(e as Map<String, dynamic>))
          .toList(),
      data['totalCount'] as int,
    );
  }
}
