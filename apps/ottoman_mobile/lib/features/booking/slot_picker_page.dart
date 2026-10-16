import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// Step 3 of booking: pick a date and time, add a note and confirm.
/// The server re-checks everything; a 409 simply refreshes the times.
class SlotPickerPage extends StatefulWidget {
  const SlotPickerPage({
    super.key,
    required this.catalog,
    required this.reservations,
    required this.employee,
    required this.services,
    required this.onBooked,
  });

  final CatalogApi catalog;
  final ReservationsApi reservations;
  final Employee employee;
  final List<Service> services;
  final VoidCallback onBooked;

  @override
  State<SlotPickerPage> createState() => _SlotPickerPageState();
}

class _SlotPickerPageState extends State<SlotPickerPage> {
  final _controller = SlotSelectorController();
  final _note = TextEditingController();
  DateTime? _start;
  Availability? _availability;
  bool _busy = false;

  /// One key per booking intention; a different slot is a different booking, so it gets a new key.
  String _attempt = newIdempotencyKey();
  String? _error;

  @override
  void dispose() {
    _note.dispose();
    super.dispose();
  }

  Future<void> _book() async {
    final start = _start;
    if (start == null) return;
    final t = AppLocalizations.of(context);
    final navigator = Navigator.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.reservations.create(
        employeeId: widget.employee.id,
        serviceIds: widget.services.map((s) => s.id).toList(),
        startUtc: start,
        idempotencyKey: _attempt,
        note: _note.text.trim().isEmpty ? null : _note.text.trim(),
      );
      widget.onBooked();
      messenger.showSnackBar(SnackBar(content: Text(t.bookingConfirmed)));
      navigator.popUntil((r) => r.isFirst);
    } on AppException catch (e) {
      if (!mounted) return;
      // Reload first: clearing the stale choice also resets the error, so set the message afterwards.
      if (e.isConflict) await _controller.reload();
      if (!mounted) return;
      setState(() => _error = e.isConflict ? t.slotTaken : e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    final total = widget.services.fold<double>(0, (a, s) => a + s.price);
    final minutes = widget.services.fold<int>(
      0,
      (a, s) => a + s.durationMinutes,
    );

    return Scaffold(
      appBar: AppBar(title: Text(widget.employee.fullName)),
      body: Column(
        children: [
          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Text(
                  widget.services.map((s) => s.name).join(' + '),
                  style: const TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 16,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  '${t.minutesShort(minutes)} · ${total.toStringAsFixed(2)} ${t.currencyShort}',
                  style: TextStyle(color: scheme.onSurfaceVariant),
                ),
                const SizedBox(height: 8),
                SlotSelector(
                  catalog: widget.catalog,
                  employeeId: widget.employee.id,
                  serviceIds: widget.services.map((s) => s.id).toList(),
                  controller: _controller,
                  onChanged: (start, availability) => setState(() {
                    _start = start;
                    _attempt = newIdempotencyKey();
                    _availability = availability;
                    _error = null;
                  }),
                ),
                const SizedBox(height: 16),
                TextField(
                  key: const Key('booking_note'),
                  controller: _note,
                  maxLines: 3,
                  maxLength: 500,
                  decoration: InputDecoration(
                    labelText: t.note,
                    hintText: t.noteHint,
                    alignLabelWithHint: true,
                  ),
                ),
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(
                      _error!,
                      key: const Key('booking_error'),
                      style: TextStyle(color: scheme.error),
                    ),
                  ),
              ],
            ),
          ),
          SafeArea(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (_availability != null && _start != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 8),
                      child: Text(
                        '${_availability!.date} · ${_availability!.localTimes[_availability!.starts.indexOf(_start!)]}',
                        key: const Key('chosen_time'),
                        textAlign: TextAlign.center,
                        style: const TextStyle(fontWeight: FontWeight.w700),
                      ),
                    ),
                  FilledButton(
                    key: const Key('book'),
                    onPressed: (_start == null || _busy) ? null : _book,
                    child: _busy
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : Text(t.book),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
