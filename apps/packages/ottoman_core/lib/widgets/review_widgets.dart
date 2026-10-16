import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import '../network/app_exception.dart';
import '../notifications/notification_labels.dart';
import '../reviews/reviews_api.dart';
import 'dialog_header.dart';
import 'star_rating.dart';

/// Asks for stars and an optional comment, then saves. Returns true when a review was saved.
/// The server decides whether this person may review (completed visit / collected order).
Future<bool> showReviewDialog(
  BuildContext context, {
  required ReviewsApi api,
  required String targetType,
  required String targetKey,
  required String title,
  Review? existing,
}) async {
  final saved = await showDialog<bool>(
    context: context,
    builder: (_) => _ReviewDialog(
      api: api,
      targetType: targetType,
      targetKey: targetKey,
      title: title,
      existing: existing,
    ),
  );
  return saved ?? false;
}

/// Owns its text controller, so it is disposed only when the dialog is really gone (not while it animates out).
class _ReviewDialog extends StatefulWidget {
  const _ReviewDialog({
    required this.api,
    required this.targetType,
    required this.targetKey,
    required this.title,
    this.existing,
  });

  final ReviewsApi api;
  final String targetType;
  final String targetKey;
  final String title;
  final Review? existing;

  @override
  State<_ReviewDialog> createState() => _ReviewDialogState();
}

class _ReviewDialogState extends State<_ReviewDialog> {
  late final _comment = TextEditingController(text: widget.existing?.comment);
  late int _rating = widget.existing?.rating ?? 0;
  String? _error;
  bool _busy = false;

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final t = AppLocalizations.of(context);
    if (_rating == 0) {
      setState(() => _error = t.pickRating);
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    final text = _comment.text.trim().isEmpty ? null : _comment.text.trim();
    try {
      if (widget.existing == null) {
        await widget.api.create(
          widget.targetType,
          widget.targetKey,
          _rating,
          comment: text,
        );
      } else {
        await widget.api.update(widget.existing!.id, _rating, comment: text);
      }
      if (mounted) Navigator.pop(context, true);
    } on AppException catch (e) {
      // The server explains why (not eligible yet, already reviewed, ...).
      if (mounted) {
        setState(() {
          _busy = false;
          _error = e.message;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AlertDialog(
      title: DialogHeader(widget.title),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(t.yourRating),
          const SizedBox(height: 4),
          StarRating(
            key: const Key('review_stars'),
            value: _rating.toDouble(),
            size: 36,
            onChanged: (v) => setState(() => _rating = v),
          ),
          const SizedBox(height: 8),
          TextField(
            key: const Key('review_comment'),
            controller: _comment,
            maxLines: 3,
            maxLength: 1000,
            decoration: InputDecoration(labelText: t.yourComment),
          ),
          if (_error != null)
            Text(
              _error!,
              key: const Key('review_error'),
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
        ],
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context, false),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: const Key('review_save'),
          style: FilledButton.styleFrom(minimumSize: const Size(96, 44)),
          onPressed: _busy ? null : _save,
          child: Text(t.save),
        ),
      ],
    );
  }
}

/// One review card: stars, author, text and the salon's reply if any.
class ReviewTile extends StatelessWidget {
  const ReviewTile({super.key, required this.review, this.trailing});

  final Review review;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    return Card(
      key: Key('review_${review.id}'),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                StarRating(value: review.rating.toDouble()),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    review.authorName,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                ),
                Text(
                  formatWhen(review.createdAtUtc).substring(0, 11),
                  style: TextStyle(color: scheme.outline, fontSize: 11),
                ),
                ?trailing,
              ],
            ),
            if (review.comment != null) ...[
              const SizedBox(height: 6),
              Text(review.comment!),
            ],
            if (review.replyText != null) ...[
              const SizedBox(height: 8),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                  color: scheme.surfaceContainerHighest,
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      t.salonReply,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 12,
                      ),
                    ),
                    Text(review.replyText!),
                  ],
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
