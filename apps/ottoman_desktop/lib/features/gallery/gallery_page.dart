import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import '../images/picture_editor.dart';

String _detail(AppException e) {
  final detail = e.errors.values.expand((x) => x).join('\n');
  return detail.isEmpty ? e.message : detail;
}

/// Admin: the work gallery. Pictures of finished haircuts with their style and the barber who did them; the ones
/// marked "on home" make up "Our work" in the mobile app.
class GalleryPage extends StatefulWidget {
  const GalleryPage({
    super.key,
    required this.gallery,
    required this.catalog,
    this.chooser = const FileImageChooser(),
  });

  final GalleryApi gallery;
  final CatalogApi catalog;
  final ImageChooser chooser;

  @override
  State<GalleryPage> createState() => _GalleryPageState();
}

class _GalleryPageState extends State<GalleryPage> {
  int _version = 0;
  int? _categoryId;

  void _refresh() => setState(() => _version++);

  Future<void> _edit([GalleryItem? item]) async {
    final saved = await showDialog<bool>(
      context: context,
      builder: (_) => WorkDialog(
        gallery: widget.gallery,
        catalog: widget.catalog,
        chooser: widget.chooser,
        item: item,
        initialCategoryId: _categoryId,
      ),
    );
    if (saved == true && mounted) _refresh();
  }

  Future<void> _manageStyles() async {
    await showDialog<void>(
      context: context,
      builder: (_) => StylesDialog(gallery: widget.gallery),
    );
    if (!mounted) return;
    // A deleted style that was being filtered must not leave an empty filter behind.
    setState(() {
      _categoryId = null;
      _version++;
    });
  }

  Future<void> _delete(GalleryItem item) async {
    final t = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: DialogHeader(t.delete),
        content: Text(t.deleteWorkQuestion),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: Text(t.cancel),
          ),
          FilledButton(
            key: const Key('confirm_delete_work'),
            onPressed: () => Navigator.pop(context, true),
            child: Text(t.delete),
          ),
        ],
      ),
    );
    if (ok != true) return;
    try {
      await widget.gallery.delete(item.id);
      if (mounted) _refresh();
    } on AppException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(_detail(e))));
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Padding(
      padding: const EdgeInsets.fromLTRB(32, 0, 32, 24),
      child:
          AsyncView<
            ({List<HaircutCategory> categories, Paged<GalleryItem> items})
          >(
            key: ValueKey('gallery_${_version}_$_categoryId'),
            load: () async {
              final categories = await widget.gallery.categories();
              final items = await widget.gallery.items(
                categoryId: _categoryId,
                pageSize: 100,
              );
              return (categories: categories, items: items);
            },
            skeleton: const ListSkeleton(),
            builder: (context, data, _) => Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: SingleChildScrollView(
                        scrollDirection: Axis.horizontal,
                        child: Row(
                          children: [
                            ChoiceChip(
                              key: const Key('style_all'),
                              label: Text(t.allStyles),
                              selected: _categoryId == null,
                              onSelected: (_) =>
                                  setState(() => _categoryId = null),
                            ),
                            for (final c in data.categories) ...[
                              const SizedBox(width: 8),
                              ChoiceChip(
                                key: Key('style_${c.id}'),
                                label: Text('${c.name} (${c.count})'),
                                selected: _categoryId == c.id,
                                onSelected: (_) =>
                                    setState(() => _categoryId = c.id),
                              ),
                            ],
                          ],
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                    OutlinedButton.icon(
                      key: const Key('manage_styles'),
                      style: OutlinedButton.styleFrom(
                        minimumSize: const Size(120, 44),
                      ),
                      onPressed: _manageStyles,
                      icon: const Icon(Icons.style_outlined),
                      label: Text(t.styles),
                    ),
                    const SizedBox(width: 12),
                    FilledButton.icon(
                      key: const Key('add_work'),
                      style: FilledButton.styleFrom(
                        minimumSize: const Size(140, 44),
                      ),
                      onPressed: _edit,
                      icon: const Icon(Icons.add_photo_alternate_outlined),
                      label: Text(t.addWork),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                Expanded(
                  child: data.items.items.isEmpty
                      ? EmptyState(
                          icon: Icons.photo_library_outlined,
                          message: t.galleryEmpty,
                        )
                      : GridView.builder(
                          gridDelegate:
                              const SliverGridDelegateWithMaxCrossAxisExtent(
                                maxCrossAxisExtent: 240,
                                mainAxisSpacing: 14,
                                crossAxisSpacing: 14,
                                childAspectRatio: 0.78,
                              ),
                          itemCount: data.items.items.length,
                          itemBuilder: (context, i) => _WorkTile(
                            item: data.items.items[i],
                            onEdit: () => _edit(data.items.items[i]),
                            onDelete: () => _delete(data.items.items[i]),
                          ),
                        ),
                ),
              ],
            ),
          ),
    );
  }
}

class _WorkTile extends StatelessWidget {
  const _WorkTile({
    required this.item,
    required this.onEdit,
    required this.onDelete,
  });

  final GalleryItem item;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Card(
      key: Key('work_${item.id}'),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onEdit,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Stack(
                fit: StackFit.expand,
                children: [
                  NetworkPicture(url: item.imageUrl),
                  if (item.isFeatured)
                    Positioned(
                      left: 8,
                      top: 8,
                      child: Container(
                        key: Key('work_home_${item.id}'),
                        padding: const EdgeInsets.symmetric(
                          horizontal: 8,
                          vertical: 3,
                        ),
                        decoration: BoxDecoration(
                          color: WorkspaceColors.amber,
                          borderRadius: BorderRadius.circular(99),
                        ),
                        child: Text(
                          t.onHomeBadge,
                          style: const TextStyle(
                            color: WorkspaceColors.ink,
                            fontSize: 11,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ),
                    ),
                  Positioned(
                    top: 6,
                    right: 6,
                    child: Material(
                      color: Colors.black.withValues(alpha: 0.6),
                      shape: const CircleBorder(),
                      child: InkWell(
                        key: Key('delete_work_${item.id}'),
                        customBorder: const CircleBorder(),
                        onTap: onDelete,
                        child: const Padding(
                          padding: EdgeInsets.all(5),
                          child: Icon(
                            Icons.close,
                            size: 15,
                            color: Colors.white,
                          ),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(10),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    item.categoryName,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontWeight: FontWeight.w800),
                  ),
                  if (item.employeeName != null)
                    Text(
                      '${t.doneBy}: ${item.employeeName}',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: WorkspaceColors.muted,
                        fontSize: 12,
                      ),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Adds a work (choose the picture, style, barber, caption) or edits one that is already there.
class WorkDialog extends StatefulWidget {
  const WorkDialog({
    super.key,
    required this.gallery,
    required this.catalog,
    this.item,
    this.initialCategoryId,
    this.chooser = const FileImageChooser(),
  });

  final GalleryApi gallery;
  final CatalogApi catalog;
  final GalleryItem? item;
  final int? initialCategoryId;
  final ImageChooser chooser;

  @override
  State<WorkDialog> createState() => _WorkDialogState();
}

class _WorkDialogState extends State<WorkDialog> {
  final _form = GlobalKey<FormState>();
  late final _caption = TextEditingController(text: widget.item?.caption);
  late int? _categoryId = widget.item?.categoryId ?? widget.initialCategoryId;
  late String? _employeeId = widget.item?.employeeId;
  late bool _featured = widget.item?.isFeatured ?? false;
  ChosenImage? _image;
  bool _busy = false;
  bool _pictureMissing = false;

  late final Future<(List<HaircutCategory>, List<Employee>)> _lookups =
      () async {
        final categories = await widget.gallery.categories();
        final employees = await widget.catalog.employees();
        return (categories, employees);
      }();

  bool get _isNew => widget.item == null;

  @override
  void dispose() {
    _caption.dispose();
    super.dispose();
  }

  Future<void> _choose() async {
    final image = await widget.chooser.choose();
    if (image != null && mounted) {
      setState(() {
        _image = image;
        _pictureMissing = false;
      });
    }
  }

  Future<void> _save() async {
    final valid = _form.currentState!.validate();
    if (_isNew && _image == null) {
      setState(() => _pictureMissing = true);
      return;
    }
    if (!valid) return;
    final messenger = ScaffoldMessenger.of(context);
    final navigator = Navigator.of(context);
    final t = AppLocalizations.of(context);
    setState(() => _busy = true);
    try {
      if (_isNew) {
        await widget.gallery.add(
          bytes: _image!.bytes,
          filename: _image!.name,
          categoryId: _categoryId!,
          employeeId: _employeeId,
          caption: _caption.text,
          featured: _featured,
        );
        messenger.showSnackBar(SnackBar(content: Text(t.workAdded)));
      } else {
        await widget.gallery.update(
          widget.item!.id,
          categoryId: _categoryId!,
          employeeId: _employeeId,
          caption: _caption.text.trim().isEmpty ? null : _caption.text.trim(),
          featured: _featured,
        );
      }
      navigator.pop(true);
    } on AppException catch (e) {
      // The refusal (wrong type, too big, the same picture again) is a toast with what is accepted.
      messenger.showSnackBar(
        SnackBar(
          content: Text(
            _isNew ? '${_detail(e)}\n${t.pictureHint}' : _detail(e),
          ),
        ),
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AlertDialog(
      title: DialogHeader(_isNew ? t.addWork : t.editWork),
      content: SizedBox(
        width: 460,
        child: FutureBuilder(
          future: _lookups,
          builder: (context, snapshot) {
            if (snapshot.hasError) {
              return Text(
                snapshot.error is AppException
                    ? _detail(snapshot.error! as AppException)
                    : '${snapshot.error}',
              );
            }
            if (!snapshot.hasData) {
              return const SizedBox(
                height: 120,
                child: Center(child: CircularProgressIndicator()),
              );
            }
            final (categories, employees) = snapshot.data!;
            return Form(
              key: _form,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Center(
                      child: _isNew
                          ? _NewPicture(
                              image: _image,
                              missing: _pictureMissing,
                              onTap: _busy ? null : _choose,
                            )
                          : SizedBox(
                              width: 148,
                              height: 148,
                              child: ClipRRect(
                                borderRadius: BorderRadius.circular(14),
                                child: NetworkPicture(
                                  url: widget.item!.imageUrl,
                                ),
                              ),
                            ),
                    ),
                    const SizedBox(height: 16),
                    DropdownButtonFormField<int>(
                      key: const Key('work_style'),
                      initialValue: categories.any((c) => c.id == _categoryId)
                          ? _categoryId
                          : null,
                      isExpanded: true,
                      decoration: InputDecoration(labelText: t.haircutStyle),
                      items: [
                        for (final c in categories)
                          DropdownMenuItem(value: c.id, child: Text(c.name)),
                      ],
                      validator: (v) => v == null ? t.chooseStyle : null,
                      onChanged: (v) => setState(() => _categoryId = v),
                    ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<String?>(
                      key: const Key('work_barber'),
                      initialValue: employees.any((e) => e.id == _employeeId)
                          ? _employeeId
                          : null,
                      isExpanded: true,
                      decoration: InputDecoration(labelText: t.employee),
                      items: [
                        DropdownMenuItem<String?>(
                          value: null,
                          child: Text(t.noBarber),
                        ),
                        for (final e in employees)
                          DropdownMenuItem<String?>(
                            value: e.id,
                            child: Text('${e.firstName} ${e.lastName}'),
                          ),
                      ],
                      onChanged: (v) => setState(() => _employeeId = v),
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      key: const Key('work_caption'),
                      controller: _caption,
                      maxLength: 200,
                      decoration: InputDecoration(labelText: t.captionOptional),
                    ),
                    SwitchListTile(
                      key: const Key('work_featured'),
                      contentPadding: EdgeInsets.zero,
                      title: Text(t.showOnHome),
                      value: _featured,
                      onChanged: (v) => setState(() => _featured = v),
                    ),
                  ],
                ),
              ),
            );
          },
        ),
      ),
      actions: [
        TextButton(
          onPressed: _busy ? null : () => Navigator.pop(context, false),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: const Key('work_save'),
          style: FilledButton.styleFrom(minimumSize: const Size(120, 44)),
          onPressed: _busy ? null : _save,
          child: Text(t.save),
        ),
      ],
    );
  }
}

/// The picture chosen for a new work, or the dashed tile that opens the file dialog.
class _NewPicture extends StatelessWidget {
  const _NewPicture({
    required this.image,
    required this.missing,
    required this.onTap,
  });

  final ChosenImage? image;
  final bool missing;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final colors = Theme.of(context).colorScheme;
    return Column(
      children: [
        InkWell(
          key: const Key('work_choose_picture'),
          borderRadius: BorderRadius.circular(14),
          onTap: onTap,
          child: Container(
            width: 148,
            height: 148,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(14),
              border: Border.all(
                color: missing ? colors.error : WorkspaceColors.line,
                width: missing ? 2 : 1,
              ),
            ),
            child: image == null
                ? Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(
                        Icons.add_photo_alternate_outlined,
                        size: 30,
                        color: missing ? colors.error : WorkspaceColors.muted,
                      ),
                      const SizedBox(height: 8),
                      Text(
                        t.choosePicture,
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w700,
                          color: missing ? colors.error : WorkspaceColors.muted,
                        ),
                      ),
                    ],
                  )
                : ClipRRect(
                    borderRadius: BorderRadius.circular(13),
                    child: Image.memory(image!.bytes, fit: BoxFit.cover),
                  ),
          ),
        ),
        if (image != null)
          TextButton(
            key: const Key('work_change_picture'),
            onPressed: onTap,
            child: Text(t.changePicture),
          ),
      ],
    );
  }
}

/// The haircut styles: rename, add, delete (a style that still has works cannot be deleted).
class StylesDialog extends StatefulWidget {
  const StylesDialog({super.key, required this.gallery});

  final GalleryApi gallery;

  @override
  State<StylesDialog> createState() => _StylesDialogState();
}

class _StylesDialogState extends State<StylesDialog> {
  final _name = TextEditingController();
  late Future<List<HaircutCategory>> _styles = widget.gallery.categories();
  bool _busy = false;

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  Future<void> _run(Future<void> Function() action) async {
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _busy = true);
    try {
      await action();
      if (mounted) {
        setState(() {
          _styles = widget.gallery.categories();
        });
      }
    } on AppException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(_detail(e))));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _add() async {
    final name = _name.text.trim();
    if (name.isEmpty) return;
    await _run(() async {
      await widget.gallery.saveCategory(name: name);
      _name.clear();
    });
  }

  Future<void> _rename(HaircutCategory c) async {
    final t = AppLocalizations.of(context);
    final controller = TextEditingController(text: c.name);
    final name = await showDialog<String>(
      context: context,
      builder: (_) => AlertDialog(
        title: DialogHeader(t.edit),
        content: TextField(
          key: const Key('rename_style_field'),
          controller: controller,
          autofocus: true,
          decoration: InputDecoration(labelText: t.styleName),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: Text(t.cancel),
          ),
          FilledButton(
            key: const Key('rename_style_save'),
            onPressed: () => Navigator.pop(context, controller.text.trim()),
            child: Text(t.save),
          ),
        ],
      ),
    );
    controller.dispose();
    if (name == null || name.isEmpty || name == c.name) return;
    await _run(() => widget.gallery.saveCategory(id: c.id, name: name));
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AlertDialog(
      title: DialogHeader(t.styles),
      content: SizedBox(
        width: 420,
        height: 420,
        child: Column(
          children: [
            Row(
              children: [
                Expanded(
                  child: TextField(
                    key: const Key('new_style_field'),
                    controller: _name,
                    decoration: InputDecoration(labelText: t.styleName),
                    onSubmitted: (_) => _add(),
                  ),
                ),
                const SizedBox(width: 8),
                FilledButton(
                  key: const Key('add_style'),
                  style: FilledButton.styleFrom(
                    minimumSize: const Size(100, 48),
                  ),
                  onPressed: _busy ? null : _add,
                  child: Text(t.add),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Expanded(
              child: FutureBuilder<List<HaircutCategory>>(
                future: _styles,
                builder: (context, snapshot) {
                  if (!snapshot.hasData) {
                    return const Center(child: CircularProgressIndicator());
                  }
                  return ListView(
                    children: [
                      for (final c in snapshot.data!)
                        ListTile(
                          key: Key('style_row_${c.id}'),
                          contentPadding: EdgeInsets.zero,
                          title: Text(c.name),
                          subtitle: Text('${c.count}'),
                          trailing: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              IconButton(
                                tooltip: t.edit,
                                icon: const Icon(Icons.edit_outlined),
                                onPressed: _busy ? null : () => _rename(c),
                              ),
                              IconButton(
                                key: Key('delete_style_${c.id}'),
                                tooltip: t.delete,
                                icon: const Icon(Icons.delete_outline),
                                onPressed: _busy
                                    ? null
                                    : () async {
                                        final ok = await confirmAction(
                                          context,
                                          title: t.delete,
                                          message: t.confirmDeleteNamed(c.name),
                                          confirmLabel: t.delete,
                                        );
                                        if (!ok) return;
                                        await _run(
                                          () => widget.gallery.deleteCategory(
                                            c.id,
                                          ),
                                        );
                                      },
                              ),
                            ],
                          ),
                        ),
                    ],
                  );
                },
              ),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: Text(t.cancel),
        ),
      ],
    );
  }
}
