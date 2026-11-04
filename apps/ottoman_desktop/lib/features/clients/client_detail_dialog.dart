import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// One client: the numbers that matter (counted by the server from real reservations and orders),
/// the latest appointments and orders, and the block switch.
class ClientDetailDialog extends StatefulWidget {
  const ClientDetailDialog({
    super.key,
    required this.api,
    required this.clientId,
  });

  final ClientsApi api;
  final String clientId;

  @override
  State<ClientDetailDialog> createState() => _ClientDetailDialogState();
}

class _ClientDetailDialogState extends State<ClientDetailDialog> {
  int _version = 0;
  bool _busy = false;
  String? _error;

  Future<void> _toggleBlock(ClientDetail c) async {
    final t = AppLocalizations.of(context);
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        content: Text(
          c.isBlocked
              ? t.clientUnblockQuestion(c.fullName)
              : t.clientBlockQuestion(c.fullName),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: Text(t.no),
          ),
          FilledButton(
            key: const Key('confirm_block'),
            onPressed: () => Navigator.pop(ctx, true),
            child: Text(t.yes),
          ),
        ],
      ),
    );
    if (ok != true) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.api.setBlocked(c.id, blocked: !c.isBlocked);
      if (mounted) setState(() => _version++);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Dialog(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 760, maxHeight: 720),
        child: AsyncView<ClientDetail>(
          key: ValueKey(_version),
          load: () => widget.api.detail(widget.clientId),
          skeleton: const SizedBox(height: 320, child: ListSkeleton()),
          builder: (context, c, _) => Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    // The client's own picture, already cropped at the zoom they chose.
                    PersonAvatar(
                      key: const Key('client_detail_avatar'),
                      photoUrl: c.photoUrl,
                      seed: c.id,
                      radius: 26,
                    ),
                    const SizedBox(width: 14),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Flexible(
                                child: Text(
                                  c.fullName,
                                  key: const Key('client_name'),
                                  style: Theme.of(context).textTheme.titleLarge
                                      ?.copyWith(fontWeight: FontWeight.w800),
                                ),
                              ),
                              if (c.isBlocked) ...[
                                const SizedBox(width: 10),
                                Chip(
                                  key: const Key('client_blocked_chip'),
                                  label: Text(t.clientBlocked),
                                  backgroundColor: WorkspaceColors.danger
                                      .withValues(alpha: 0.12),
                                  side: BorderSide.none,
                                  labelStyle: const TextStyle(
                                    color: WorkspaceColors.danger,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                              ],
                            ],
                          ),
                          Text(
                            [
                              c.email,
                              if (c.phone != null) c.phone!,
                              '${t.clientRegistered} ${formatWhen(c.registeredAtUtc)}',
                            ].join('  ·  '),
                            style: const TextStyle(
                              color: WorkspaceColors.muted,
                            ),
                          ),
                        ],
                      ),
                    ),
                    FilledButton(
                      key: const Key('toggle_block'),
                      style: FilledButton.styleFrom(
                        backgroundColor: c.isBlocked
                            ? WorkspaceColors.ink
                            : WorkspaceColors.danger,
                      ),
                      onPressed: _busy ? null : () => _toggleBlock(c),
                      child: Text(
                        c.isBlocked ? t.clientUnblock : t.clientBlock,
                      ),
                    ),
                  ],
                ),
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(
                      _error!,
                      key: const Key('client_error'),
                      style: const TextStyle(color: WorkspaceColors.danger),
                    ),
                  ),
                const SizedBox(height: 16),
                _StatsRow(stats: c.stats, t: t),
                const SizedBox(height: 16),
                Expanded(
                  child: SingleChildScrollView(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        _Heading(t.clientRecentReservations),
                        if (c.reservations.isEmpty)
                          Text(t.noData)
                        else
                          for (final r in c.reservations)
                            _Line(
                              key: Key('reservation_${r.id}'),
                              when: formatWhen(r.startUtc),
                              text: '${r.services} · ${r.employeeName}',
                              status: reservationStatusLabel(t, r.status),
                              amount: money(t, r.total),
                            ),
                        const SizedBox(height: 16),
                        _Heading(t.clientRecentOrders),
                        if (c.orders.isEmpty)
                          Text(t.noData)
                        else
                          for (final o in c.orders)
                            _Line(
                              key: Key('order_${o.id}'),
                              when: formatWhen(o.createdAtUtc),
                              text: '#${o.id} · ${o.items}',
                              status: orderStatusLabel(t, o.status),
                              amount: money(t, o.total),
                            ),
                      ],
                    ),
                  ),
                ),
                Align(
                  alignment: Alignment.centerRight,
                  child: TextButton(
                    key: const Key('client_close'),
                    onPressed: () => Navigator.pop(context),
                    child: Text(t.close),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _StatsRow extends StatelessWidget {
  const _StatsRow({required this.stats, required this.t});

  final ClientStats stats;
  final AppLocalizations t;

  @override
  Widget build(BuildContext context) {
    final items = <(String, String, String)>[
      ('visits', t.clientVisits, '${stats.visits}'),
      ('noshows', t.clientNoShows, '${stats.noShows}'),
      ('cancelled', t.clientCancelled, '${stats.cancelled}'),
      ('upcoming', t.clientUpcoming, '${stats.upcoming}'),
      ('services', t.clientSpentServices, money(t, stats.servicesSpent)),
      ('products', t.clientSpentProducts, money(t, stats.productsSpent)),
      (
        'last',
        t.clientLastVisit,
        stats.lastVisitUtc == null
            ? t.clientNever
            : formatWhen(stats.lastVisitUtc!),
      ),
    ];
    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: [
        for (final (id, label, value) in items)
          Container(
            key: Key('stat_$id'),
            width: 170,
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: WorkspaceColors.tableHead,
              borderRadius: BorderRadius.circular(10),
              border: Border.all(color: WorkspaceColors.line),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: WorkspaceColors.muted,
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 4),
                FittedBox(
                  fit: BoxFit.scaleDown,
                  alignment: Alignment.centerLeft,
                  child: Text(
                    value,
                    key: Key('stat_value_$id'),
                    style: const TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  }
}

class _Heading extends StatelessWidget {
  const _Heading(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Text(
      text,
      style: Theme.of(context).textTheme.titleSmall
          ?.copyWith(fontWeight: FontWeight.w800),
    ),
  );
}

class _Line extends StatelessWidget {
  const _Line({
    super.key,
    required this.when,
    required this.text,
    required this.status,
    required this.amount,
  });

  final String when;
  final String text;
  final String status;
  final String amount;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 6),
    child: Row(
      children: [
        SizedBox(
          width: 130,
          child: Text(
            when,
            style: const TextStyle(color: WorkspaceColors.muted, fontSize: 13),
          ),
        ),
        Expanded(child: Text(text, overflow: TextOverflow.ellipsis)),
        const SizedBox(width: 12),
        SizedBox(
          width: 120,
          child: Text(
            status,
            style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
          ),
        ),
        SizedBox(
          width: 90,
          child: Text(
            amount,
            textAlign: TextAlign.right,
            style: const TextStyle(fontWeight: FontWeight.w800),
          ),
        ),
      ],
    ),
  );
}
