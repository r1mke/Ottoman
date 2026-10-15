import 'package:flutter/material.dart';

import '../catalog/catalog_api.dart';
import '../catalog/catalog_models.dart';
import '../l10n/app_localizations.dart';
import '../network/app_exception.dart';
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

  @override
  State<SlotSelector> createState() => _SlotSelectorState();
}

class _SlotSelectorState extends State<SlotSelector> {
  late DateTime _date = _today();
  bool _all = false;
  DateTime? _selected;
  Future<Availability>? _future;

  static DateTime _today() {
    final n = DateTime.now();
    return DateTime(n.year, n.month, n.day);
  }

  @override
  void initState() {
    super.initState();
    widget.controller?._reload = _reload;
    _load();
  }

  @override
  void didUpdateWidget(covariant SlotSelector old) {
    super.didUpdateWidget(old);
    widget.controller?._reload = _reload;
    if (old.employeeId != widget.employeeId ||
        old.serviceIds.join(',') != widget.serviceIds.join(',')) {
      _select(null, null);
      _load();
    }
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
    _load();
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

/// Localised label for an API reservation status.
String reservationStatusLabel(AppLocalizations t, String status) =>
    switch (status) {
      'Pending' => t.statusPending,
      'Confirmed' => t.statusConfirmed,
      'Completed' => t.statusCompleted,
      'Cancelled' => t.statusCancelled,
      'NoShow' => t.statusNoShow,
      _ => status,
    };
