import 'package:dio/dio.dart';

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
    this.imageUrl,
    this.category,
  });

  final int id;

  /// For a message from an announcement: News, Promotion or Notice (see [AnnouncementCategory]).
  final String? category;
  final String title;
  final String body;

  /// The picture of a news item; null for the ordinary messages of the system.
  final String? imageUrl;

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
    imageUrl: imageUrl,
    category: category,
  );

  factory AppNotification.fromJson(Map<String, dynamic> j) => AppNotification(
    id: j['id'] as int,
    title: j['title'] as String,
    body: j['body'] as String,
    kind: j['kind'] as String,
    createdAtUtc: DateTime.parse(j['createdAtUtc'] as String),
    isRead: j['isRead'] as bool,
    imageUrl: j['imageUrl'] as String?,
    category: j['category'] as String?,
  );
}

abstract final class AnnouncementCategory {
  static const news = 'News';
  static const promotion = 'Promotion';
  static const notice = 'Notice';
  static const all = [news, promotion, notice];
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
    this.imageUrl,
    this.category = AnnouncementCategory.news,
    this.expiresAtUtc,
    this.isHidden = false,
    this.isExpired = false,
  });

  final int id;
  final String title;
  final String body;
  final String audience;
  final bool sendEmail;
  final int recipientCount;
  final DateTime createdAtUtc;
  final String? imageUrl;
  final String category;
  final DateTime? expiresAtUtc;

  /// Taken down by the admin; recipients no longer see it.
  final bool isHidden;

  /// Past its end date; recipients no longer see it.
  final bool isExpired;

  factory AnnouncementItem.fromJson(Map<String, dynamic> j) => AnnouncementItem(
    id: j['id'] as int,
    title: j['title'] as String,
    body: j['body'] as String,
    audience: j['audience'] as String,
    sendEmail: j['sendEmail'] as bool,
    recipientCount: j['recipientCount'] as int,
    createdAtUtc: DateTime.parse(j['createdAtUtc'] as String),
    imageUrl: j['imageUrl'] as String?,
    category: j['category'] as String? ?? AnnouncementCategory.news,
    expiresAtUtc: j['expiresAtUtc'] == null
        ? null
        : DateTime.parse(j['expiresAtUtc'] as String),
    isHidden: j['isHidden'] as bool? ?? false,
    isExpired: j['isExpired'] as bool? ?? false,
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
    String category = AnnouncementCategory.news,
    DateTime? expiresAtUtc,
    List<int>? imageBytes,
    String? imageName,
  }) async {
    final expires = expiresAtUtc?.toUtc().toIso8601String();
    // A news item with a picture goes as a form; one without goes as plain JSON.
    if (imageBytes != null) {
      final form = FormData.fromMap({
        'title': title,
        'body': body,
        'audience': audience,
        'targetUserId': ?targetUserId,
        'sendEmail': sendEmail.toString(),
        'category': category,
        'expiresAtUtc': ?expires,
        'file': MultipartFile.fromBytes(
          imageBytes,
          filename: imageName ?? 'obavijest.jpg',
        ),
      });
      return AnnouncementItem.fromJson(
        await _api.post<Map<String, dynamic>>(
          '/api/v1/announcements/with-image',
          body: form,
        ),
      );
    }
    return AnnouncementItem.fromJson(
      await _api.post<Map<String, dynamic>>(
        '/api/v1/announcements',
        body: {
          'title': title,
          'body': body,
          'audience': audience,
          'targetUserId': targetUserId,
          'sendEmail': sendEmail,
          'category': category,
          'expiresAtUtc': expires,
        },
      ),
    );
  }

  Future<Paged<AnnouncementItem>> announcements({
    String? search,
    int page = 1,
    int pageSize = 30,
  }) async {
    final data = await _api.get<Map<String, dynamic>>(
      '/api/v1/announcements',
      query: {
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
        'page': page,
        'pageSize': pageSize,
      },
    );
    return Paged(
      (data['items'] as List)
          .map((e) => AnnouncementItem.fromJson(e as Map<String, dynamic>))
          .toList(),
      data['totalCount'] as int,
    );
  }

  /// Changes the text, category and end date of a sent announcement; nothing is sent again.
  Future<AnnouncementItem> updateAnnouncement(
    int id, {
    required String title,
    required String body,
    required String category,
    DateTime? expiresAtUtc,
  }) async => AnnouncementItem.fromJson(
    await _api.put<Map<String, dynamic>>(
      '/api/v1/announcements/$id',
      body: {
        'title': title,
        'body': body,
        'category': category,
        'expiresAtUtc': expiresAtUtc?.toUtc().toIso8601String(),
      },
    ),
  );

  Future<AnnouncementItem> setAnnouncementHidden(int id, bool hidden) async =>
      AnnouncementItem.fromJson(
        await _api.put<Map<String, dynamic>>(
          '/api/v1/announcements/$id/hide',
          body: {'hidden': hidden},
        ),
      );

  Future<AnnouncementItem> setAnnouncementImage(
    int id,
    List<int> bytes,
    String name,
  ) async => AnnouncementItem.fromJson(
    await _api.put<Map<String, dynamic>>(
      '/api/v1/announcements/$id/image',
      body: FormData.fromMap({
        'file': MultipartFile.fromBytes(bytes, filename: name),
      }),
    ),
  );

  Future<AnnouncementItem> clearAnnouncementImage(int id) async =>
      AnnouncementItem.fromJson(
        await _api.delete<Map<String, dynamic>>(
          '/api/v1/announcements/$id/image',
        ),
      );
}
