import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'package:latlong2/latlong.dart';

import '../images/picture_editor.dart';
import 'location_picker.dart';

String _hhmm(int minutes) =>
    '${(minutes ~/ 60).toString().padLeft(2, '0')}:${(minutes % 60).toString().padLeft(2, '0')}';

/// Admin: what the mobile home screen says about the salon: logo, name, description, address, map link and
/// the opening hours.
class SalonPage extends StatefulWidget {
  const SalonPage({
    super.key,
    required this.salon,
    this.chooser = const FileImageChooser(),
    this.search = searchOpenStreetMap,
  });

  final SalonApi salon;
  final ImageChooser chooser;
  final AddressSearch search;

  @override
  State<SalonPage> createState() => _SalonPageState();
}

class _SalonPageState extends State<SalonPage> {
  @override
  Widget build(BuildContext context) => AsyncView<SalonProfile>(
    load: widget.salon.profile,
    skeleton: const ListSkeleton(),
    builder: (context, profile, _) => SalonForm(
      salon: widget.salon,
      profile: profile,
      chooser: widget.chooser,
      search: widget.search,
    ),
  );
}

class SalonForm extends StatefulWidget {
  const SalonForm({
    super.key,
    required this.salon,
    required this.profile,
    this.chooser = const FileImageChooser(),
    this.search = searchOpenStreetMap,
  });

  final SalonApi salon;
  final SalonProfile profile;
  final ImageChooser chooser;
  final AddressSearch search;

  @override
  State<SalonForm> createState() => _SalonFormState();
}

class _SalonFormState extends State<SalonForm> {
  final _form = GlobalKey<FormState>();
  late final _name = TextEditingController(text: widget.profile.name);
  late final _description = TextEditingController(
    text: widget.profile.description,
  );
  late final _address = TextEditingController(text: widget.profile.address);
  late final _phone = TextEditingController(text: widget.profile.phone);
  late final _maps = TextEditingController(text: widget.profile.mapsUrl);
  late final _instagram = TextEditingController(
    text: widget.profile.instagramUrl,
  );
  late final _facebook = TextEditingController(
    text: widget.profile.facebookUrl,
  );
  late LatLng? _pin =
      widget.profile.latitude == null || widget.profile.longitude == null
      ? null
      : LatLng(widget.profile.latitude!, widget.profile.longitude!);

  /// The map link as the server last stored it, and whether the admin placed the pin by hand since.
  /// A changed link with an untouched pin lets the server read the new place out of the link on save.
  late String? _savedMaps = _blankText(widget.profile.mapsUrl);
  bool _pinTouched = false;
  late final List<OpeningHours> _hours = [...widget.profile.hours];
  late String? _logoUrl = widget.profile.logoUrl;
  bool _busy = false;
  String? _hoursError;

  @override
  void dispose() {
    for (final c in [
      _name,
      _description,
      _address,
      _phone,
      _maps,
      _instagram,
      _facebook,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  static String? _blankText(String? s) =>
      s == null || s.trim().isEmpty ? null : s.trim();

  List<EditablePicture> _logo() => [
    if (_logoUrl != null) EditablePicture('logo', _logoUrl!),
  ];

  String _dayName(AppLocalizations t, int day) => switch (day) {
    1 => t.dayMonday,
    2 => t.dayTuesday,
    3 => t.dayWednesday,
    4 => t.dayThursday,
    5 => t.dayFriday,
    6 => t.daySaturday,
    _ => t.daySunday,
  };

  Future<void> _pickTime(int index, {required bool open}) async {
    final h = _hours[index];
    final current =
        (open ? h.openMinutes : h.closeMinutes) ?? (open ? 540 : 1080);
    final picked = await showTimePicker(
      context: context,
      initialTime: TimeOfDay(hour: current ~/ 60, minute: current % 60),
      builder: (context, child) => MediaQuery(
        data: MediaQuery.of(context).copyWith(alwaysUse24HourFormat: true),
        child: child!,
      ),
    );
    if (picked == null) return;
    final minutes = picked.hour * 60 + picked.minute;
    setState(() {
      _hoursError = null;
      _hours[index] = open
          ? h.copyWith(
              openMinutes: minutes,
              closeMinutes: h.closeMinutes ?? 1080,
            )
          : h.copyWith(
              closeMinutes: minutes,
              openMinutes: h.openMinutes ?? 540,
            );
    });
  }

  Future<void> _save() async {
    final t = AppLocalizations.of(context);
    final fieldsOk = _form.currentState!.validate();
    final orderOk = _hours.every(
      (h) => h.isClosed || h.openMinutes! < h.closeMinutes!,
    );
    setState(() => _hoursError = orderOk ? null : t.hoursOrder);
    if (!fieldsOk || !orderOk) return;
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _busy = true);
    try {
      String? blank(TextEditingController c) => _blankText(c.text);
      final linkChanged = blank(_maps) != _savedMaps;
      // A new link with a pin the admin did not move: send no pin, so the server reads it from the link.
      final pin = linkChanged && !_pinTouched ? null : _pin;
      final saved = await widget.salon.save(
        name: _name.text.trim(),
        description: blank(_description),
        address: blank(_address),
        phone: blank(_phone),
        mapsUrl: blank(_maps),
        instagramUrl: blank(_instagram),
        facebookUrl: blank(_facebook),
        latitude: pin?.latitude,
        longitude: pin?.longitude,
        hours: _hours,
      );
      if (!mounted) return;
      // The server may have read the pin out of the pasted link: show what it found.
      setState(() {
        _pin = saved.latitude == null || saved.longitude == null
            ? null
            : LatLng(saved.latitude!, saved.longitude!);
        _savedMaps = _blankText(saved.mapsUrl);
        _pinTouched = false;
      });
      messenger.showSnackBar(SnackBar(content: Text(t.salonSaved)));
    } on AppException catch (e) {
      if (mounted) messenger.showSnackBar(SnackBar(content: Text(e.detail)));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(36, 0, 36, 24),
      child: Align(
        alignment: Alignment.topLeft,
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 560),
          child: Card(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  PictureEditor(
                    key: ValueKey('logo_$_logoUrl'),
                    chooser: widget.chooser,
                    fallbackIcon: Icons.storefront_outlined,
                    pictures: _logo(),
                    onAdd: (img) async {
                      final p = await widget.salon.setLogo(img.bytes, img.name);
                      if (mounted) setState(() => _logoUrl = p.logoUrl);
                      return _logo();
                    },
                    onRemove: (_) async {
                      final p = await widget.salon.clearLogo();
                      if (mounted) setState(() => _logoUrl = p.logoUrl);
                      return _logo();
                    },
                  ),
                  const SizedBox(height: 20),
                  Form(
                    key: _form,
                    child: Column(
                      children: [
                        TextFormField(
                          key: const Key('salon_name'),
                          controller: _name,
                          decoration: InputDecoration(labelText: t.salonName),
                          validator: (v) => Validators.required(t, v),
                        ),
                        const SizedBox(height: 16),
                        TextFormField(
                          key: const Key('salon_description'),
                          controller: _description,
                          maxLines: 4,
                          maxLength: 1000,
                          decoration: InputDecoration(
                            labelText: t.description,
                            alignLabelWithHint: true,
                          ),
                        ),
                        const SizedBox(height: 8),
                        TextFormField(
                          key: const Key('salon_address'),
                          controller: _address,
                          decoration: InputDecoration(labelText: t.address),
                        ),
                        const SizedBox(height: 16),
                        TextFormField(
                          key: const Key('salon_phone'),
                          controller: _phone,
                          keyboardType: TextInputType.phone,
                          decoration: InputDecoration(labelText: t.phone),
                          validator: (v) => Validators.phone(t, v),
                        ),
                        const SizedBox(height: 16),
                        TextFormField(
                          key: const Key('salon_maps'),
                          controller: _maps,
                          keyboardType: TextInputType.url,
                          decoration: InputDecoration(
                            labelText: t.mapsLink,
                            helperText: t.mapsLinkHint,
                          ),
                          validator: (v) => Validators.url(t, v),
                        ),
                        const SizedBox(height: 16),
                        TextFormField(
                          key: const Key('salon_instagram'),
                          controller: _instagram,
                          keyboardType: TextInputType.url,
                          decoration: InputDecoration(
                            labelText: t.instagramLink,
                          ),
                          validator: (v) => Validators.url(t, v),
                        ),
                        const SizedBox(height: 16),
                        TextFormField(
                          key: const Key('salon_facebook'),
                          controller: _facebook,
                          keyboardType: TextInputType.url,
                          decoration: InputDecoration(
                            labelText: t.facebookLink,
                          ),
                          validator: (v) => Validators.url(t, v),
                        ),
                        const SizedBox(height: 16),
                        LocationPicker(
                          value: _pin,
                          address: _address.text,
                          search: widget.search,
                          onChanged: (p) => setState(() {
                            _pin = p;
                            _pinTouched = true;
                          }),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 24),
                  Text(
                    t.workingHours,
                    style: const TextStyle(
                      fontWeight: FontWeight.w800,
                      fontSize: 14,
                    ),
                  ),
                  const SizedBox(height: 8),
                  for (var i = 0; i < _hours.length; i++)
                    _HoursRow(
                      key: Key('hours_${_hours[i].day}'),
                      label: _dayName(t, _hours[i].day),
                      hours: _hours[i],
                      onToggle: (open) => setState(() {
                        _hoursError = null;
                        _hours[i] = open
                            ? _hours[i].copyWith(
                                openMinutes: 540,
                                closeMinutes: 1080,
                              )
                            : _hours[i].copyWith(closed: true);
                      }),
                      onPickOpen: () => _pickTime(i, open: true),
                      onPickClose: () => _pickTime(i, open: false),
                    ),
                  if (_hoursError != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 6),
                      child: Text(
                        _hoursError!,
                        key: const Key('hours_error'),
                        style: TextStyle(
                          color: Theme.of(context).colorScheme.error,
                        ),
                      ),
                    ),
                  const SizedBox(height: 24),
                  FilledButton(
                    key: const Key('salon_save'),
                    style: FilledButton.styleFrom(
                      minimumSize: const Size.fromHeight(48),
                    ),
                    onPressed: _busy ? null : _save,
                    child: Text(t.saveChanges),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _HoursRow extends StatelessWidget {
  const _HoursRow({
    super.key,
    required this.label,
    required this.hours,
    required this.onToggle,
    required this.onPickOpen,
    required this.onPickClose,
  });

  final String label;
  final OpeningHours hours;
  final ValueChanged<bool> onToggle;
  final VoidCallback onPickOpen;
  final VoidCallback onPickClose;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        children: [
          SizedBox(width: 120, child: Text(label)),
          Switch(
            key: Key('hours_open_${hours.day}'),
            value: !hours.isClosed,
            onChanged: onToggle,
          ),
          const SizedBox(width: 8),
          if (hours.isClosed)
            Text(
              t.closedDay,
              style: const TextStyle(color: WorkspaceColors.muted),
            )
          else ...[
            OutlinedButton(
              key: Key('hours_from_${hours.day}'),
              style: OutlinedButton.styleFrom(minimumSize: const Size(84, 38)),
              onPressed: onPickOpen,
              child: Text(_hhmm(hours.openMinutes!)),
            ),
            const Padding(
              padding: EdgeInsets.symmetric(horizontal: 8),
              child: Text('–'),
            ),
            OutlinedButton(
              key: Key('hours_to_${hours.day}'),
              style: OutlinedButton.styleFrom(minimumSize: const Size(84, 38)),
              onPressed: onPickClose,
              child: Text(_hhmm(hours.closeMinutes!)),
            ),
          ],
        ],
      ),
    );
  }
}
