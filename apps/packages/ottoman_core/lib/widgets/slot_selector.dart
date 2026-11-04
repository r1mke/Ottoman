import 'package:flutter/material.dart';

import '../catalog/catalog_api.dart';
import '../catalog/catalog_models.dart';
import '../l10n/app_localizations.dart';
import '../network/app_exception.dart';
import '../reservations/reservation_models.dart';
import 'availability_calendar.dart';
import 'states.dart';

/// Lets the parent re-fetch the times (e.g. after the server says a slot was just taken).
class SlotSelectorController {
  Future<void> Function()? _reload;
  Future<void> reload() => _reload?.call() ?? Future.value();
}

/// Calendar + available start times for one barber and a set of services.
/// Shows the curated offer by default; "show all times" lists every 5-minute start.
class SlotSelector extends StatefulWidget {
  const SlotSelector({
    super.key,
    required this.catalog,
    required this.employeeId,
    required this.serviceIds,
    required this.onChanged,
    this.controller,
    this.horizonDays = 90,
    this.excludeReservationId,
    this.rich = false,
  });

  final CatalogApi catalog;
  final String employeeId;
  final List<int> serviceIds;

  /// Called with the chosen UTC start, or null when nothing is selected.
  final void Function(DateTime? startUtc, Availability? availability) onChanged;
  final SlotSelectorController? controller;
  final int horizonDays;

  /// Admin only: treat this reservation as free (rescheduling it).
  final int? excludeReservationId;

  /// The mobile look: a month calendar that marks the days that cannot be booked, with headed blocks for the date
  /// and the time. Off by default (the desktop uses the plain date picker).
  final bool rich;

  @override
  State<SlotSelector> createState() => _SlotSelectorState();
}

class _SlotSelectorState extends State<SlotSelector> {
  late DateTime _date = _today();
  bool _all = false;
  DateTime? _selected;
  Future<Availability>? _future;

  // Rich calendar: the month on show and what is known about each month's free days.
  late DateTime _focused = _date;
  final Map<String, MonthAvailability> _months = {};
  bool _userPicked = false;

  static String _monthKey(DateTime m) => '${m.year}-${m.month}';

  static DateTime _today() {
    final n = DateTime.now();
    return DateTime(n.year, n.month, n.day);
  }

  @override
  void initState() {
    super.initState();
    widget.controller?._reload = _reload;
    _load();
    if (widget.rich) _loadMonth(_focused);
  }

  @override
  void didUpdateWidget(covariant SlotSelector old) {
    super.didUpdateWidget(old);
    widget.controller?._reload = _reload;
    if (old.employeeId != widget.employeeId ||
        old.serviceIds.join(',') != widget.serviceIds.join(',')) {
      // The parent is being rebuilt right now, so it must not be told to rebuild again until this frame is done.
      _selected = null;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) widget.onChanged(null, null);
      });
      _months.clear();
      _userPicked = false;
      _load();
      if (widget.rich) _loadMonth(_focused);
    }
  }

  /// Fetches which days of [month] are free. A failure leaves the month unmarked: the day view still decides.
  Future<void> _loadMonth(DateTime month) async {
    final key = _monthKey(month);
    if (_months.containsKey(key)) return;
    final employeeId = widget.employeeId;
    final serviceIds = widget.serviceIds.join(',');
    try {
      final loaded = await widget.catalog.monthAvailability(
        employeeId: employeeId,
        month: DateTime(month.year, month.month),
        serviceIds: widget.serviceIds,
      );
      // The barber or the services may have changed while this was on its way.
      if (!mounted ||
          employeeId != widget.employeeId ||
          serviceIds != widget.serviceIds.join(',')) {
        return;
      }
      setState(() => _months[key] = loaded);
      _pickFirstFreeDay(loaded);
    } catch (_) {
      // Left unmarked.
    }
  }

  /// When the day shown first (today) is full, start on the first day that has times, unless the customer chose.
  void _pickFirstFreeDay(MonthAvailability loaded) {
    if (_userPicked) return;
    final key = _monthKey(_date);
    if (_monthKey(loaded.month) != key || loaded.isAvailable(_date)) return;
    final days =
        loaded.availableDays.where((d) => !d.isBefore(_today())).toList()
          ..sort();
    if (days.isEmpty) return;
    _date = days.first;
    _focused = days.first;
    _select(null, null);
    _load();
  }

  void _load() {
    setState(() {
      _future = widget.catalog.availability(
        employeeId: widget.employeeId,
        date: _date,
        serviceIds: widget.serviceIds,
        allTimes: _all,
        excludeReservationId: widget.excludeReservationId,
      );
    });
  }

  Future<void> _reload() async {
    _select(null, null);
    _months.clear();
    _load();
    if (widget.rich) _loadMonth(_focused);
    try {
      await _future;
    } catch (_) {
      // Shown by the FutureBuilder.
    }
  }

  void _select(DateTime? start, Availability? availability) {
    _selected = start;
    widget.onChanged(start, availability);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final today = _today();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (widget.rich) ...[
          _BlockTitle(t.chooseDate),
          AvailabilityCalendar(
            selected: _date,
            first: today,
            last: today.add(Duration(days: widget.horizonDays)),
            focused: _focused,
            available: _months[_monthKey(_focused)]?.availableDays,
            onSelected: (d) {
              _userPicked = true;
              _date = d;
              _focused = d;
              _select(null, null);
              _load();
            },
            onMonthChanged: (m) {
              setState(() => _focused = DateTime(m.year, m.month));
              _loadMonth(DateTime(m.year, m.month));
            },
          ),
          const SizedBox(height: 24),
          _BlockTitle(t.chooseTime),
        ] else ...[
          CalendarDatePicker(
            key: const Key('slot_calendar'),
            initialDate: _date,
            firstDate: today,
            lastDate: today.add(Duration(days: widget.horizonDays)),
            onDateChanged: (d) {
              _date = DateTime(d.year, d.month, d.day);
              _select(null, null);
              _load();
            },
          ),
          const Divider(),
        ],
        const SizedBox(height: 8),
        FutureBuilder<Availability>(
          future: _future,
          builder: (context, snap) {
            if (snap.connectionState != ConnectionState.done) {
              return const Padding(
                padding: EdgeInsets.all(24),
                child: Center(child: CircularProgressIndicator()),
              );
            }
            if (snap.hasError) {
              final e = snap.error;
              return ErrorView(
                message: e is AppException ? e.message : t.genericError,
                retryLabel: t.retry,
                onRetry: _load,
              );
            }
            final a = snap.data!;
            if (a.starts.isEmpty) {
              return Column(
                children: [
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 16),
                    child: Text(
                      t.noSlots,
                      key: const Key('no_slots'),
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        color: Theme.of(context).colorScheme.onSurfaceVariant,
                      ),
                    ),
                  ),
                  if (!_all) _toggle(t),
                ],
              );
            }
            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                for (final group in _groups(t, a)) ...[
                  Padding(
                    padding: const EdgeInsets.only(top: 8, bottom: 6),
                    child: Text(
                      group.label,
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                  ),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      for (final i in group.indexes)
                        ChoiceChip(
                          key: Key('slot_${a.localTimes[i]}'),
                          label: Text(a.localTimes[i]),
                          selected: _selected == a.starts[i],
                          onSelected: (on) {
                            setState(
                              () => _select(
                                on ? a.starts[i] : null,
                                on ? a : null,
                              ),
                            );
                          },
                        ),
                    ],
                  ),
                ],
                _toggle(t),
              ],
            );
          },
        ),
      ],
    );
  }

  Widget _toggle(AppLocalizations t) => Align(
    alignment: Alignment.centerLeft,
    child: TextButton(
      key: const Key('toggle_all_times'),
      onPressed: () {
        _all = !_all;
        _select(null, null);
        _load();
      },
      child: Text(_all ? t.showFewerTimes : t.showAllTimes),
    ),
  );

  List<({String label, List<int> indexes})> _groups(
    AppLocalizations t,
    Availability a,
  ) {
    final morning = <int>[], afternoon = <int>[], evening = <int>[];
    for (var i = 0; i < a.localTimes.length; i++) {
      final hour = int.parse(a.localTimes[i].substring(0, 2));
      (hour < 12
              ? morning
              : hour < 17
              ? afternoon
              : evening)
          .add(i);
    }
    return [
      if (morning.isNotEmpty) (label: t.morning, indexes: morning),
      if (afternoon.isNotEmpty) (label: t.afternoon, indexes: afternoon),
      if (evening.isNotEmpty) (label: t.evening, indexes: evening),
    ];
  }
}

/// The heading of a block of the rich selector: a bold title with a short amber bar under it.
class _BlockTitle extends StatelessWidget {
  const _BlockTitle(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(2, 4, 0, 14),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          text,
          style: Theme.of(context).textTheme.headlineSmall
              ?.copyWith(fontWeight: FontWeight.w800),
        ),
        const SizedBox(height: 8),
        Container(
          width: 56,
          height: 5,
          decoration: BoxDecoration(
            color: Theme.of(context).colorScheme.primary,
            borderRadius: BorderRadius.circular(3),
          ),
        ),
      ],
    ),
  );
}

/// Localised label for an API reservation status.
/// The label of a reservation, telling a declined booking from a cancelled one.
String reservationLabel(AppLocalizations t, Reservation r) =>
    r.declined ? t.statusDeclined : reservationStatusLabel(t, r.status);

String reservationStatusLabel(AppLocalizations t, String status) =>
    switch (status) {
      'Pending' => t.statusPending,
      'Confirmed' => t.statusConfirmed,
      'Completed' => t.statusCompleted,
      'Cancelled' => t.statusCancelled,
      'NoShow' => t.statusNoShow,
      _ => status,
    };
