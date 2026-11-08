import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import '../../widgets/list_search_field.dart';
import '../images/picture_editor.dart';

/// Admin: write a message to a group (or one person) and see what was sent before.
class NotificationsPage extends StatefulWidget {
  const NotificationsPage({
    super.key,
    required this.api,
    required this.reservations,
    this.chooser = const FileImageChooser(),
  });

  final NotificationsApi api;

  /// Only used to find a single client when sending a personal message.
  final ReservationsApi reservations;
  final ImageChooser chooser;

  @override
  State<NotificationsPage> createState() => _NotificationsPageState();
}

class _NotificationsPageState extends State<NotificationsPage> {
  final _form = GlobalKey<FormState>();
  final _title = TextEditingController();
  final _body = TextEditingController();
  final _search = TextEditingController();

  String _query = '';

  String _audience = Audience.clients;
  String _category = AnnouncementCategory.news;
  DateTime? _expires;
  bool _email = false;
  int _page = 1;
  static const _pageSize = 10;
  ClientItem? _target;
  ChosenImage? _picture;
  List<ClientItem> _found = const [];
  bool _busy = false;
  String? _error;
  int _version = 0;

  @override
  void dispose() {
    _title.dispose();
    _body.dispose();
    _search.dispose();
    super.dispose();
  }

  void _reloadList({bool firstPage = false}) => setState(() {
    if (firstPage) _page = 1;
    _version++;
  });

  Future<void> _edit(AnnouncementItem a) async {
    final t = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final saved = await showDialog<bool>(
      context: context,
      builder: (_) => AnnouncementEditDialog(
        api: widget.api,
        announcement: a,
        chooser: widget.chooser,
      ),
    );
    if (saved == true) {
      messenger.showSnackBar(SnackBar(content: Text(t.announcementSaved)));
      if (mounted) _reloadList();
    }
  }

  Future<void> _toggleHidden(AnnouncementItem a) async {
    final t = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    if (!a.isHidden) {
      final ok = await confirmAction(
        context,
        title: t.hideAnnouncement,
        message: t.hideAnnouncementQuestion,
        confirmLabel: t.hideAnnouncement,
      );
      if (!ok) return;
    }
    try {
      await widget.api.setAnnouncementHidden(a.id, !a.isHidden);
    } on AppException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.detail)));
    }
    if (mounted) _reloadList();
  }

  Future<void> _find() async {
    try {
      final page = await widget.reservations.clients(search: _search.text);
      if (mounted) setState(() => _found = page.items);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  Future<void> _pickPicture() async {
    final image = await widget.chooser.choose();
    if (image != null && mounted) setState(() => _picture = image);
  }

  Future<void> _send() async {
    final t = AppLocalizations.of(context);
    if (!_form.currentState!.validate()) return;
    if (_audience == Audience.user && _target == null) {
      setState(() => _error = t.chooseClient);
      return;
    }
    // A message to a whole group (and its e-mails) cannot be taken back: ask first.
    if (_audience != Audience.user) {
      final who = audienceLabel(t, _audience);
      final ok = await confirmAction(
        context,
        title: t.send,
        message: _email
            ? t.announceEmailQuestion(who)
            : t.announceQuestion(who),
        confirmLabel: t.send,
      );
      if (!ok || !mounted) return;
    }
    final messenger = ScaffoldMessenger.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.api.announce(
        title: _title.text.trim(),
        body: _body.text.trim(),
        audience: _audience,
        targetUserId: _audience == Audience.user ? _target!.id : null,
        sendEmail: _email,
        category: _category,
        expiresAtUtc: _expires,
        imageBytes: _picture?.bytes,
        imageName: _picture?.name,
      );
      // The form is emptied after a successful send, as the next message starts from scratch.
      _title.clear();
      _body.clear();
      _picture = null;
      _expires = null;
      _category = AnnouncementCategory.news;
      _page = 1;
      _form.currentState?.reset();
      messenger.showSnackBar(SnackBar(content: Text(t.announcementSent)));
      if (mounted) setState(() => _version++);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = e.detail);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Widget _sentCard(BuildContext context, AnnouncementItem a) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    final status = a.isHidden
        ? t.announcementHidden
        : a.isExpired
        ? t.announcementExpired
        : null;
    return Opacity(
      opacity: status == null ? 1 : 0.6,
      child: Card(
        key: Key('sent_${a.id}'),
        child: ListTile(
          leading: a.imageUrl == null
              ? const Icon(Icons.campaign_outlined)
              : ClipRRect(
                  borderRadius: BorderRadius.circular(8),
                  child: NetworkPicture(
                    key: Key('sent_picture_${a.id}'),
                    url: a.imageUrl,
                    width: 52,
                    height: 52,
                  ),
                ),
          title: Wrap(
            spacing: 8,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Text(
                a.title,
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
              _Pill(
                key: Key('sent_category_${a.id}'),
                text: announcementCategoryLabel(t, a.category),
                color: scheme.secondaryContainer,
              ),
              if (status != null)
                _Pill(
                  key: Key('sent_status_${a.id}'),
                  text: status,
                  color: scheme.errorContainer,
                ),
            ],
          ),
          subtitle: Text(
            [
              a.body,
              if (a.expiresAtUtc != null)
                '${t.expiresOn}: ${formatWhen(a.expiresAtUtc!)}',
            ].join('\n'),
            maxLines: 3,
            overflow: TextOverflow.ellipsis,
          ),
          trailing: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text(
                    '${audienceLabel(t, a.audience)} · ${t.recipientsCount(a.recipientCount)}',
                    style: const TextStyle(fontSize: 12),
                  ),
                  Text(
                    formatWhen(a.createdAtUtc),
                    style: TextStyle(fontSize: 11, color: scheme.outline),
                  ),
                ],
              ),
              const SizedBox(width: 8),
              IconButton(
                key: Key('edit_${a.id}'),
                tooltip: t.editAnnouncement,
                icon: const Icon(Icons.edit_outlined),
                onPressed: () => _edit(a),
              ),
              IconButton(
                key: Key('hide_${a.id}'),
                tooltip: a.isHidden ? t.showAnnouncement : t.hideAnnouncement,
                icon: Icon(
                  a.isHidden
                      ? Icons.visibility_outlined
                      : Icons.visibility_off_outlined,
                ),
                onPressed: () => _toggleHidden(a),
              ),
            ],
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;

    return Padding(
      padding: const EdgeInsets.fromLTRB(32, 0, 32, 24),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 460,
            child: Card(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Form(
                  key: _form,
                  child: SingleChildScrollView(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Text(
                          t.newMessage,
                          style: Theme.of(context).textTheme.titleMedium
                              ?.copyWith(fontWeight: FontWeight.w700),
                        ),
                        const SizedBox(height: 16),
                        DropdownButtonFormField<String>(
                          key: const Key('audience'),
                          initialValue: _audience,
                          decoration: InputDecoration(labelText: t.audience),
                          items: [
                            for (final a in [
                              Audience.clients,
                              Audience.employees,
                              Audience.all,
                              Audience.user,
                            ])
                              DropdownMenuItem(
                                value: a,
                                child: Text(audienceLabel(t, a)),
                              ),
                          ],
                          onChanged: (v) => setState(() {
                            _audience = v!;
                            _target = null;
                            _found = const [];
                          }),
                        ),
                        if (_audience == Audience.user) ...[
                          const SizedBox(height: 12),
                          TextField(
                            key: const Key('target_search'),
                            controller: _search,
                            decoration: InputDecoration(
                              labelText: t.searchClient,
                              suffixIcon: IconButton(
                                icon: const Icon(Icons.search),
                                onPressed: _find,
                              ),
                            ),
                            onSubmitted: (_) => _find(),
                          ),
                          if (_target != null)
                            ListTile(
                              key: const Key('target_chosen'),
                              dense: true,
                              leading: const Icon(Icons.check_circle_outline),
                              title: Text(_target!.fullName),
                            ),
                          for (final c in _found.take(5))
                            ListTile(
                              key: Key('target_${c.id}'),
                              dense: true,
                              title: Text(c.fullName),
                              subtitle: Text(c.email),
                              onTap: () => setState(() {
                                _target = c;
                                _found = const [];
                              }),
                            ),
                        ],
                        const SizedBox(height: 12),
                        TextFormField(
                          key: const Key('msg_title'),
                          controller: _title,
                          maxLength: 120,
                          decoration: InputDecoration(
                            labelText: t.messageTitle,
                          ),
                          validator: (v) => (v == null || v.trim().isEmpty)
                              ? t.requiredField
                              : null,
                        ),
                        TextFormField(
                          key: const Key('msg_body'),
                          controller: _body,
                          maxLines: 5,
                          maxLength: 2000,
                          decoration: InputDecoration(
                            labelText: t.messageBody,
                            alignLabelWithHint: true,
                          ),
                          validator: (v) => (v == null || v.trim().isEmpty)
                              ? t.requiredField
                              : null,
                        ),
                        AnnouncementCategoryField(
                          fieldKey: const Key('msg_category'),
                          value: _category,
                          onChanged: (v) => setState(() => _category = v),
                        ),
                        const SizedBox(height: 8),
                        ExpiryField(
                          keyPrefix: 'msg_expiry',
                          value: _expires,
                          onChanged: (v) => setState(() => _expires = v),
                        ),
                        const SizedBox(height: 4),
                        Wrap(
                          spacing: 12,
                          runSpacing: 8,
                          crossAxisAlignment: WrapCrossAlignment.center,
                          children: [
                            if (_picture != null)
                              ClipRRect(
                                borderRadius: BorderRadius.circular(8),
                                child: Image.memory(
                                  _picture!.bytes,
                                  key: const Key('msg_picture_preview'),
                                  width: 96,
                                  height: 64,
                                  fit: BoxFit.cover,
                                ),
                              ),
                            OutlinedButton.icon(
                              key: const Key('msg_picture_add'),
                              onPressed: _pickPicture,
                              icon: const Icon(Icons.image_outlined, size: 18),
                              label: Text(
                                _picture == null
                                    ? t.messagePicture
                                    : t.addPicture,
                              ),
                            ),
                            if (_picture != null)
                              TextButton(
                                key: const Key('msg_picture_remove'),
                                onPressed: () =>
                                    setState(() => _picture = null),
                                child: Text(t.removePictureShort),
                              ),
                          ],
                        ),
                        CheckboxListTile(
                          key: const Key('msg_email'),
                          contentPadding: EdgeInsets.zero,
                          controlAffinity: ListTileControlAffinity.leading,
                          title: Text(t.alsoSendEmail),
                          value: _email,
                          onChanged: (v) => setState(() => _email = v ?? false),
                        ),
                        if (_error != null)
                          Padding(
                            padding: const EdgeInsets.only(bottom: 8),
                            child: Text(
                              _error!,
                              key: const Key('msg_error'),
                              style: TextStyle(color: scheme.error),
                            ),
                          ),
                        FilledButton.icon(
                          key: const Key('msg_send'),
                          style: FilledButton.styleFrom(
                            minimumSize: const Size.fromHeight(48),
                          ),
                          onPressed: _busy ? null : _send,
                          icon: const Icon(Icons.send_outlined),
                          label: Text(t.send),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
          const SizedBox(width: 24),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  t.sentMessages,
                  style: Theme.of(context).textTheme.titleMedium
                      ?.copyWith(fontWeight: FontWeight.w700),
                ),
                const SizedBox(height: 12),
                ListSearchField(
                  fieldKey: const Key('ann_search'),
                  hint: t.searchAnnouncements,
                  onSearch: (q) {
                    _query = q;
                    _reloadList(firstPage: true);
                  },
                ),
                const SizedBox(height: 12),
                Expanded(
                  child: AsyncView<Paged<AnnouncementItem>>(
                    key: ValueKey(_version),
                    refreshEvery: const Duration(seconds: 60),
                    load: () => widget.api.announcements(
                      search: _query,
                      page: _page,
                      pageSize: _pageSize,
                    ),
                    skeleton: const ListSkeleton(),
                    isEmpty: (p) => p.items.isEmpty,
                    emptyMessage: t.noData,
                    builder: (context, page, _) {
                      final pages =
                          (page.totalCount + _pageSize - 1) ~/ _pageSize;
                      return Column(
                        children: [
                          Expanded(
                            child: ListView.separated(
                              itemCount: page.items.length,
                              separatorBuilder: (_, _) =>
                                  const SizedBox(height: 8),
                              itemBuilder: (context, i) =>
                                  _sentCard(context, page.items[i]),
                            ),
                          ),
                          if (pages > 1)
                            Row(
                              mainAxisAlignment: MainAxisAlignment.end,
                              children: [
                                TextButton.icon(
                                  key: const Key('ann_prev'),
                                  onPressed: _page > 1
                                      ? () => setState(() {
                                          _page--;
                                          _version++;
                                        })
                                      : null,
                                  icon: const Icon(Icons.chevron_left),
                                  label: Text(t.previousPage),
                                ),
                                Text(
                                  '$_page / $pages',
                                  key: const Key('ann_page'),
                                ),
                                TextButton.icon(
                                  key: const Key('ann_next'),
                                  onPressed: _page < pages
                                      ? () => setState(() {
                                          _page++;
                                          _version++;
                                        })
                                      : null,
                                  icon: const Icon(Icons.chevron_right),
                                  label: Text(t.nextPage),
                                ),
                              ],
                            ),
                        ],
                      );
                    },
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

class _Pill extends StatelessWidget {
  const _Pill({super.key, required this.text, required this.color});

  final String text;
  final Color color;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
    decoration: BoxDecoration(
      color: color,
      borderRadius: BorderRadius.circular(999),
    ),
    child: Text(
      text,
      style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w700),
    ),
  );
}

/// News, promotion or notice.
class AnnouncementCategoryField extends StatelessWidget {
  const AnnouncementCategoryField({
    super.key,
    required this.fieldKey,
    required this.value,
    required this.onChanged,
  });

  final Key fieldKey;
  final String value;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return KeyedSubtree(
      key: ValueKey('category_$value'),
      child: DropdownButtonFormField<String>(
        key: fieldKey,
        initialValue: value,
        decoration: InputDecoration(labelText: t.announcementCategory),
        items: [
          for (final c in AnnouncementCategory.all)
            DropdownMenuItem(
              value: c,
              child: Text(announcementCategoryLabel(t, c)),
            ),
        ],
        onChanged: (v) => onChanged(v!),
      ),
    );
  }
}

/// An optional last day: after it recipients no longer see the announcement.
class ExpiryField extends StatelessWidget {
  const ExpiryField({
    super.key,
    required this.keyPrefix,
    required this.value,
    required this.onChanged,
  });

  final String keyPrefix;
  final DateTime? value;
  final ValueChanged<DateTime?> onChanged;

  Future<void> _pick(BuildContext context) async {
    final now = DateTime.now();
    final day = await showDatePicker(
      context: context,
      initialDate: value ?? now.add(const Duration(days: 7)),
      firstDate: DateTime(now.year, now.month, now.day),
      lastDate: DateTime(now.year + 2, 12, 31),
    );
    // Valid through the whole chosen day, in the admin's local time.
    if (day != null) onChanged(DateTime(day.year, day.month, day.day, 23, 59));
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final v = value;
    return Row(
      children: [
        Expanded(
          child: OutlinedButton.icon(
            key: Key(keyPrefix),
            onPressed: () => _pick(context),
            icon: const Icon(Icons.event_outlined, size: 18),
            label: Text(
              v == null
                  ? t.noExpiry
                  : '${t.expiresOn}: ${v.day.toString().padLeft(2, '0')}.${v.month.toString().padLeft(2, '0')}.${v.year}.',
            ),
          ),
        ),
        if (v != null)
          IconButton(
            key: Key('${keyPrefix}_clear'),
            tooltip: t.clearExpiry,
            icon: const Icon(Icons.close),
            onPressed: () => onChanged(null),
          ),
      ],
    );
  }
}

/// Edits a sent announcement: text, category, end date and picture. Nothing is sent again.
class AnnouncementEditDialog extends StatefulWidget {
  const AnnouncementEditDialog({
    super.key,
    required this.api,
    required this.announcement,
    required this.chooser,
  });

  final NotificationsApi api;
  final AnnouncementItem announcement;
  final ImageChooser chooser;

  @override
  State<AnnouncementEditDialog> createState() => _AnnouncementEditDialogState();
}

class _AnnouncementEditDialogState extends State<AnnouncementEditDialog> {
  final _form = GlobalKey<FormState>();
  late final _title = TextEditingController(text: widget.announcement.title);
  late final _body = TextEditingController(text: widget.announcement.body);
  late String _category = widget.announcement.category;
  late DateTime? _expires = widget.announcement.expiresAtUtc?.toLocal();
  late String? _imageUrl = widget.announcement.imageUrl;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _title.dispose();
    _body.dispose();
    super.dispose();
  }

  Future<void> _picture(Future<AnnouncementItem> Function() change) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final a = await change();
      if (mounted) setState(() => _imageUrl = a.imageUrl);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = e.detail);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _choosePicture() async {
    final image = await widget.chooser.choose();
    if (image == null || !mounted) return;
    await _picture(
      () => widget.api.setAnnouncementImage(
        widget.announcement.id,
        image.bytes,
        image.name,
      ),
    );
  }

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.api.updateAnnouncement(
        widget.announcement.id,
        title: _title.text.trim(),
        body: _body.text.trim(),
        category: _category,
        expiresAtUtc: _expires,
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
    final scheme = Theme.of(context).colorScheme;
    String? required(String? v) =>
        (v == null || v.trim().isEmpty) ? t.requiredField : null;
    return AlertDialog(
      title: DialogHeader(t.editAnnouncement),
      content: SizedBox(
        width: 480,
        child: Form(
          key: _form,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextFormField(
                  key: const Key('edit_title'),
                  controller: _title,
                  maxLength: 120,
                  decoration: InputDecoration(labelText: t.messageTitle),
                  validator: required,
                ),
                TextFormField(
                  key: const Key('edit_body'),
                  controller: _body,
                  maxLines: 5,
                  maxLength: 2000,
                  decoration: InputDecoration(
                    labelText: t.messageBody,
                    alignLabelWithHint: true,
                  ),
                  validator: required,
                ),
                AnnouncementCategoryField(
                  fieldKey: const Key('edit_category'),
                  value: _category,
                  onChanged: (v) => setState(() => _category = v),
                ),
                const SizedBox(height: 8),
                ExpiryField(
                  keyPrefix: 'edit_expiry',
                  value: _expires,
                  onChanged: (v) => setState(() => _expires = v),
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 12,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    if (_imageUrl != null)
                      ClipRRect(
                        borderRadius: BorderRadius.circular(8),
                        child: NetworkPicture(
                          key: const Key('edit_picture'),
                          url: _imageUrl,
                          width: 96,
                          height: 64,
                        ),
                      ),
                    OutlinedButton.icon(
                      key: const Key('edit_picture_change'),
                      onPressed: _busy ? null : _choosePicture,
                      icon: const Icon(Icons.image_outlined, size: 18),
                      label: Text(
                        _imageUrl == null ? t.messagePicture : t.addPicture,
                      ),
                    ),
                    if (_imageUrl != null)
                      TextButton(
                        key: const Key('edit_picture_remove'),
                        onPressed: _busy
                            ? null
                            : () => _picture(
                                () => widget.api.clearAnnouncementImage(
                                  widget.announcement.id,
                                ),
                              ),
                        child: Text(t.removePictureShort),
                      ),
                  ],
                ),
                const SizedBox(height: 8),
                Text(
                  t.editNotResent,
                  style: TextStyle(fontSize: 12, color: scheme.outline),
                ),
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(
                      _error!,
                      key: const Key('edit_error'),
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
          key: const Key('edit_save'),
          onPressed: _busy ? null : _save,
          child: Text(t.save),
        ),
      ],
    );
  }
}
