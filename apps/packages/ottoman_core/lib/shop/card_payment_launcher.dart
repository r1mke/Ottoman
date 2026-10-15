import 'shop_models.dart';

enum CardPaymentResult { paid, cancelled, unavailable }

/// Seam for the card payment sheet, so the shop screens stay testable without a native SDK.
/// The default implementation reports that card payment is not available.
abstract class CardPaymentLauncher {
  const CardPaymentLauncher();

  bool get isAvailable;

  /// Called once after sign-in so the launcher can fetch its configuration.
  Future<void> prepare() async {}

  /// [order] must carry a `paymentClientSecret` (only the owner's unpaid card order does).
  Future<CardPaymentResult> pay(ShopOrder order);
}

class UnavailableCardPayment extends CardPaymentLauncher {
  const UnavailableCardPayment();

  @override
  bool get isAvailable => false;

  @override
  Future<CardPaymentResult> pay(ShopOrder order) async =>
      CardPaymentResult.unavailable;
}
