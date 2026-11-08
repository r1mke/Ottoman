import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import 'shop_models.dart';

String orderStatusLabel(AppLocalizations t, String status) => switch (status) {
  OrderStatus.pendingPayment => t.orderStatusPendingPayment,
  OrderStatus.placed => t.orderStatusPlaced,
  OrderStatus.fulfilled => t.orderStatusFulfilled,
  OrderStatus.cancelled => t.orderStatusCancelled,
  OrderStatus.expired => t.orderStatusExpired,
  _ => status,
};

String paymentStatusLabel(AppLocalizations t, String? status) =>
    switch (status) {
      'Pending' => t.paymentStatusPending,
      'Succeeded' => t.paymentStatusSucceeded,
      'Failed' => t.paymentStatusFailed,
      'Refunded' => t.paymentStatusRefunded,
      'RefundPending' => t.paymentStatusRefundPending,
      _ => '',
    };

String paymentMethodLabel(AppLocalizations t, String method) =>
    method == PaymentMethod.card ? t.payByCard : t.payOnReceipt;

/// Always "Preuzimanje u salonu": pickup is the only way an order is handed over.
String fulfilmentLabel(AppLocalizations t, String f) => t.pickup;

Color orderStatusColor(ColorScheme scheme, String status) => switch (status) {
  OrderStatus.placed => scheme.primary,
  OrderStatus.fulfilled => Colors.green.shade700,
  OrderStatus.pendingPayment => Colors.orange.shade800,
  _ => scheme.outline,
};

/// "12.50 KM". The server owns the numbers; this only formats them.
String money(AppLocalizations t, double amount) =>
    '${amount.toStringAsFixed(2)} ${t.currencyShort}';
