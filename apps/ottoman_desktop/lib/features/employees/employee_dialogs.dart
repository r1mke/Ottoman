import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import '../images/picture_editor.dart';

String _message(AppException e) {
  final detail = e.errors.values.expand((x) => x).join('\n');
  return detail.isEmpty ? e.message : detail;
}

/// Create (no [employee]) or edit an employee.
class EmployeeDialog extends StatefulWidget {
  const EmployeeDialog({
    super.key,
    required this.catalog,
    this.employee,
    this.chooser = const FileImageChooser(),
  });

  final CatalogApi catalog;
  final Employee? employee;
  final ImageChooser chooser;

  @override
  State<EmployeeDialog> createState() => _EmployeeDialogState();
}

class _EmployeeDialogState extends State<EmployeeDialog> {
  static List<EditablePicture> _pictures(String? url) => [
    if (url != null) EditablePicture('photo', url),
  ];

  final _form = GlobalKey<FormState>();
  late final _first = TextEditingController(text: widget.employee?.firstName);
  late final _last = TextEditingController(text: widget.employee?.lastName);
  late final _email = TextEditingController(text: widget.employee?.email);
  late final _phone = TextEditingController(text: widget.employee?.phone);
  late final _title = TextEditingController(text: widget.employee?.title);
  late final _bio = TextEditingController(text: widget.employee?.bio);
  final _password = TextEditingController();
  late bool _active = widget.employee?.isActive ?? true;
  bool _busy = false;
  String? _error;

  bool get _isNew => widget.employee == null;

  @override
  void dispose() {
    for (final c in [_first, _last, _email, _phone, _title, _bio, _password]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    String? nullIfEmpty(String s) => s.trim().isEmpty ? null : s.trim();
    try {
      if (_isNew) {
        await widget.catalog.createEmployee(
          firstName: _first.text.trim(),
          lastName: _last.text.trim(),
          email: _email.text.trim(),
          phone: nullIfEmpty(_phone.text),
          password: _password.text,
          title: _title.text.trim(),
          bio: nullIfEmpty(_bio.text),
        );
      } else {
        await widget.catalog.updateEmployee(
          widget.employee!.id,
          firstName: _first.text.trim(),
          lastName: _last.text.trim(),
          phone: nullIfEmpty(_phone.text),
          title: _title.text.trim(),
          bio: nullIfEmpty(_bio.text),
          isActive: _active,
        );
      }
      if (mounted) Navigator.pop(context, true);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = _message(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    String? required(String? v) =>
        (v == null || v.trim().isEmpty) ? t.requiredField : null;

    return AlertDialog(
      title: Text(_isNew ? t.add : t.edit),
      content: SizedBox(
        width: 460,
        child: Form(
          key: _form,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: TextFormField(
                        key: const Key('emp_first'),
                        controller: _first,
                        decoration: InputDecoration(labelText: t.firstName),
                        validator: required,
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: TextFormField(
                        key: const Key('emp_last'),
                        controller: _last,
                        decoration: InputDecoration(labelText: t.lastName),
                        validator: required,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                if (_isNew) ...[
                  TextFormField(
                    key: const Key('emp_email'),
                    controller: _email,
                    decoration: InputDecoration(labelText: t.email),
                    validator: (v) =>
                        (v == null || !v.contains('@')) ? t.invalidEmail : null,
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    key: const Key('emp_password'),
                    controller: _password,
                    obscureText: true,
                    decoration: InputDecoration(
                      labelText: t.password,
                      helperText: t.passwordRules,
                      helperMaxLines: 2,
                    ),
                    validator: required,
                  ),
                  const SizedBox(height: 12),
                ],
                TextFormField(
                  controller: _phone,
                  decoration: InputDecoration(labelText: t.phone),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  key: const Key('emp_title'),
                  controller: _title,
                  decoration: InputDecoration(labelText: t.title),
                  validator: required,
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _bio,
                  decoration: InputDecoration(labelText: t.bio),
                  maxLines: 2,
                ),
                if (!_isNew)
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    title: Text(t.active),
                    value: _active,
                    onChanged: (v) => setState(() => _active = v),
                  ),
                if (!_isNew) ...[
                  const SizedBox(height: 8),
                  PictureEditor(
                    key: const Key('employee_pictures'),
                    chooser: widget.chooser,
                    fallbackIcon: Icons.person_outline,
                    pictures: _pictures(widget.employee!.photoUrl),
                    onAdd: (img) async => _pictures(
                      (await widget.catalog.setEmployeePhoto(
                        widget.employee!.id,
                        img.bytes,
                        img.name,
                      )).photoUrl,
                    ),
                    onRemove: (_) async => _pictures(
                      (await widget.catalog.clearEmployeePhoto(
                        widget.employee!.id,
                      )).photoUrl,
                    ),
                  ),
                ],
                if (_error != null)
                  Align(
                    alignment: Alignment.centerLeft,
                    child: Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: Text(
                        _error!,
                        style: TextStyle(
                          color: Theme.of(context).colorScheme.error,
                        ),
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _busy ? null : () => Navigator.pop(context),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: const Key('emp_save'),
          style: FilledButton.styleFrom(minimumSize: const Size(120, 44)),
          onPressed: _busy ? null : _save,
          child: Text(t.save),
        ),
      ],
    );
  }
}

class AssignServicesDialog extends StatefulWidget {
  const AssignServicesDialog({
    super.key,
    required this.catalog,
    required this.employee,
  });

  final CatalogApi catalog;
  final Employee employee;

  @override
  State<AssignServicesDialog> createState() => _AssignServicesDialogState();
}

class _AssignServicesDialogState extends State<AssignServicesDialog> {
  late final Set<int> _selected = {...widget.employee.serviceIds};
  bool _busy = false;
  String? _error;

  Future<void> _save() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.catalog.setEmployeeServices(
        widget.employee.id,
        _selected.toList(),
      );
      if (mounted) Navigator.pop(context, true);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = _message(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AlertDialog(
      title: Text('${t.services} — ${widget.employee.fullName}'),
      content: SizedBox(
        width: 420,
        height: 360,
        child: AsyncView<List<Service>>(
          load: widget.catalog.services,
          skeleton: const ListSkeleton(rows: 4, rowHeight: 48),
          builder: (context, services, _) => ListView(
            children: [
              for (final s in services)
                CheckboxListTile(
                  key: Key('assign_${s.id}'),
                  value: _selected.contains(s.id),
                  onChanged: (v) => setState(
                    () => v! ? _selected.add(s.id) : _selected.remove(s.id),
                  ),
                  title: Text(s.name),
                  subtitle: Text(
                    '${t.minutesShort(s.durationMinutes)} · ${s.price.toStringAsFixed(2)} ${t.currencyShort}',
                  ),
                ),
              if (_error != null)
                Text(
                  _error!,
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
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
          key: const Key('assign_save'),
          style: FilledButton.styleFrom(minimumSize: const Size(120, 44)),
          onPressed: _busy ? null : _save,
          child: Text(t.save),
        ),
      ],
    );
  }
}

/// Edits the whole week. A day that is switched off is a day off (it is simply omitted on save).
class WorkingHoursDialog extends StatefulWidget {
  const WorkingHoursDialog({
    super.key,
    required this.catalog,
    required this.employee,
  });

  final CatalogApi catalog;
  final Employee employee;

  @override
  State<WorkingHoursDialog> createState() => _WorkingHoursDialogState();
}

class _DayRow {
  _DayRow(this.dayOfWeek, WorkingHours? h)
    : enabled = h != null,
      start = TextEditingController(text: h?.start ?? '09:00'),
      end = TextEditingController(text: h?.end ?? '17:00'),
      breakStart = TextEditingController(text: h?.breakStart ?? ''),
      breakEnd = TextEditingController(text: h?.breakEnd ?? '');

  final int dayOfWeek;
  bool enabled;
  final TextEditingController start, end, breakStart, breakEnd;

  void dispose() {
    for (final c in [start, end, breakStart, breakEnd]) {
      c.dispose();
    }
  }

  WorkingHours toModel() => WorkingHours(
    dayOfWeek: dayOfWeek,
    start: start.text.trim(),
    end: end.text.trim(),
    breakStart: breakStart.text.trim().isEmpty ? null : breakStart.text.trim(),
    breakEnd: breakEnd.text.trim().isEmpty ? null : breakEnd.text.trim(),
  );
}

class _WorkingHoursDialogState extends State<WorkingHoursDialog> {
  // Index = .NET DayOfWeek (0 = Sunday).
  static List<String> _dayNames(AppLocalizations t) => [
    t.daySunday,
    t.dayMonday,
    t.dayTuesday,
    t.dayWednesday,
    t.dayThursday,
    t.dayFriday,
    t.daySaturday,
  ];
  // Monday first in the UI; values match .NET DayOfWeek (0 = Sunday).
  static const _order = [1, 2, 3, 4, 5, 6, 0];

  List<_DayRow>? _rows;
  Object? _loadError;
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    widget.catalog
        .workingHours(widget.employee.id)
        .then((hours) {
          if (!mounted) return;
          setState(() {
            _rows = [
              for (final d in _order)
                _DayRow(d, hours.where((h) => h.dayOfWeek == d).firstOrNull),
            ];
          });
        })
        .catchError((Object e) {
          if (mounted) setState(() => _loadError = e);
        });
  }

  @override
  void dispose() {
    for (final r in _rows ?? const <_DayRow>[]) {
      r.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.catalog.setWorkingHours(widget.employee.id, [
        for (final r in _rows!.where((r) => r.enabled)) r.toModel(),
      ]);
      if (mounted) Navigator.pop(context, true);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = _message(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Widget _time(TextEditingController c, String label, {bool enabled = true}) =>
      SizedBox(
        width: 84,
        child: TextField(
          controller: c,
          enabled: enabled,
          decoration: InputDecoration(
            labelText: label,
            isDense: true,
            hintText: 'HH:mm',
          ),
        ),
      );

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AlertDialog(
      title: Text('${t.workingHours} — ${widget.employee.fullName}'),
      content: SizedBox(
        width: 640,
        height: 440,
        child: _loadError != null
            ? ErrorView(
                message: _loadError is AppException
                    ? (_loadError as AppException).message
                    : t.genericError,
              )
            : _rows == null
            ? const Center(child: CircularProgressIndicator())
            : ListView(
                children: [
                  for (final r in _rows!)
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 4),
                      child: Row(
                        children: [
                          SizedBox(
                            width: 150,
                            child: Row(
                              children: [
                                Switch(
                                  key: Key('day_${r.dayOfWeek}'),
                                  value: r.enabled,
                                  onChanged: (v) =>
                                      setState(() => r.enabled = v),
                                ),
                                Expanded(
                                  child: Text(_dayNames(t)[r.dayOfWeek]),
                                ),
                              ],
                            ),
                          ),
                          if (!r.enabled)
                            Text(
                              t.dayOff,
                              style: TextStyle(
                                color: Theme.of(context)
                                    .colorScheme
                                    .onSurfaceVariant,
                              ),
                            )
                          else ...[
                            _time(r.start, t.from),
                            const SizedBox(width: 8),
                            _time(r.end, t.to),
                            const SizedBox(width: 16),
                            _time(
                              r.breakStart,
                              '${t.breakLabel} ${t.from.toLowerCase()}',
                            ),
                            const SizedBox(width: 8),
                            _time(
                              r.breakEnd,
                              '${t.breakLabel} ${t.to.toLowerCase()}',
                            ),
                          ],
                        ],
                      ),
                    ),
                  if (_error != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: Text(
                        _error!,
                        style: TextStyle(
                          color: Theme.of(context).colorScheme.error,
                        ),
                      ),
                    ),
                ],
              ),
      ),
      actions: [
        TextButton(
          onPressed: _busy ? null : () => Navigator.pop(context),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: const Key('hours_save'),
          style: FilledButton.styleFrom(minimumSize: const Size(120, 44)),
          onPressed: (_busy || _rows == null) ? null : _save,
          child: Text(t.save),
        ),
      ],
    );
  }
}
