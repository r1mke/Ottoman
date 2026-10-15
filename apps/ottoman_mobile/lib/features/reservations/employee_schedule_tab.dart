import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'my_reservations_tab.dart';

/// The employee's day: navigate between days and finish started appointments.
class EmployeeScheduleTab extends StatefulWidget {
  const EmployeeScheduleTab({super.key, required this.reservations});

  final ReservationsApi reservations;

  @override
  State<EmployeeScheduleTab> createState() => _EmployeeScheduleTabState();
}

class _EmployeeScheduleTabState extends State<EmployeeScheduleTab> {
  late DateTime _day = _today();
  int _version = 0;

  static DateTime _today() {
    final n = DateTime.now();
    return DateTime(n.year, n.month, n.day);
  }

  String get _label =>
      '${_day.day.toString().padLeft(2, '0')}.${_day.month.toString().padLeft(2, '0')}.${_day.year}';

  void _shift(int days) =>
      setState(() => _day = _day.add(Duration(days: days)));

  Future<void> _act(Future<Reservation> Function() action) async {
    final messenger = ScaffoldMessenger.of(context);
    try {
      await action();
    } on AppException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
    if (mounted) setState(() => _version++);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              IconButton(
                key: const Key('prev_day'),
                tooltip: t.previousDay,
                onPressed: () => _shift(-1),
                icon: const Icon(Icons.chevron_left),
              ),
              TextButton(
                onPressed: () => setState(() => _day = _today()),
                child: Text(
                  _label,
                  key: const Key('day_label'),
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 16,
                  ),
                ),
              ),
              IconButton(
                key: const Key('next_day'),
                tooltip: t.nextDay,
                onPressed: () => _shift(1),
                icon: const Icon(Icons.chevron_right),
              ),
            ],
          ),
        ),
        Expanded(
          child: AsyncView<Paged<Reservation>>(
            key: ValueKey('$_label-$_version'),
            load: () => widget.reservations.list(from: _day, to: _day),
            skeleton: const ListSkeleton(rowHeight: 110),
            isEmpty: (p) => p.items.isEmpty,
            emptyMessage: t.noAppointments,
            builder: (context, page, reload) {
              final now = DateTime.now().toUtc();
              final items = [...page.items]
                ..sort((a, b) => a.startUtc.compareTo(b.startUtc));
              return RefreshIndicator(
                onRefresh: reload,
                child: ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: items.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 12),
                  itemBuilder: (context, i) {
                    final r = items[i];
                    return Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        ReservationCard(reservation: r),
                        if (r.canFinish(now))
                          Row(
                            mainAxisAlignment: MainAxisAlignment.end,
                            children: [
                              TextButton(
                                key: Key('noshow_${r.id}'),
                                onPressed: () => _act(
                                  () => widget.reservations.markNoShow(r.id),
                                ),
                                child: Text(t.markNoShow),
                              ),
                              FilledButton(
                                key: Key('complete_${r.id}'),
                                style: FilledButton.styleFrom(
                                  minimumSize: const Size(120, 40),
                                ),
                                onPressed: () => _act(
                                  () => widget.reservations.complete(r.id),
                                ),
                                child: Text(t.complete),
                              ),
                            ],
                          ),
                      ],
                    );
                  },
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}
