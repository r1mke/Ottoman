import '../network/api_client.dart';
import '../reservations/reservation_models.dart';

class ClientStats {
  const ClientStats({
    required this.visits,
    required this.noShows,
    required this.cancelled,
    required this.upcoming,
    required this.servicesSpent,
    required this.orders,
    required this.productsSpent,
    this.lastVisitUtc,
  });

  final int visits;
  final int noShows;
  final int cancelled;
  final int upcoming;
  final double servicesSpent;
  final int orders;
  final double productsSpent;
  final DateTime? lastVisitUtc;

  double get totalSpent => servicesSpent + productsSpent;

  factory ClientStats.fromJson(Map<String, dynamic> j) => ClientStats(
    visits: j['visits'] as int,
    noShows: j['noShows'] as int,
    cancelled: j['cancelled'] as int,
    upcoming: j['upcoming'] as int,
    servicesSpent: (j['servicesSpent'] as num).toDouble(),
    orders: j['orders'] as int,
    productsSpent: (j['productsSpent'] as num).toDouble(),
    lastVisitUtc: j['lastVisitUtc'] == null
        ? null
        : DateTime.parse(j['lastVisitUtc'] as String),
  );
}

class ClientReservation {
  const ClientReservation({
    required this.id,
    required this.startUtc,
    required this.status,
    required this.employeeName,
    required this.services,
    required this.total,
  });

  final int id;
  final DateTime startUtc;
  final String status;
  final String employeeName;
  final String services;
  final double total;

  factory ClientReservation.fromJson(Map<String, dynamic> j) =>
      ClientReservation(
        id: j['id'] as int,
        startUtc: DateTime.parse(j['startUtc'] as String),
        status: j['status'] as String,
        employeeName: j['employeeName'] as String,
        services: j['services'] as String,
        total: (j['total'] as num).toDouble(),
      );
}

class ClientOrder {
  const ClientOrder({
    required this.id,
    required this.createdAtUtc,
    required this.status,
    required this.total,
    required this.items,
  });

  final int id;
  final DateTime createdAtUtc;
  final String status;
  final double total;
  final String items;

  factory ClientOrder.fromJson(Map<String, dynamic> j) => ClientOrder(
    id: j['id'] as int,
    createdAtUtc: DateTime.parse(j['createdAtUtc'] as String),
    status: j['status'] as String,
    total: (j['total'] as num).toDouble(),
    items: j['items'] as String,
  );
}

class ClientDetail {
  const ClientDetail({
    required this.id,
    required this.fullName,
    required this.email,
    required this.registeredAtUtc,
    required this.isBlocked,
    required this.stats,
    required this.reservations,
    required this.orders,
    this.phone,
    this.photoUrl,
    this.photoZoom = 1.0,
    this.lockedUntilUtc,
  });

  final String id;
  final String fullName;
  final String email;
  final String? phone;
  final DateTime registeredAtUtc;
  final bool isBlocked;
  final ClientStats stats;
  final List<ClientReservation> reservations;
  final List<ClientOrder> orders;

  /// The client's own picture, already cropped at [photoZoom] (1 to 2) around the face.
  final String? photoUrl;
  final double photoZoom;

  /// Set while too many wrong passwords keep the client from signing in.
  final DateTime? lockedUntilUtc;

  factory ClientDetail.fromJson(Map<String, dynamic> j) => ClientDetail(
    id: j['id'] as String,
    fullName: j['fullName'] as String,
    email: j['email'] as String,
    phone: j['phone'] as String?,
    registeredAtUtc: DateTime.parse(j['registeredAtUtc'] as String),
    isBlocked: j['isBlocked'] as bool,
    stats: ClientStats.fromJson(j['stats'] as Map<String, dynamic>),
    reservations: (j['reservations'] as List)
        .map((e) => ClientReservation.fromJson(e as Map<String, dynamic>))
        .toList(),
    orders: (j['orders'] as List)
        .map((e) => ClientOrder.fromJson(e as Map<String, dynamic>))
        .toList(),
    photoUrl: j['photoUrl'] as String?,
    photoZoom: (j['photoZoom'] as num?)?.toDouble() ?? 1.0,
    lockedUntilUtc: j['lockedUntilUtc'] == null
        ? null
        : DateTime.parse(j['lockedUntilUtc'] as String),
  );
}

/// Admin: the people who book and buy.
class ClientsApi {
  ClientsApi(this._api);

  final ApiClient _api;

  Future<Paged<ClientItem>> list({
    String? search,
    int page = 1,
    int pageSize = 20,
  }) async {
    final data = await _api.get<Map<String, dynamic>>(
      '/api/v1/clients',
      query: {
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
        'page': page,
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

  Future<ClientDetail> detail(String id) async => ClientDetail.fromJson(
    await _api.get<Map<String, dynamic>>('/api/v1/clients/$id'),
  );

  /// Ends a sign-in lock (too many wrong passwords) at once. Admin only.
  Future<void> unlock(String id) =>
      _api.post<dynamic>('/api/v1/users/$id/unlock');

  /// Sets a new password without the old one and ends every session of the client. Admin only.
  Future<void> setPassword(String id, String newPassword) => _api.post<dynamic>(
    '/api/v1/users/$id/password',
    body: {'newPassword': newPassword},
  );

  /// Blocking ends every session of the client at once.
  Future<ClientItem> setBlocked(String id, {required bool blocked}) async =>
      ClientItem.fromJson(
        await _api.post<Map<String, dynamic>>(
          '/api/v1/clients/$id/${blocked ? 'block' : 'unblock'}',
        ),
      );
}
