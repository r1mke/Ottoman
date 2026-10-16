// The classic FCM registration token (getToken/onTokenRefresh) is what the backend sends to through the HTTP v1
// API. The newer register()/onRegistered API hands out a different identifier (a Firebase Installation ID for
// direct send), so the deprecated calls are used on purpose until the backend moves to it.
// ignore_for_file: deprecated_member_use
import 'dart:async';

import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// [PushClient] on top of Firebase Cloud Messaging. Firebase must be initialised before this is created.
class FirebasePushClient implements PushClient {
  FirebasePushClient([FirebaseMessaging? messaging])
    : _messaging = messaging ?? FirebaseMessaging.instance;

  final FirebaseMessaging _messaging;

  @override
  Future<bool> requestPermission() async {
    final settings = await _messaging.requestPermission();
    return settings.authorizationStatus == AuthorizationStatus.authorized ||
        settings.authorizationStatus == AuthorizationStatus.provisional;
  }

  @override
  Future<String?> token() => _messaging.getToken();

  @override
  Stream<String> get onTokenRefresh => _messaging.onTokenRefresh;

  @override
  Stream<PushNote> get onForeground => FirebaseMessaging.onMessage
      .where((m) => m.notification != null)
      .map(
        (m) => PushNote(
          title: m.notification!.title ?? '',
          body: m.notification!.body ?? '',
          kind: m.data['kind'] as String?,
        ),
      );

  @override
  Future<void> deleteToken() => _messaging.deleteToken();
}
