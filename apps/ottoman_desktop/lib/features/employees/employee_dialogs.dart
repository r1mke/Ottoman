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
  late String _title = widget.employee?.title ?? '';
  late String? _gender = widget.employee?.gender;
  late String? _photoUrl = widget.employee?.photoUrl;
  late final _bio = TextEditingController(text: widget.employee?.bio);
  final _password = TextEditingController();
  late bool _active = widget.employee?.isActive ?? true;
  bool _busy = false;
  String? _error;

  bool get _isNew => widget.employee == null;

  void _takePhoto(Employee e) {
    if (!mounted) return;
    setState(() => _photoUrl = e.photoUrl);
  }

  @override
  void dispose() {
    for (final c in [_first, _last, _email, _phone, _bio, _password]) {
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
          title: _title.trim(),
          gender: _gender,
          bio: nullIfEmpty(_bio.text),
        );
      } else {
        await widget.catalog.updateEmployee(
          widget.employee!.id,
          firstName: _first.text.trim(),
          lastName: _last.text.trim(),
          phone: nullIfEmpty(_phone.text),
          title: _title.trim(),
          gender: _gender,
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
      title: DialogHeader(_isNew ? t.add : t.edit),
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
                  key: const Key('emp_phone'),
                  controller: _phone,
                  keyboardType: TextInputType.phone,
                  decoration: InputDecoration(labelText: t.phone),
                  validator: (v) => Validators.phone(t, v),
                ),
                const SizedBox(height: 12),
                _TitleField(
                  catalog: widget.catalog,
                  value: _title,
                  onChanged: (v) => setState(() => _title = v),
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<String>(
                  key: const Key('emp_gender'),
                  initialValue: _gender,
                  isExpanded: true,
                  decoration: InputDecoration(labelText: t.gender),
                  items: [
                    DropdownMenuItem(value: 'Male', child: Text(t.genderMale)),
                    DropdownMenuItem(
                      value: 'Female',
                      child: Text(t.genderFemale),
                    ),
                  ],
                  // Needed to pick the default avatar; an older barber without one can still be edited.
                  validator: (v) =>
                      (_isNew && v == null) ? t.requiredField : null,
                  onChanged: (v) => setState(() => _gender = v),
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
                    pictures: _pictures(_photoUrl),
                    onAdd: (img) async {
                      final e = await widget.catalog.setEmployeePhoto(
                        widget.employee!.id,
                        img.bytes,
                        img.name,
                      );
                      _takePhoto(e);
                      return _pictures(e.photoUrl);
                    },
                    onRemove: (_) async {
                      final e = await widget.catalog.clearEmployeePhoto(
                        widget.employee!.id,
                      );
                      _takePhoto(e);
                      return _pictures(e.photoUrl);
                    },
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
      title: DialogHeader('${t.services} — ${widget.employee.fullName}'),
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
      title: DialogHeader('${t.workingHours} — ${widget.employee.fullName}'),
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

/// The title of a barber: a drop-down over the shared list. The last entry opens the list editor, so a new title can
/// be added (or an unused one removed) without leaving the form. A title the barber already has stays selectable
/// even if it is no longer on the list.
class _TitleField extends StatefulWidget {
  const _TitleField({
    required this.catalog,
    required this.value,
    required this.onChanged,
  });

  final CatalogApi catalog;
  final String value;
  final ValueChanged<String> onChanged;

  @override
  State<_TitleField> createState() => _TitleFieldState();
}

class _TitleFieldState extends State<_TitleField> {
  static const _manage = '\u0000manage';

  late Future<List<EmployeeTitle>> _titles = widget.catalog.employeeTitles();

  Future<void> _openManager() async {
    final added = await showDialog<String>(
      context: context,
      builder: (_) => TitleListDialog(catalog: widget.catalog),
    );
    if (!mounted) return;
    final fresh = widget.catalog.employeeTitles();
    setState(() {
      _titles = fresh;
    });
    if (added != null) widget.onChanged(added);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return FutureBuilder<List<EmployeeTitle>>(
      future: _titles,
      builder: (context, snap) {
        final names = <String>[
          for (final x in snap.data ?? const <EmployeeTitle>[]) x.name,
        ];
        final current = widget.value.trim();
        if (current.isNotEmpty &&
            !names.any((n) => n.toLowerCase() == current.toLowerCase())) {
          names.insert(0, current);
        }
        final selected = names.cast<String?>().firstWhere(
          (n) => n!.toLowerCase() == current.toLowerCase(),
          orElse: () => null,
        );
        // A new key per value rebuilds the field: initialValue is only read when the field is created.
        return KeyedSubtree(
          key: ValueKey(selected),
          child: DropdownButtonFormField<String>(
            key: const Key('emp_title'),
            initialValue: selected,
            isExpanded: true,
            decoration: InputDecoration(labelText: t.title),
            items: [
              for (final n in names) DropdownMenuItem(value: n, child: Text(n)),
              DropdownMenuItem(
                value: _manage,
                child: Row(
                  children: [
                    const Icon(Icons.edit_outlined, size: 18),
                    const SizedBox(width: 8),
                    Text(
                      t.manageTitles,
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                  ],
                ),
              ),
            ],
            validator: (v) => (v == null || v.isEmpty) ? t.requiredField : null,
            onChanged: (v) {
              if (v == _manage) {
                _openManager();
              } else if (v != null) {
                widget.onChanged(v);
              }
            },
          ),
        );
      },
    );
  }
}

/// Add and remove titles of the shared list. Pops with the name that was added last, so the form can select it.
class TitleListDialog extends StatefulWidget {
  const TitleListDialog({super.key, required this.catalog});

  final CatalogApi catalog;

  @override
  State<TitleListDialog> createState() => _TitleListDialogState();
}

class _TitleListDialogState extends State<TitleListDialog> {
  final _name = TextEditingController();
  List<EmployeeTitle>? _titles;
  String? _lastAdded;
  String? _error;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _reload();
  }

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  Future<void> _reload() async {
    try {
      final titles = await widget.catalog.employeeTitles();
      if (mounted) setState(() => _titles = titles);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  Future<void> _run(Future<void> Function() action) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await action();
      await _reload();
    } on AppException catch (e) {
      final detail = e.errors.values.expand((x) => x).join('\n');
      if (mounted) setState(() => _error = detail.isEmpty ? e.message : detail);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _add() async {
    final name = _name.text.trim();
    if (name.isEmpty) return;
    await _run(() async {
      final created = await widget.catalog.addEmployeeTitle(name);
      _lastAdded = created.name;
      _name.clear();
    });
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final titles = _titles;
    return AlertDialog(
      title: DialogHeader(t.manageTitles),
      content: SizedBox(
        width: 440,
        height: 420,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: TextField(
                    key: const Key('title_new'),
                    controller: _name,
                    maxLength: 100,
                    buildCounter: (
                      _, {
                      required currentLength,
                      required isFocused,
                      maxLength,
                    }) => null,
                    decoration: InputDecoration(labelText: t.newTitle),
                    onSubmitted: (_) => _busy ? null : _add(),
                  ),
                ),
                const SizedBox(width: 8),
                FilledButton.icon(
                  key: const Key('title_add'),
                  onPressed: _busy ? null : _add,
                  icon: const Icon(Icons.add),
                  label: Text(t.add),
                ),
              ],
            ),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Text(
                  _error!,
                  key: const Key('title_error'),
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ),
            const SizedBox(height: 8),
            Expanded(
              child: titles == null
                  ? const Center(child: CircularProgressIndicator())
                  : ListView(
                      children: [
                        for (final x in titles)
                          ListTile(
                            key: Key('title_${x.id}'),
                            dense: true,
                            title: Text(x.name),
                            subtitle: x.inUse > 0
                                ? Text(t.titleUsedBy(x.inUse))
                                : null,
                            trailing: IconButton(
                              key: Key('title_delete_${x.id}'),
                              tooltip: x.inUse > 0
                                  ? t.titleInUseHint
                                  : t.delete,
                              icon: const Icon(Icons.delete_outline),
                              onPressed: _busy || x.inUse > 0
                                  ? null
                                  : () => _run(
                                      () => widget.catalog.deleteEmployeeTitle(
                                        x.id,
                                      ),
                                    ),
                            ),
                          ),
                      ],
                    ),
            ),
          ],
        ),
      ),
      actions: [
        FilledButton(
          key: const Key('titles_done'),
          onPressed: () => Navigator.pop(context, _lastAdded),
          child: Text(t.close),
        ),
      ],
    );
  }
}
