import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'my_reservations_tab.dart';

/// The employee's schedule: one day or one whole week at a time, with the buttons to finish started appointments.
class EmployeeScheduleTab extends StatefulWidget {
  const EmployeeScheduleTab({super.key, required this.reservations, this.now});

  final ReservationsApi reservations;

  /// Test seam for "today".
  final DateTime? now;

  @override
  State<EmployeeScheduleTab> createState() => _EmployeeScheduleTabState();
}

class _EmployeeScheduleTabState extends State<EmployeeScheduleTab> {
  late DateTime _day = _today();
  bool _week = false;
  int _version = 0;

  DateTime _today() {
    final n = widget.now ?? DateTime.now();
    return DateTime(n.year, n.month, n.day);
  }

  static String _fmt(DateTime d, {bool year = true}) =>
      '${d.day.toString().padLeft(2, '0')}.${d.month.toString().padLeft(2, '0')}.${year ? '${d.year}' : ''}';

  (DateTime, DateTime) get _range =>
      _week ? weekContaining(_day) : (_day, _day);

  String get _label {
    final (from, to) = _range;
    return _week ? '${_fmt(from, year: false)} – ${_fmt(to)}' : _fmt(from);
  }

  void _shift(int steps) => setState(() {
    final days = _week ? 7 * steps : steps;
    _day = DateTime(_day.year, _day.month, _day.day + days);
  });

  /// Completing or marking a no-show closes the appointment for good: ask first.
  Future<void> _confirmThen({
    required String title,
    required String message,
    required Future<Reservation> Function() action,
  }) async {
    final ok = await confirmAction(
      context,
      title: title,
      message: message,
      confirmLabel: title,
    );
    if (ok && mounted) await _act(action);
  }

  Future<void> _act(Future<Reservation> Function() action) async {
    final messenger = ScaffoldMessenger.of(context);
    try {
      await action();
    } on AppException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
    if (mounted) setState(() => _version++);
  }

  String _weekday(AppLocalizations t, DateTime d) => switch (d.weekday) {
    DateTime.monday => t.dayMonday,
    DateTime.tuesday => t.dayTuesday,
    DateTime.wednesday => t.dayWednesday,
    DateTime.thursday => t.dayThursday,
    DateTime.friday => t.dayFriday,
    DateTime.saturday => t.daySaturday,
    _ => t.daySunday,
  };

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final (from, to) = _range;
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
          child: SegmentedButton<bool>(
            key: const Key('view_mode'),
            showSelectedIcon: false,
            segments: [
              ButtonSegment(
                value: false,
                label: Text(t.viewDay, key: const Key('mode_day')),
              ),
              ButtonSegment(
                value: true,
                label: Text(t.viewWeek, key: const Key('mode_week')),
              ),
            ],
            selected: {_week},
            onSelectionChanged: (s) => setState(() => _week = s.first),
          ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              IconButton(
                key: const Key('prev_day'),
                tooltip: _week ? t.previousWeek : t.previousDay,
                onPressed: () => _shift(-1),
                icon: const Icon(Icons.chevron_left),
              ),
              Flexible(
                child: TextButton(
                  onPressed: () => setState(() => _day = _today()),
                  child: FittedBox(
                    fit: BoxFit.scaleDown,
                    child: Text(
                      _label,
                      key: const Key('day_label'),
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 16,
                      ),
                    ),
                  ),
                ),
              ),
              IconButton(
                key: const Key('next_day'),
                tooltip: _week ? t.nextWeek : t.nextDay,
                onPressed: () => _shift(1),
                icon: const Icon(Icons.chevron_right),
              ),
            ],
          ),
        ),
        Expanded(
          child: AsyncView<Paged<Reservation>>(
            key: ValueKey('$_label-$_week-$_version'),
            load: () => widget.reservations.list(from: from, to: to),
            skeleton: const ListSkeleton(rowHeight: 110),
            isEmpty: (p) => p.items.isEmpty,
            emptyMessage: t.noAppointments,
            builder: (context, page, reload) {
              final now = DateTime.now().toUtc();
              final items = [...page.items]
                ..sort((a, b) => a.startUtc.compareTo(b.startUtc));
              return RefreshIndicator(
                onRefresh: reload,
                child: ListView(
                  padding: const EdgeInsets.all(16),
                  children: [
                    for (var i = 0; i < items.length; i++) ...[
                      if (_week && _startsNewDay(items, i))
                        Padding(
                          key: Key('day_header_${_dayKey(items[i])}'),
                          padding: EdgeInsets.only(
                            top: i == 0 ? 0 : 12,
                            bottom: 8,
                          ),
                          child: Text(
                            '${_weekday(t, items[i].startUtc.toLocal())}, ${_fmt(items[i].startUtc.toLocal(), year: false)}',
                            style: const TextStyle(
                              fontWeight: FontWeight.w800,
                              fontSize: 15,
                            ),
                          ),
                        ),
                      _Entry(
                        reservation: items[i],
                        canFinish: items[i].canFinish(now),
                        onNoShow: () => _confirmThen(
                          title: t.markNoShow,
                          message: t.noShowQuestion,
                          action: () =>
                              widget.reservations.markNoShow(items[i].id),
                        ),
                        onComplete: () => _confirmThen(
                          title: t.complete,
                          message: t.completeQuestion,
                          action: () =>
                              widget.reservations.complete(items[i].id),
                        ),
                        t: t,
                      ),
                      const SizedBox(height: 12),
                    ],
                  ],
                ),
              );
            },
          ),
        ),
      ],
    );
  }

  static String _dayKey(Reservation r) {
    final l = r.startUtc.toLocal();
    return '${l.year}-${l.month}-${l.day}';
  }

  static bool _startsNewDay(List<Reservation> items, int i) =>
      i == 0 || _dayKey(items[i]) != _dayKey(items[i - 1]);
}

class _Entry extends StatelessWidget {
  const _Entry({
    required this.reservation,
    required this.canFinish,
    required this.onNoShow,
    required this.onComplete,
    required this.t,
  });

  final Reservation reservation;
  final bool canFinish;
  final VoidCallback onNoShow;
  final VoidCallback onComplete;
  final AppLocalizations t;

  @override
  Widget build(BuildContext context) {
    final r = reservation;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ReservationCard(reservation: r),
        if (canFinish)
          Row(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              TextButton(
                key: Key('noshow_${r.id}'),
                onPressed: onNoShow,
                child: Text(t.markNoShow),
              ),
              FilledButton(
                key: Key('complete_${r.id}'),
                style: FilledButton.styleFrom(minimumSize: const Size(120, 40)),
                onPressed: onComplete,
                child: Text(t.complete),
              ),
            ],
          ),
      ],
    );
  }
}

/// Monday to Sunday of the week that contains [d]. Calendar arithmetic, so daylight saving cannot shift it.
(DateTime, DateTime) weekContaining(DateTime d) {
  final monday = DateTime(d.year, d.month, d.day - (d.weekday - 1));
  return (monday, DateTime(monday.year, monday.month, monday.day + 6));
}
