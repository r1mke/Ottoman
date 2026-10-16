import '../network/api_client.dart';
import '../reservations/reservation_models.dart';

class AppNotification {
  const AppNotification({
    required this.id,
    required this.title,
    required this.body,
    required this.kind,
    required this.createdAtUtc,
    required this.isRead,
  });

  final int id;
  final String title;
  final String body;

  /// Category such as ReservationCreated, Reminder, OrderPaid, Announcement.
  final String kind;
  final DateTime createdAtUtc;
  final bool isRead;

  AppNotification asRead() => AppNotification(
    id: id,
    title: title,
    body: body,
    kind: kind,
    createdAtUtc: createdAtUtc,
    isRead: true,
  );

  factory AppNotification.fromJson(Map<String, dynamic> j) => AppNotification(
    id: j['id'] as int,
    title: j['title'] as String,
    body: j['body'] as String,
    kind: j['kind'] as String,
    createdAtUtc: DateTime.parse(j['createdAtUtc'] as String),
    isRead: j['isRead'] as bool,
  );
}

abstract final class Audience {
  static const all = 'All';
  static const clients = 'Clients';
  static const employees = 'Employees';
  static const user = 'User';
}

class AnnouncementItem {
  const AnnouncementItem({
    required this.id,
    required this.title,
    required this.body,
    required this.audience,
    required this.sendEmail,
    required this.recipientCount,
    required this.createdAtUtc,
  });

  final int id;
  final String title;
  final String body;
  final String audience;
  final bool sendEmail;
  final int recipientCount;
  final DateTime createdAtUtc;

  factory AnnouncementItem.fromJson(Map<String, dynamic> j) => AnnouncementItem(
    id: j['id'] as int,
    title: j['title'] as String,
    body: j['body'] as String,
    audience: j['audience'] as String,
    sendEmail: j['sendEmail'] as bool,
    recipientCount: j['recipientCount'] as int,
    createdAtUtc: DateTime.parse(j['createdAtUtc'] as String),
  );
}

/// The signed-in user's inbox, plus (for admins) composing messages to groups.
class NotificationsApi {
  NotificationsApi(this._api);

  final ApiClient _api;

  Future<Paged<AppNotification>> list({
    bool unreadOnly = false,
    int page = 1,
    int pageSize = 30,
  }) async {
    final data = await _api.get<Map<String, dynamic>>(
      '/api/v1/notifications',
      query: {
        if (unreadOnly) 'unreadOnly': true,
        'page': page,
        'pageSize': pageSize,
      },
    );
    return Paged(
      (data['items'] as List)
          .map((e) => AppNotification.fromJson(e as Map<String, dynamic>))
          .toList(),
      data['totalCount'] as int,
    );
  }

  Future<int> unreadCount() async {
    final data = await _api.get<Map<String, dynamic>>(
      '/api/v1/notifications/unread-count',
    );
    return data['count'] as int;
  }

  Future<void> markRead(int id) =>
      _api.post<dynamic>('/api/v1/notifications/$id/read');

  Future<void> markAllRead() =>
      _api.post<dynamic>('/api/v1/notifications/read-all');

  // ---- admin

  Future<AnnouncementItem> announce({
    required String title,
    required String body,
    required String audience,
    String? targetUserId,
    bool sendEmail = false,
  }) async => AnnouncementItem.fromJson(
    await _api.post<Map<String, dynamic>>(
      '/api/v1/announcements',
      body: {
        'title': title,
        'body': body,
        'audience': audience,
        'targetUserId': targetUserId,
        'sendEmail': sendEmail,
      },
    ),
  );

  Future<Paged<AnnouncementItem>> announcements({
    int page = 1,
    int pageSize = 30,
  }) async {
    final data = await _api.get<Map<String, dynamic>>(
      '/api/v1/announcements',
      query: {'page': page, 'pageSize': pageSize},
    );
    return Paged(
      (data['items'] as List)
          .map((e) => AnnouncementItem.fromJson(e as Map<String, dynamic>))
          .toList(),
      data['totalCount'] as int,
    );
  }
}
