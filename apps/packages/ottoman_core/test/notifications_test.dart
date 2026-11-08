import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

class _Adapter implements HttpClientAdapter {
  _Adapter(this.body, {this.status = 200});
  final Object body;
  final int status;
  RequestOptions? last;

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    last = o;
    return ResponseBody.fromString(
      jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

ApiClient _client(_Adapter a) => ApiClient(
  baseUrl: 'http://test',
  dio: Dio()..httpClientAdapter = a,
  readToken: () async => 't',
);

void main() {
  test('a booking hit by time off has its own icon', () {
    expect(notificationIcon('ReservationAffected'), Icons.event_note_outlined);
    expect(notificationIcon('Unknown'), Icons.notifications_outlined);
  });

  test('list parses the page and asks for unread only when told to', () async {
    final adapter = _Adapter({
      'items': [
        {
          'id': 5,
          'title': 'Termin potvrđen',
          'body': 'Sutra u 10:00',
          'kind': 'ReservationCreated',
          'createdAtUtc': '2030-01-07T09:00:00Z',
          'isRead': false,
        },
      ],
      'totalCount': 1,
    });

    final page = await NotificationsApi(_client(adapter))
        .list(unreadOnly: true);

    expect(adapter.last!.uri.queryParameters['unreadOnly'], 'true');
    expect(page.items.single.title, 'Termin potvrđen');
    expect(page.items.single.isRead, isFalse);
    expect(page.items.single.asRead().isRead, isTrue);
  });

  test('unreadCount reads the count', () async {
    final api = NotificationsApi(_client(_Adapter({'count': 4})));
    expect(await api.unreadCount(), 4);
  });

  test('announce posts the audience as its name', () async {
    final adapter = _Adapter({
      'id': 1,
      'title': 'T',
      'body': 'B',
      'audience': 'Clients',
      'sendEmail': true,
      'recipientCount': 12,
      'createdAtUtc': '2030-01-07T09:00:00Z',
    }, status: 201);

    final item = await NotificationsApi(_client(adapter)).announce(
      title: 'T',
      body: 'B',
      audience: Audience.clients,
      sendEmail: true,
    );

    final sent = adapter.last!.data as Map;
    expect(sent['audience'], 'Clients');
    expect(sent['sendEmail'], true);
    expect(sent['targetUserId'], isNull);
    expect(item.recipientCount, 12);
  });

  test('resetPassword sends the code and the new password', () async {
    final adapter = _Adapter({});
    final auth = AuthController(
      baseUrl: 'http://test',
      storage: MemoryTokenStorage(),
      api: _client(adapter),
    );

    await auth.resetPassword(
      email: 'a@b.c',
      token: 'CODE',
      newPassword: 'Nova123!x',
    );

    expect(adapter.last!.path, '/api/v1/auth/reset-password');
    expect((adapter.last!.data as Map)['token'], 'CODE');
  });
}
