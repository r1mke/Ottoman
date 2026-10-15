import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

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
  final _form = GlobalKey<FormState>();
  final _address = TextEditingController();
  final _note = TextEditingController();
  String _fulfilment = FulfilmentType.pickup;
  String _payment = PaymentMethod.onReceipt;
  bool _busy = false;
  String? _error;

  bool get _cardAvailable => widget.cardPayment.isAvailable;

  @override
  void dispose() {
    _address.dispose();
    _note.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    final t = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final navigator = Navigator.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      var order = await widget.shop.createOrder(
        quantities: widget.cart.toQuantities(),
        fulfilment: _fulfilment,
        paymentMethod: _payment,
        deliveryAddress: _fulfilment == FulfilmentType.delivery
            ? _address.text.trim()
            : null,
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
    final delivery = _fulfilment == FulfilmentType.delivery;
    return Scaffold(
      appBar: AppBar(title: Text(t.checkout)),
      body: Form(
        key: _form,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Text('${t.pickup} / ${t.delivery}', style: _heading(context)),
            RadioGroup<String>(
              groupValue: _fulfilment,
              onChanged: (v) => setState(() {
                _fulfilment = v!;
                if (_fulfilment == FulfilmentType.delivery && _cardAvailable) {
                  _payment = PaymentMethod.card;
                }
              }),
              child: Column(
                children: [
                  RadioListTile<String>(
                    key: const Key('fulfil_pickup'),
                    value: FulfilmentType.pickup,
                    title: Text(t.pickup),
                  ),
                  RadioListTile<String>(
                    key: const Key('fulfil_delivery'),
                    value: FulfilmentType.delivery,
                    // Delivery is paid by card only; without a card launcher it cannot be offered.
                    enabled: _cardAvailable,
                    title: Text(t.delivery),
                  ),
                ],
              ),
            ),
            if (delivery)
              TextFormField(
                key: const Key('delivery_address'),
                controller: _address,
                decoration: InputDecoration(labelText: t.deliveryAddress),
                validator: (v) =>
                    (v == null || v.trim().isEmpty) ? t.requiredField : null,
              ),
            const SizedBox(height: 16),
            Text(t.payment, style: _heading(context)),
            RadioGroup<String>(
              groupValue: _payment,
              onChanged: (v) => setState(() => _payment = v!),
              child: Column(
                children: [
                  RadioListTile<String>(
                    key: const Key('pay_receipt'),
                    value: PaymentMethod.onReceipt,
                    enabled: !delivery,
                    title: Text(t.payOnReceipt),
                  ),
                  RadioListTile<String>(
                    key: const Key('pay_card'),
                    value: PaymentMethod.card,
                    enabled: _cardAvailable,
                    title: Text(t.payByCard),
                    subtitle: _cardAvailable ? null : Text(t.cardUnavailable),
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
      ),
    );
  }

  TextStyle? _heading(BuildContext context) =>
      Theme.of(context).textTheme.titleMedium
          ?.copyWith(fontWeight: FontWeight.w700);
}
