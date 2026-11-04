class ReservationItem {
  const ReservationItem({
    required this.serviceId,
    required this.name,
    required this.price,
    required this.durationMinutes,
  });

  final int serviceId;
  final String name;
  final double price;
  final int durationMinutes;

  factory ReservationItem.fromJson(Map<String, dynamic> j) => ReservationItem(
    serviceId: j['serviceId'] as int,
    name: j['name'] as String,
    price: (j['price'] as num).toDouble(),
    durationMinutes: j['durationMinutes'] as int,
  );
}

/// Status values as sent by the API.
abstract final class ReservationStatus {
  static const pending = 'Pending';
  static const confirmed = 'Confirmed';
  static const completed = 'Completed';
  static const cancelled = 'Cancelled';
  static const noShow = 'NoShow';
}

class Reservation {
  const Reservation({
    required this.id,
    required this.clientId,
    required this.clientName,
    required this.employeeId,
    required this.employeeName,
    required this.startUtc,
    required this.endUtc,
    required this.localStart,
    required this.status,
    required this.totalPrice,
    required this.totalDurationMinutes,
    required this.items,
    required this.canCancel,
    this.note,
    this.cancelReason,
    this.canConfirm = false,
    this.canDecline = false,
    this.declined = false,
    this.confirmedBy,
    this.completedBy,
  });

  final int id;
  final String clientId;
  final String clientName;
  final String employeeId;
  final String employeeName;
  final DateTime startUtc;
  final DateTime endUtc;

  /// Salon-local "yyyy-MM-dd HH:mm".
  final String localStart;
  final String status;
  final String? note;
  final double totalPrice;
  final int totalDurationMinutes;
  final List<ReservationItem> items;
  final bool canCancel;
  final String? cancelReason;

  /// The server says this person may confirm / decline it now (staff, booking still waiting).
  final bool canConfirm;
  final bool canDecline;

  /// The salon turned it down before confirming it (the reason is in [cancelReason]).
  final bool declined;

  /// "System" (auto-confirmed), "Employee" or "Admin".
  final String? confirmedBy;
  final String? completedBy;

  bool get isPending => status == ReservationStatus.pending;

  String get localDate => localStart.substring(0, 10);
  String get localTime => localStart.substring(11);
  String get serviceNames => items.map((i) => i.name).join(', ');
  bool get isOpen =>
      status == ReservationStatus.confirmed ||
      status == ReservationStatus.pending;

  /// Staff may finish an appointment once it has started and was confirmed.
  bool canFinish(DateTime nowUtc) =>
      status == ReservationStatus.confirmed && !startUtc.isAfter(nowUtc);

  factory Reservation.fromJson(Map<String, dynamic> j) => Reservation(
    id: j['id'] as int,
    clientId: j['clientId'] as String,
    clientName: j['clientName'] as String,
    employeeId: j['employeeId'] as String,
    employeeName: j['employeeName'] as String,
    startUtc: DateTime.parse(j['startUtc'] as String).toUtc(),
    endUtc: DateTime.parse(j['endUtc'] as String).toUtc(),
    localStart: j['localStart'] as String,
    status: j['status'] as String,
    note: j['note'] as String?,
    totalPrice: (j['totalPrice'] as num).toDouble(),
    totalDurationMinutes: j['totalDurationMinutes'] as int,
    items: (j['items'] as List)
        .map((e) => ReservationItem.fromJson(e as Map<String, dynamic>))
        .toList(),
    canCancel: j['canCancel'] as bool,
    cancelReason: j['cancelReason'] as String?,
    canConfirm: (j['canConfirm'] as bool?) ?? false,
    canDecline: (j['canDecline'] as bool?) ?? false,
    declined: (j['declined'] as bool?) ?? false,
    confirmedBy: j['confirmedBy'] as String?,
    completedBy: j['completedBy'] as String?,
  );
}

class ReservationSummary {
  const ReservationSummary({
    required this.upcoming,
    required this.completed,
    required this.cancelled,
    required this.noShow,
    required this.totalClients,
  });

  final int upcoming;
  final int completed;
  final int cancelled;
  final int noShow;
  final int totalClients;

  factory ReservationSummary.fromJson(Map<String, dynamic> j) =>
      ReservationSummary(
        upcoming: j['upcoming'] as int,
        completed: j['completed'] as int,
        cancelled: j['cancelled'] as int,
        noShow: j['noShow'] as int,
        totalClients: j['totalClients'] as int,
      );
}

class Paged<T> {
  const Paged(this.items, this.totalCount);
  final List<T> items;
  final int totalCount;
}

class ClientItem {
  const ClientItem({
    required this.id,
    required this.fullName,
    required this.email,
    this.phone,
    this.registeredAtUtc,
    this.isBlocked = false,
  });

  final String id;
  final String fullName;
  final String email;
  final String? phone;
  final DateTime? registeredAtUtc;
  final bool isBlocked;

  factory ClientItem.fromJson(Map<String, dynamic> j) => ClientItem(
    id: j['id'] as String,
    fullName: j['fullName'] as String,
    email: j['email'] as String,
    phone: j['phone'] as String?,
    registeredAtUtc: j['registeredAtUtc'] == null
        ? null
        : DateTime.parse(j['registeredAtUtc'] as String),
    isBlocked: (j['isBlocked'] as bool?) ?? false,
  );
}
