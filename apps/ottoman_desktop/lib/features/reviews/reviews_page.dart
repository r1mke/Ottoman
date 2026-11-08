import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// Admin: every review, newest first. Hide abusive ones (they stop counting for the average) and reply publicly.
class ReviewsPage extends StatefulWidget {
  const ReviewsPage({super.key, required this.api});

  final ReviewsApi api;

  @override
  State<ReviewsPage> createState() => _ReviewsPageState();
}

class _ReviewsPageState extends State<ReviewsPage> {
  bool _onlyHidden = false;
  int _version = 0;

  void _refresh() => setState(() => _version++);

  void _say(String message) =>
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(message)));

  Future<void> _toggleHidden(Review r) async {
    try {
      await widget.api.setHidden(r.id, !r.isHidden);
    } on AppException catch (e) {
      _say(e.message);
    }
    if (mounted) _refresh();
  }

  Future<void> _reply(Review r) async {
    final t = AppLocalizations.of(context);
    final text = await showDialog<String>(
      context: context,
      builder: (_) => _ReplyDialog(initial: r.replyText),
    );
    if (text == null || text.isEmpty) return;
    try {
      await widget.api.reply(r.id, text);
      _say(t.replySaved);
    } on AppException catch (e) {
      _say(e.message);
    }
    if (mounted) _refresh();
  }

  Future<void> _delete(Review r) async {
    final t = AppLocalizations.of(context);
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: DialogHeader(t.delete),
        content: Text(r.comment ?? r.targetName),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: Text(t.no),
          ),
          FilledButton(
            key: const Key('confirm_delete_review'),
            style: FilledButton.styleFrom(minimumSize: const Size(96, 44)),
            onPressed: () => Navigator.pop(ctx, true),
            child: Text(t.yes),
          ),
        ],
      ),
    );
    if (ok != true) return;
    try {
      await widget.api.delete(r.id);
    } on AppException catch (e) {
      _say(e.message);
    }
    if (mounted) _refresh();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.fromLTRB(32, 0, 32, 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          FilterChip(
            key: const Key('only_hidden'),
            label: Text(t.hiddenReviews),
            selected: _onlyHidden,
            onSelected: (v) {
              _onlyHidden = v;
              _refresh();
            },
          ),
          const SizedBox(height: 16),
          Expanded(
            child: AsyncView<Paged<Review>>(
              key: ValueKey(_version),
              load: () => widget.api.all(onlyHidden: _onlyHidden),
              skeleton: const ListSkeleton(),
              isEmpty: (p) => p.items.isEmpty,
              emptyMessage: t.noReviews,
              builder: (context, page, _) => ListView.separated(
                itemCount: page.items.length,
                separatorBuilder: (_, _) => const SizedBox(height: 4),
                itemBuilder: (context, i) {
                  final r = page.items[i];
                  return Opacity(
                    opacity: r.isHidden ? 0.55 : 1,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Padding(
                          padding: const EdgeInsets.only(left: 4, top: 8),
                          child: Text(
                            '${r.targetName} · ${reviewTargetLabel(t, r.targetType)}',
                            style: TextStyle(
                              color: scheme.outline,
                              fontSize: 12,
                            ),
                          ),
                        ),
                        ReviewTile(
                          review: r,
                          trailing: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              IconButton(
                                key: Key('reply_${r.id}'),
                                tooltip: t.reply,
                                icon: const Icon(Icons.reply_outlined),
                                onPressed: () => _reply(r),
                              ),
                              IconButton(
                                key: Key('hide_${r.id}'),
                                tooltip: r.isHidden
                                    ? t.showReview
                                    : t.hideReview,
                                icon: Icon(
                                  r.isHidden
                                      ? Icons.visibility_outlined
                                      : Icons.visibility_off_outlined,
                                ),
                                onPressed: () => _toggleHidden(r),
                              ),
                              IconButton(
                                key: Key('delete_${r.id}'),
                                tooltip: t.delete,
                                icon: const Icon(Icons.delete_outline),
                                onPressed: () => _delete(r),
                              ),
                            ],
                          ),
                        ),
                      ],
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

class _ReplyDialog extends StatefulWidget {
  const _ReplyDialog({this.initial});

  final String? initial;

  @override
  State<_ReplyDialog> createState() => _ReplyDialogState();
}

class _ReplyDialogState extends State<_ReplyDialog> {
  late final _text = TextEditingController(text: widget.initial);

  @override
  void dispose() {
    _text.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AlertDialog(
      title: DialogHeader(t.reply),
      content: SizedBox(
        width: 420,
        child: TextField(
          key: const Key('reply_text'),
          controller: _text,
          autofocus: true,
          maxLines: 4,
          maxLength: 1000,
          decoration: InputDecoration(labelText: t.yourReply),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: const Key('reply_send'),
          style: FilledButton.styleFrom(minimumSize: const Size(96, 44)),
          onPressed: () => Navigator.pop(context, _text.text.trim()),
          child: Text(t.send),
        ),
      ],
    );
  }
}
