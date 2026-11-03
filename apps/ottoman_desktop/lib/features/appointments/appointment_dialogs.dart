import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

String _message(AppException e) {
  final detail = e.errors.values.expand((x) => x).join('\n');
  return detail.isEmpty ? e.message : detail;
}

/// Admin edit: move an open reservation to another free time. The reservation itself is treated as free.
class RescheduleDialog extends StatefulWidget {
  const RescheduleDialog({
    super.key,
    required this.catalog,
    required this.reservations,
    required this.reservation,
  });

  final CatalogApi catalog;
  final ReservationsApi reservations;
  final Reservation reservation;

  @override
  State<RescheduleDialog> createState() => _RescheduleDialogState();
}

class _RescheduleDialogState extends State<RescheduleDialog> {
  final _controller = SlotSelectorController();
  DateTime? _start;
  bool _busy = false;
  String? _error;

  Future<void> _save() async {
    final start = _start;
    if (start == null) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.reservations.reschedule(
        widget.reservation.id,
        startUtc: start,
      );
      if (mounted) Navigator.pop(context, true);
    } on AppException catch (e) {
      if (!mounted) return;
      if (e.isConflict) await _controller.reload();
      if (!mounted) return;
      setState(() => _error = _message(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final r = widget.reservation;
    return AlertDialog(
      title: DialogHeader(t.reschedule),
      content: SizedBox(
        width: 520,
        height: 620,
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '${r.clientName} · ${r.serviceNames}',
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
              Text('${r.employeeName} · ${r.localDate} ${r.localTime}'),
              const SizedBox(height: 8),
              SlotSelector(
                catalog: widget.catalog,
                employeeId: r.employeeId,
                serviceIds: r.items.map((i) => i.serviceId).toList(),
                excludeReservationId: r.id,
                controller: _controller,
                onChanged: (start, _) => setState(() {
                  _start = start;
                  _error = null;
                }),
              ),
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text(
                    _error!,
                    key: const Key('reschedule_error'),
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _busy ? null : () => Navigator.pop(context),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: const Key('reschedule_save'),
          style: FilledButton.styleFrom(minimumSize: const Size(120, 44)),
          onPressed: (_start == null || _busy) ? null : _save,
          child: Text(t.save),
        ),
      ],
    );
  }
}

/// Admin walk-in: pick a client, a barber, services and a time.
class NewAppointmentDialog extends StatefulWidget {
  const NewAppointmentDialog({
    super.key,
    required this.catalog,
    required this.reservations,
  });

  final CatalogApi catalog;
  final ReservationsApi reservations;

  @override
  State<NewAppointmentDialog> createState() => _NewAppointmentDialogState();
}

class _NewAppointmentDialogState extends State<NewAppointmentDialog> {
  final _search = TextEditingController();
  final _note = TextEditingController();
  final _controller = SlotSelectorController();
  late final Future<List<Employee>> _employees = widget.catalog.employees();
  late final Future<List<Service>> _services = widget.catalog.services();
  Future<Paged<ClientItem>>? _clients;
  ClientItem? _client;
  Employee? _employee;
  final _serviceIds = <int>{};
  DateTime? _start;
  bool _busy = false;

  /// A repeat of the same booking (double click, retry after a lost answer) must not book twice; changing anything
  /// about it makes it a different booking with its own key.
  String _attemptKey = newIdempotencyKey();
  String? _attemptFor;

  String _keyFor(String fingerprint) {
    if (_attemptFor != fingerprint) {
      _attemptFor = fingerprint;
      _attemptKey = newIdempotencyKey();
    }
    return _attemptKey;
  }

  String? _error;

  @override
  void initState() {
    super.initState();
    _searchClients();
  }

  @override
  void dispose() {
    _search.dispose();
    _note.dispose();
    super.dispose();
  }

  void _searchClients() {
    final found = widget.reservations.clients(search: _search.text);
    setState(() {
      _clients = found;
    });
  }

  Future<void> _save() async {
    final start = _start, client = _client, employee = _employee;
    if (start == null || client == null || employee == null) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.reservations.create(
        employeeId: employee.id,
        serviceIds: _serviceIds.toList(),
        startUtc: start,
        note: _note.text.trim().isEmpty ? null : _note.text.trim(),
        clientId: client.id,
        idempotencyKey: _keyFor(
          '${client.id}|${employee.id}|${start.toIso8601String()}|${(_serviceIds.toList()..sort())}|${_note.text.trim()}',
        ),
      );
      if (mounted) Navigator.pop(context, true);
    } on AppException catch (e) {
      if (!mounted) return;
      if (e.isConflict) await _controller.reload();
      if (!mounted) return;
      setState(() => _error = _message(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final ready =
        _client != null && _employee != null && _serviceIds.isNotEmpty;

    return AlertDialog(
      title: DialogHeader(t.newAppointment),
      content: SizedBox(
        width: 560,
        height: 680,
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              TextField(
                key: const Key('client_search'),
                controller: _search,
                decoration: InputDecoration(
                  labelText: t.searchClient,
                  prefixIcon: const Icon(Icons.search),
                ),
                onChanged: (_) => _searchClients(),
              ),
              const SizedBox(height: 8),
              SizedBox(
                height: 120,
                child: FutureBuilder<Paged<ClientItem>>(
                  future: _clients,
                  builder: (context, snap) {
                    if (!snap.hasData) {
                      return const Center(child: CircularProgressIndicator());
                    }
                    return ListView(
                      children: [
                        for (final c in snap.data!.items)
                          ListTile(
                            key: Key('client_${c.id}'),
                            dense: true,
                            selected: _client?.id == c.id,
                            selectedTileColor: WorkspaceColors.amberSoft,
                            selectedColor: WorkspaceColors.amberInk,
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(8),
                            ),
                            title: Text(c.fullName),
                            subtitle: Text(c.email),
                            trailing: _client?.id == c.id
                                ? const Icon(Icons.check_circle)
                                : null,
                            onTap: () => setState(() => _client = c),
                          ),
                      ],
                    );
                  },
                ),
              ),
              const SizedBox(height: 8),
              FutureBuilder<List<Employee>>(
                future: _employees,
                builder: (context, snap) => DropdownButtonFormField<Employee>(
                  key: const Key('new_employee'),
                  initialValue: _employee,
                  decoration: InputDecoration(labelText: t.employee),
                  items: [
                    for (final e in snap.data ?? const <Employee>[])
                      DropdownMenuItem(value: e, child: Text(e.fullName)),
                  ],
                  onChanged: (e) => setState(() {
                    _employee = e;
                    _serviceIds.clear();
                    _start = null;
                  }),
                ),
              ),
              if (_employee != null) ...[
                const SizedBox(height: 12),
                FutureBuilder<List<Service>>(
                  future: _services,
                  builder: (context, snap) => _ServicesField(
                    services: (snap.data ?? const <Service>[])
                        .where((s) => _employee!.serviceIds.contains(s.id))
                        .toList(),
                    selected: _serviceIds,
                    onToggle: (id, on) => setState(() {
                      on ? _serviceIds.add(id) : _serviceIds.remove(id);
                      _start = null;
                    }),
                  ),
                ),
                const SizedBox(height: 4),
              ],
              if (_employee != null && _serviceIds.isNotEmpty)
                SlotSelector(
                  catalog: widget.catalog,
                  employeeId: _employee!.id,
                  serviceIds: _serviceIds.toList(),
                  controller: _controller,
                  onChanged: (start, _) => setState(() {
                    _start = start;
                    _error = null;
                  }),
                ),
              TextField(
                controller: _note,
                decoration: InputDecoration(labelText: t.note),
              ),
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text(
                    _error!,
                    key: const Key('new_error'),
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _busy ? null : () => Navigator.pop(context),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: const Key('new_save'),
          style: FilledButton.styleFrom(minimumSize: const Size(120, 44)),
          onPressed: (!ready || _start == null || _busy) ? null : _save,
          child: Text(t.book),
        ),
      ],
    );
  }
}

/// The services of a booking, picked from a drop-down (several can be ticked) so it looks like the barber field above.
class _ServicesField extends StatelessWidget {
  const _ServicesField({
    required this.services,
    required this.selected,
    required this.onToggle,
  });

  final List<Service> services;
  final Set<int> selected;
  final void Function(int id, bool on) onToggle;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final chosen = services.where((s) => selected.contains(s.id)).toList();
    final total = chosen.fold<double>(0, (sum, s) => sum + s.price);
    final summary = chosen.isEmpty
        ? ''
        : '${chosen.map((s) => s.name).join(', ')} \u00b7 ${total.toStringAsFixed(0)} ${t.currencyShort}';

    return LayoutBuilder(
      builder: (context, box) => MenuAnchor(
        style: MenuStyle(
          minimumSize: WidgetStatePropertyAll(Size(box.maxWidth, 0)),
          maximumSize: WidgetStatePropertyAll(Size(box.maxWidth, 320)),
          backgroundColor: const WidgetStatePropertyAll(
            WorkspaceColors.surface,
          ),
        ),
        menuChildren: [
          for (final s in services)
            CheckboxMenuButton(
              key: Key('new_service_${s.id}'),
              closeOnActivate: false,
              value: selected.contains(s.id),
              onChanged: (on) => onToggle(s.id, on ?? false),
              child: Text(
                '${s.name} \u00b7 ${s.price.toStringAsFixed(0)} ${t.currencyShort}',
              ),
            ),
        ],
        builder: (context, controller, _) => InkWell(
          key: const Key('new_services'),
          borderRadius: BorderRadius.circular(10),
          onTap: services.isEmpty
              ? null
              : () =>
                    controller.isOpen ? controller.close() : controller.open(),
          child: InputDecorator(
            isEmpty: summary.isEmpty,
            isFocused: controller.isOpen,
            decoration: InputDecoration(
              labelText: t.services,
              hintText: t.selectServices,
              suffixIcon: const Icon(Icons.arrow_drop_down),
            ),
            child: Text(summary, maxLines: 1, overflow: TextOverflow.ellipsis),
          ),
        ),
      ),
    );
  }
}
