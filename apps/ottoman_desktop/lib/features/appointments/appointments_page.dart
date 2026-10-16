import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'appointment_dialogs.dart';

enum _Period { today, week, month }

/// Admin "Termini": schedule per barber and period, with stats and edit/cancel actions.
class AppointmentsPage extends StatefulWidget {
  const AppointmentsPage({
    super.key,
    required this.catalog,
    required this.reservations,
    required this.isAdmin,
  });

  final CatalogApi catalog;
  final ReservationsApi reservations;

  /// Admins see every barber and may create/move appointments; employees see only their own.
  final bool isAdmin;

  @override
  State<AppointmentsPage> createState() => _AppointmentsPageState();
}

class _AppointmentsPageState extends State<AppointmentsPage> {
  String? _employeeId; // null = everyone
  _Period _period = _Period.week;
  int _version = 0;
  late final Future<List<Employee>> _employees = widget.catalog.employees();

  (DateTime, DateTime) get _range {
    final n = DateTime.now();
    final today = DateTime(n.year, n.month, n.day);
    return switch (_period) {
      _Period.today => (today, today),
      _Period.week => (
        today.subtract(Duration(days: today.weekday - 1)),
        today
            .subtract(Duration(days: today.weekday - 1))
            .add(const Duration(days: 6)),
      ),
      _Period.month => (
        DateTime(n.year, n.month, 1),
        DateTime(n.year, n.month + 1, 0),
      ),
    };
  }

  void _refresh() => setState(() => _version++);

  Future<void> _dialog(Widget dialog) async {
    final changed = await showDialog<bool>(
      context: context,
      builder: (_) => dialog,
    );
    if (changed == true) _refresh();
  }

  Future<void> _cancel(Reservation r) async {
    final messenger = ScaffoldMessenger.of(context);
    final reason = await showCancelReasonDialog(
      context,
      summary: '${r.clientName} · ${r.localDate} ${r.localTime}',
      reasonRequired: true,
    );
    if (reason == null) return;
    try {
      await widget.reservations.cancel(r.id, reason: reason);
    } on AppException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
    if (mounted) _refresh();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final (from, to) = _range;

    return Padding(
      padding: const EdgeInsets.fromLTRB(32, 0, 32, 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              if (widget.isAdmin) ...[
                FutureBuilder<List<Employee>>(
                  future: _employees,
                  builder: (context, snap) => SizedBox(
                    width: 240,
                    child: DropdownButtonFormField<String?>(
                      isExpanded: true,
                      key: const Key('employee_filter'),
                      initialValue: _employeeId,
                      decoration: InputDecoration(
                        labelText: t.employee,
                        isDense: true,
                      ),
                      items: [
                        DropdownMenuItem<String?>(
                          value: null,
                          child: Text(t.allEmployees),
                        ),
                        for (final e in snap.data ?? const <Employee>[])
                          DropdownMenuItem<String?>(
                            value: e.id,
                            child: Text(e.fullName),
                          ),
                      ],
                      onChanged: (v) => setState(() => _employeeId = v),
                    ),
                  ),
                ),
                const SizedBox(width: 16),
              ],
              SegmentedButton<_Period>(
                key: const Key('period_filter'),
                showSelectedIcon: false,
                segments: [
                  ButtonSegment(value: _Period.today, label: Text(t.today)),
                  ButtonSegment(value: _Period.week, label: Text(t.weekly)),
                  ButtonSegment(value: _Period.month, label: Text(t.monthly)),
                ],
                selected: {_period},
                onSelectionChanged: (s) => setState(() => _period = s.first),
              ),
              const Spacer(),
              if (widget.isAdmin)
                FilledButton.icon(
                  key: const Key('new_appointment'),
                  style: FilledButton.styleFrom(
                    minimumSize: const Size(160, 44),
                  ),
                  onPressed: () => _dialog(
                    NewAppointmentDialog(
                      catalog: widget.catalog,
                      reservations: widget.reservations,
                    ),
                  ),
                  icon: const Icon(Icons.add),
                  label: Text(t.newAppointment),
                ),
            ],
          ),
          const SizedBox(height: 16),
          AsyncView<ReservationSummary>(
            key: ValueKey('summary-$_employeeId-$_period-$_version'),
            load: () => widget.reservations.summary(
              from: from,
              to: to,
              employeeId: _employeeId,
            ),
            skeleton: const Row(
              children: [
                Expanded(child: SkeletonBox(height: 84, radius: 12)),
                SizedBox(width: 16),
                Expanded(child: SkeletonBox(height: 84, radius: 12)),
                SizedBox(width: 16),
                Expanded(child: SkeletonBox(height: 84, radius: 12)),
                SizedBox(width: 16),
                Expanded(child: SkeletonBox(height: 84, radius: 12)),
              ],
            ),
            builder: (context, s, _) => Row(
              children: [
                _StatCard(
                  key: const Key('stat_upcoming'),
                  label: t.statUpcoming,
                  value: s.upcoming,
                ),
                const SizedBox(width: 16),
                _StatCard(
                  key: const Key('stat_completed'),
                  label: t.statCompleted,
                  value: s.completed,
                ),
                const SizedBox(width: 16),
                _StatCard(
                  key: const Key('stat_cancelled'),
                  label: t.statCancelled,
                  value: s.cancelled,
                ),
                const SizedBox(width: 16),
                _StatCard(
                  key: const Key('stat_clients'),
                  label: t.statClients,
                  value: s.totalClients,
                ),
              ],
            ),
          ),
          const SizedBox(height: 24),
          Text(
            t.appointmentsOverview,
            style: Theme.of(context).textTheme.titleLarge
                ?.copyWith(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 8),
          Expanded(
            child: AsyncView<Paged<Reservation>>(
              key: ValueKey('list-$_employeeId-$_period-$_version'),
              load: () => widget.reservations.list(
                from: from,
                to: to,
                employeeId: _employeeId,
                pageSize: 100,
              ),
              skeleton: const ListSkeleton(rowHeight: 48),
              isEmpty: (p) => p.items.isEmpty,
              emptyMessage: t.noAppointments,
              builder: (context, page, _) {
                final items = [...page.items]
                  ..sort((a, b) => a.startUtc.compareTo(b.startUtc));
                return Card(
                  child: SingleChildScrollView(
                    child: SizedBox(
                      width: double.infinity,
                      child: DataTable(
                        columns: [
                          DataColumn(label: Text(t.client)),
                          DataColumn(label: Text(t.service)),
                          DataColumn(label: Text(t.date)),
                          DataColumn(label: Text(t.employee)),
                          DataColumn(label: Text(t.status)),
                          const DataColumn(label: Text('')),
                        ],
                        rows: [
                          for (final r in items)
                            DataRow(
                              key: ValueKey('row_${r.id}'),
                              cells: [
                                DataCell(Text(r.clientName)),
                                DataCell(Text(r.serviceNames)),
                                DataCell(Text('${r.localDate} ${r.localTime}')),
                                DataCell(Text(r.employeeName)),
                                DataCell(
                                  Chip(
                                    label: Text(
                                      reservationStatusLabel(t, r.status),
                                    ),
                                    visualDensity: VisualDensity.compact,
                                  ),
                                ),
                                DataCell(
                                  r.isOpen
                                      ? Row(
                                          mainAxisSize: MainAxisSize.min,
                                          children: [
                                            if (widget.isAdmin) ...[
                                              OutlinedButton(
                                                key: Key('edit_${r.id}'),
                                                style: OutlinedButton.styleFrom(
                                                  minimumSize: const Size(
                                                    72,
                                                    36,
                                                  ),
                                                ),
                                                onPressed: () => _dialog(
                                                  RescheduleDialog(
                                                    catalog: widget.catalog,
                                                    reservations:
                                                        widget.reservations,
                                                    reservation: r,
                                                  ),
                                                ),
                                                child: Text(t.edit),
                                              ),
                                              const SizedBox(width: 8),
                                            ],
                                            OutlinedButton(
                                              key: Key('cancel_${r.id}'),
                                              style: OutlinedButton.styleFrom(
                                                minimumSize: const Size(72, 36),
                                              ),
                                              onPressed: () => _cancel(r),
                                              child: Text(t.cancel),
                                            ),
                                          ],
                                        )
                                      : const SizedBox.shrink(),
                                ),
                              ],
                            ),
                        ],
                      ),
                    ),
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({super.key, required this.label, required this.value});

  final String label;
  final int value;

  @override
  Widget build(BuildContext context) => Expanded(
    child: Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              label,
              style: TextStyle(
                color: Theme.of(context).colorScheme.onSurfaceVariant,
              ),
            ),
            const SizedBox(height: 6),
            Text(
              '$value',
              style: Theme.of(context).textTheme.headlineMedium
                  ?.copyWith(fontWeight: FontWeight.w800),
            ),
          ],
        ),
      ),
    ),
  );
}
