import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import '../images/picture_editor.dart';

String _kindLabel(AppLocalizations t, String kind) => switch (kind) {
  ShopSectionKind.newArrivals => t.kindNewArrivals,
  ShopSectionKind.onSale => t.kindOnSale,
  ShopSectionKind.category => t.kindCategory,
  ShopSectionKind.categoryTiles => t.kindCategoryTiles,
  ShopSectionKind.brandTiles => t.kindBrandTiles,
  ShopSectionKind.recommended => t.kindRecommended,
  _ => t.kindManual,
};

/// Admin: the rows customers see on top of the shop ("Novo u ponudi", "Akcije", a category, a hand-picked set).
/// The admin chooses what each row shows, which are visible and in which order.
class ShopSectionsPage extends StatefulWidget {
  const ShopSectionsPage({
    super.key,
    required this.shop,
    this.chooser = const FileImageChooser(),
  });

  final ShopApi shop;
  final ImageChooser chooser;

  @override
  State<ShopSectionsPage> createState() => _ShopSectionsPageState();
}

class _ShopSectionsPageState extends State<ShopSectionsPage> {
  int _version = 0;

  /// The list as last loaded: the dialog needs to know which built-in blocks already exist.
  List<ShopSectionRule> _latest = const [];

  void _refresh() => setState(() => _version++);

  void _toast(String message) => ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));

  Future<void> _edit([ShopSectionRule? rule]) async {
    final saved = await showDialog<bool>(
      context: context,
      builder: (_) => SectionDialog(
        shop: widget.shop,
        rule: rule,
        chooser: widget.chooser,
        existingKinds: {for (final r in _latest) r.kind},
      ),
    );
    if (saved == true && mounted) _refresh();
  }

  /// Changes only the visibility: every other setting is sent back exactly as loaded (the server replaces the
  /// whole section on save, so a missing field would wipe hand-picked tiles or the style).
  Future<void> _toggle(ShopSectionRule r, bool visible) async {
    try {
      await widget.shop.saveSection(
        id: r.id,
        title: r.title,
        kind: r.kind,
        categoryId: r.categoryId,
        brandId: r.brandId,
        maxItems: r.maxItems,
        isActive: visible,
        productIds: r.productIds,
        showAsCard: r.showAsCard,
        layout: r.layout,
        eyebrow: r.eyebrow,
        entryIds: r.entryIds,
        style: r.style,
      );
    } on AppException catch (e) {
      if (mounted) _toast(e.detail);
    }
    if (mounted) _refresh();
  }

  Future<void> _delete(ShopSectionRule r) async {
    final t = AppLocalizations.of(context);
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: DialogHeader(t.delete),
        content: Text(t.deleteSectionQuestion),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: Text(t.cancel),
          ),
          FilledButton(
            key: const Key('confirm_delete_section'),
            onPressed: () => Navigator.pop(context, true),
            child: Text(t.delete),
          ),
        ],
      ),
    );
    if (ok != true) return;
    try {
      await widget.shop.deleteSection(r.id);
    } on AppException catch (e) {
      if (mounted) _toast(e.detail);
    }
    if (mounted) _refresh();
  }

  Future<void> _reorder(List<ShopSectionRule> rules, int from, int to) async {
    final ids = rules.map((r) => r.id).toList();
    ids.insert(to, ids.removeAt(from));
    try {
      await widget.shop.reorderSections(ids);
    } on AppException catch (e) {
      if (mounted) _toast(e.detail);
    }
    if (mounted) _refresh();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.fromLTRB(36, 8, 36, 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  t.shopSectionsHint,
                  style: TextStyle(color: scheme.outline),
                ),
              ),
              FilledButton.icon(
                key: const Key('add_section'),
                onPressed: () => _edit(),
                icon: const Icon(Icons.add),
                label: Text(t.addSection),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Expanded(
            child: AsyncView<List<ShopSectionRule>>(
              key: ValueKey(_version),
              load: widget.shop.sectionRules,
              isEmpty: (r) => r.isEmpty,
              emptyMessage: t.noSections,
              builder: (context, rules, reload) {
                _latest = rules;
                return ReorderableListView.builder(
                  buildDefaultDragHandles: true,
                  itemCount: rules.length,
                  onReorderItem: (a, b) => _reorder(rules, a, b),
                  itemBuilder: (context, i) {
                    final r = rules[i];
                    return Card(
                      key: ValueKey('section_${r.id}'),
                      child: ListTile(
                        title: Text(
                          r.title,
                          style: const TextStyle(fontWeight: FontWeight.w700),
                        ),
                        subtitle: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(switch (r.kind) {
                              ShopSectionKind.category => [
                                _kindLabel(t, r.kind),
                                ?r.categoryName,
                                ?r.brandName,
                              ].join(' · '),
                              ShopSectionKind.manual =>
                                '${_kindLabel(t, r.kind)} (${r.productIds.length})',
                              ShopSectionKind.categoryTiles ||
                              ShopSectionKind.brandTiles =>
                                '${_kindLabel(t, r.kind)} · ${t.sectionShows(r.productCount)}'
                                    '${r.entryIds.isNotEmpty ? ' · ${t.tilesManual}' : ''}',
                              _ => _kindLabel(t, r.kind),
                            }),
                            if (r.productCount == 0 &&
                                r.kind != ShopSectionKind.recommended)
                              Padding(
                                padding: const EdgeInsets.only(top: 2),
                                child: Text(
                                  t.sectionEmptyShort,
                                  key: Key('section_empty_${r.id}'),
                                  style: TextStyle(
                                    color: scheme.error,
                                    fontWeight: FontWeight.w600,
                                    fontSize: 12,
                                  ),
                                ),
                              ),
                          ],
                        ),
                        trailing: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Tooltip(
                              message: t.sectionVisible,
                              child: Switch(
                                key: Key('section_visible_${r.id}'),
                                value: r.isActive,
                                onChanged: (v) => _toggle(r, v),
                              ),
                            ),
                            IconButton(
                              key: Key('section_edit_${r.id}'),
                              tooltip: t.edit,
                              onPressed: () => _edit(r),
                              icon: const Icon(Icons.edit_outlined),
                            ),
                            if (ShopSectionKind.isBuiltIn(r.kind))
                              Tooltip(
                                message: t.blockNoDelete,
                                child: const Padding(
                                  padding: EdgeInsets.all(8),
                                  child: Icon(Icons.lock_outline, size: 20),
                                ),
                              )
                            else
                              IconButton(
                                key: Key('section_delete_${r.id}'),
                                tooltip: t.delete,
                                onPressed: () => _delete(r),
                                icon: const Icon(Icons.delete_outline),
                              ),
                            const SizedBox(
                              width: 28,
                            ), // room for the drag handle
                          ],
                        ),
                      ),
                    );
                  },
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class SectionDialog extends StatefulWidget {
  const SectionDialog({
    super.key,
    required this.shop,
    this.rule,
    this.chooser = const FileImageChooser(),
    this.existingKinds = const {},
  });

  final ShopApi shop;
  final ShopSectionRule? rule;
  final ImageChooser chooser;

  /// The kinds the shop already has: the built-in blocks exist once, so they are not offered again.
  final Set<String> existingKinds;

  @override
  State<SectionDialog> createState() => _SectionDialogState();
}

class _SectionDialogState extends State<SectionDialog> {
  final _form = GlobalKey<FormState>();
  late final _title = TextEditingController(text: widget.rule?.title);
  late final _eyebrow = TextEditingController(text: widget.rule?.eyebrow);
  late final _max = TextEditingController(
    text: (widget.rule?.maxItems ?? 10).toString(),
  );
  final _search = TextEditingController();
  late String _kind = widget.rule?.kind ?? ShopSectionKind.newArrivals;
  late int? _categoryId = widget.rule?.categoryId;
  late int? _brandId = widget.rule?.brandId;
  late bool _active = widget.rule?.isActive ?? true;
  late bool _showAsCard = widget.rule?.showAsCard ?? false;
  late String _layout = widget.rule?.layout ?? ShopSectionLayout.row;
  late ShopSectionStyle _style = widget.rule?.style ?? const ShopSectionStyle();

  /// The categories or brands picked by hand for a tile block, in the order to show them.
  late final List<int> _entryIds = [...?widget.rule?.entryIds];
  late bool _manualTiles = (widget.rule?.entryIds ?? const <int>[]).isNotEmpty;

  /// What is on the shelf (categories and brands that have products): the picks and the preview come from it.
  late final Future<ShopOverview> _shelf = widget.shop.overview();

  bool get _builtIn => ShopSectionKind.isBuiltIn(_kind);
  bool get _isTiles =>
      _kind == ShopSectionKind.categoryTiles ||
      _kind == ShopSectionKind.brandTiles;
  late String? _imageUrl = widget.rule?.imageUrl;
  late int? _pickedImageId = widget.rule?.imageProductImageId;
  bool _pictureBusy = false;
  late final Set<int> _picked = {...?widget.rule?.productIds};
  final Map<int, String> _names = {};
  late final Future<List<Category>> _categories = widget.shop.categories();
  late final Future<List<Brand>> _brands = widget.shop.brands();
  Future<Paged<Product>>? _results;
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    // The preview shows the title as it is typed.
    _title.addListener(() => setState(() {}));
    _eyebrow.addListener(() => setState(() {}));
    if (_kind == ShopSectionKind.manual) _find();
  }

  @override
  void dispose() {
    _title.dispose();
    _eyebrow.dispose();
    _max.dispose();
    _search.dispose();
    super.dispose();
  }

  void _find() => setState(() {
    _results = widget.shop.products(search: _search.text, pageSize: 30);
  });

  /// What "how many to show" really is for the chosen kind, never a parse error:
  /// - a hand-picked section shows all its picks (the hidden count field must not cut them off);
  /// - automatic category tiles sit in two columns, so the count is even (as the dropdown shows it);
  /// - otherwise the typed number, or the default if the field is hidden and holds something else.
  int _maxItems() {
    if (_kind == ShopSectionKind.manual) return _picked.length.clamp(1, 30);
    final categories = _kind == ShopSectionKind.categoryTiles;
    final typed = int.tryParse(_max.text.trim());
    var n = typed == null || typed < 1 || typed > 30
        ? (categories ? 6 : 10)
        : typed;
    if (categories && !_manualTiles && n.isOdd) n = n == 30 ? 30 : n + 1;
    return n;
  }

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    final t = AppLocalizations.of(context);
    if (_kind == ShopSectionKind.manual &&
        (_picked.isEmpty || _picked.length > 30)) {
      setState(() => _error = t.sectionProductsCountError);
      return;
    }
    // Hand-picked categories sit in two columns: an odd number would leave the last row half empty.
    if (_kind == ShopSectionKind.categoryTiles &&
        _manualTiles &&
        _entryIds.length.isOdd) {
      setState(() => _error = t.categoriesEvenError);
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.shop.saveSection(
        id: widget.rule?.id,
        title: _title.text.trim(),
        kind: _kind,
        categoryId: _kind == ShopSectionKind.category ? _categoryId : null,
        brandId: _kind == ShopSectionKind.category ? _brandId : null,
        maxItems: _maxItems(),
        isActive: _active,
        productIds: _kind == ShopSectionKind.manual ? _picked.toList() : [],
        showAsCard: _showAsCard,
        layout: _layout,
        eyebrow: _eyebrow.text.trim().isEmpty ? null : _eyebrow.text.trim(),
        entryIds: _isTiles && _manualTiles ? _entryIds : const [],
        style: _style,
      );
      if (mounted) Navigator.pop(context, true);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = e.detail);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// The picture changes go to the server at once (like every picture editor), not with "Save".
  Future<void> _changePicture(Future<ShopSectionRule> Function() action) async {
    setState(() {
      _pictureBusy = true;
      _error = null;
    });
    try {
      final next = await action();
      if (mounted) {
        setState(() {
          _imageUrl = next.imageUrl;
          _pickedImageId = next.imageProductImageId;
        });
      }
    } on AppException catch (e) {
      if (mounted) setState(() => _error = e.detail);
    } finally {
      if (mounted) setState(() => _pictureBusy = false);
    }
  }

  Future<void> _upload() async {
    final image = await widget.chooser.choose();
    if (image == null) return;
    await _changePicture(
      () =>
          widget.shop.setSectionImage(widget.rule!.id, image.bytes, image.name),
    );
  }

  Future<void> _pickFromProducts() async {
    final id = await showDialog<int>(
      context: context,
      builder: (_) => ProductPicturePickerDialog(shop: widget.shop),
    );
    if (id == null) return;
    await _changePicture(
      () => widget.shop.pickSectionImage(widget.rule!.id, id),
    );
  }

  Widget _pictureBlock(AppLocalizations t, ColorScheme scheme) {
    if (widget.rule == null) {
      return Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Text(
          t.saveSectionFirst,
          key: const Key('section_picture_hint'),
          style: TextStyle(color: scheme.outline),
        ),
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(t.cardPicture, style: Theme.of(context).textTheme.titleSmall),
        const SizedBox(height: 8),
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(12),
              child: SizedBox(
                width: 132,
                height: 88,
                child: _imageUrl == null
                    ? Container(
                        key: const Key('section_picture_none'),
                        color: scheme.surfaceContainerHighest,
                        alignment: Alignment.center,
                        padding: const EdgeInsets.all(8),
                        child: Text(
                          t.cardPictureAuto,
                          textAlign: TextAlign.center,
                          style: const TextStyle(fontSize: 11),
                        ),
                      )
                    : NetworkPicture(
                        key: const Key('section_picture'),
                        url: _imageUrl,
                      ),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  OutlinedButton.icon(
                    key: const Key('section_picture_upload'),
                    style: OutlinedButton.styleFrom(
                      minimumSize: const Size(0, 40),
                    ),
                    onPressed: _pictureBusy ? null : _upload,
                    icon: const Icon(Icons.upload_outlined, size: 18),
                    label: Text(t.uploadPicture),
                  ),
                  OutlinedButton.icon(
                    key: const Key('section_picture_pick'),
                    style: OutlinedButton.styleFrom(
                      minimumSize: const Size(0, 40),
                    ),
                    onPressed: _pictureBusy ? null : _pickFromProducts,
                    icon: const Icon(Icons.photo_library_outlined, size: 18),
                    label: Text(t.pickProductPicture),
                  ),
                  if (_imageUrl != null)
                    TextButton.icon(
                      key: const Key('section_picture_remove'),
                      onPressed: _pictureBusy
                          ? null
                          : () => _changePicture(
                              () => widget.shop.clearSectionImage(
                                widget.rule!.id,
                              ),
                            ),
                      icon: const Icon(Icons.delete_outline, size: 18),
                      label: Text(t.removePicture),
                    ),
                ],
              ),
            ),
          ],
        ),
        if (_pickedImageId != null) const SizedBox.shrink(),
        const SizedBox(height: 8),
      ],
    );
  }

  /// A row of choices with a small heading: backgrounds, sizes, alignment.
  Widget _choice(
    String label,
    Key key,
    List<(String, String)> options,
    String selected,
    ValueChanged<String> onChanged,
  ) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Padding(
        padding: const EdgeInsets.only(top: 12, bottom: 6),
        child: Text(label, style: Theme.of(context).textTheme.titleSmall),
      ),
      SizedBox(
        width: double.infinity,
        child: SegmentedButton<String>(
          key: key,
          showSelectedIcon: false,
          segments: [
            for (final (value, text) in options)
              ButtonSegment(value: value, label: Text(text)),
          ],
          selected: {selected},
          onSelectionChanged: (v) => onChanged(v.first),
        ),
      ),
    ],
  );

  Widget _styleControls(AppLocalizations t, ColorScheme scheme) {
    final banner = _layout == ShopSectionLayout.banner && !_builtIn;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SizedBox(height: 18),
        Text(t.styleTitle, style: Theme.of(context).textTheme.titleMedium),
        _choice(
          t.backgroundLabel,
          const Key('section_background'),
          [('None', t.bgNone), ('Soft', t.bgSoft), ('Amber', t.bgAmber)],
          _style.background,
          (v) => setState(() => _style = _style.copyWith(background: v)),
        ),
        if (banner)
          Padding(
            padding: const EdgeInsets.only(top: 4),
            child: Text(
              t.bannerAlwaysPanel,
              key: const Key('section_banner_panel_hint'),
              style: TextStyle(color: scheme.outline, fontSize: 12),
            ),
          ),
        _choice(
          t.alignLabel,
          const Key('section_align'),
          [('Left', t.alignLeft), ('Center', t.alignCenter)],
          _style.headingAlign,
          (v) => setState(() => _style = _style.copyWith(headingAlign: v)),
        ),
        if (!_builtIn) ...[
          _choice(
            t.tileSizeLabel,
            const Key('section_tile_size'),
            [
              ('Compact', t.tileCompact),
              ('Normal', t.tileNormal),
              ('Large', t.tileLarge),
            ],
            _style.tileSize,
            (v) => setState(() => _style = _style.copyWith(tileSize: v)),
          ),
          if (_showAsCard)
            _choice(
              t.cardSpanLabel,
              const Key('section_card_span'),
              [('Half', t.spanHalf), ('Full', t.spanFull)],
              _style.cardSpan,
              (v) => setState(() => _style = _style.copyWith(cardSpan: v)),
            ),
          SwitchListTile(
            key: const Key('section_see_all'),
            contentPadding: EdgeInsets.zero,
            title: Text(t.showSeeAllLabel),
            value: _style.showSeeAll,
            onChanged: (v) =>
                setState(() => _style = _style.copyWith(showSeeAll: v)),
          ),
        ],
      ],
    );
  }

  /// How many tiles a category or brand block shows, and whether the admin picks them by hand.
  Widget _tileSettings(AppLocalizations t, ColorScheme scheme) {
    final categories = _kind == ShopSectionKind.categoryTiles;
    final current = int.tryParse(_max.text) ?? (categories ? 6 : 12);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (!_manualTiles) ...[
          const SizedBox(height: 12),
          if (categories)
            DropdownButtonFormField<int>(
              key: const Key('section_tile_count'),
              initialValue: current.isEven ? current : current + 1,
              decoration: InputDecoration(labelText: t.tileCountCategories),
              items: [
                for (final n in {
                  for (var i = 2; i <= 16; i += 2) i,
                  if (current.isEven) current else current + 1,
                }.toList()..sort())
                  DropdownMenuItem(value: n, child: Text('$n')),
              ],
              onChanged: (v) => setState(() => _max.text = '${v ?? 6}'),
            )
          else
            TextFormField(
              key: const Key('section_max'),
              controller: _max,
              keyboardType: TextInputType.number,
              decoration: InputDecoration(labelText: t.tileCountBrands),
              onChanged: (_) => setState(() {}),
              validator: (v) {
                final n = int.tryParse(v ?? '');
                return (n == null || n < 1 || n > 30) ? t.invalidNumber : null;
              },
            ),
        ],
        SwitchListTile(
          key: const Key('section_tiles_manual'),
          contentPadding: EdgeInsets.zero,
          title: Text(t.tilesManual),
          subtitle: Text(
            _manualTiles ? t.tilesPickHint : t.tilesAutoHint,
            style: TextStyle(color: scheme.outline, fontSize: 12),
          ),
          value: _manualTiles,
          onChanged: (v) => setState(() => _manualTiles = v),
        ),
        if (_manualTiles)
          FutureBuilder<ShopOverview>(
            future: _shelf,
            builder: (context, snap) {
              final all = categories
                  ? (snap.data?.categories ?? const <ShopTile>[])
                  : (snap.data?.brands ?? const <ShopTile>[]);
              ShopTile? tile(int id) =>
                  all.where((x) => x.id == id).firstOrNull;
              return Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '${t.tilesSelected}: ${_entryIds.length}'
                    '${categories && _entryIds.length.isOdd ? '  ·  ${t.categoriesEvenError}' : ''}',
                    key: const Key('section_tiles_count'),
                    style: TextStyle(
                      fontWeight: FontWeight.w600,
                      color: categories && _entryIds.length.isOdd
                          ? scheme.error
                          : null,
                    ),
                  ),
                  ReorderableListView(
                    key: const Key('section_tiles_selected'),
                    shrinkWrap: true,
                    physics: const NeverScrollableScrollPhysics(),
                    onReorderItem: (from, to) => setState(
                      () => _entryIds.insert(to, _entryIds.removeAt(from)),
                    ),
                    children: [
                      for (final id in _entryIds)
                        ListTile(
                          key: ValueKey('picked_$id'),
                          dense: true,
                          title: Text(tile(id)?.name ?? '#$id'),
                          trailing: IconButton(
                            key: Key('unpick_$id'),
                            tooltip: t.removeFromSelection,
                            icon: const Icon(Icons.close, size: 18),
                            onPressed: () =>
                                setState(() => _entryIds.remove(id)),
                          ),
                        ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Text(
                    t.tilesAvailable,
                    style: Theme.of(context).textTheme.titleSmall,
                  ),
                  const SizedBox(height: 6),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      for (final x in all.where(
                        (x) => !_entryIds.contains(x.id),
                      ))
                        ActionChip(
                          key: Key('pick_tile_${x.id}'),
                          label: Text('${x.name} (${x.productCount})'),
                          onPressed: () => setState(() => _entryIds.add(x.id)),
                        ),
                    ],
                  ),
                ],
              );
            },
          ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    return AlertDialog(
      title: DialogHeader(widget.rule == null ? t.addSection : t.editSection),
      content: SizedBox(
        width: 520,
        child: Form(
          key: _form,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (widget.rule != null && widget.rule!.productCount == 0)
                  Container(
                    key: const Key('section_empty_warning'),
                    width: double.infinity,
                    margin: const EdgeInsets.only(bottom: 14),
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: scheme.errorContainer.withValues(alpha: 0.5),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Icon(
                          Icons.warning_amber_rounded,
                          size: 20,
                          color: scheme.error,
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Text(
                            t.sectionEmptyWarning,
                            style: const TextStyle(fontSize: 13),
                          ),
                        ),
                      ],
                    ),
                  ),
                TextFormField(
                  key: const Key('section_eyebrow'),
                  controller: _eyebrow,
                  maxLength: ShopSectionRule.maxEyebrowLength,
                  decoration: InputDecoration(
                    labelText: t.sectionEyebrow,
                    helperText: t.sectionEyebrowHint,
                  ),
                ),
                const SizedBox(height: 22),
                TextFormField(
                  key: const Key('section_title'),
                  controller: _title,
                  maxLength: ShopSectionRule.maxTitleLength,
                  decoration: InputDecoration(
                    labelText: t.sectionTitleWhite,
                    helperText: t.sectionTitleHint,
                  ),
                  validator: (v) {
                    final title = (v ?? '').trim().replaceAll(
                      RegExp(r'\s+'),
                      ' ',
                    );
                    if (title.isEmpty) return t.requiredField;
                    if (title.length < ShopSectionRule.minTitleLength) {
                      return t.sectionTitleTooShort(
                        ShopSectionRule.minTitleLength,
                      );
                    }
                    return title.length > ShopSectionRule.maxTitleLength
                        ? t.sectionTitleTooLong(ShopSectionRule.maxTitleLength)
                        : null;
                  },
                ),
                const SizedBox(height: 22),
                DropdownButtonFormField<String>(
                  key: const Key('section_kind'),
                  isExpanded: true,
                  initialValue: _kind,
                  decoration: InputDecoration(labelText: t.sectionKind),
                  items: [
                    // A built-in block cannot change into something else, and exists once.
                    for (final k
                        in (widget.rule != null &&
                                ShopSectionKind.isBuiltIn(widget.rule!.kind))
                            ? [widget.rule!.kind]
                            : [
                                ...ShopSectionKind.all,
                                for (final k in [
                                  ShopSectionKind.categoryTiles,
                                  ShopSectionKind.brandTiles,
                                  ShopSectionKind.recommended,
                                ])
                                  if (!widget.existingKinds.contains(k)) k,
                              ])
                      DropdownMenuItem(value: k, child: Text(_kindLabel(t, k))),
                  ],
                  onChanged:
                      (widget.rule != null &&
                          ShopSectionKind.isBuiltIn(widget.rule!.kind))
                      ? null
                      : (v) {
                          setState(() {
                            _kind = v!;
                            // A sensible start for the number of tiles of a new block.
                            if (widget.rule == null) {
                              if (v == ShopSectionKind.categoryTiles) {
                                _max.text = '6';
                              }
                              if (v == ShopSectionKind.brandTiles) {
                                _max.text = '12';
                              }
                            }
                          });
                          // Products are only needed to hand-pick; do not fetch them for the other kinds.
                          if (v == ShopSectionKind.manual && _results == null) {
                            _find();
                          }
                        },
                ),
                if (_isTiles) _tileSettings(t, scheme),
                const SizedBox(height: 12),
                if (_kind == ShopSectionKind.category) ...[
                  FutureBuilder<List<Category>>(
                    future: _categories,
                    builder: (context, snap) => DropdownButtonFormField<int?>(
                      key: const Key('section_category'),
                      isExpanded: true,
                      initialValue: _categoryId,
                      decoration: InputDecoration(labelText: t.category),
                      items: [
                        DropdownMenuItem(value: null, child: Text(t.any)),
                        for (final c in snap.data ?? const <Category>[])
                          DropdownMenuItem(value: c.id, child: Text(c.name)),
                      ],
                      onChanged: (v) => _categoryId = v,
                      // One of the two is enough; both together narrow it down.
                      validator: (v) => (v == null && _brandId == null)
                          ? t.pickCategoryOrBrand
                          : null,
                    ),
                  ),
                  const SizedBox(height: 12),
                  FutureBuilder<List<Brand>>(
                    future: _brands,
                    builder: (context, snap) => DropdownButtonFormField<int?>(
                      key: const Key('section_brand'),
                      isExpanded: true,
                      initialValue: _brandId,
                      decoration: InputDecoration(labelText: t.brand),
                      items: [
                        DropdownMenuItem(value: null, child: Text(t.any)),
                        for (final b in snap.data ?? const <Brand>[])
                          DropdownMenuItem(value: b.id, child: Text(b.name)),
                      ],
                      onChanged: (v) => _brandId = v,
                    ),
                  ),
                ],
                if (_kind != ShopSectionKind.manual && !_builtIn) ...[
                  const SizedBox(height: 12),
                  TextFormField(
                    key: const Key('section_max'),
                    controller: _max,
                    keyboardType: TextInputType.number,
                    decoration: InputDecoration(labelText: t.sectionMaxItems),
                    validator: (v) {
                      final n = int.tryParse(v ?? '');
                      return (n == null || n < 1 || n > 30)
                          ? t.invalidNumber
                          : null;
                    },
                  ),
                ],
                if (_kind == ShopSectionKind.manual) ...[
                  const SizedBox(height: 12),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: Text(
                      '${t.sectionPickProducts} · ${t.sectionSelected}: ${_picked.length}',
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                  ),
                  TextField(
                    key: const Key('section_product_search'),
                    controller: _search,
                    decoration: InputDecoration(
                      labelText: t.searchProducts,
                      prefixIcon: const Icon(Icons.search),
                    ),
                    onSubmitted: (_) => _find(),
                  ),
                  SizedBox(
                    height: 220,
                    child: FutureBuilder<Paged<Product>>(
                      future: _results,
                      builder: (context, snap) {
                        final items = snap.data?.items ?? const <Product>[];
                        return ListView(
                          children: [
                            for (final p in items)
                              CheckboxListTile(
                                key: Key('pick_${p.id}'),
                                dense: true,
                                value: _picked.contains(p.id),
                                title: Text(p.name),
                                subtitle: Text(p.brandName ?? ''),
                                onChanged: (v) => setState(() {
                                  _names[p.id] = p.name;
                                  v == true
                                      ? _picked.add(p.id)
                                      : _picked.remove(p.id);
                                }),
                              ),
                          ],
                        );
                      },
                    ),
                  ),
                ],
                if (!_builtIn) ...[
                  const SizedBox(height: 20),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: Text(
                      t.sectionLayout,
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                  ),
                  const SizedBox(height: 8),
                  SizedBox(
                    width: double.infinity,
                    child: SegmentedButton<String>(
                      key: const Key('section_layout'),
                      showSelectedIcon: false,
                      segments: [
                        ButtonSegment(
                          value: ShopSectionLayout.row,
                          label: Text(t.layoutRow),
                          icon: const Icon(Icons.view_carousel_outlined),
                        ),
                        ButtonSegment(
                          value: ShopSectionLayout.grid,
                          label: Text(t.layoutGrid),
                          icon: const Icon(Icons.grid_view_outlined),
                        ),
                        ButtonSegment(
                          value: ShopSectionLayout.banner,
                          label: Text(t.layoutBanner),
                          icon: const Icon(Icons.view_day_outlined),
                        ),
                      ],
                      selected: {_layout},
                      onSelectionChanged: (v) =>
                          setState(() => _layout = v.first),
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.only(top: 6, bottom: 4),
                    child: Align(
                      alignment: Alignment.centerLeft,
                      child: Text(
                        switch (_layout) {
                          ShopSectionLayout.grid => t.layoutGridHint,
                          ShopSectionLayout.banner => t.layoutBannerHint,
                          _ => t.layoutRowHint,
                        },
                        key: const Key('section_layout_hint'),
                        style: TextStyle(color: scheme.outline, fontSize: 12),
                      ),
                    ),
                  ),
                  SwitchListTile(
                    key: const Key('section_as_card'),
                    contentPadding: EdgeInsets.zero,
                    title: Text(t.showAsCard),
                    value: _showAsCard,
                    onChanged: (v) => setState(() => _showAsCard = v),
                  ),
                  if (_showAsCard || _layout == ShopSectionLayout.banner)
                    _pictureBlock(t, scheme),
                ],
                _styleControls(t, scheme),
                SwitchListTile(
                  key: const Key('section_active'),
                  contentPadding: EdgeInsets.zero,
                  title: Text(t.sectionVisible),
                  value: _active,
                  onChanged: (v) => setState(() => _active = v),
                ),
                const SizedBox(height: 12),
                SectionPreview(
                  eyebrow: _eyebrow.text.trim().replaceAll(RegExp(r'\s+'), ' '),
                  title: _title.text.trim().replaceAll(RegExp(r'\s+'), ' '),
                  kind: _kind,
                  layout: _builtIn ? ShopSectionLayout.row : _layout,
                  showAsCard: !_builtIn && _showAsCard,
                  imageUrl: _imageUrl,
                  style: _style,
                  tileCount: int.tryParse(_max.text) ?? 6,
                ),
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(
                      _error!,
                      key: const Key('section_error'),
                      style: TextStyle(color: scheme.error),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _busy ? null : () => Navigator.pop(context, false),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: const Key('section_save'),
          onPressed: _busy ? null : _save,
          child: Text(t.save),
        ),
      ],
    );
  }
}

/// Choose one of the pictures of the shop's products: search a product, tap its picture.
class ProductPicturePickerDialog extends StatefulWidget {
  const ProductPicturePickerDialog({super.key, required this.shop});

  final ShopApi shop;

  @override
  State<ProductPicturePickerDialog> createState() =>
      _ProductPicturePickerDialogState();
}

class _ProductPicturePickerDialogState
    extends State<ProductPicturePickerDialog> {
  final _search = TextEditingController();
  late Future<Paged<Product>> _results = _load();

  Future<Paged<Product>> _load() =>
      widget.shop.products(search: _search.text, pageSize: 24);

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AlertDialog(
      title: DialogHeader(t.pickPictureTitle),
      content: SizedBox(
        width: 560,
        height: 460,
        child: Column(
          children: [
            TextField(
              key: const Key('picker_search'),
              controller: _search,
              decoration: InputDecoration(
                labelText: t.searchProducts,
                prefixIcon: const Icon(Icons.search),
              ),
              onSubmitted: (_) => setState(() => _results = _load()),
            ),
            const SizedBox(height: 12),
            Expanded(
              child: FutureBuilder<Paged<Product>>(
                future: _results,
                builder: (context, snap) {
                  if (snap.connectionState != ConnectionState.done) {
                    return const Center(child: CircularProgressIndicator());
                  }
                  final withPictures = (snap.data?.items ?? const <Product>[])
                      .where((p) => p.images.isNotEmpty)
                      .toList();
                  if (withPictures.isEmpty) {
                    return Center(child: Text(t.noPicturesFound));
                  }
                  return ListView(
                    children: [
                      for (final p in withPictures)
                        Padding(
                          padding: const EdgeInsets.only(bottom: 14),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                p.name,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                              const SizedBox(height: 6),
                              Wrap(
                                spacing: 8,
                                runSpacing: 8,
                                children: [
                                  for (final image in p.images)
                                    InkWell(
                                      key: Key('picker_image_${image.id}'),
                                      borderRadius: BorderRadius.circular(10),
                                      onTap: () =>
                                          Navigator.pop(context, image.id),
                                      child: ClipRRect(
                                        borderRadius: BorderRadius.circular(10),
                                        child: NetworkPicture(
                                          url: image.url,
                                          width: 96,
                                          height: 96,
                                        ),
                                      ),
                                    ),
                                ],
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

/// A small preview of how the section (and its card) will look in the shop, in the app's own dark colours. It follows
/// what is typed and chosen in the dialog, so a too long title, a missing picture or a wrong panel shows up before
/// saving. Products are drawn as outlines: the preview is about layout, text and colour.
class SectionPreview extends StatelessWidget {
  const SectionPreview({
    super.key,
    required this.title,
    required this.kind,
    required this.layout,
    required this.showAsCard,
    this.imageUrl,
    this.eyebrow = '',
    this.style = const ShopSectionStyle(),
    this.tileCount = 6,
  });

  /// The small amber text above the title (empty when left out).
  final String eyebrow;
  final String title;
  final String kind;
  final String layout;
  final bool showAsCard;
  final String? imageUrl;
  final ShopSectionStyle style;

  /// How many category or brand tiles a tile block is set to show.
  final int tileCount;

  static const _ground = Color(0xFF0E0E14);
  static const _card = Color(0xFF1C1D27);
  static const _line = Color(0xFF2A2B36);
  static const _amber = Color(0xFFFFB000);

  bool get _banner => layout == ShopSectionLayout.banner;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final shown = title.isEmpty ? t.previewSectionTitlePlaceholder : title;
    final builtIn = ShopSectionKind.isBuiltIn(kind);
    // A banner always stands on a panel together with its products.
    final background = !builtIn && _banner && style.background == 'None'
        ? 'Soft'
        : style.background;

    return Container(
      key: const Key('section_preview'),
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: _ground,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: _line),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            t.previewTitle.toUpperCase(),
            style: const TextStyle(
              color: _amber,
              fontWeight: FontWeight.w800,
              fontSize: 11,
              letterSpacing: 0.6,
            ),
          ),
          if (showAsCard) ...[
            const SizedBox(height: 10),
            _label(t.previewCard),
            const SizedBox(height: 6),
            SizedBox(
              width: style.fullCard ? double.infinity : 200,
              height: 130,
              child: _PictureCard(
                key: const Key('preview_card'),
                eyebrow: eyebrow,
                title: shown,
                subtitle: kind == ShopSectionKind.onSale
                    ? t.discountUpTo(30)
                    : t.productsCount(8),
                imageUrl: imageUrl,
                radius: 20,
                titleSize: 15,
              ),
            ),
          ],
          const SizedBox(height: 12),
          _label(t.previewSection),
          const SizedBox(height: 6),
          _panel(background, _section(t, shown)),
        ],
      ),
    );
  }

  /// The ground the section stands on, as in the app.
  Widget _panel(String background, Widget child) {
    if (background == 'None') return child;
    final amber = background == 'Amber';
    return Container(
      key: Key('preview_panel_${background.toLowerCase()}'),
      width: double.infinity,
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: amber ? _amber.withValues(alpha: 0.35) : _line,
        ),
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: amber
              ? const [Color(0xFF3A2B0A), Color(0xFF181510)]
              : const [Color(0xFF1B1C26), Color(0xFF14151C)],
        ),
      ),
      child: child,
    );
  }

  Widget _section(AppLocalizations t, String shown) {
    final centered = style.centered;
    final cross = centered
        ? CrossAxisAlignment.center
        : CrossAxisAlignment.start;
    switch (kind) {
      case ShopSectionKind.categoryTiles:
        final n = tileCount.clamp(2, 6);
        return Column(
          key: const Key('preview_categories'),
          crossAxisAlignment: cross,
          children: [
            _heading(shown, trailing: t.allProducts),
            const SizedBox(height: 8),
            for (var row = 0; row < (n / 2).ceil(); row++)
              Padding(
                padding: const EdgeInsets.only(bottom: 6),
                child: Row(
                  children: [
                    Expanded(child: _imageTile()),
                    const SizedBox(width: 6),
                    Expanded(child: _imageTile()),
                  ],
                ),
              ),
          ],
        );
      case ShopSectionKind.brandTiles:
        return Column(
          key: const Key('preview_brands'),
          crossAxisAlignment: cross,
          children: [
            _heading(shown),
            const SizedBox(height: 8),
            SizedBox(
              height: 56,
              child: Row(
                mainAxisAlignment: centered
                    ? MainAxisAlignment.center
                    : MainAxisAlignment.start,
                children: [
                  for (var i = 0; i < 4; i++)
                    Container(
                      width: 70,
                      margin: const EdgeInsets.only(right: 8),
                      decoration: BoxDecoration(
                        color: const Color(0xFFF4F4F6),
                        borderRadius: BorderRadius.circular(12),
                      ),
                    ),
                ],
              ),
            ),
          ],
        );
      case ShopSectionKind.recommended:
        return Column(
          key: const Key('preview_recommended'),
          crossAxisAlignment: cross,
          children: [
            _heading(shown),
            const SizedBox(height: 8),
            _productRow(3, 74, 84),
          ],
        );
    }

    final (width, height) = switch (style.tileSize) {
      'Compact' => (58.0, 76.0),
      'Large' => (88.0, 100.0),
      _ => (72.0, 86.0),
    };
    return Column(
      crossAxisAlignment: cross,
      children: [
        if (_banner)
          SizedBox(
            height: 96,
            child: _PictureCard(
              key: const Key('preview_banner'),
              eyebrow: eyebrow,
              bar: true,
              title: shown,
              imageUrl: imageUrl,
              radius: 18,
              titleSize: 20,
            ),
          )
        else
          _heading(shown),
        const SizedBox(height: 10),
        if (layout == ShopSectionLayout.grid)
          Column(
            key: const Key('preview_grid'),
            children: [
              Row(
                children: [
                  Expanded(child: _tile(height)),
                  const SizedBox(width: 8),
                  Expanded(child: _tile(height)),
                ],
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  Expanded(child: _tile(height)),
                  const SizedBox(width: 8),
                  Expanded(child: _tile(height)),
                ],
              ),
            ],
          )
        else
          _productRow(4, width, height),
        if (style.showSeeAll)
          Padding(
            padding: const EdgeInsets.only(top: 6),
            child: Align(
              alignment: centered ? Alignment.center : Alignment.centerRight,
              child: Text(
                '${t.previewSeeAll} →',
                key: const Key('preview_see_all'),
                style: const TextStyle(
                  color: _amber,
                  fontWeight: FontWeight.w700,
                  fontSize: 12,
                ),
              ),
            ),
          ),
      ],
    );
  }

  /// Eyebrow, white title and the amber bar: the heading every block of the app has.
  Widget _heading(String shown, {String? trailing}) {
    final centered = style.centered;
    final title = Text(
      shown,
      key: const Key('preview_title'),
      maxLines: 1,
      textAlign: centered ? TextAlign.center : TextAlign.start,
      overflow: TextOverflow.ellipsis,
      style: const TextStyle(
        color: Colors.white,
        fontWeight: FontWeight.w800,
        fontSize: 18,
      ),
    );
    return Column(
      crossAxisAlignment: centered
          ? CrossAxisAlignment.center
          : CrossAxisAlignment.start,
      children: [
        if (eyebrow.isNotEmpty)
          Text(
            eyebrow.toUpperCase(),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: _amber,
              fontWeight: FontWeight.w800,
              fontSize: 11,
              letterSpacing: 0.5,
            ),
          ),
        if (trailing != null && !centered)
          Row(
            children: [
              Expanded(child: title),
              Text(
                trailing,
                style: const TextStyle(color: _amber, fontSize: 11),
              ),
            ],
          )
        else
          title,
        const SizedBox(height: 5),
        Container(
          width: 34,
          height: 3,
          decoration: BoxDecoration(
            color: _amber,
            borderRadius: BorderRadius.circular(2),
          ),
        ),
      ],
    );
  }

  Widget _productRow(int count, double width, double height) => SizedBox(
    key: const Key('preview_row'),
    height: height,
    child: Row(
      mainAxisAlignment: style.centered
          ? MainAxisAlignment.center
          : MainAxisAlignment.start,
      children: [
        for (var i = 0; i < count; i++) ...[
          SizedBox(width: width, child: _tile(height)),
          const SizedBox(width: 8),
        ],
      ],
    ),
  );

  static Widget _label(String text) => Text(
    text,
    style: const TextStyle(color: Color(0xFF8E8FA0), fontSize: 11),
  );

  /// A category tile reduced to its outline: a picture with a name line.
  static Widget _imageTile() => Container(
    height: 44,
    decoration: BoxDecoration(
      color: _card,
      borderRadius: BorderRadius.circular(12),
      border: Border.all(color: _line),
    ),
    alignment: Alignment.bottomLeft,
    padding: const EdgeInsets.all(8),
    child: Container(height: 4, width: 44, color: const Color(0xFF8E8FA0)),
  );

  /// A product tile reduced to its outline: a picture, a name line and a price line.
  static Widget _tile(double height) => Container(
    height: height,
    decoration: BoxDecoration(
      color: _card,
      borderRadius: BorderRadius.circular(12),
      border: Border.all(color: _line),
    ),
    padding: const EdgeInsets.all(6),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Container(
            decoration: BoxDecoration(
              color: const Color(0xFF30313F),
              borderRadius: BorderRadius.circular(8),
            ),
          ),
        ),
        const SizedBox(height: 5),
        Container(height: 4, width: 40, color: const Color(0xFF8E8FA0)),
        const SizedBox(height: 3),
        Container(height: 4, width: 24, color: _amber),
      ],
    ),
  );
}

/// The picture-with-title block used by cards and banners: the picture (or a plain ground when there is none), a dark
/// fade and the title at the bottom, cut to two lines the way the app cuts it.
class _PictureCard extends StatelessWidget {
  const _PictureCard({
    super.key,
    required this.title,
    required this.radius,
    required this.titleSize,
    this.subtitle,
    this.imageUrl,
    this.eyebrow = '',
    this.bar = false,
  });

  final String title;
  final String eyebrow;

  /// Draws the short amber bar under the title (banners).
  final bool bar;
  final String? subtitle;
  final String? imageUrl;
  final double radius;
  final double titleSize;

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(radius),
      child: Stack(
        fit: StackFit.expand,
        children: [
          if (imageUrl != null)
            NetworkPicture(url: imageUrl)
          else
            const ColoredBox(color: SectionPreview._card),
          const DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                colors: [Colors.transparent, Color(0xCC000000)],
                stops: [0.3, 1],
              ),
            ),
          ),
          Positioned(
            left: 12,
            right: 10,
            bottom: 10,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                if (eyebrow.isNotEmpty)
                  Text(
                    eyebrow.toUpperCase(),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: SectionPreview._amber,
                      fontWeight: FontWeight.w800,
                      fontSize: 10,
                      letterSpacing: 0.5,
                    ),
                  ),
                Text(
                  title,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w800,
                    fontSize: titleSize,
                    height: 1.15,
                  ),
                ),
                if (bar) ...[
                  const SizedBox(height: 4),
                  Container(
                    width: 30,
                    height: 3,
                    decoration: BoxDecoration(
                      color: SectionPreview._amber,
                      borderRadius: BorderRadius.circular(2),
                    ),
                  ),
                ],
                if (subtitle != null)
                  Text(
                    subtitle!,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: Colors.white70,
                      fontWeight: FontWeight.w700,
                      fontSize: 11,
                    ),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
