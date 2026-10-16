import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import 'notifications_api.dart';

String audienceLabel(AppLocalizations t, String audience) => switch (audience) {
  Audience.all => t.audienceAll,
  Audience.clients => t.audienceClients,
  Audience.employees => t.audienceEmployees,
  Audience.user => t.audienceUser,
  _ => audience,
};

IconData notificationIcon(String kind) => switch (kind) {
  'ReservationCreated' => Icons.event_available_outlined,
  'ReservationCancelled' => Icons.event_busy_outlined,
  'ReservationRescheduled' => Icons.edit_calendar_outlined,
  'Reminder' => Icons.alarm_outlined,
  'OrderPlaced' ||
  'OrderPaid' ||
  'OrderCancelled' => Icons.receipt_long_outlined,
  'ProductLowStock' => Icons.inventory_2_outlined,
  'Announcement' => Icons.campaign_outlined,
  _ => Icons.notifications_outlined,
};

/// "07.01.2030. 10:00" in the device time zone.
String formatWhen(DateTime utc) {
  final l = utc.toLocal();
  String two(int n) => n.toString().padLeft(2, '0');
  return '${two(l.day)}.${two(l.month)}.${l.year}. ${two(l.hour)}:${two(l.minute)}';
}
