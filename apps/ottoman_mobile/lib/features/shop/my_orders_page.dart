import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

class MyOrdersPage extends StatefulWidget {
  const MyOrdersPage({
    super.key,
    required this.shop,
    required this.cardPayment,
  });

  final ShopApi shop;
  final CardPaymentLauncher cardPayment;

  @override
  State<MyOrdersPage> createState() => _MyOrdersPageState();
}

class _MyOrdersPageState extends State<MyOrdersPage> {
  int _version = 0;

  Future<void> _cancel(ShopOrder o) async {
    final t = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(t.cancelOrder),
        content: Text(t.cancelOrderQuestion),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: Text(t.no),
          ),
          FilledButton(
            key: const Key('confirm_cancel_order'),
            style: FilledButton.styleFrom(minimumSize: const Size(96, 44)),
            onPressed: () => Navigator.pop(ctx, true),
            child: Text(t.yes),
          ),
        ],
      ),
    );
    if (ok != true) return;
    try {
      await widget.shop.cancelOrder(o.id);
    } on AppException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
    if (mounted) setState(() => _version++);
  }

  Future<void> _pay(ShopOrder o) async {
    final t = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    // The list does not carry payment secrets; fetch the one order.
    final result = await widget.cardPayment.pay(await widget.shop.order(o.id));
    if (result == CardPaymentResult.unavailable) {
      messenger.showSnackBar(SnackBar(content: Text(t.cardUnavailable)));
    }
    if (mounted) setState(() => _version++);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(t.myOrders)),
      body: AsyncView<Paged<ShopOrder>>(
        key: ValueKey(_version),
        load: () => widget.shop.orders(),
        skeleton: const ListSkeleton(rowHeight: 110),
        isEmpty: (p) => p.items.isEmpty,
        emptyMessage: t.noOrders,
        builder: (context, page, reload) => RefreshIndicator(
          onRefresh: reload,
          child: ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: page.items.length,
            separatorBuilder: (_, _) => const SizedBox(height: 12),
            itemBuilder: (context, i) {
              final o = page.items[i];
              return OrderCard(
                order: o,
                onCancel: o.clientCanCancel ? () => _cancel(o) : null,
                onPay: o.awaitsPayment ? () => _pay(o) : null,
              );
            },
          ),
        ),
      ),
    );
  }
}

class OrderCard extends StatelessWidget {
  const OrderCard({super.key, required this.order, this.onCancel, this.onPay});

  final ShopOrder order;
  final VoidCallback? onCancel;
  final VoidCallback? onPay;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    final o = order;
    final local = o.createdAtUtc.toLocal();
    return Card(
      key: Key('order_${o.id}'),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    t.orderNumber(o.id),
                    style: const TextStyle(fontWeight: FontWeight.w800),
                  ),
                ),
                Chip(
                  label: Text(
                    orderStatusLabel(t, o.status),
                    style: TextStyle(
                      color: orderStatusColor(scheme, o.status),
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
            Text(o.itemSummary),
            const SizedBox(height: 4),
            Text(
              '${paymentMethodLabel(t, o.paymentMethod)} · ${fulfilmentLabel(t, o.fulfilment)}',
              style: TextStyle(color: scheme.outline, fontSize: 12),
            ),
            Text(
              '${local.day}.${local.month}.${local.year}.',
              style: TextStyle(color: scheme.outline, fontSize: 12),
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                Text(
                  money(t, o.totalPrice),
                  style: const TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 16,
                  ),
                ),
                Expanded(
                  child: Wrap(
                    alignment: WrapAlignment.end,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    spacing: 8,
                    children: [
                      if (onPay != null)
                        FilledButton(
                          key: Key('pay_${o.id}'),
                          onPressed: onPay,
                          child: Text(t.payNow),
                        ),
                      if (onCancel != null)
                        TextButton(
                          key: Key('cancel_order_${o.id}'),
                          onPressed: onCancel,
                          child: Text(t.cancelOrder),
                        ),
                    ],
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
