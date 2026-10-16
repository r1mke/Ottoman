import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

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

  /// Where the optional picture comes from (a file dialog; faked in tests).
  final ImageChooser chooser;

  @override
  State<NotificationsPage> createState() => _NotificationsPageState();
}

class _NotificationsPageState extends State<NotificationsPage> {
  final _form = GlobalKey<FormState>();
  final _title = TextEditingController();
  final _body = TextEditingController();
  final _search = TextEditingController();

  String _audience = Audience.clients;
  bool _email = false;
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

  Future<void> _find() async {
    try {
      final page = await widget.reservations.clients(search: _search.text);
      if (mounted) setState(() => _found = page.items);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  Future<void> _send() async {
    final t = AppLocalizations.of(context);
    if (!_form.currentState!.validate()) return;
    if (_audience == Audience.user && _target == null) {
      setState(() => _error = t.chooseClient);
      return;
    }
    final messenger = ScaffoldMessenger.of(context);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final sent = await widget.api.announce(
        title: _title.text.trim(),
        body: _body.text.trim(),
        audience: _audience,
        targetUserId: _audience == Audience.user ? _target!.id : null,
        sendEmail: _email,
      );
      final picture = _picture;
      String? pictureProblem;
      if (picture != null) {
        try {
          await widget.api.setAnnouncementImage(
            sent.id,
            picture.bytes,
            picture.name,
          );
        } on AppException catch (e) {
          // The message is already sent; say so, and why the picture is missing.
          pictureProblem = e.message;
        }
      }
      // The form is emptied after a successful send, as the next message starts from scratch.
      _title.clear();
      _body.clear();
      _form.currentState?.reset();
      messenger.showSnackBar(
        SnackBar(
          content: Text(
            pictureProblem == null
                ? t.announcementSent
                : '${t.announcementSent} ${t.pictureNotSaved}: $pictureProblem',
          ),
        ),
      );
      if (mounted) {
        setState(() {
          _picture = null;
          _version++;
        });
      }
    } on AppException catch (e) {
      final detail = e.errors.values.expand((x) => x).join('\n');
      if (mounted) setState(() => _error = detail.isEmpty ? e.message : detail);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
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
                        const SizedBox(height: 4),
                        Row(
                          children: [
                            OutlinedButton.icon(
                              key: const Key('msg_picture'),
                              onPressed: _busy
                                  ? null
                                  : () async {
                                      final chosen = await widget.chooser
                                          .choose();
                                      if (chosen != null && mounted) {
                                        setState(() => _picture = chosen);
                                      }
                                    },
                              icon: const Icon(
                                Icons.add_photo_alternate_outlined,
                              ),
                              label: Text(
                                _picture == null
                                    ? t.addPicture
                                    : t.changePicture,
                              ),
                            ),
                            if (_picture != null) ...[
                              const SizedBox(width: 12),
                              ClipRRect(
                                borderRadius: BorderRadius.circular(8),
                                child: Image.memory(
                                  _picture!.bytes,
                                  key: const Key('msg_picture_preview'),
                                  width: 56,
                                  height: 56,
                                  fit: BoxFit.cover,
                                  errorBuilder: (_, _, _) => const SizedBox(
                                    width: 56,
                                    height: 56,
                                    child: Icon(Icons.image_outlined),
                                  ),
                                ),
                              ),
                              IconButton(
                                key: const Key('msg_picture_remove'),
                                tooltip: t.removePicture,
                                icon: const Icon(Icons.close),
                                onPressed: () =>
                                    setState(() => _picture = null),
                              ),
                            ],
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
                Expanded(
                  child: AsyncView<Paged<AnnouncementItem>>(
                    key: ValueKey(_version),
                    refreshEvery: const Duration(seconds: 60),
                    load: () => widget.api.announcements(),
                    skeleton: const ListSkeleton(),
                    isEmpty: (p) => p.items.isEmpty,
                    emptyMessage: t.noData,
                    builder: (context, page, _) => ListView.separated(
                      itemCount: page.items.length,
                      separatorBuilder: (_, _) => const SizedBox(height: 8),
                      itemBuilder: (context, i) {
                        final a = page.items[i];
                        return Card(
                          key: Key('sent_${a.id}'),
                          child: ListTile(
                            leading: ClipRRect(
                              borderRadius: BorderRadius.circular(8),
                              child: NetworkPicture(
                                key: Key('sent_picture_${a.id}'),
                                url: a.imageUrl,
                                width: 48,
                                height: 48,
                                fallbackIcon: Icons.campaign_outlined,
                              ),
                            ),
                            title: Text(
                              a.title,
                              style: const TextStyle(
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                            subtitle: Text(
                              a.body,
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                            ),
                            trailing: Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              crossAxisAlignment: CrossAxisAlignment.end,
                              children: [
                                Text(
                                  '${audienceLabel(t, a.audience)} · ${t.recipientsCount(a.recipientCount)}',
                                  style: const TextStyle(fontSize: 12),
                                ),
                                Text(
                                  formatWhen(a.createdAtUtc),
                                  style: TextStyle(
                                    fontSize: 11,
                                    color: scheme.outline,
                                  ),
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
          ),
        ],
      ),
    );
  }
}
