import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// Admin: all shop orders. The salon hands goods over (fulfil) or cancels; a paid card order is refunded on cancel.
class OrdersPage extends StatefulWidget {
  const OrdersPage({super.key, required this.shop});

  final ShopApi shop;

  @override
  State<OrdersPage> createState() => _OrdersPageState();
}

class _OrdersPageState extends State<OrdersPage> {
  String? _status;
  int _version = 0;

  void _refresh() => setState(() => _version++);

  void _say(String message) =>
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(message)));

  Future<void> _fulfil(ShopOrder o) async {
    final t = AppLocalizations.of(context);
    final ok = await confirmAction(
      context,
      title: t.markFulfilled,
      message: t.fulfilOrderQuestion(
        formatDay(o.createdAtUtc),
        o.clientName ?? '-',
      ),
      confirmLabel: t.markFulfilled,
    );
    if (!ok || !mounted) return;
    try {
      await widget.shop.fulfilOrder(o.id);
    } on AppException catch (e) {
      _say(e.message);
    }
    if (mounted) _refresh();
  }

  Future<void> _cancel(ShopOrder o) async {
    final t = AppLocalizations.of(context);
    final reason = await showCancelReasonDialog(
      context,
      summary: '${o.clientName ?? ''} · ${o.itemSummary}',
      reasonRequired: true,
      title: t.cancelOrder,
      question: t.cancelOrderQuestion,
    );
    if (reason == null) return;
    try {
      await widget.shop.cancelOrder(o.id, reason: reason);
    } on AppException catch (e) {
      _say(e.message);
    }
    if (mounted) _refresh();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.fromLTRB(32, 0, 32, 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 260,
            child: DropdownButtonFormField<String?>(
              key: const Key('order_status_filter'),
              isExpanded: true,
              initialValue: _status,
              decoration: InputDecoration(labelText: t.status, isDense: true),
              items: [
                DropdownMenuItem(value: null, child: Text(t.allStatuses)),
                for (final s in [
                  OrderStatus.pendingPayment,
                  OrderStatus.placed,
                  OrderStatus.fulfilled,
                  OrderStatus.cancelled,
                  OrderStatus.expired,
                ])
                  DropdownMenuItem(
                    value: s,
                    child: Text(orderStatusLabel(t, s)),
                  ),
              ],
              onChanged: (v) {
                _status = v;
                _refresh();
              },
            ),
          ),
          const SizedBox(height: 16),
          Expanded(
            child: AsyncView<Paged<ShopOrder>>(
              key: ValueKey(_version),
              load: () => widget.shop.orders(status: _status, pageSize: 100),
              skeleton: const ListSkeleton(),
              isEmpty: (p) => p.items.isEmpty,
              emptyMessage: t.noOrders,
              builder: (context, page, _) => Card(
                clipBehavior: Clip.antiAlias,
                child: SingleChildScrollView(
                  child: SizedBox(
                    width: double.infinity,
                    child: DataTable(
                      columnSpacing: 24,
                      headingRowColor: WidgetStatePropertyAll(
                        scheme.surfaceContainerHighest,
                      ),
                      dataRowMaxHeight: 72,
                      columns: [
                        DataColumn(label: Text(t.date)),
                        DataColumn(label: Text(t.client)),
                        DataColumn(label: Text(t.products)),
                        DataColumn(label: Text(t.total), numeric: true),
                        DataColumn(label: Text(t.payment)),
                        DataColumn(label: Text(t.status)),
                        const DataColumn(label: SizedBox.shrink()),
                      ],
                      rows: [
                        for (final o in page.items)
                          DataRow(
                            key: ValueKey('order_row_${o.id}'),
                            cells: [
                              DataCell(Text(formatWhen(o.createdAtUtc))),
                              DataCell(Text(o.clientName ?? '-')),
                              DataCell(
                                ConstrainedBox(
                                  constraints: const BoxConstraints(
                                    maxWidth: 260,
                                  ),
                                  child: Text(
                                    o.itemSummary,
                                    maxLines: 2,
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                ),
                              ),
                              DataCell(Text(money(t, o.totalPrice))),
                              DataCell(
                                Column(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      paymentMethodLabel(t, o.paymentMethod),
                                    ),
                                    if (paymentStatusLabel(
                                      t,
                                      o.paymentStatus,
                                    ).isNotEmpty)
                                      Text(
                                        paymentStatusLabel(t, o.paymentStatus),
                                        key: Key('pay_status_${o.id}'),
                                        style: TextStyle(
                                          fontSize: 12,
                                          color: scheme.outline,
                                        ),
                                      ),
                                  ],
                                ),
                              ),
                              DataCell(
                                Chip(
                                  label: Text(
                                    orderStatusLabel(t, o.status),
                                    style: TextStyle(
                                      color: orderStatusColor(scheme, o.status),
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                  visualDensity: VisualDensity.compact,
                                ),
                              ),
                              DataCell(
                                Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    if (o.status == OrderStatus.placed)
                                      IconButton.filled(
                                        key: Key('fulfil_${o.id}'),
                                        tooltip: t.markFulfilled,
                                        icon: const Icon(Icons.check),
                                        onPressed: () => _fulfil(o),
                                      ),
                                    if (o.status == OrderStatus.placed ||
                                        o.status == OrderStatus.pendingPayment)
                                      IconButton(
                                        key: Key('cancel_${o.id}'),
                                        tooltip: t.cancelOrder,
                                        icon: const Icon(Icons.close),
                                        onPressed: () => _cancel(o),
                                      ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
