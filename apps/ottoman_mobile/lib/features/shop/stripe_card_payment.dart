import 'package:flutter/material.dart' show ThemeMode;
import 'package:flutter_stripe/flutter_stripe.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// Opens Stripe's payment sheet. The publishable key comes from the API, so it is never baked
/// into the app, and cards are offered only when the server has both Stripe keys.
class StripeCardPayment extends CardPaymentLauncher {
  StripeCardPayment(this._api);

  final ApiClient _api;
  bool _ready = false;

  @override
  bool get isAvailable => _ready;

  @override
  Future<void> prepare() async {
    try {
      final cfg = await _api.get<Map<String, dynamic>>(
        '/api/v1/payments/config',
      );
      final key = cfg['publishableKey'] as String?;
      if (cfg['cardsEnabled'] == true && key != null && key.isNotEmpty) {
        Stripe.publishableKey = key;
        await Stripe.instance.applySettings();
        _ready = true;
      }
    } on Object {
      _ready = false; // card payment simply stays hidden
    }
  }

  @override
  Future<CardPaymentResult> pay(ShopOrder order) async {
    final secret = order.paymentClientSecret;
    if (!_ready || secret == null) return CardPaymentResult.unavailable;
    try {
      await Stripe.instance.initPaymentSheet(
        paymentSheetParameters: SetupPaymentSheetParameters(
          paymentIntentClientSecret: secret,
          merchantDisplayName: 'Ottoman',
          style: ThemeMode.dark,
        ),
      );
      await Stripe.instance.presentPaymentSheet();
      return CardPaymentResult.paid;
    } on StripeException {
      // Cancelled or declined: the order stays unpaid and can be paid later from "Moje narudžbe".
      return CardPaymentResult.cancelled;
    }
  }
}
