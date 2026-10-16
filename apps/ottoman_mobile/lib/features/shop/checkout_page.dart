import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// Goods are collected at the salon, so the only choice is how to pay: by card now or on receipt.
class CheckoutPage extends StatefulWidget {
  const CheckoutPage({
    super.key,
    required this.shop,
    required this.cart,
    required this.cardPayment,
  });

  final ShopApi shop;
  final CartController cart;
  final CardPaymentLauncher cardPayment;

  @override
  State<CheckoutPage> createState() => _CheckoutPageState();
}

class _CheckoutPageState extends State<CheckoutPage> {
  final _note = TextEditingController();
  late String _payment = widget.cardPayment.isAvailable
      ? PaymentMethod.card
      : PaymentMethod.onReceipt;
  bool _busy = false;
  String? _error;

  bool get _cardAvailable => widget.cardPayment.isAvailable;

  /// One key for one intention to order: repeating the same tap (or a retry after a lost response) cannot create
  /// a second order. It changes when the choices that make a different order change.
  String _attempt = newIdempotencyKey();

  @override
  void dispose() {
    _note.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final t = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final navigator = Navigator.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final order = await widget.shop.createOrder(
        quantities: widget.cart.toQuantities(),
        fulfilment: FulfilmentType.pickup,
        paymentMethod: _payment,
        idempotencyKey: _attempt,
        note: _note.text.trim().isEmpty ? null : _note.text.trim(),
      );
      // The order exists (and its stock is held) from here on, whatever happens with the payment.
      widget.cart.clear();

      var message = t.orderPlaced;
      if (order.awaitsPayment) {
        final result = await widget.cardPayment.pay(order);
        message = result == CardPaymentResult.paid
            ? t.orderPlaced
            : t.orderAwaitsPayment;
      }
      messenger.showSnackBar(SnackBar(content: Text(message)));
      navigator.popUntil((r) => r.isFirst);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(t.checkout)),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          ListTile(
            contentPadding: EdgeInsets.zero,
            leading: const Icon(Icons.storefront_outlined),
            title: Text(t.pickup),
          ),
          const Divider(),
          Text(t.payment, style: _heading(context)),
          RadioGroup<String>(
            groupValue: _payment,
            onChanged: (v) => setState(() {
              _payment = v!;
              _attempt = newIdempotencyKey();
            }),
            child: Column(
              children: [
                RadioListTile<String>(
                  key: const Key('pay_card'),
                  value: PaymentMethod.card,
                  enabled: _cardAvailable,
                  title: Text(t.payByCard),
                  subtitle: _cardAvailable ? null : Text(t.cardUnavailable),
                ),
                RadioListTile<String>(
                  key: const Key('pay_receipt'),
                  value: PaymentMethod.onReceipt,
                  title: Text(t.payOnReceipt),
                ),
              ],
            ),
          ),
          const SizedBox(height: 8),
          TextFormField(
            controller: _note,
            maxLines: 2,
            maxLength: 500,
            decoration: InputDecoration(labelText: t.note),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Text(t.total, style: Theme.of(context).textTheme.titleMedium),
              const Spacer(),
              Text(
                money(t, widget.cart.total),
                style: Theme.of(context).textTheme.titleLarge
                    ?.copyWith(fontWeight: FontWeight.w800),
              ),
            ],
          ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(top: 12),
              child: Text(
                _error!,
                key: const Key('checkout_error'),
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
          const SizedBox(height: 16),
          FilledButton(
            key: const Key('place_order'),
            style: FilledButton.styleFrom(
              minimumSize: const Size.fromHeight(52),
            ),
            onPressed: _busy ? null : _submit,
            child: _busy
                ? const SizedBox(
                    width: 22,
                    height: 22,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : Text(t.placeOrder),
          ),
        ],
      ),
    );
  }

  TextStyle? _heading(BuildContext context) =>
      Theme.of(context).textTheme.titleMedium
          ?.copyWith(fontWeight: FontWeight.w700);
}
