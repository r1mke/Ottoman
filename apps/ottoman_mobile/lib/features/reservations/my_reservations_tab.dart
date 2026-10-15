import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// The client's own appointments, upcoming and past, with cancellation.
class MyReservationsTab extends StatelessWidget {
  const MyReservationsTab({super.key, required this.reservations});

  final ReservationsApi reservations;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return DefaultTabController(
      length: 2,
      child: Column(
        children: [
          TabBar(
            tabs: [
              Tab(text: t.upcoming),
              Tab(text: t.past),
            ],
          ),
          Expanded(
            child: TabBarView(
              children: [
                _List(reservations: reservations, upcoming: true),
                _List(reservations: reservations, upcoming: false),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _List extends StatefulWidget {
  const _List({required this.reservations, required this.upcoming});

  final ReservationsApi reservations;
  final bool upcoming;

  @override
  State<_List> createState() => _ListState();
}

class _ListState extends State<_List> {
  int _version = 0;

  Future<void> _cancel(Reservation r) async {
    final t = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(t.cancelBooking),
        content: Text(t.cancelBookingQuestion),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: Text(t.no),
          ),
          FilledButton(
            key: const Key('confirm_cancel'),
            style: FilledButton.styleFrom(minimumSize: const Size(96, 44)),
            onPressed: () => Navigator.pop(ctx, true),
            child: Text(t.yes),
          ),
        ],
      ),
    );
    if (ok != true) return;
    try {
      await widget.reservations.cancel(r.id);
      if (mounted) setState(() => _version++);
    } on AppException catch (e) {
      // The server states why (e.g. inside the cancellation window).
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
      if (mounted) setState(() => _version++);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AsyncView<Paged<Reservation>>(
      key: ValueKey(_version),
      load: () => widget.reservations.list(upcoming: widget.upcoming),
      skeleton: const ListSkeleton(rowHeight: 110),
      isEmpty: (p) => p.items.isEmpty,
      emptyMessage: t.noAppointments,
      builder: (context, page, reload) => RefreshIndicator(
        onRefresh: reload,
        child: ListView.separated(
          padding: const EdgeInsets.all(16),
          itemCount: page.items.length,
          separatorBuilder: (_, _) => const SizedBox(height: 12),
          itemBuilder: (context, i) => ReservationCard(
            reservation: page.items[i],
            onCancel: page.items[i].canCancel
                ? () => _cancel(page.items[i])
                : null,
          ),
        ),
      ),
    );
  }
}

class ReservationCard extends StatelessWidget {
  const ReservationCard({super.key, required this.reservation, this.onCancel});

  final Reservation reservation;
  final VoidCallback? onCancel;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    final r = reservation;
    final inactive =
        r.status == ReservationStatus.cancelled ||
        r.status == ReservationStatus.noShow;

    return Card(
      key: Key('reservation_${r.id}'),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    '${r.localDate} · ${r.localTime}',
                    style: const TextStyle(
                      fontWeight: FontWeight.w800,
                      fontSize: 16,
                    ),
                  ),
                ),
                Chip(
                  label: Text(reservationStatusLabel(t, r.status)),
                  backgroundColor: inactive
                      ? scheme.errorContainer
                      : scheme.secondaryContainer,
                  visualDensity: VisualDensity.compact,
                ),
              ],
            ),
            const SizedBox(height: 4),
            Text(r.serviceNames),
            Text(
              r.employeeName,
              style: TextStyle(color: scheme.onSurfaceVariant),
            ),
            const SizedBox(height: 4),
            Text(
              '${t.minutesShort(r.totalDurationMinutes)} · ${r.totalPrice.toStringAsFixed(2)} ${t.currencyShort}',
              style: const TextStyle(fontWeight: FontWeight.w700),
            ),
            if (onCancel != null)
              Align(
                alignment: Alignment.centerRight,
                child: TextButton(
                  key: Key('cancel_${r.id}'),
                  onPressed: onCancel,
                  child: Text(t.cancelBooking),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
