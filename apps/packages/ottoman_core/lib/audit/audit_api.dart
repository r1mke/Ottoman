import '../network/api_client.dart';
import '../reservations/reservation_models.dart';

class AuditEntry {
  const AuditEntry({
    required this.id,
    required this.atUtc,
    required this.userId,
    required this.userName,
    required this.role,
    required this.method,
    required this.action,
    required this.target,
    required this.statusCode,
    this.ip,
    this.targetLabel,
  });

  final int id;
  final DateTime atUtc;
  final String userId;
  final String userName;
  final String role;
  final String method;

  /// "Controller.Action", e.g. Products.AdjustStock.
  final String action;

  /// Ids from the URL, e.g. "id=7".
  final String target;
  final int statusCode;
  final String? ip;

  /// What the target is called in words ("Proizvod: Vosak"); null when the server cannot name it.
  final String? targetLabel;

  String get controller => action.split('.').first;
  String get verb => action.contains('.') ? action.split('.').last : action;

  factory AuditEntry.fromJson(Map<String, dynamic> j) => AuditEntry(
    id: j['id'] as int,
    atUtc: DateTime.parse(j['atUtc'] as String),
    userId: j['userId'] as String,
    userName: j['userName'] as String,
    role: j['role'] as String,
    method: j['method'] as String,
    action: j['action'] as String,
    target: j['target'] as String,
    statusCode: j['statusCode'] as int,
    ip: j['ip'] as String?,
    targetLabel: j['targetLabel'] as String?,
  );
}

/// Admin: who changed what. Read-only.
class AuditApi {
  AuditApi(this._api);

  final ApiClient _api;

  Future<Paged<AuditEntry>> list({
    String? search,
    String? userId,
    int page = 1,
    int pageSize = 30,
  }) async {
    final data = await _api.get<Map<String, dynamic>>(
      '/api/v1/audit',
      query: {
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
        'userId': ?userId,
        'page': page,
        'pageSize': pageSize,
      },
    );
    return Paged(
      (data['items'] as List)
          .map((e) => AuditEntry.fromJson(e as Map<String, dynamic>))
          .toList(),
      data['totalCount'] as int,
    );
  }
}

/// Section names as the admin knows them; unknown controllers keep their code name.
String auditAreaLabel(String controller) => switch (controller) {
  'Products' => 'Proizvodi',
  'Brands' => 'Brendovi',
  'Categories' => 'Kategorije',
  'Services' => 'Usluge',
  'Employees' => 'Uposlenici',
  'Clients' => 'Klijenti',
  'Orders' => 'Narudžbe',
  'Reservations' => 'Termini',
  'Reviews' => 'Recenzije',
  'Announcements' => 'Obavijesti',
  'Settings' => 'Postavke',
  'Reports' => 'Izvještaji',
  _ => controller,
};
