import 'shop_models.dart';

enum CardPaymentResult { paid, cancelled, unavailable }

/// Seam for the card payment sheet. The default implementation reports that card
/// payment is not available in this build; plug in a `flutter_stripe` launcher
/// (publishable key + native setup) to take payments for real - see docs/06 #67.
abstract interface class CardPaymentLauncher {
  bool get isAvailable;
  Future<CardPaymentResult> pay(ShopOrder order);
}

class UnavailableCardPayment implements CardPaymentLauncher {
  const UnavailableCardPayment();

  @override
  bool get isAvailable => false;

  @override
  Future<CardPaymentResult> pay(ShopOrder order) async =>
      CardPaymentResult.unavailable;
}
