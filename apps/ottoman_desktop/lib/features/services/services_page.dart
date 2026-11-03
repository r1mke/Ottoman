import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import '../images/picture_editor.dart';

/// Admin: services and prices. Deactivation is a soft delete (past bookings keep their snapshot).
class ServicesPage extends StatefulWidget {
  const ServicesPage({super.key, required this.catalog});

  final CatalogApi catalog;

  @override
  State<ServicesPage> createState() => _ServicesPageState();
}

class _ServicesPageState extends State<ServicesPage> {
  int _version = 0;

  void _refresh() => setState(() => _version++);

  Future<void> _edit([Service? service]) async {
    await showDialog<bool>(
      context: context,
      builder: (_) => ServiceDialog(catalog: widget.catalog, service: service),
    );
    // Pictures change on the server straight away, so the list is refreshed even when the dialog was cancelled.
    if (mounted) _refresh();
  }

  Future<void> _toggle(Service s) async {
    try {
      if (s.isActive) {
        await widget.catalog.deactivateService(s.id);
      } else {
        await widget.catalog.saveService(
          id: s.id,
          name: s.name,
          description: s.description,
          price: s.price,
          durationMinutes: s.durationMinutes,
          isActive: true,
        );
      }
      _refresh();
    } on AppException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Padding(
      padding: const EdgeInsets.fromLTRB(32, 0, 32, 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Align(
            alignment: Alignment.centerRight,
            child: FilledButton.icon(
              key: const Key('add_service'),
              style: FilledButton.styleFrom(minimumSize: const Size(140, 44)),
              onPressed: _edit,
              icon: const Icon(Icons.add),
              label: Text(t.add),
            ),
          ),
          const SizedBox(height: 16),
          Expanded(
            child: AsyncView<List<Service>>(
              key: ValueKey(_version),
              load: () => widget.catalog.services(includeInactive: true),
              skeleton: const ListSkeleton(),
              isEmpty: (s) => s.isEmpty,
              builder: (context, services, _) => ListView.separated(
                itemCount: services.length,
                separatorBuilder: (_, _) => const SizedBox(height: 8),
                itemBuilder: (context, i) {
                  final s = services[i];
                  return Card(
                    child: ListTile(
                      key: Key('service_row_${s.id}'),
                      title: Text(
                        s.name,
                        style: const TextStyle(fontWeight: FontWeight.w700),
                      ),
                      subtitle: Text(
                        '${t.minutesShort(s.durationMinutes)} · ${s.price.toStringAsFixed(2)} ${t.currencyShort}',
                      ),
                      leading: ClipRRect(
                        borderRadius: BorderRadius.circular(8),
                        child: NetworkPicture(
                          key: Key('service_picture_${s.id}'),
                          url: s.imageUrl,
                          width: 48,
                          height: 48,
                          fallbackIcon: Icons.content_cut,
                        ),
                      ),
                      trailing: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Chip(label: Text(s.isActive ? t.active : t.inactive)),
                          const SizedBox(width: 8),
                          IconButton(
                            tooltip: t.edit,
                            icon: const Icon(Icons.edit_outlined),
                            onPressed: () => _edit(s),
                          ),
                          IconButton(
                            tooltip: s.isActive ? t.deactivate : t.active,
                            icon: Icon(
                              s.isActive
                                  ? Icons.visibility_off_outlined
                                  : Icons.visibility_outlined,
                            ),
                            onPressed: () => _toggle(s),
                          ),
                        ],
                      ),
                    ),
                  );
                },
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class ServiceDialog extends StatefulWidget {
  const ServiceDialog({
    super.key,
    required this.catalog,
    this.service,
    this.chooser = const FileImageChooser(),
  });

  final CatalogApi catalog;
  final Service? service;
  final ImageChooser chooser;

  @override
  State<ServiceDialog> createState() => _ServiceDialogState();
}

class _ServiceDialogState extends State<ServiceDialog> {
  static List<EditablePicture> _pictures(String? url) => [
    if (url != null) EditablePicture('service', url),
  ];

  final _form = GlobalKey<FormState>();
  late final _name = TextEditingController(text: widget.service?.name);
  late final _description = TextEditingController(
    text: widget.service?.description,
  );
  late final _price = TextEditingController(
    text: widget.service?.price.toStringAsFixed(2),
  );
  late final _duration = TextEditingController(
    text: widget.service?.durationMinutes.toString(),
  );
  late bool _active = widget.service?.isActive ?? true;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    for (final c in [_name, _description, _price, _duration]) {
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
    try {
      await widget.catalog.saveService(
        id: widget.service?.id,
        name: _name.text.trim(),
        description: _description.text.trim().isEmpty
            ? null
            : _description.text.trim(),
        price: double.parse(_price.text.replaceAll(',', '.')),
        durationMinutes: int.parse(_duration.text),
        isActive: _active,
      );
      if (mounted) Navigator.pop(context, true);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = e.detail);
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
      title: DialogHeader(widget.service == null ? t.add : t.edit),
      content: SizedBox(
        width: 420,
        child: Form(
          key: _form,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextFormField(
                  key: const Key('svc_name'),
                  controller: _name,
                  decoration: InputDecoration(labelText: t.name),
                  validator: required,
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _description,
                  decoration: InputDecoration(labelText: t.description),
                  maxLines: 2,
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: TextFormField(
                        key: const Key('svc_price'),
                        controller: _price,
                        keyboardType: const TextInputType.numberWithOptions(
                          decimal: true,
                        ),
                        decoration: InputDecoration(
                          labelText: '${t.price} (${t.currencyShort})',
                        ),
                        validator: (v) {
                          final n = double.tryParse(
                            (v ?? '').replaceAll(',', '.'),
                          );
                          return (n == null || n <= 0) ? t.invalidNumber : null;
                        },
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: TextFormField(
                        key: const Key('svc_duration'),
                        controller: _duration,
                        keyboardType: TextInputType.number,
                        decoration: InputDecoration(labelText: t.duration),
                        validator: (v) {
                          final n = int.tryParse(v ?? '');
                          if (n == null || n <= 0) return t.invalidNumber;
                          return n % 5 == 0 ? null : t.durationStepError;
                        },
                      ),
                    ),
                  ],
                ),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(t.active),
                  value: _active,
                  onChanged: (v) => setState(() => _active = v),
                ),
                const SizedBox(height: 8),
                if (widget.service == null)
                  Align(
                    alignment: Alignment.centerLeft,
                    child: Text(
                      t.savePictureFirst,
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  )
                else
                  PictureEditor(
                    key: const Key('service_pictures'),
                    chooser: widget.chooser,
                    fallbackIcon: Icons.content_cut,
                    pictures: _pictures(widget.service!.imageUrl),
                    onAdd: (img) async => _pictures(
                      (await widget.catalog.setServiceImage(
                        widget.service!.id,
                        img.bytes,
                        img.name,
                      )).imageUrl,
                    ),
                    onRemove: (_) async => _pictures(
                      (await widget.catalog.clearServiceImage(
                        widget.service!.id,
                      )).imageUrl,
                    ),
                  ),
                if (_error != null)
                  Align(
                    alignment: Alignment.centerLeft,
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
        ),
      ),
      actions: [
        TextButton(
          onPressed: _busy ? null : () => Navigator.pop(context),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: const Key('svc_save'),
          style: FilledButton.styleFrom(minimumSize: const Size(120, 44)),
          onPressed: _busy ? null : _save,
          child: Text(t.save),
        ),
      ],
    );
  }
}
